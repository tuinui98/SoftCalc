// RandomBeacons.cs - Public random values from the NIST Randomness Beacon and drand (design/random_plan.md R5)
//
// Owner 2026-11-12: two public sources for the Entropy tab. Requests
// carry nothing about the user: a GET of the latest value, then of the
// values before it for Get x10. The values are public, so they count
// as no secret bits.

using System.Globalization;
using System.Text.Json.Nodes;
using SoftCalc.Logic;

namespace SoftCalc.Engine;

internal static class RandomBeacons
{
	public const string NistLast = "https://beacon.nist.gov/beacon/2.0/pulse/last";
	public const string DrandLatest = "https://api.drand.sh/public/latest";
	private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

	// count values, newest first, or null and the reason
	public static async Task<(List<EntropySample>? Samples, string? Error)> GetAsync(string id, int count)
	{
		try
		{
			using var http = new HttpClient { Timeout = Timeout };
			http.DefaultRequestHeaders.UserAgent.ParseAdd("SoftCalc");
			if (id == "nist")
			{
				var last = ParseNist(await http.GetStringAsync(NistLast));
				var older = await Task.WhenAll(Enumerable.Range(1, count - 1).Select(k =>
					http.GetStringAsync($"https://beacon.nist.gov/beacon/2.0/chain/{last.Chain}/pulse/{last.Index - k}")));
				return (older.Select(ParseNist).Prepend(last).Select(p => Sample($"pulse {p.Index}, {p.Time}", p.Hex)).ToList(), null);
			}
			var latest = ParseDrand(await http.GetStringAsync(DrandLatest));
			var before = await Task.WhenAll(Enumerable.Range(1, count - 1).Select(k =>
				http.GetStringAsync($"https://api.drand.sh/public/{latest.Round - k}")));
			return (before.Select(ParseDrand).Prepend(latest).Select(r => Sample($"round {r.Round}", r.Hex)).ToList(), null);
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or FormatException
			or InvalidOperationException or System.Text.Json.JsonException)
		{
			return (null, "The beacon could not be reached: " + ex.Message);
		}
	}

	private static EntropySample Sample(string label, string hex)
	{
		return new EntropySample($"{label}: {hex[..Math.Min(32, hex.Length)]}...", Convert.FromHexString(hex), 0);
	}

	internal static (long Chain, long Index, string Time, string Hex) ParseNist(string json)
	{
		var pulse = JsonNode.Parse(json)?["pulse"] ?? throw new FormatException("no pulse");
		string time = (string?)pulse["timeStamp"] ?? "";
		if (DateTime.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var at))
		{
			time = at.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
		}
		return ((long?)pulse["chainIndex"] ?? throw new FormatException("no chainIndex"),
			(long?)pulse["pulseIndex"] ?? throw new FormatException("no pulseIndex"), time,
			(string?)pulse["outputValue"] ?? throw new FormatException("no outputValue"));
	}

	internal static (long Round, string Hex) ParseDrand(string json)
	{
		var node = JsonNode.Parse(json) ?? throw new FormatException("empty");
		return ((long?)node["round"] ?? throw new FormatException("no round"),
			(string?)node["randomness"] ?? throw new FormatException("no randomness"));
	}
}
