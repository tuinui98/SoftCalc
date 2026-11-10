// RatesSource.cs - Fetch, cache and hand ECB rates to the engine (E8)
//
// The only network access in SoftCalc. A fetch happens only when the
// engine reports `rates_needed` for a currency conversion; the raw ECB
// file is cached as rates.xml and loaded at start without fetching.
// Before the first fetch the published exe has the rates of its build
// day (build/publish.ps1 embeds them as rates-seed.xml, owner
// 2026-11-10): held and due at once, so the first currency line still
// fetches, and shows them `, offline` when that fails.

using System.Text.Json.Nodes;
using SoftCalc.Logic;
using SoftCalc.Persistence;

namespace SoftCalc.Engine;

internal sealed class RatesSource
{
	public const string EcbUrl = "https://www.ecb.europa.eu/stats/eurofxref/eurofxref-daily.xml";
	private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(5);

	private readonly string _cachePath;
	private readonly Func<CancellationToken, Task<string>> _download;
	private readonly Func<DateTime> _clock;
	private readonly Func<string?> _seed;
	private EcbRates? _held;

	public RatesSource(string cachePath, Func<CancellationToken, Task<string>>? download = null, Func<DateTime>? clock = null,
		Func<string?>? seed = null)
	{
		_cachePath = cachePath;
		_download = download ?? DownloadAsync;
		_clock = clock ?? (() => DateTime.UtcNow);
		_seed = seed ?? EmbeddedSeed;
	}

	public EcbRates? Held => _held;

	// Start-up: the cached rates, due at once when they are not the latest
	public JsonObject LoadCached()
	{
		try
		{
			_held = File.Exists(_cachePath) ? EcbRates.Parse(File.ReadAllText(_cachePath)) : null;
		}
		catch (IOException)
		{
			_held = null;
		}
		var now = _clock();
		if (_held == null)
		{
			string? seed = _seed();
			_held = seed == null ? null : EcbRates.Parse(seed);
			return Payload(_held, stale: false, now);    // build-day rates: due at once
		}
		return Payload(_held, stale: false, EcbCalendar.NextCheckUtc(_held.Date, now, TimeSpan.Zero));
	}

	// rates-seed.xml in the published exe; development builds have none
	private static string? EmbeddedSeed()
	{
		using var stream = typeof(RatesSource).Assembly.GetManifestResourceStream("rates-seed.xml");
		if (stream == null)
		{
			return null;
		}
		using var reader = new StreamReader(stream);
		return reader.ReadToEnd();
	}

	// Called on `rates_needed`: fetch, or fall back to what is held
	public async Task<JsonObject> FetchAsync()
	{
		EcbRates? fetched = null;
		string? xml = null;
		try
		{
			using var cancel = new CancellationTokenSource(FetchTimeout);
			xml = await _download(cancel.Token);
			fetched = EcbRates.Parse(xml);
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or IOException)
		{
		}
		var now = _clock();
		if (fetched == null)
		{
			return Payload(_held, stale: true, now + EcbCalendar.RetryDelay);
		}
		_held = fetched;
		try
		{
			DataFolder.WriteAtomic(_cachePath, xml!);
		}
		catch (IOException)
		{
			// the rates still work for this session
		}
		return Payload(fetched, stale: false, EcbCalendar.NextCheckUtc(fetched.Date, now, EcbCalendar.RetryDelay));
	}

	internal static JsonObject Payload(EcbRates? rates, bool stale, DateTime nextCheckUtc)
	{
		var map = new JsonObject();
		foreach (var (code, rate) in rates?.PerEur ?? new Dictionary<string, string>())
		{
			map[code] = rate;
		}
		return new JsonObject
		{
			["date"] = rates?.Date.ToString("yyyy-MM-dd"),
			["rates"] = map,
			["stale"] = stale,
			["next_check"] = new DateTimeOffset(DateTime.SpecifyKind(nextCheckUtc, DateTimeKind.Utc)).ToUnixTimeSeconds(),
		};
	}

	private static async Task<string> DownloadAsync(CancellationToken cancel)
	{
		using var http = new HttpClient();
		http.DefaultRequestHeaders.UserAgent.ParseAdd("SoftCalc/1.0");
		return await http.GetStringAsync(EcbUrl, cancel);
	}
}
