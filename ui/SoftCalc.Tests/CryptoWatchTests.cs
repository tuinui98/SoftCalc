// CryptoWatchTests.cs - What a watched address reports between two looks (design/crypto_plan.md C2a)

using SoftCalc.Logic;
using SoftCalc.Persistence;

namespace SoftCalc.Tests;

public sealed class CryptoWatchTests
{
	private static readonly WatchRules Rules = new(BtcConfirmations: 2, TronConfirmations: 1, DustUsd: 1);
	private static readonly DateTime Now = new(2026, 11, 16, 3, 0, 0, DateTimeKind.Utc);
	private const string Mine = "bc1qcr8te4kr609gcawutmrza0j4xv80jy8z306fyu";
	private const string Friend = "bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh";

	private static decimal? Usd(string asset)
	{
		return asset switch
		{
			"BTC" => 65_000m,
			"USDT" => 1m,
			"TRX" => 0.3m,
			_ => null,
		};
	}

	private static BalanceReport Btc(params CryptoPayment[] payments)
	{
		decimal total = payments.Sum(p => p.Amount);
		return new BalanceReport("Bitcoin", new[] { new AssetBalance("BTC", "btc", total, 0) }, payments.Reverse().ToList(), Array.Empty<string>());
	}

	private static CryptoPayment Pay(string id, decimal amount, int confirmations, string? party = Friend)
	{
		return new CryptoPayment(null, "BTC", amount, "", id, party, confirmations);
	}

	private static List<WatchEvent> Look(WatchEntry entry, BalanceReport report)
	{
		return CryptoWatch.Update(entry, report, Rules, Usd, new[] { Mine }, Now);
	}

	[Fact]
	public void FirstLookOnlyLearns()
	{
		var entry = new WatchEntry { Label = "Binance BTC", Address = Mine };
		Assert.Empty(Look(entry, Btc(Pay("a", 0.01m, 10))));
		Assert.True(entry.Learned);
		Assert.Equal(0.01m, entry.Balances["BTC"]);
		Assert.Contains(Friend, entry.Counterparties);
	}

	[Fact]
	public void IncomingThenConfirmedThenOutgoing()
	{
		var entry = new WatchEntry { Label = "Binance BTC", Address = Mine };
		Look(entry, Btc(Pay("a", 0.01m, 10)));
		var arrived = Look(entry, Btc(Pay("a", 0.01m, 10), Pay("b", 0.002m, 0)));
		Assert.Equal(WatchKind.Incoming, arrived.Single().Kind);
		Assert.Contains("+0.002 BTC arrived (pending)", arrived.Single().Text);
		Assert.True(entry.Waiting);
		Assert.Empty(Look(entry, Btc(Pay("a", 0.01m, 11), Pay("b", 0.002m, 1))));    // 1 of 2
		var confirmed = Look(entry, Btc(Pay("a", 0.01m, 12), Pay("b", 0.002m, 2)));
		Assert.Equal((WatchKind.Confirmed, "b"), (confirmed.Single().Kind, confirmed.Single().TxId));
		Assert.False(entry.Waiting);
		Assert.Empty(Look(entry, Btc(Pay("a", 0.01m, 13), Pay("b", 0.002m, 3))));    // told once
		var sent = Look(entry, Btc(Pay("a", 0.01m, 13), Pay("b", 0.002m, 3), Pay("c", -0.005m, 0, Friend)));
		Assert.Equal(WatchKind.Outgoing, sent.Single().Kind);
	}

	[Fact]
	public void PoisoningIsFlagged()
	{
		var entry = new WatchEntry { Label = "Binance BTC", Address = Mine };
		Look(entry, Btc(Pay("a", 0.01m, 10)));
		// same first and last 4 characters as a known party, different middle
		string twin = "bc1qxy2kAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAx0wlh";
		var copied = Look(entry, Btc(Pay("a", 0.01m, 10), Pay("p", 0.0001m, 1, twin)));
		Assert.Equal(WatchKind.Suspicious, copied.Single().Kind);
		Assert.Contains("imitates", copied.Single().Text);
		Assert.DoesNotContain(twin, entry.Counterparties);    // a poisoner is not remembered
		var dust = Look(entry, Btc(Pay("a", 0.01m, 10), Pay("p", 0.0001m, 1, twin), Pay("d", 0.00000546m, 1, "bc1qsomeoneelse00000000000000000000000zzzz")));
		Assert.Contains("dust", dust.Single().Text);    // 546 sat is about 0.35 USD
		var zero = CryptoWatch.Suspicion(new CryptoPayment(null, "USDT", 0, "", "z", "TXyz", 19), Usd, 1, entry, new[] { Mine });
		Assert.Contains("0 USDT transfer", zero);
	}

	[Theory]
	[InlineData("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", "0x5aaeb6053f3e94c9b9a09f33669435e7ef1beaed", false)]    // the same, other case
	[InlineData("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", "0x5aAe000000000000000000000000000000001234", false)]    // last 4 differ
	[InlineData("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", "0x5aAe00000000000000000000000000000000eAed", true)]
	[InlineData("TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t", "TR7NHxxxxxxxxxxxxxxxxxxxxxxxxxLj6t", true)]
	public void LookAlikes(string a, string b, bool alike)
	{
		Assert.Equal(alike, CryptoWatch.LookAlike(a, b));
	}

	[Fact]
	public void EthereumBalanceMoves()
	{
		var entry = new WatchEntry { Label = "Bitkub ETH", Address = "0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed" };
		BalanceReport Eth(decimal eth, decimal usdt) => new("Ethereum",
			new[] { new AssetBalance("ETH", "eth", eth, 0), new AssetBalance("USDT (ERC-20)", "usdt", usdt, 0) }, Array.Empty<CryptoPayment>(),
			Array.Empty<string>());
		Look(entry, Eth(1, 0));
		var events = Look(entry, Eth(1, 50));
		Assert.Equal("USDT (ERC-20) balance +50 (now 50).", events.Single().Text);
		Assert.Equal(Now, entry.ChangedUtc);
	}

	[Fact]
	public void StoreKeepsOnlySingleAddresses()
	{
		string dir = Path.Combine(Path.GetTempPath(), "softcalc-watch-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try
		{
			string path = Path.Combine(dir, "crypto.json");
			File.WriteAllText(path, """
				{"entries":[{"label":"ok","address":"bc1qcr8te4kr609gcawutmrza0j4xv80jy8z306fyu","seen":{"a":3}},
				 {"label":"typo","address":"bc1qcr8te4kr609gcawutmrza0j4xv80jy8z306fyv"},
				 {"label":"wallet","address":"xpub6BosfCnifzxcFwrSzQiqu2DBVTshkCXacvNsWGYJVVhhawA7d4R5WSWGFNbi8Aw6ZRc1brxMyWMzG3DSSSSoekkudhUd9yLb6qx39T9nMdj"}],
				 "events":[{"time_utc":"2026-11-16T03:00:00Z","label":"ok","kind":"suspicious","text":"x","tx_id":"t"}]}
				""");
			var (file, warning) = CryptoWatchStore.Load(path);
			Assert.Null(warning);
			Assert.Equal("ok", file.Entries.Single().Label);
			Assert.Equal(3, file.Entries[0].Seen["a"]);
			Assert.Equal(WatchKind.Suspicious, file.Events.Single().Kind);
		}
		finally
		{
			Directory.Delete(dir, true);
		}
	}
}
