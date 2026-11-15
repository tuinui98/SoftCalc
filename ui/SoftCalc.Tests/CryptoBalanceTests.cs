// CryptoBalanceTests.cs - Balances from canned service answers, watch-only keys, coin prices (design/crypto_plan.md C1)

using System.Text.Json.Nodes;
using NBitcoin;
using NBitcoin.DataEncoders;
using SoftCalc.Engine;
using SoftCalc.Logic;

namespace SoftCalc.Tests;

public sealed class CryptoBalanceTests
{
	private static readonly CryptoEndpoints Api = new("https://esplora.test/api", "https://rpc.test", "https://tron.test");

	private static Task NoWait(TimeSpan wait, CancellationToken cancel)
	{
		return Task.CompletedTask;
	}

	[Fact]
	public async Task BusyServiceIsAskedAgainAfterWaits()
	{
		var waits = new List<TimeSpan>();
		int calls = 0;
		var messages = new List<string>();
		CryptoFetch busyTwice = (_, _, _) => ++calls <= 2
			? throw new ServiceBusyException("mempool.space", "429 Too Many Requests", calls == 2 ? TimeSpan.FromSeconds(5) : null)
			: Task.FromResult("ok");
		var patient = CryptoBalance.Patient(busyTwice, TimeSpan.Zero, new SyncProgress(messages), (w, _) =>
		{
			waits.Add(w);
			return Task.CompletedTask;
		});
		Assert.Equal("ok", await patient("https://mempool.space/api/x", null, default));
		Assert.Equal(new[] { TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(5) }, waits);    // then the service's own Retry-After
		Assert.Contains("trying again in 15 s", messages[0]);
		CryptoFetch alwaysBusy = (_, _, _) => throw new ServiceBusyException("mempool.space", "no answer", null);
		var giveUp = CryptoBalance.Patient(alwaysBusy, TimeSpan.Zero, null, NoWait);
		await Assert.ThrowsAsync<ServiceBusyException>(() => giveUp("https://mempool.space/api/x", null, default));
	}

	[Theory]
	[InlineData("http://umbrel.local:3006/api", true)]
	[InlineData("http://192.168.1.20:3006/api", true)]
	[InlineData("http://localhost:8080", true)]
	[InlineData("http://mynode/api", true)]
	[InlineData("https://mempool.space/api", false)]
	[InlineData("http://8.8.8.8/api", false)]
	public void OwnNodesNeedNoGap(string url, bool local)
	{
		Assert.Equal(local, CryptoBalance.IsLocal(url));
	}

	// Progress<T> posts to a sync context; this one reports at once
	private sealed class SyncProgress(List<string> messages) : IProgress<string>
	{
		public void Report(string value)
		{
			messages.Add(value);
		}
	}

	// Answers by URL prefix (POST bodies by method); every call is recorded
	private sealed class Fake
	{
		public readonly List<string> Calls = new();
		public readonly Dictionary<string, string> Answers = new();

		public Task<string> Fetch(string url, string? body, CancellationToken cancel)
		{
			string key = body == null ? url : url + " " + (JsonNode.Parse(body)?["method"]?.ToString() ?? "");
			Calls.Add(key);
			foreach (var (prefix, answer) in Answers.OrderByDescending(a => a.Key.Length))
			{
				if (key.StartsWith(prefix, StringComparison.Ordinal))
				{
					return Task.FromResult(answer);
				}
			}
			return Task.FromResult("{\"chain_stats\":{\"tx_count\":0},\"mempool_stats\":{\"tx_count\":0}}");
		}
	}

	[Fact]
	public async Task BitcoinAddressBalanceAndHistory()
	{
		const string address = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";
		var fake = new Fake();
		fake.Answers[$"{Api.Esplora}/address/{address}/txs"] = """
			[{"txid":"aa","status":{"confirmed":true,"block_height":100,"block_time":1700000000},
			  "vin":[{"prevout":{"scriptpubkey_address":"other","value":900}}],
			  "vout":[{"scriptpubkey_address":"1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa","value":500},{"scriptpubkey_address":"other","value":300}]},
			 {"txid":"bb","status":{"confirmed":false},
			  "vin":[{"prevout":{"scriptpubkey_address":"1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa","value":500}}],
			  "vout":[{"scriptpubkey_address":"other","value":450}]}]
			""";
		fake.Answers[$"{Api.Esplora}/address/{address}"] =
			"""{"chain_stats":{"funded_txo_sum":5000,"spent_txo_sum":1000,"tx_count":3},"mempool_stats":{"funded_txo_sum":0,"spent_txo_sum":500,"tx_count":1}}""";
		fake.Answers[$"{Api.Esplora}/blocks/tip/height"] = "101";
		var report = await CryptoBalance.CheckAsync(CryptoAddress.Check(address), 0, Api, fake.Fetch, null, default, NoWait);
		Assert.Equal(new AssetBalance("BTC", "btc", 0.00004m, -0.000005m), report.Assets.Single());
		Assert.Equal(2, report.Payments.Count);
		Assert.Equal(("pending", -0.000005m), (report.Payments[0].Status, report.Payments[0].Amount));
		Assert.Equal(("2 confirmations", 0.000005m), (report.Payments[1].Status, report.Payments[1].Amount));
	}

