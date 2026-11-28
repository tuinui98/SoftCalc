// MainForm.Timers.cs - Stopwatch, timers and alarms (owner 2026-11-27/28)
//
// The engine reads `:sw`, `:lap`, `:timer`, `:alarm` lines and sends their
// action (engine timers.py); this part keeps the clock in TimerState
// (settings.json), shows it at the right of the hint line, records the
// stopwatch's times in History through the engine (protocol `record`, so
// they are answer and $n), says when a timer or alarm is done, and drives
// the Timers window and the F8 keys.

using System.Media;
using System.Runtime.InteropServices;
using SoftCalc.Engine;
using SoftCalc.Logic;

namespace SoftCalc.UI;

internal sealed partial class MainForm
{
	private static readonly TimeSpan RepeatFor = TimeSpan.FromMinutes(1);    // a repeating sound stops by itself
	private readonly System.Windows.Forms.Timer _timerTick = new() { Interval = 100 };
	private TimersWindow? _timersWindow;
	private DateTime _soundUntil = DateTime.MinValue;
	private DateTime _nextSound = DateTime.MinValue;
	private bool _timersCaughtUp;

	private TimerState Timers => _prefs.Timers;

	private void InitTimers()
	{
		_hint.ReadoutClick += (_, _) => OpenTimersWindow();
		_timerTick.Tick += (_, _) => TimerTick();
		_timerTick.Start();    // done ones that came due while SoftCalc was closed show on the first tick
	}

	//==================================================================
	//  BLOCK 1 - The clock: readout, due timers and alarms, the sound
	//==================================================================
	private void TimerTick()
	{
		var now = DateTime.UtcNow;
		foreach (var countdown in Timers.TakeDue(now))
		{
			Done(countdown, late: !_timersCaughtUp && now - countdown.Due > TimeSpan.FromSeconds(2));
		}
		_timersCaughtUp = true;
		_hint.Readout = Timers.Readout(now);
		if (_soundUntil > now && now >= _nextSound)
		{
			SystemSounds.Exclamation.Play();
			_nextSound = now.AddSeconds(2);
		}
		if (_timersWindow is { IsDisposed: false, Visible: true })
		{
			_timersWindow.ShowState(Timers, now, _soundUntil > now);
		}
		if (!Timers.Active && _soundUntil <= now && _timersWindow is not { IsDisposed: false, Visible: true })
		{
			_timerTick.Stop();    // nothing to count; a line or the window starts it again
		}
	}

	private void WakeTimers()
	{
		_timerTick.Start();
		TimerTick();
	}

	private void Done(Countdown countdown, bool late)
	{
		string at = countdown.Due.ToLocalTime().ToString("HH:mm:ss");
		_history.Append(new HistoryEntry("notice", "", TimerState.Title(countdown) + " done",
			late ? $"at {at}, while SoftCalc was closed" : $"at {at}"));
		if (late)
		{
			return;    // no sound for one long gone
		}
		if (Timers.Sound == "once")
		{
			SystemSounds.Exclamation.Play();
		}
		else if (Timers.Sound == "repeat")
		{
			_soundUntil = DateTime.UtcNow + RepeatFor;
			_nextSound = DateTime.UtcNow;
		}
		if (!ContainsFocus)
		{
			NativeMethods.Flash(Handle);    // the taskbar button, until SoftCalc comes forward
		}
	}

	// Any key in SoftCalc, or Quiet in the Timers window, stops a repeating sound
	private void QuietTimers()
	{
		_soundUntil = DateTime.MinValue;
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		QuietTimers();
		if (TimerKey(e.KeyData))
		{
			e.Handled = true;
			e.SuppressKeyPress = true;
			return;
		}
		base.OnKeyDown(e);
	}

