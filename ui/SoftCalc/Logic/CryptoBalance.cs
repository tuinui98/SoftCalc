// CryptoBalance.cs - Balances and recent payments of an address or a watch-only Bitcoin key (design/crypto_plan.md C1)
//
// Owner 2026-11-15: read only, asked only when the user presses Check.
// Bitcoin through an Esplora API (mempool.space by default, or one's own
// node), Ethereum through JSON-RPC (ETH and USDT-ERC20; no history, which
// plain RPC cannot give by address), Tron through TronGrid (TRX and
// USDT-TRC20 with their history). A Bitcoin xpub/ypub/zpub is scanned
// address by address until 20 unused in a row on each chain (receive and
// change); only public keys are involved.

using System.Globalization;
using System.Numerics;
using System.Text.Json.Nodes;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace SoftCalc.Logic;

internal sealed record CryptoEndpoints(string Esplora, string EthereumRpc, string TronGrid)
{
	public static readonly CryptoEndpoints Default = new("https://mempool.space/api", "https://ethereum-rpc.publicnode.com",
		"https://api.trongrid.io");
}

internal sealed record AssetBalance(string Asset, string Coin, decimal Amount, decimal Pending);

internal sealed record CryptoPayment(DateTime? TimeUtc, string Asset, decimal Amount, string Status, string Id);

internal sealed record BalanceReport(string Network, IReadOnlyList<AssetBalance> Assets, IReadOnlyList<CryptoPayment> Payments,
	IReadOnlyList<string> Notes);

// GET (body null) or POST JSON; returns the response text
internal delegate Task<string> CryptoFetch(string url, string? jsonBody, CancellationToken cancel);

internal static class CryptoBalance
{
	public const string UsdtErc20 = "0xdAC17F958D2ee523a2206206994597C13D831ec7";    // checked 2026-11-15: symbol USDT, 6 decimals
	public const string UsdtTrc20 = "TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t";    // checked 2026-11-15: TetherToken
	public const int GapLimit = 20;
	public static readonly string[] XpubTypes = { "Auto", "Legacy (1...)", "SegWit in P2SH (3...)", "Native SegWit (bc1q...)", "Taproot (bc1p...)" };

	// Which service an address or key will be sent to
	public static string Service(AddressCheck check, CryptoEndpoints endpoints)
	{
		string url = check.Network switch
		{
			"Ethereum" => endpoints.EthereumRpc,
			"Tron" => endpoints.TronGrid,
			_ => endpoints.Esplora,
		};
		return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host + (uri.IsDefaultPort ? "" : ":" + uri.Port) : url;
	}

	public static async Task<BalanceReport> CheckAsync(AddressCheck check, int xpubType, CryptoEndpoints endpoints, CryptoFetch fetch,
		IProgress<string>? progress, CancellationToken cancel)
	{
		if (!check.Valid || check.Normal == null)
		{
			throw new InvalidOperationException("not a valid address");
		}
		return check.Network switch
		{
			"Ethereum" => await EthereumAsync(check.Normal, endpoints.EthereumRpc, fetch, cancel),
			"Tron" => await TronAsync(check.Normal, endpoints.TronGrid, fetch, cancel),
			_ when check.Kind?.Contains("extended public key") == true => await WalletAsync(check.Normal, xpubType, endpoints.Esplora, fetch,
				progress, cancel),
			_ => await BitcoinAsync(new[] { check.Normal }, endpoints.Esplora, fetch, cancel),
		};
	}

	//==================================================================
	//  Bitcoin (Esplora): balances in satoshis, history with confirmations
	//==================================================================
	private static async Task<BalanceReport> BitcoinAsync(IReadOnlyList<string> addresses, string api, CryptoFetch fetch, CancellationToken cancel)
	{
		long confirmed = 0, pending = 0;
		foreach (string address in addresses)
		{
			var stats = Object(await fetch($"{api}/address/{address}", null, cancel));
			confirmed += Sum(stats["chain_stats"]);
			pending += Sum(stats["mempool_stats"]);
		}
		var payments = await BitcoinPaymentsAsync(addresses, api, fetch, cancel);
		return new BalanceReport("Bitcoin", new[] { new AssetBalance("BTC", "btc", Sats(confirmed), Sats(pending)) }, payments,
			Array.Empty<string>());
	}

	private static long Sum(JsonNode? stats)
	{
		return Long(stats?["funded_txo_sum"]) - Long(stats?["spent_txo_sum"]);
	}