	[Fact]
	public async Task EthereumBalanceAndUsdt()
	{
		var fake = new Fake();
		fake.Answers[$"{Api.EthereumRpc} eth_getBalance"] = """{"jsonrpc":"2.0","id":1,"result":"0xde0b6b3a7640000"}""";    // 1 ETH
		fake.Answers[$"{Api.EthereumRpc} eth_call"] = """{"jsonrpc":"2.0","id":2,"result":"0x00000000000000000000000000000000000000000000000000000000004c4b40"}""";
		var report = await CryptoBalance.CheckAsync(CryptoAddress.Check("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed"), 0, Api, fake.Fetch, null,
			default, NoWait);
		Assert.Equal(1m, report.Assets[0].Amount);
		Assert.Equal(("USDT (ERC-20)", 5m), (report.Assets[1].Asset, report.Assets[1].Amount));
		Assert.Empty(report.Payments);
	}

	[Fact]
	public async Task TronBalanceAndTransfers()
	{
		byte[] data = new byte[21];
		data[0] = 0x41;
		data[5] = 9;
		string address = Encoders.Base58Check.EncodeData(data);
		string hex = Convert.ToHexString(data).ToLowerInvariant();
		var fake = new Fake();
		fake.Answers[$"{Api.TronGrid}/v1/accounts/{address}/transactions/trc20"] =
			"{\"data\":[{\"transaction_id\":\"t1\",\"block_timestamp\":1700000000000,\"from\":\"Tother\",\"to\":\"" + address
			+ "\",\"value\":\"2500000\"}]}";
		fake.Answers[$"{Api.TronGrid}/v1/accounts/{address}/transactions"] =
			"{\"data\":[{\"txID\":\"t2\",\"block_timestamp\":1700000100000,\"raw_data\":{\"contract\":[{\"type\":\"TransferContract\","
			+ "\"parameter\":{\"value\":{\"amount\":1500000,\"owner_address\":\"" + hex + "\",\"to_address\":\"41ff\"}}}]}}]}";
		fake.Answers[$"{Api.TronGrid}/v1/accounts/{address}"] =
			"{\"data\":[{\"balance\":12000000,\"trc20\":[{\"" + CryptoBalance.UsdtTrc20 + "\":\"7000000\"}]}]}";
		var report = await CryptoBalance.CheckAsync(CryptoAddress.Check(address), 0, Api, fake.Fetch, null, default, NoWait);
		Assert.Equal(new[] { 12m, 7m }, report.Assets.Select(a => a.Amount));
		Assert.Equal(new[] { ("TRX", -1.5m), ("USDT", 2.5m) }, report.Payments.Select(p => (p.Asset, p.Amount)));
	}

	[Fact]
	public void HugeTokenAmountsDoNotOverflow()
	{
		Assert.Equal(1.5m, CryptoBalance.Units(1_500_000, 6));
		Assert.Equal(-1.5m, CryptoBalance.Units(-1_500_000, 6));
		Assert.Equal(decimal.MaxValue, CryptoBalance.Units(System.Numerics.BigInteger.Parse("999666888000000000000000000000000000001"), 6));
		Assert.Equal(1m, CryptoBalance.Units(System.Numerics.BigInteger.Pow(10, 18), 18));
	}