	//==================================================================
	//  BLOCK 2 - A :sw / :timer / :alarm line's action
	//==================================================================
	private async Task TimerLineAsync(string input, EvalReply reply)
	{
		var action = reply.Timer!;
		string kind = (string?)action["kind"] ?? "";
		string does = (string?)action["do"] ?? "";
		if (kind == "stopwatch" && does != "list")    // :sw * lists all, as :timer * does
		{
			await StopwatchAsync(does, input);
			return;
		}
		string? name = (string?)action["name"];
		var now = DateTime.UtcNow;
		string text = reply.Text;
		switch (does)
		{
			case "list":
				ListAll(input, now);
				WakeTimers();
				return;
			case "start":
				string label = (string?)action["label"] ?? "";
				double seconds = (double?)action["seconds"] ?? 0;
				if (kind == "alarm")
				{
					seconds = TimerState.SecondsTo((string?)action["at"], seconds, now);    // on the second it names
				}
				var (countdown, changed) = Timers.Add(kind, label, name, seconds, now);
				if (changed)    // a name in use: that one's time is set again (owner 2026-11-28)
				{
					string due = countdown.Due.ToLocalTime().ToString(kind == "alarm" ? "dd.MM.yyyy HH:mm:ss" : "HH:mm:ss");
					text = kind == "alarm" ? $"Alarm \"{name}\" set again to {due}" : $"Timer \"{name}\" set again to {label}, done at {due}";
				}
				break;
			case "off":
				int gone = Timers.Cancel(kind, name);
				text = gone > 0 ? text : name != null ? $"No {kind} named \"{name}\"" : $"No {kind}s";
				break;
			case "show":
				var list = Timers.Of(kind).Where(c => name == null || string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
					.Select(c => $"{TimerState.Title(c)}, {TimerState.Left(c, now)} left").ToList();
				text = list.Count > 0 ? string.Join("; ", list) : $"No {kind}s";
				break;
		}
		_history.Append(Stamp(new HistoryEntry("definition", input, input, text)));
		WakeTimers();
	}

	// `*` after :sw, :timer or :alarm (owner 2026-11-28): what is going, the stopwatch and every timer and alarm
	private void ListAll(string input, DateTime now)
	{
		int timers = Timers.Of("timer").Count(), alarms = Timers.Of("alarm").Count();
		var going = new List<string>();
		if (Timers.Started)
		{
			going.Add(Timers.Running ? "stopwatch running" : "stopwatch paused");
		}
		if (timers > 0)
		{
			going.Add($"{timers} timer{(timers == 1 ? "" : "s")}");
		}
		if (alarms > 0)
		{
			going.Add($"{alarms} alarm{(alarms == 1 ? "" : "s")}");
		}
		_history.Append(Stamp(new HistoryEntry("definition", input, input, going.Count > 0 ? string.Join(", ", going) : "Nothing running")));
		if (Timers.Started)
		{
			Notice($"Stopwatch {TimerState.Clock(Timers.Elapsed(now), 1)}", Timers.Running ? "running" : "paused");
			Marks(Timers.Laps, Timers.Pauses);
		}
		foreach (var countdown in Timers.Countdowns)
		{
			string at = countdown.Due.ToLocalTime().ToString(countdown.Kind == "alarm" ? "dd.MM.yyyy HH:mm:ss" : "HH:mm:ss");
			Notice(TimerState.Title(countdown), $"{TimerState.Left(countdown, now)} left, at {at}");
		}
	}

	private void Marks(IReadOnlyList<Lap> laps, IReadOnlyList<double> pauses)
	{
		foreach (var (title, detail) in TimerState.Marks(laps, pauses))
		{
			Notice(title, detail);
		}
	}

	private void Notice(string title, string detail) => _history.Append(new HistoryEntry("notice", "", title, detail));

	// does: toggle, start, go, pause, stop, lap, reset; input: the line History shows
	private async Task StopwatchAsync(string does, string input)
	{
		var now = DateTime.UtcNow;
		if (does == "toggle")
		{
			does = Timers.Running ? "stop" : "start";    // a paused one goes on
		}
		string? message = null;
		switch (does)
		{
			case "last":    // the last run, kept over a restart (owner 2026-11-28)
				if (Timers.LastRun is StopwatchRun run)
				{
					_history.Append(Stamp(new HistoryEntry("definition", input, input,
						$"Stopwatch, last run {TimerState.Clock(run.Total, 2)}, ended {run.Ended.ToLocalTime():dd.MM.yyyy HH:mm:ss}")));
					Marks(run.Laps, run.Pauses);
				}
				else
				{
					message = "No stopwatch record";
				}
				break;
			case "start":
			case "go":
				message = Timers.Started ? $"Stopwatch goes on at {TimerState.Clock(Timers.Elapsed(now), 1)}" : "Stopwatch started";
				Timers.Start(now);
				break;
			case "pause":
				message = Timers.Running ? $"Stopwatch paused at {TimerState.Clock(Timers.Elapsed(now), 1)}" : "The stopwatch is not running";
				Timers.Pause(now);
				break;
			case "reset":
				message = Timers.Started ? "Stopwatch reset" : "The stopwatch was not started";
				Timers.Reset();
				break;
			case "lap":
				if (Timers.AddLap(now) is Lap lap)
				{
					await RecordAsync($"Lap {lap.Number}", TimerState.Clock(lap.Seconds, 2), $"total {TimerState.Clock(lap.Total, 2)}");
				}
				else
				{
					message = Timers.Started ? "The stopwatch is paused; :sw go goes on" : "The stopwatch was not started";
				}
				break;
			case "stop":
				int laps = Timers.Laps.Count;
				if (Timers.Stop(now) is double total)
				{
					await RecordAsync("Stopwatch", TimerState.Clock(total, 2), laps > 0 ? $"{laps} laps" : "");
					Marks(Timers.LastRun!.Laps, Timers.LastRun.Pauses);    // the run's laps and pauses (owner 2026-11-28)
				}
				else
				{
					message = "The stopwatch was not started";
				}
				break;
		}
		if (message != null)
		{
			_history.Append(Stamp(new HistoryEntry("definition", input, input, message)));
		}
		WakeTimers();
	}

	private async Task RecordAsync(string label, string value, string note)
	{
		var reply = await _session.RecordAsync(label, value, note);
		_history.Append(Stamp(new HistoryEntry(reply.Kind, value, reply.DisplayInput, reply.Text)));
		_history.SelectedIndex = -1;
		_lastWasResult = reply.Kind == "result";
	}

	// The countdown page's Change time (owner 2026-11-28): the new length or time read by the engine as its
	// :timer / :alarm line; a timer counts from now or from its start. Null, or the error to show.
	private async Task<string?> ChangeTimeAsync(Countdown countdown, string text, bool fromStart)
	{
		if (!_engineReady)
		{
			return "The engine is not ready";
		}
		var reply = await _session.EvalAsync($":{countdown.Kind} {text}");
		if (reply.Kind == "error" || reply.Timer is not { } action || (double?)action["seconds"] is not double seconds)
		{
			return reply.Text.StartsWith("ERROR:", StringComparison.Ordinal) ? reply.Text[6..] : "Write a length or a time";
		}
		var now = DateTime.UtcNow;
		if (countdown.Kind == "alarm")
		{
			seconds = TimerState.SecondsTo((string?)action["at"], seconds, now);    // on the second it names
		}
		var start = countdown.Kind == "timer" && fromStart ? countdown.Set : now;
		if (start.AddSeconds(seconds) <= now)
		{
			return "That time is already past";
		}
		countdown.Set = start;
		countdown.Due = start.AddSeconds(seconds);
		countdown.Label = (string?)action["label"] ?? text;
		if (!Timers.Countdowns.Contains(countdown))
		{
			Timers.Countdowns.Add(countdown);    // done already: it runs again
		}
		Timers.Countdowns.Sort((a, b) => a.Due.CompareTo(b.Due));
		string due = TimerState.Stamp(countdown.Due);
		Notice($"{TimerState.Title(countdown)}, time changed", $"ends {due}");
		WakeTimers();
		return null;
	}

	// A line from the Timers window (Start a timer or an alarm): evaluated as if typed
	private async Task RunTimerLineAsync(string line)
	{
		if (!_engineReady || _busy)
		{
			return;
		}
		var reply = await _session.EvalAsync(line);
		if (reply.Timer != null)
		{
			await TimerLineAsync(line, reply);
		}
		else
		{
			_history.Append(Stamp(new HistoryEntry(reply.Kind, reply.Input, reply.DisplayInput, reply.Text)));
		}
	}

	//==================================================================
	//  BLOCK 3 - Keys and the window
	//==================================================================
	// F8 start / stop, Shift+F8 lap, Ctrl+F8 pause / go on, Ctrl+Shift+F8 reset (owner 2026-11-28: F9-F12 are his macros)
	private bool TimerKey(Keys keys)
	{
		string? does = keys switch
		{
			Keys.F8 => "toggle",
			Keys.F8 | Keys.Shift => "lap",
			Keys.F8 | Keys.Control => Timers.Running ? "pause" : "go",
			Keys.F8 | Keys.Control | Keys.Shift => "reset",
			_ => null,
		};
		if (does == null || !_engineReady)
		{
			return false;
		}
		string line = does switch { "toggle" => ":sw", "lap" => ":lap", _ => ":sw " + does };
		_ = StopwatchAsync(does, line);
		return true;
	}

	private void OpenTimersWindow()
	{
		if (_timersWindow == null || _timersWindow.IsDisposed)
		{
			_timersWindow = new TimersWindow(Timers,
				does => _ = StopwatchAsync(does, does switch { "toggle" => ":sw", "lap" => ":lap", _ => ":sw " + does }),
				line => _ = RunTimerLineAsync(line),
				countdown =>
				{
					Timers.Cancel(countdown);
					_history.Append(new HistoryEntry("notice", "", TimerState.Title(countdown) + " off", ""));
				},
				QuietTimers, ChangeTimeAsync);
			_timersWindow.Show(this);
		}
		else
		{
			_timersWindow.Activate();
		}
		WakeTimers();
	}

	private static class NativeMethods
	{
		private const uint FlashAll = 3, FlashUntilForeground = 12;

		[StructLayout(LayoutKind.Sequential)]
		private struct FlashInfo
		{
			public uint Size;
			public IntPtr Window;
			public uint Flags;
			public uint Count;
			public uint Timeout;
		}

		[DllImport("user32.dll")]
		private static extern bool FlashWindowEx(ref FlashInfo info);

		public static void Flash(IntPtr window)
		{
			var info = new FlashInfo
			{
				Size = (uint)Marshal.SizeOf<FlashInfo>(), Window = window, Flags = FlashAll | FlashUntilForeground,
			};
			FlashWindowEx(ref info);
		}
	}
}
