// TimersWindow.cs - Tools > Stopwatch & Timers (owner 2026-11-28)
//
// The running numbers big, with buttons, beside what Input can do with
// `:sw`, `:timer` and `:alarm`. The state is the main window's TimerState
// (MainForm.Timers.cs): this window only shows it, and its buttons call
// back, so a line typed in Input and a button do the same thing. A new
// timer or alarm goes through the engine as its `:timer` / `:alarm` line,
// so it takes the same calculations (`0:05*3`, `now+0:15`).

using SoftCalc.Logic;

namespace SoftCalc.UI;

internal sealed class TimersWindow : Form
{
	private readonly TimerState _state;
	private readonly Action<string> _stopwatch;
	private readonly Action<string> _runLine;
	private readonly Action<Countdown> _cancel;
	private readonly Action _quiet;
	private readonly Label _clock = new() { AutoSize = false, Dock = DockStyle.Top, Height = 70, TextAlign = ContentAlignment.MiddleCenter };
	private readonly Button _startStop = new() { AutoSize = true };
	private readonly Button _pause = new() { AutoSize = true };
	private readonly Button _lapButton = new() { Text = "&Lap", AutoSize = true };
	private readonly ListView _laps = Table(("Lap", 60), ("Time", 110), ("Total", 110));
	private readonly Dictionary<string, ListView> _lists = new();
	private readonly ComboBox _sound = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
	private readonly Button _quietButton = new() { Text = "&Quiet", AutoSize = true, Enabled = false };
	private int _shownLaps = -1;