	[Fact]
	public void ZpubGivesTheSameAddressesAsTheSeed()
	{
		// BIP84's test words; the zpub is made here from the seed, then read back as a watch-only key
		var root = new Mnemonic("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about").DeriveExtKey();
		var account = root.Derive(new KeyPath("m/84'/0'/0'")).Neuter();
		byte[] data = Encoders.Base58Check.DecodeData(account.GetWif(Network.Main).ToString());
		new byte[] { 0x04, 0xB2, 0x47, 0x46 }.CopyTo(data, 0);    // zpub
		string zpub = Encoders.Base58Check.EncodeData(data);
		Assert.StartsWith("zpub", zpub);
		var check = CryptoAddress.Check(zpub);
		Assert.True(check.Valid);
		var (read, network, type) = CryptoBalance.Account(zpub, 0);
		Assert.Equal(ScriptPubKeyType.Segwit, type);
		string first = read.Derive(0).Derive(0).PubKey.GetAddress(type, network).ToString();
		Assert.Equal(root.Derive(new KeyPath("m/84'/0'/0'/0/0")).GetPublicKey().GetAddress(ScriptPubKeyType.Segwit, Network.Main).ToString(), first);
		Assert.Equal("bc1qcr8te4kr609gcawutmrza0j4xv80jy8z306fyu", first);    // BIP84's first receive address
	}

	[Fact]
	public async Task WalletScanStopsAfterTwentyUnused()
	{
		var root = new Mnemonic("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about").DeriveExtKey();
		string xpub = root.Derive(new KeyPath("m/84'/0'/0'")).Neuter().GetWif(Network.Main).ToString();
		var fake = new Fake();
		fake.Answers[$"{Api.Esplora}/blocks/tip/height"] = "10";
		var report = await CryptoBalance.CheckAsync(CryptoAddress.Check(xpub), 3, Api, fake.Fetch, null, default, NoWait);
		Assert.Equal(0m, report.Assets.Single().Amount);
		Assert.Equal(2 * CryptoBalance.GapLimit, fake.Calls.Count);    // 20 receive and 20 change, all unused
		Assert.Contains("0 used addresses of 40", report.Notes.Single());
	}

	[Fact]
	public async Task WalletScanAsksEachAddressOnce()
	{
		var root = new Mnemonic("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about").DeriveExtKey();
		string xpub = root.Derive(new KeyPath("m/84'/0'/0'")).Neuter().GetWif(Network.Main).ToString();
		const string first = "bc1qcr8te4kr609gcawutmrza0j4xv80jy8z306fyu";
		var fake = new Fake();
		fake.Answers[$"{Api.Esplora}/blocks/tip/height"] = "10";
		fake.Answers[$"{Api.Esplora}/address/{first}/txs"] = "[]";
		fake.Answers[$"{Api.Esplora}/address/{first}"] =
			"""{"chain_stats":{"funded_txo_sum":7000,"spent_txo_sum":2000,"tx_count":2},"mempool_stats":{"funded_txo_sum":0,"spent_txo_sum":0,"tx_count":0}}""";
		var report = await CryptoBalance.CheckAsync(CryptoAddress.Check(xpub), 3, Api, fake.Fetch, null, default, NoWait);
		Assert.Equal(0.00005m, report.Assets.Single().Amount);
		Assert.Single(fake.Calls, c => c == $"{Api.Esplora}/address/{first}");    // the scan's answer is reused for the balance
		Assert.Equal(21 + 20 + 2, fake.Calls.Count);    // receive 0..20, change 0..19, tip, one history
	}

	[Fact]
	public async Task PricesAreFetchedOnceAndCached()
	{
		string dir = Path.Combine(Path.GetTempPath(), "softcalc-prices-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try
		{
			int calls = 0;
			var now = new DateTime(2026, 11, 15, 7, 0, 0, DateTimeKind.Utc);
			var source = new PriceSource(Path.Combine(dir, "prices.json"), (url, cancel) =>
			{
				calls++;
				Assert.Contains("ids=bitcoin,ethereum,tron,tether", url);
				return Task.FromResult("""{"bitcoin":{"thb":2200000,"usd":65000,"last_updated_at":1},"tether":{"thb":34}}""");
			}, () => now);
			var payload = await source.FetchAsync();
			Assert.Equal("2200000", (string?)payload["prices"]?["btc"]?["thb"]);
			await source.FetchAsync();
			Assert.Equal(1, calls);    // fresh for 5 minutes
			now = now.AddMinutes(6);
			await source.FetchAsync();
			Assert.Equal(2, calls);
			var reload = new PriceSource(Path.Combine(dir, "prices.json"), (_, _) => throw new HttpRequestException("offline"), () => now);
			reload.LoadCached();
			Assert.Equal(34m, reload.Held!.Price("usdt", "thb"));
			now = now.AddMinutes(10);
			var stale = await reload.FetchAsync();
			Assert.True((bool?)stale["stale"]);
		}
		finally
		{
			Directory.Delete(dir, true);
		}
	}
}