	private static async Task<List<CryptoPayment>> BitcoinPaymentsAsync(IReadOnlyList<string> addresses, string api, CryptoFetch fetch,
		CancellationToken cancel)
	{
		var mine = addresses.ToHashSet(StringComparer.Ordinal);
		long tip = long.Parse((await fetch($"{api}/blocks/tip/height", null, cancel)).Trim(), CultureInfo.InvariantCulture);
		var seen = new Dictionary<string, CryptoPayment>();
		foreach (string address in addresses)
		{
			foreach (var tx in Items(await fetch($"{api}/address/{address}/txs", null, cancel)))
			{
				string id = (string?)tx?["txid"] ?? "";
				if (id.Length == 0 || seen.ContainsKey(id))
				{
					continue;
				}
				long change = 0;
				foreach (var output in tx!["vout"]?.AsArray() ?? new JsonArray())
				{
					change += mine.Contains((string?)output?["scriptpubkey_address"] ?? "") ? Long(output?["value"]) : 0;
				}
				foreach (var input in tx["vin"]?.AsArray() ?? new JsonArray())
				{
					change -= mine.Contains((string?)input?["prevout"]?["scriptpubkey_address"] ?? "") ? Long(input?["prevout"]?["value"]) : 0;
				}
				var status = tx["status"];
				bool done = (bool?)status?["confirmed"] ?? false;
				long height = Long(status?["block_height"]);
				DateTime? time = done ? DateTimeOffset.FromUnixTimeSeconds(Long(status?["block_time"])).UtcDateTime : null;
				seen[id] = new CryptoPayment(time, "BTC", Sats(change), done ? Confirmations(tip - height + 1) : "pending", id);
			}
		}
		return seen.Values.OrderByDescending(p => p.TimeUtc ?? DateTime.MaxValue).ToList();
	}

	private static string Confirmations(long count)
	{
		return count >= 6 ? "confirmed (6+)" : $"{count} confirmation{(count == 1 ? "" : "s")}";
	}

	private static decimal Sats(long sats)
	{
		return sats / 100_000_000m;
	}

	//==================================================================
	//  A watch-only Bitcoin wallet: receive and change addresses until 20 unused in a row
	//==================================================================
	private static async Task<BalanceReport> WalletAsync(string key, int type, string api, CryptoFetch fetch, IProgress<string>? progress,
		CancellationToken cancel)
	{
		var (account, network, scriptType) = Account(key, type);
		var used = new List<string>();
		int looked = 0;
		foreach (uint chain in new uint[] { 0, 1 })
		{
			var branch = account.Derive(chain);
			for (uint i = 0, unused = 0; unused < GapLimit; i++)
			{
				string address = branch.Derive(i).PubKey.GetAddress(scriptType, network).ToString();
				var stats = Object(await fetch($"{api}/address/{address}", null, cancel));
				long count = Long(stats["chain_stats"]?["tx_count"]) + Long(stats["mempool_stats"]?["tx_count"]);
				unused = count > 0 ? 0 : unused + 1;
				if (count > 0)
				{
					used.Add(address);
				}
				looked++;
				progress?.Report($"Looked at {looked} addresses, {used.Count} used...");
			}
		}
		var report = used.Count == 0
			? new BalanceReport("Bitcoin", new[] { new AssetBalance("BTC", "btc", 0, 0) }, Array.Empty<CryptoPayment>(), Array.Empty<string>())
			: await BitcoinAsync(used, api, fetch, cancel);
		return report with
		{
			Notes = new[] { $"{used.Count} used addresses of {looked} looked at ({XpubTypes[TypeIndex(key, type)]}, gap limit {GapLimit})." },
		};
	}

	// The account key in the xpub form NBitcoin reads, its network and the address type
	public static (ExtPubKey Account, Network Network, ScriptPubKeyType Type) Account(string key, int type)
	{
		byte[] data = Encoders.Base58Check.DecodeData(key);
		bool test = key[0] is 't' or 'u' or 'v' or 'T' or 'U' or 'V';
		var network = test ? Network.TestNet : Network.Main;
		// the version bytes of xpub / tpub, so any of x/y/z pub reads as one
		byte[] version = test ? new byte[] { 0x04, 0x35, 0x87, 0xCF } : new byte[] { 0x04, 0x88, 0xB2, 0x1E };
		version.CopyTo(data, 0);
		var account = new BitcoinExtPubKey(Encoders.Base58Check.EncodeData(data), network).ExtPubKey;
		var scriptType = TypeIndex(key, type) switch
		{
			1 => ScriptPubKeyType.Legacy,
			2 => ScriptPubKeyType.SegwitP2SH,
			4 => ScriptPubKeyType.TaprootBIP86,
			_ => ScriptPubKeyType.Segwit,
		};
		return (account, network, scriptType);
	}

	// Auto: x/tpub legacy, y/upub SegWit in P2SH, z/vpub native SegWit
	private static int TypeIndex(string key, int type)
	{
		return type > 0 ? type : char.ToLowerInvariant(key[0]) switch
		{
			'y' or 'u' => 2,
			'z' or 'v' => 3,
			_ => 1,
		};
	}

	//==================================================================
	//  Ethereum (JSON-RPC): ETH and USDT; no history by address
	//==================================================================
	private static async Task<BalanceReport> EthereumAsync(string address, string rpc, CryptoFetch fetch, CancellationToken cancel)
	{
		string wei = Rpc(await fetch(rpc, $"{{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_getBalance\",\"params\":[\"{address}\",\"latest\"]}}",
			cancel));
		string data = "0x70a08231" + address[2..].ToLowerInvariant().PadLeft(64, '0');    // balanceOf(address)
		string usdt = Rpc(await fetch(rpc, "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"eth_call\",\"params\":[{\"to\":\""
			+ UsdtErc20 + "\",\"data\":\"" + data + "\"},\"latest\"]}", cancel));
		return new BalanceReport("Ethereum",
			new[] { new AssetBalance("ETH", "eth", Units(Hex(wei), 18), 0), new AssetBalance("USDT (ERC-20)", "usdt", Units(Hex(usdt), 6), 0) },
			Array.Empty<CryptoPayment>(),
			new[] { "Ethereum's public RPC gives balances but no payment history by address." });
	}

