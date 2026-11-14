// PriceSource.cs - Fetch, cache and hand coin prices to the engine (design/crypto_plan.md C1)
//
// Owner 2026-11-15: prices of BTC, ETH, TRX and USDT come from CoinGecko,
// only when a line needs them (the engine reports `prices_needed`) or the
// Crypto window shows a value. One request asks for every ECB currency;
// the answer is cached as prices.json and is good for 5 minutes. When a
// fetch fails the cached prices are used and shown `, offline`. The
// request carries no address, only the coins and currencies.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using SoftCalc.Persistence;

namespace SoftCalc.Engine;

internal sealed class PriceSource
{
	public const string Api = "https://api.coingecko.com/api/v3/simple/price";
	public static readonly TimeSpan Fresh = TimeSpan.FromMinutes(5);
	private static readonly TimeSpan Retry = TimeSpan.FromMinutes(1);
	private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(8);

	// CoinGecko id -> the code SoftCalc uses
	public static readonly IReadOnlyDictionary<string, string> Coins = new Dictionary<string, string>
	{
		["bitcoin"] = "btc", ["ethereum"] = "eth", ["tron"] = "trx", ["tether"] = "usdt",
	};

	// The ECB's currencies (engine currency.CODES), asked of CoinGecko in one go
	private static readonly string[] Currencies =
	{
		"aud", "brl", "cad", "chf", "cny", "czk", "dkk", "eur", "gbp", "hkd", "huf", "idr", "ils", "inr", "isk", "jpy", "krw", "mxn",
		"myr", "nok", "nzd", "php", "pln", "ron", "sek", "sgd", "thb", "try", "usd", "zar",
	};

	private readonly string _cachePath;
	private readonly Func<string, CancellationToken, Task<string>> _download;
	private readonly Func<DateTime> _clock;
	private CoinPrices? _held;

	public PriceSource(string cachePath, Func<string, CancellationToken, Task<string>>? download = null, Func<DateTime>? clock = null)
	{
		_cachePath = cachePath;
		_download = download ?? DownloadAsync;
		_clock = clock ?? (() => DateTime.UtcNow);
	}

	public CoinPrices? Held => _held;

	public static string Url => $"{Api}?ids={string.Join(",", Coins.Keys)}&vs_currencies={string.Join(",", Currencies)}"
		+ "&include_last_updated_at=true";

	// Start-up: the cached prices, due at once (a line asks for fresh ones)
	public JsonObject LoadCached()
	{
		try
		{
			_held = File.Exists(_cachePath) ? CoinPrices.Parse(File.ReadAllText(_cachePath)) : null;
		}
		catch (Exception ex) when (ex is IOException or JsonException or FormatException)
		{
			_held = null;
		}
		return Payload(_held, stale: false, _clock());
	}

	// Called on `prices_needed` (and by the Crypto window): fetch, or fall back to what is held
	public async Task<JsonObject> FetchAsync()
	{
		var now = _clock();
		if (_held != null && now - _held.FetchedUtc < Fresh)
		{
			return Payload(_held, stale: false, _held.FetchedUtc + Fresh);
		}
		try
		{
			using var cancel = new CancellationTokenSource(FetchTimeout);
			string json = await _download(Url, cancel.Token);
			var fetched = CoinPrices.Parse(json, now);
			_held = fetched;
			try
			{
				DataFolder.WriteAtomic(_cachePath, fetched.ToJson());
			}
			catch (IOException)
			{
				// the prices still work for this session
			}
			return Payload(fetched, stale: false, now + Fresh);
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or IOException
			or JsonException or FormatException)
		{
			return Payload(_held, stale: true, now + Retry);
		}
	}

	internal static JsonObject Payload(CoinPrices? prices, bool stale, DateTime nextCheckUtc)
	{
		var map = new JsonObject();
		foreach (var (coin, perCurrency) in prices?.PerCoin ?? new Dictionary<string, Dictionary<string, decimal>>())
		{
			var inner = new JsonObject();
			foreach (var (currency, price) in perCurrency)
			{
				inner[currency] = price.ToString(CultureInfo.InvariantCulture);
			}
			map[coin] = inner;
		}
		return new JsonObject
		{
			["time"] = prices?.FetchedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
			["prices"] = map,
			["stale"] = stale,
			["next_check"] = new DateTimeOffset(DateTime.SpecifyKind(nextCheckUtc, DateTimeKind.Utc)).ToUnixTimeSeconds(),
		};
	}

	private static async Task<string> DownloadAsync(string url, CancellationToken cancel)
	{
		using var http = new HttpClient();
		http.DefaultRequestHeaders.UserAgent.ParseAdd("SoftCalc/1.0");
		return await http.GetStringAsync(url, cancel);
	}
}

// Prices of the coins in each currency, as CoinGecko gave them, and when they were fetched
internal sealed class CoinPrices
{
	public DateTime FetchedUtc { get; init; }
	public Dictionary<string, Dictionary<string, decimal>> PerCoin { get; init; } = new();    // btc -> thb -> price

	public decimal? Price(string coin, string currency)
	{
		return PerCoin.TryGetValue(coin, out var map) && map.TryGetValue(currency, out decimal price) ? price : null;
	}

	// CoinGecko's answer ({"bitcoin":{"thb":2200561,...,"last_updated_at":...}}), or the cache (the same plus "fetched")
	public static CoinPrices Parse(string json, DateTime? fetchedUtc = null)
	{
		var root = JsonNode.Parse(json)?.AsObject() ?? throw new FormatException("prices: not an object");
		var perCoin = new Dictionary<string, Dictionary<string, decimal>>();
		foreach (var (id, code) in PriceSource.Coins)
		{
			if (root[id] is not JsonObject prices)
			{
				continue;
			}
			var map = new Dictionary<string, decimal>();
			foreach (var (currency, value) in prices)
			{
				if (currency.Length == 3 && value is JsonValue v && v.TryGetValue(out decimal price) && price > 0)
				{
					map[currency] = price;
				}
			}
			if (map.Count > 0)
			{
				perCoin[code] = map;
			}
		}
		if (perCoin.Count == 0)
		{
			throw new FormatException("prices: no coin in the answer");
		}
		var fetched = fetchedUtc ?? (root["fetched"] is JsonValue f && f.TryGetValue(out long seconds)
			? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime : DateTime.MinValue);
		return new CoinPrices { FetchedUtc = fetched, PerCoin = perCoin };
	}

	public string ToJson()
	{
		var root = new JsonObject { ["fetched"] = new DateTimeOffset(DateTime.SpecifyKind(FetchedUtc, DateTimeKind.Utc)).ToUnixTimeSeconds() };
		foreach (var (id, code) in PriceSource.Coins)
		{
			if (PerCoin.TryGetValue(code, out var map))
			{
				var inner = new JsonObject();
				foreach (var (currency, price) in map)
				{
					inner[currency] = price;
				}
				root[id] = inner;
			}
		}
		return root.ToJsonString();
	}
}