	public TimersWindow(TimerState state, Action<string> stopwatch, Action<string> runLine, Action<Countdown> cancel, Action quiet)
	{
		(_state, _stopwatch, _runLine, _cancel, _quiet) = (state, stopwatch, runLine, cancel, quiet);
		Text = "Stopwatch & Timers";
		Size = new Size(460, 440);
		MinimumSize = new Size(380, 340);
		StartPosition = FormStartPosition.CenterParent;
		ShowInTaskbar = false;
		Icon = SvgIcon.ProgramIcon();
		KeyPreview = true;

		var tabs = new TabControl { Dock = DockStyle.Fill };
		tabs.TabPages.Add(StopwatchPage());
		tabs.TabPages.Add(CountdownPage("timer", "Timer", "5m, 90s, 1h30m, 0:05*3"));
		tabs.TabPages.Add(CountdownPage("alarm", "Alarm", "14:30, 30.11.2026 7:00, now+0:15"));

		var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(6) };
		bottom.Controls.Add(new Label { Text = "Sound when done:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
		_sound.Items.AddRange(TimerState.Sounds.Cast<object>().ToArray());
		_sound.SelectedItem = _state.Sound;
		_sound.SelectedIndexChanged += (_, _) => _state.Sound = (string)_sound.SelectedItem!;
		bottom.Controls.Add(_sound);
		_quietButton.Click += (_, _) => _quiet();
		bottom.Controls.Add(_quietButton);
		Controls.Add(tabs);
		Controls.Add(bottom);
	}

	protected override void OnFontChanged(EventArgs e)
	{
		base.OnFontChanged(e);
		_clock.Font = new Font(Font.FontFamily, Font.Size * 3f);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		_quiet();
		string? does = e.KeyData switch    // the main window's F8 keys work here too
		{
			Keys.F8 => "toggle",
			Keys.F8 | Keys.Shift => "lap",
			Keys.F8 | Keys.Control => _state.Running ? "pause" : "go",
			Keys.F8 | Keys.Control | Keys.Shift => "reset",
			_ => null,
		};
		if (does != null)
		{
			_stopwatch(does);
			e.Handled = e.SuppressKeyPress = true;
			return;
		}
		base.OnKeyDown(e);
	}

	//==================================================================
	//  BLOCK 1 - The pages
	//==================================================================
	private TabPage StopwatchPage()
	{
		var page = new TabPage("Stopwatch");
		_clock.Font = new Font(Font.FontFamily, Font.Size * 3f);
		var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4) };
		_startStop.Click += (_, _) => _stopwatch(_state.Running ? "stop" : "start");
		_pause.Click += (_, _) => _stopwatch(_state.Running ? "pause" : "go");
		var lap = _lapButton;
		lap.Click += (_, _) => _stopwatch("lap");
		var reset = new Button { Text = "&Reset", AutoSize = true };
		reset.Click += (_, _) => _stopwatch("reset");
		buttons.Controls.AddRange(new Control[] { _startStop, _pause, lap, reset });
		var keys = Footnote("F8 start / stop, Shift+F8 lap, Ctrl+F8 pause / go on, Ctrl+Shift+F8 reset. Stop and Lap go to History.");
		page.Controls.Add(_laps);
		page.Controls.Add(buttons);
		page.Controls.Add(_clock);
		page.Controls.Add(keys);
		return page;
	}

	private TabPage CountdownPage(string kind, string title, string examples)
	{
		var page = new TabPage(title);
		var list = Table(("Name", 110), ("Left", 80), (kind == "alarm" ? "At" : "Done at", 150));
		list.MultiSelect = false;
		_lists[kind] = list;
		var entry = new TextBox { Width = 170, PlaceholderText = examples };
		var name = new TextBox { Width = 80, PlaceholderText = "name" };
		var start = new Button { Text = "&Start", AutoSize = true };
		start.Click += (_, _) =>
		{
			if (entry.Text.Trim().Length == 0)
			{
				return;
			}
			string named = name.Text.Trim().Length > 0 ? $" \"{name.Text.Trim().Replace("\"", "")}\"" : "";
			_runLine($":{kind} {entry.Text.Trim()}{named}");
			entry.Clear();
			name.Clear();
		};
		entry.KeyDown += (_, e) =>
		{
			if (e.KeyCode == Keys.Enter)
			{
				start.PerformClick();
				e.SuppressKeyPress = true;
			}
		};
		var cancel = new Button { Text = "&Cancel", AutoSize = true };
		cancel.Click += (_, _) =>
		{
			if (list.SelectedItems.Count > 0 && list.SelectedItems[0].Tag is Countdown countdown)
			{
				_cancel(countdown);
			}
		};
		var row = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4) };
		row.Controls.AddRange(new Control[] { entry, name, start, cancel });
		var help = Footnote(kind == "alarm" ? "In Input: :alarm 14:30 \"name\", :alarm off" : "In Input: :timer 5m \"name\", :timer off");
		page.Controls.Add(list);
		page.Controls.Add(row);
		page.Controls.Add(help);
		return page;
	}

	// A grey note under a page, wrapped to the window's width and as tall as its lines
	// (owner 2026-11-28: on a narrow window an AutoSize label ran past the edge, cut)
	private static Label Footnote(string text)
	{
		var label = new Label
		{
			Dock = DockStyle.Bottom, AutoSize = false, Padding = new Padding(4), ForeColor = SystemColors.GrayText, Text = text,
		};
		label.SizeChanged += (_, _) =>
		{
			int width = Math.Max(1, label.ClientSize.Width - label.Padding.Horizontal);
			int height = TextRenderer.MeasureText(label.Text, label.Font, new Size(width, 0), TextFormatFlags.WordBreak).Height
				+ label.Padding.Vertical;
			if (label.Height != height)
			{
				label.Height = height;
			}
		};
		return label;
	}

	private static ListView Table(params (string Title, int Width)[] columns)
	{
		var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false };
		foreach (var (title, width) in columns)
		{
			list.Columns.Add(title, width);
		}
		return list;
	}

	//==================================================================
	//  BLOCK 2 - Showing the state (the main window's tick, 10 a second)
	//==================================================================
	public void ShowState(TimerState state, DateTime now, bool sounding)
	{
		_clock.Text = TimerState.Clock(state.Elapsed(now), 1);
		_startStop.Text = state.Running ? "St&op" : "&Start";
		_pause.Text = state.Running ? "&Pause" : "&Go on";
		_pause.Enabled = state.Started;
		_lapButton.Enabled = state.Running;    // a lap while paused would repeat the last time (owner 2026-11-28)
		_quietButton.Enabled = sounding;
		// this run's laps and pauses, or the last run's when none is going (owner 2026-11-28)
		var laps = state.Started || state.LastRun == null ? state.Laps : state.LastRun.Laps;
		var pauses = state.Started || state.LastRun == null ? state.Pauses : state.LastRun.Pauses;
		int shown = laps.Count * 1000 + pauses.Count + (ReferenceEquals(laps, state.Laps) ? 0 : 500);
		if (_shownLaps != shown)
		{
			_shownLaps = shown;
			_laps.BeginUpdate();
			_laps.Items.Clear();
			var marks = laps.Select(l => (l.Total, Row: new[] { l.Number.ToString(), TimerState.Clock(l.Seconds, 2), TimerState.Clock(l.Total, 2) }))
				.Concat(pauses.Select(p => (Total: p, Row: new[] { "⏸", "paused", TimerState.Clock(p, 1) })))
				.OrderByDescending(m => m.Total);    // the newest on top
			foreach (var (_, row) in marks)
			{
				_laps.Items.Add(new ListViewItem(row));
			}
			_laps.EndUpdate();
		}
		foreach (var (kind, list) in _lists)
		{
			var countdowns = state.Of(kind).ToList();
			bool same = list.Items.Count == countdowns.Count
				&& list.Items.Cast<ListViewItem>().Select(i => i.Tag).SequenceEqual(countdowns);
			if (!same)
			{
				list.BeginUpdate();
				list.Items.Clear();
				foreach (var countdown in countdowns)
				{
					string title = countdown.Name ?? countdown.Label;
					string at = countdown.Due.ToLocalTime().ToString(kind == "alarm" ? "dd.MM.yyyy HH:mm:ss" : "HH:mm:ss");
					list.Items.Add(new ListViewItem(new[] { title, "", at }) { Tag = countdown });
				}
				list.EndUpdate();
			}
			foreach (ListViewItem item in list.Items)
			{
				item.SubItems[1].Text = TimerState.Left((Countdown)item.Tag!, now);
			}
		}
	}
}
