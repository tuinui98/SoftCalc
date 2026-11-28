// TimerState.cs - The stopwatch, timers and alarms the UI keeps (owner 2026-11-27/28)
//
// The engine reads `:sw`, `:timer`, `:alarm` lines and sends an action
// (engine timers.py); this class keeps the clock: one stopwatch with its
// laps, and a list of timers and alarms, each with the moment it is due.
// Times are UTC wall clock, so the state goes into settings.json and a
// running stopwatch or a timer survives closing SoftCalc. Pure: every
// method takes "now", so the tests need no waiting. web/app/src/lib/timers.ts
// is the same.

using System.Globalization;
using System.Text.Json.Serialization;

namespace SoftCalc.Logic;

internal sealed class Lap
{
	public int Number { get; set; }
	public double Seconds { get; set; }    // this lap
	public double Total { get; set; }      // since the start
}

// A finished stopwatch run, kept so `:sw *` shows it after a restart (owner 2026-11-28)
internal sealed class StopwatchRun
{
	public double Total { get; set; }
	public List<Lap> Laps { get; set; } = new();
	public List<double> Pauses { get; set; } = new();    // the time shown at each pause
	public DateTime Ended { get; set; }                   // UTC
}

internal sealed class Countdown
{
	public string Kind { get; set; } = "timer";    // timer | alarm
	public string Label { get; set; } = "";        // 5m, 0:15:00, 28.11.2026 14:30:00
	public string? Name { get; set; }
	public DateTime Due { get; set; }               // UTC
	public DateTime Set { get; set; }               // UTC, when it was set
}

internal sealed class TimerState
{
	public static readonly string[] Sounds = { "once", "repeat", "off" };

	public double Before { get; set; }              // seconds measured before the current run
	public DateTime? RunningSince { get; set; }     // UTC; null when stopped or paused
	public List<Lap> Laps { get; set; } = new();
	public List<double> Pauses { get; set; } = new();     // the time shown at each pause of this run
	public StopwatchRun? LastRun { get; set; }
	public List<Countdown> Countdowns { get; set; } = new();
	public string Sound { get; set; } = "once";    // when one is done (owner 2026-11-28: an option)

	[JsonIgnore]
	public bool Running => RunningSince != null;

	[JsonIgnore]
	public bool Started => Running || Before > 0 || Laps.Count > 0;

	[JsonIgnore]
	public bool Active => Started || Countdowns.Count > 0;

	public double Elapsed(DateTime now) => Before + (RunningSince is DateTime since ? Math.Max(0, (now - since).TotalSeconds) : 0);

	public void Start(DateTime now)
	{
		RunningSince ??= now;
	}

	public void Pause(DateTime now)
	{
		if (RunningSince != null)
		{
			Before = Elapsed(now);
			RunningSince = null;
			Pauses.Add(Before);
		}
	}

	/// <summary>The time measured, and the stopwatch cleared; null when it never started.</summary>
	public double? Stop(DateTime now)
	{
		if (!Started)
		{
			return null;
		}
		double total = Elapsed(now);
		LastRun = new StopwatchRun { Total = total, Laps = new List<Lap>(Laps), Pauses = new List<double>(Pauses), Ended = now };
		Reset();
		return total;
	}

	public void Reset()
	{
		Before = 0;
		RunningSince = null;
		Laps = new List<Lap>();
		Pauses = new List<double>();
	}

	/// <summary>A new lap, or null when the stopwatch has not started.</summary>
	public Lap? AddLap(DateTime now)
	{
		if (!Started)
		{
			return null;
		}
		double total = Elapsed(now);
		var lap = new Lap { Number = Laps.Count + 1, Seconds = total - (Laps.Count > 0 ? Laps[^1].Total : 0), Total = total };
		Laps.Add(lap);
		return lap;
	}

	/// <summary>A new timer or alarm; a name already in use sets that one again (owner 2026-11-28). Changed: it was there.</summary>
	public (Countdown Countdown, bool Changed) Add(string kind, string label, string? name, double seconds, DateTime now)
	{
		var countdown = name == null ? null
			: Countdowns.FirstOrDefault(c => c.Kind == kind && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
		bool changed = countdown != null;
		countdown ??= new Countdown { Kind = kind, Name = name };
		countdown.Label = label;
		countdown.Due = now.AddSeconds(seconds);
		countdown.Set = now;
		if (!changed)
		{
			Countdowns.Add(countdown);
		}
		Countdowns.Sort((a, b) => a.Due.CompareTo(b.Due));
		return (countdown, changed);
	}

	/// <summary>Cancels the timers or alarms of a kind, all or by name; how many went.</summary>
	public int Cancel(string kind, string? name)
	{
		return Countdowns.RemoveAll(c => c.Kind == kind
			&& (name == null || string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)));
	}

