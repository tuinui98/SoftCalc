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

	// The countdown page's three numbers, [show, format] each, kept once for timers and once for alarms (owner 2026-11-28)
	public List<string[]> TimerSlots { get; set; } = DefaultSlots("timer");
	public List<string[]> AlarmSlots { get; set; } = DefaultSlots("alarm");
	public bool QuietCountdown { get; set; }    // no ticks in the last 10 seconds

	public static readonly string[] Shows = { "left", "since", "done", "remain", "set", "started", "ends", "clock" };
	public static readonly string[] ShowNames = { "Time left", "Since start", "% done", "% left", "Set length", "Started", "Ends", "Clock" };
	public static readonly string[] Formats = { "ddd hh:mm:ss.s", "hhh:mm:ss.s", "mmm:ss.s", "ssss.s" };
	// a time of day (owner 2026-11-28: the clock beside the end time): started, ends, the computer's clock
	public static readonly string[] ClockFormats = { "HH:mm:ss.s", "HH:mm:ss", "dd.MM.yyyy HH:mm:ss.s" };

	public static bool IsClock(string show) => show is "started" or "ends" or "clock";

	/// <summary>The formats a show takes: lengths, or times of day.</summary>
	public static string[] FormatsOf(string show) => IsClock(show) ? ClockFormats : Formats;

	public static List<string[]> DefaultSlots(string kind)
	{
		string format = kind == "alarm" ? "ddd hh:mm:ss.s" : "hhh:mm:ss.s";
		return new List<string[]> { new[] { "left", format }, new[] { "since", format }, new[] { "done", format } };
	}

	public List<string[]> Slots(string kind) => kind == "alarm" ? AlarmSlots : TimerSlots;

	[JsonIgnore]
	public bool Running => RunningSince != null;

	[JsonIgnore]
	public bool Started => Running || Before > 0 || Laps.Count > 0 || Pauses.Count > 0;    // paused at 0 is still started

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

	/// <summary>A new lap, or null when the stopwatch is not running (paused: the same time again, owner 2026-11-28).</summary>
	public Lap? AddLap(DateTime now)
	{
		if (!Running)
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
		countdown.Due = now.AddTicks((long)Math.Round(seconds * TimeSpan.TicksPerSecond));    // AddSeconds left 10:49:59.999
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

	//==================================================================
	//  The countdown page (owner 2026-11-28): its numbers and lines
	//==================================================================
	/// <summary>A length in one of Formats; up rounds tenths down, a countdown rounds them up (it never shows 0 early).</summary>
	public static string Span(double seconds, string format, bool roundUp)
	{
		double tenths = roundUp ? Math.Ceiling(Math.Max(0, seconds) * 10 - 1e-9) : Math.Floor(Math.Max(0, seconds) * 10 + 1e-9);
		long whole = (long)(tenths / 10);
		int tenth = (int)(tenths % 10);
		long s = whole % 60, m = whole / 60 % 60, h = whole / 3600, d = whole / 86400;
		return format switch
		{
			"ddd hh:mm:ss.s" => $"{d} {h % 24:00}:{m:00}:{s:00}.{tenth}",
			"hhh:mm:ss.s" => $"{h}:{m:00}:{s:00}.{tenth}",
			"mmm:ss.s" => $"{whole / 60}:{s:00}.{tenth}",
			_ => $"{whole}.{tenth}",
		};
	}

	/// <summary>The Set line: no days or hours when they are 0, at least mm:ss.ss (owner 2026-11-28).</summary>
	public static string Length(double seconds)
	{
		long hundredths = (long)Math.Round(Math.Max(0, seconds) * 100);
		long whole = hundredths / 100;
		string hund = (hundredths % 100).ToString("00");
		long s = whole % 60, m = whole / 60 % 60, h = whole / 3600 % 24, d = whole / 86400;
		if (d > 0)
		{
			return $"{d} {h:00}:{m:00}:{s:00}.{hund}";
		}
		return h > 0 ? $"{h}:{m:00}:{s:00}.{hund}" : $"{m:00}:{s:00}.{hund}";
	}

	/// <summary>Local date and time to a tenth: 28.11.2026 14:03:12.4.</summary>
	public static string Stamp(DateTime utc)
	{
		var local = utc.ToLocalTime();
		return local.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture) + "." + local.Millisecond / 100;
	}

	/// <summary>One number of the countdown page: T- 0:04:12.3, then T+ once it is past; 15.9 %.</summary>
	public static string Slot(Countdown countdown, string show, string format, DateTime now)
	{
		double total = Math.Max(0.001, (countdown.Due - countdown.Set).TotalSeconds);
		double since = (now - countdown.Set).TotalSeconds, left = (countdown.Due - now).TotalSeconds;
		double done = Math.Clamp(since / total * 100, 0, 100);
		return show switch
		{
			"left" => left > 0 ? "T- " + Span(left, format, true) : "T+ " + Span(-left, format, false),
			"since" => Span(since, format, false),
			"done" => done.ToString("0.0", CultureInfo.InvariantCulture) + " %",
			"remain" => (100 - done).ToString("0.0", CultureInfo.InvariantCulture) + " %",
			"started" => TimeOfDay(countdown.Set, format),
			"ends" => TimeOfDay(countdown.Due, format),
			"clock" => TimeOfDay(now, format),
			_ => Span(total, format, false),
		};
	}

	/// <summary>A moment in one of ClockFormats, local time: 14:03:12.4.</summary>
	public static string TimeOfDay(DateTime utc, string format)
	{
		var local = utc.ToLocalTime();
		string tenth = "." + local.Millisecond / 100;
		return format switch
		{
			"HH:mm:ss" => local.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
			"dd.MM.yyyy HH:mm:ss.s" => local.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture) + tenth,
			_ => local.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + tenth,
		};
	}

	/// <summary>Seconds from now to an alarm's time as the engine sends it ("28.11.2026 10:50:00", local), or
	/// fallback when it does not read. The engine's own seconds count from its whole-second now, so an alarm
	/// set at 10:49:12.7 for 10:50 would ring at 10:50:00.7 (owner 2026-11-28).</summary>
	public static double SecondsTo(string? at, double fallback, DateTime now)
	{
		if (at != null && DateTime.TryParseExact(at, "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture,
			DateTimeStyles.AssumeLocal, out var local))
		{
			return (local.ToUniversalTime() - now).TotalSeconds;
		}
		return fallback;
	}

	/// <summary>What a countdown is called in History: Timer 5m "tea".</summary>
	public static string Title(Countdown countdown)
	{
		string kind = countdown.Kind == "alarm" ? "Alarm" : "Timer";
		return $"{kind} {countdown.Label}" + (countdown.Name != null ? $" \"{countdown.Name}\"" : "");
	}
}
