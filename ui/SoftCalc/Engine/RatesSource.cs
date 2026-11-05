// RatesSource.cs - Fetch, cache and hand ECB rates to the engine (E8)
//
// The only network access in SoftCalc. A fetch happens only when the
// engine reports `rates_needed` for a currency conversion; the raw ECB
// file is cached as rates.xml and loaded at start without fetching.

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
	private EcbRates? _held;

	public RatesSource(string cachePath, Func<CancellationToken, Task<string>>? download = null, Func<DateTime>? clock = null)
	{
		_cachePath = cachePath;
		_download = download ?? DownloadAsync;
		_clock = clock ?? (() => DateTime.UtcNow);
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
		var nextCheck = _held == null ? now : EcbCalendar.NextCheckUtc(_held.Date, now, TimeSpan.Zero);
		return Payload(_held, stale: false, nextCheck);
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
