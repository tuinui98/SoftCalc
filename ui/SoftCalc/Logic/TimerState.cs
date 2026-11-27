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
		Reset();
		return total;
	}

	public void Reset()
	{
		Before = 0;
		RunningSince = null;
		Laps.Clear();
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

	public Countdown Add(string kind, string label, string? name, double seconds, DateTime now)
	{
		var countdown = new Countdown { Kind = kind, Label = label, Name = name, Due = now.AddSeconds(seconds), Set = now };
		Countdowns.Add(countdown);
		Countdowns.Sort((a, b) => a.Due.CompareTo(b.Due));
		return countdown;
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

	/// <summary>What a countdown is called in History: Timer 5m "tea".</summary>
	public static string Title(Countdown countdown)
	{
		string kind = countdown.Kind == "alarm" ? "Alarm" : "Timer";
		return $"{kind} {countdown.Label}" + (countdown.Name != null ? $" \"{countdown.Name}\"" : "");
	}
}