	private static string Rpc(string json)
	{
		var reply = Object(json);
		if (reply["error"] is JsonNode error)
		{
			throw new InvalidDataException("Ethereum RPC: " + ((string?)error["message"] ?? error.ToJsonString()));
		}
		return (string?)reply["result"] ?? throw new InvalidDataException("Ethereum RPC: no result");
	}

	private static BigInteger Hex(string hex)
	{
		string digits = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : hex;
		return digits.Length == 0 ? BigInteger.Zero : BigInteger.Parse("0" + digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
	}

	// amount / 10^decimals; a whole part past what decimal holds (spam tokens) shows as the largest decimal
	public static decimal Units(BigInteger amount, int decimals)
	{
		var whole = BigInteger.DivRem(BigInteger.Abs(amount), BigInteger.Pow(10, decimals), out var rest);
		if (whole > new BigInteger(decimal.MaxValue) - 1)
		{
			return amount.Sign < 0 ? decimal.MinValue : decimal.MaxValue;
		}
		decimal value = (decimal)whole + (decimal)rest / (decimal)BigInteger.Pow(10, decimals);
		return amount.Sign < 0 ? -value : value;
	}

	//==================================================================
	//  Tron (TronGrid): TRX and USDT, with their recent transfers
	//==================================================================
	private static async Task<BalanceReport> TronAsync(string address, string api, CryptoFetch fetch, CancellationToken cancel)
	{
		var account = Items(Object(await fetch($"{api}/v1/accounts/{address}", null, cancel))["data"]?.ToJsonString() ?? "[]")
			.FirstOrDefault();
		decimal trx = Long(account?["balance"]) / 1_000_000m;
		decimal usdt = 0;
		foreach (var token in account?["trc20"]?.AsArray() ?? new JsonArray())
		{
			if (token?[UsdtTrc20] is JsonNode value)
			{
				usdt = Units(BigInteger.Parse((string?)value ?? "0", CultureInfo.InvariantCulture), 6);
			}
		}
		var payments = new List<CryptoPayment>();
		string hex = Convert.ToHexString(Encoders.Base58Check.DecodeData(address)).ToLowerInvariant();
		foreach (var tx in Items(Object(await fetch($"{api}/v1/accounts/{address}/transactions/trc20?limit=20&contract_address={UsdtTrc20}",
			null, cancel))["data"]?.ToJsonString() ?? "[]"))
		{
			decimal amount = Units(BigInteger.Parse((string?)tx?["value"] ?? "0", CultureInfo.InvariantCulture), 6);
			bool incoming = (string?)tx?["to"] == address;
			payments.Add(new CryptoPayment(Millis(tx?["block_timestamp"]), "USDT", incoming ? amount : -amount, "confirmed",
				(string?)tx?["transaction_id"] ?? ""));
		}
		foreach (var tx in Items(Object(await fetch($"{api}/v1/accounts/{address}/transactions?only_confirmed=true&limit=20", null, cancel))
			["data"]?.ToJsonString() ?? "[]"))
		{
			var contract = tx?["raw_data"]?["contract"]?[0];
			if ((string?)contract?["type"] != "TransferContract")
			{
				continue;
			}
			var value = contract?["parameter"]?["value"];
			decimal amount = Long(value?["amount"]) / 1_000_000m;
			bool incoming = string.Equals((string?)value?["to_address"], hex, StringComparison.OrdinalIgnoreCase);
			payments.Add(new CryptoPayment(Millis(tx?["block_timestamp"]), "TRX", incoming ? amount : -amount, "confirmed",
				(string?)tx?["txID"] ?? ""));
		}
		var notes = account == null ? new[] { "This address has never received anything (not activated on Tron)." } : Array.Empty<string>();
		return new BalanceReport("Tron", new[] { new AssetBalance("TRX", "trx", trx, 0), new AssetBalance("USDT (TRC-20)", "usdt", usdt, 0) },
			payments.OrderByDescending(p => p.TimeUtc).ToList(), notes);
	}

	private static DateTime? Millis(JsonNode? node)
	{
		long ms = Long(node);
		return ms > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime : null;
	}

	//==================================================================
	//  JSON helpers
	//==================================================================
	private static JsonObject Object(string json)
	{
		return JsonNode.Parse(json) as JsonObject ?? throw new InvalidDataException("The service did not answer with JSON: "
			+ (json.Length > 80 ? json[..80] + "..." : json));
	}

	private static IEnumerable<JsonNode?> Items(string json)
	{
		return JsonNode.Parse(json) as JsonArray ?? new JsonArray();
	}

	private static long Long(JsonNode? node)
	{
		return node is JsonValue value && value.TryGetValue(out long number) ? number : 0;
	}
}