	public int Cancel(Countdown countdown) => Countdowns.Remove(countdown) ? 1 : 0;

	/// <summary>Those due by now, taken off the list.</summary>
	public List<Countdown> TakeDue(DateTime now)
	{
		var due = Countdowns.Where(c => c.Due <= now).ToList();
		Countdowns.RemoveAll(c => c.Due <= now);
		return due;
	}

	public IEnumerable<Countdown> Of(string kind) => Countdowns.Where(c => c.Kind == kind);

	/// <summary>The faint text at the right of the hint line: the stopwatch, the nearest countdown, and how many more.</summary>
	public string Readout(DateTime now)
	{
		var parts = new List<string>();
		if (Started)
		{
			parts.Add((Running ? "⏱ " : "⏸ ") + Clock(Elapsed(now), 1));
		}
		if (Countdowns.Count > 0)
		{
			var next = Countdowns[0];
			string left = Left(next, now);
			parts.Add((next.Kind == "alarm" ? "⏰ " : "⌛ ") + left + (Countdowns.Count > 1 ? $" +{Countdowns.Count - 1}" : ""));
		}
		return string.Join("   ", parts);
	}

	/// <summary>Time left, whole seconds rounded up: 4:12, 1:04:12.</summary>
	public static string Left(Countdown countdown, DateTime now)
	{
		double seconds = Math.Ceiling(Math.Max(0, (countdown.Due - now).TotalSeconds));
		var span = TimeSpan.FromSeconds(seconds);
		int hours = (int)span.TotalHours;
		return hours > 0 ? $"{hours}:{span.Minutes:00}:{span.Seconds:00}" : $"{span.Minutes}:{span.Seconds:00}";
	}

	/// <summary>h:mm:ss with places decimals, as the engine writes a duration: 0:01:23.4.</summary>
	public static string Clock(double seconds, int places)
	{
		double scale = Math.Pow(10, places);
		double rounded = Math.Floor(Math.Max(0, seconds) * scale) / scale;
		int hours = (int)(rounded / 3600);
		int minutes = (int)(rounded / 60) % 60;
		double rest = rounded - hours * 3600 - minutes * 60;
		string secondsText = rest.ToString(places > 0 ? "00." + new string('0', places) : "00", CultureInfo.InvariantCulture);
		return $"{hours}:{minutes:00}:{secondsText}";
	}

	/// <summary>A run's laps and pauses in order, as History lists them: ("Lap 1 0:00:12.30", "total 0:00:12.30").</summary>
	public static List<(string Title, string Detail)> Marks(IReadOnlyList<Lap> laps, IReadOnlyList<double> pauses)
	{
		var marks = laps.Select(l => (l.Total, Order: 0, Title: $"Lap {l.Number}  {Clock(l.Seconds, 2)}", Detail: $"total {Clock(l.Total, 2)}"))
			.Concat(pauses.Select(p => (Total: p, Order: 1, Title: $"Paused at {Clock(p, 1)}", Detail: "")))
			.OrderBy(m => m.Total).ThenBy(m => m.Order);
		return marks.Select(m => (m.Title, m.Detail)).ToList();
	}

	/// <summary>The hint for a name being typed in quotes: `:timer 5m "te` lists the timers' names (owner 2026-11-28).</summary>
	public string? NameHint(string left)
	{
		var match = System.Text.RegularExpressions.Regex.Match(left, @"^\s*:(timer|alarm)\b[^""]*""([^""]*)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
		if (!match.Success)
		{
			return null;
		}
		string kind = match.Groups[1].Value.ToLowerInvariant();
		string typed = match.Groups[2].Value;
		var names = Of(kind).Where(c => c.Name != null).Select(c => c.Name!)
			.Where(n => n.StartsWith(typed, StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		if (names.Count == 0)
		{
			return $"{kind} names: none{(typed.Length > 0 ? " like that" : " yet")}; a new name starts a new {kind}";
		}
		return $"{kind} names: {string.Join(", ", names)}  (one of them sets its time again)";
	}

	/// <summary>What a countdown is called in History: Timer 5m "tea".</summary>
	public static string Title(Countdown countdown)
	{
		string kind = countdown.Kind == "alarm" ? "Alarm" : "Timer";
		return $"{kind} {countdown.Label}" + (countdown.Name != null ? $" \"{countdown.Name}\"" : "");
	}
}
