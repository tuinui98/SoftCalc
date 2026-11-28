// CountdownPage.cs - One timer or alarm, big, in the Stopwatch & Timers window (owner 2026-11-28)
//
// Like a launch countdown: the name top left and the computer's time top
// right; three numbers, the main one large and two below it, each chosen
// with a click (what it shows: time left, since start, % done, % left or
// the set length; and its format); then Set, Started and Ends. In the last
// 10 seconds the main number turns amber and ticks (Quiet countdown turns
// the ticks off); when done it counts on as T+ in red until Dismiss.
// Change time sends the new length or time through the engine, as typing
// `:timer 10m "tea"` would. The numbers shrink to fit, so a narrow window
// never cuts them. Owner OK'd the mock (claude.ai artifact Rxj7cGHa1fSexEkjLhBGwo).

using System.Media;
using SoftCalc.Logic;

namespace SoftCalc.UI;

internal sealed class CountdownPage : UserControl
{
	private static readonly Color Amber = Color.FromArgb(217, 119, 6);
	private static readonly Color Alert = Color.FromArgb(220, 50, 47);
	private static readonly Lazy<SoundPlayer> TickSound = new(() => new SoundPlayer(Tone(1200, 0.05)));

	private readonly TimerState _state;
	private readonly Action _back;
	private readonly Action<Countdown> _cancel;
	private readonly Func<Countdown, string, bool, Task<string?>> _change;
	private readonly Label _name = new() { AutoSize = true, Margin = new Padding(6) };
	private readonly Label _clock = new() { AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(6), ForeColor = SystemColors.GrayText };
	private readonly FitText[] _numbers = { new(), new(), new() };
	private readonly Label[] _captions = { Caption(), Caption(), Caption() };
	private readonly Label _doneNote = new() { AutoSize = true, ForeColor = Alert, Anchor = AnchorStyles.None, Margin = new Padding(3) };
	private readonly Button _dismiss = new() { Text = "&Dismiss", AutoSize = true, Visible = false };
	private readonly TableLayoutPanel _info = new() { AutoSize = true, ColumnCount = 2, Margin = new Padding(8, 4, 4, 4) };
	private readonly Label _set = Value(), _started = Value(), _ends = Value();
	private readonly FlowLayoutPanel _edit = new() { AutoSize = true, Dock = DockStyle.Fill, Visible = false, WrapContents = true };
	private readonly TextBox _newValue = new() { Width = 130 };
	private readonly RadioButton _fromNow = new() { Text = "from now", AutoSize = true, Checked = true };
	private readonly RadioButton _fromStart = new() { Text = "from its start", AutoSize = true };
	private readonly Label _editError = new() { AutoSize = true, ForeColor = Alert };
	private readonly Button _cancelButton = new() { AutoSize = true };
	private readonly CheckBox _quiet = new() { Text = "&Quiet countdown", AutoSize = true, Anchor = AnchorStyles.Right };
	private Countdown? _countdown;
	private bool _dismissed;
	private int _lastWhole = int.MinValue;

	public CountdownPage(TimerState state, Action back, Action<Countdown> cancel, Func<Countdown, string, bool, Task<string?>> change)
	{
		(_state, _back, _cancel, _change) = (state, back, cancel, change);
		Dock = DockStyle.Fill;
		var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 8 };
		grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
		grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
		grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));            // name | time
		grid.RowStyles.Add(new RowStyle(SizeType.Percent, 55));         // main number
		grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));            // its caption
		grid.RowStyles.Add(new RowStyle(SizeType.Percent, 30));         // two numbers
		grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));            // their captions
		grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));            // done
		grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));            // Set, Started, Ends
		grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));            // Change time
		grid.Controls.Add(_name, 0, 0);
		grid.Controls.Add(_clock, 1, 0);
		grid.Controls.Add(_numbers[0], 0, 1);
		grid.SetColumnSpan(_numbers[0], 2);
		grid.Controls.Add(_captions[0], 0, 2);
		grid.SetColumnSpan(_captions[0], 2);
		grid.Controls.Add(_numbers[1], 0, 3);
		grid.Controls.Add(_numbers[2], 1, 3);
		grid.Controls.Add(_captions[1], 0, 4);
		grid.Controls.Add(_captions[2], 1, 4);
		var done = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.None };
		done.Controls.AddRange(new Control[] { _doneNote, _dismiss });
		grid.Controls.Add(done, 0, 5);
		grid.SetColumnSpan(done, 2);
		grid.Controls.Add(_info, 0, 6);
		grid.SetColumnSpan(_info, 2);
		grid.Controls.Add(_edit, 0, 7);
		grid.SetColumnSpan(_edit, 2);
		for (int i = 0; i < 3; i++)
		{
			int slot = i;
			_numbers[i].Click += (_, _) => ShowMenu(slot, _numbers[slot]);
			_captions[i].Click += (_, _) => ShowMenu(slot, _numbers[slot]);
		}
		_dismiss.Click += (_, _) => { _dismissed = true; _back(); };
		foreach (var (title, value) in new[] { ("Set", _set), ("Started", _started), ("Ends", _ends) })
		{
			_info.Controls.Add(new Label { Text = title, AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 2, 12, 2) });
			_info.Controls.Add(value);
		}
		BuildEdit();

		var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(4) };
		var backButton = new Button { Text = "◂ &Back", AutoSize = true };
		backButton.Click += (_, _) => _back();
		var changeButton = new Button { Text = "C&hange time…", AutoSize = true };
		changeButton.Click += (_, _) => ToggleEdit();
		_cancelButton.Click += (_, _) =>
		{
			if (_countdown != null)
			{
				_cancel(_countdown);
				_back();
			}
		};
		_quiet.Checked = _state.QuietCountdown;
		_quiet.CheckedChanged += (_, _) => _state.QuietCountdown = _quiet.Checked;
		bar.Controls.AddRange(new Control[] { backButton, changeButton, _cancelButton, _quiet });
		Controls.Add(grid);
		Controls.Add(bar);
	}

	public Countdown? Shown => _countdown;

	public void Open(Countdown countdown)
	{
		_countdown = countdown;
		_dismissed = false;
		_lastWhole = int.MinValue;
		_edit.Visible = false;
		string kind = countdown.Kind == "alarm" ? "Alarm" : "Timer";
		_name.Text = $"{kind}: {countdown.Name ?? countdown.Label}";
		_name.Font = new Font(Font, FontStyle.Bold);
		_cancelButton.Text = $"&Cancel {countdown.Kind}";
		_fromStart.Visible = _fromNow.Visible = countdown.Kind == "timer";
	}

	private static Label Value() => new() { AutoSize = true, Margin = new Padding(0, 2, 0, 2) };

	private static Label Caption() => new()
	{
		Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopCenter, ForeColor = SystemColors.GrayText, AutoSize = false, Height = 18,
		Cursor = Cursors.Hand,
	};

	//==================================================================
	//  BLOCK 1 - Showing it (the main window's tick, 10 a second)
	//==================================================================
	public void ShowState(DateTime now)
	{
		if (_countdown is not Countdown countdown)
		{
			return;
		}
		_clock.Text = TimerState.Stamp(now);
		var slots = _state.Slots(countdown.Kind);
		for (int i = 0; i < 3; i++)
		{
			_numbers[i].Text = TimerState.Slot(countdown, slots[i][0], slots[i][1], now);
			string show = TimerState.ShowNames[Array.IndexOf(TimerState.Shows, slots[i][0])];
			_captions[i].Text = slots[i][0] is "done" or "remain" ? show : $"{show} · {slots[i][1]}";
		}
		double left = (countdown.Due - now).TotalSeconds;
		bool done = left <= 0 && !_dismissed;
		_numbers[0].ForeColor = done ? Alert : left <= 10 && left > 0 ? Amber : ForeColor;
		_numbers[0].Bold = done;
		_doneNote.Text = done ? $"Done at {TimerState.Stamp(countdown.Due)[11..]}" : "";
		_dismiss.Visible = done;
		_cancelButton.Enabled = left > 0;
		_set.Text = TimerState.Length((countdown.Due - countdown.Set).TotalSeconds)
			+ (countdown.Kind == "alarm" ? "   (waited from when it was set)" : "");
		_started.Text = TimerState.Stamp(countdown.Set);
		_ends.Text = TimerState.Stamp(countdown.Due);
		int whole = (int)Math.Ceiling(left);
		if (whole != _lastWhole && left > 0 && left <= 10 && !_state.QuietCountdown && _lastWhole != int.MinValue)
		{
			TickSound.Value.Play();
		}
		_lastWhole = whole;
	}

	private void ShowMenu(int slot, Control under)
	{
		if (_countdown == null)
		{
			return;
		}
		var slots = _state.Slots(_countdown.Kind);
		var menu = new ContextMenuStrip();
		menu.Items.Add(new ToolStripLabel("Show") { ForeColor = SystemColors.GrayText });
		for (int i = 0; i < TimerState.Shows.Length; i++)
		{
			string show = TimerState.Shows[i];
			string extra = show == "left" ? " (counts down)" : show == "since" ? " (counts up)" : "";
			menu.Items.Add(new ToolStripMenuItem(TimerState.ShowNames[i] + extra, null, (_, _) => slots[slot][0] = show) { Checked = slots[slot][0] == show });
		}
		menu.Items.Add(new ToolStripSeparator());
		menu.Items.Add(new ToolStripLabel("Format") { ForeColor = SystemColors.GrayText });
		foreach (string format in TimerState.Formats)
		{
			menu.Items.Add(new ToolStripMenuItem(format, null, (_, _) => slots[slot][1] = format)
			{
				Checked = slots[slot][1] == format && slots[slot][0] is not ("done" or "remain"),
			});
		}
		menu.Closed += (_, _) => BeginInvoke(menu.Dispose);
		menu.Show(under, new Point(under.Width / 2 - 80, under.Height / 2));
	}

	//==================================================================
	//  BLOCK 2 - Change time
	//==================================================================
	private void BuildEdit()
	{
		var set = new Button { Text = "&Set", AutoSize = true };
		var no = new Button { Text = "Cancel", AutoSize = true };
		set.Click += async (_, _) => await ApplyEdit();
		no.Click += (_, _) => _edit.Visible = false;
		_newValue.KeyDown += async (_, e) =>
		{
			if (e.KeyCode == Keys.Enter)
			{
				e.SuppressKeyPress = true;
				await ApplyEdit();
			}
		};
		_edit.Controls.AddRange(new Control[]
		{
			new Label { Text = "New:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) }, _newValue, _fromNow, _fromStart, set, no, _editError,
		});
	}

	private void ToggleEdit()
	{
		if (_countdown == null)
		{
			return;
		}
		_edit.Visible = !_edit.Visible;
		_editError.Text = _countdown.Kind == "alarm" ? "14:30, 30.11.2026 7:00, now+0:15" : "5m, 90s, 1h30m, 0:05*3";
		_editError.ForeColor = SystemColors.GrayText;
		_newValue.Text = _countdown.Label;
		if (_edit.Visible)
		{
			_newValue.Focus();
			_newValue.SelectAll();
		}
	}

	private async Task ApplyEdit()
	{
		if (_countdown == null || _newValue.Text.Trim().Length == 0)
		{
			return;
		}
		string? error = await _change(_countdown, _newValue.Text.Trim(), _fromStart.Checked);
		if (error != null)
		{
			_editError.ForeColor = Alert;
			_editError.Text = error;
			return;
		}
		_dismissed = false;
		_edit.Visible = false;
	}

	// A short click for the last 10 seconds: a 16-bit mono WAV made in memory
	private static MemoryStream Tone(int hertz, double seconds)
	{
		const int rate = 22050;
		int samples = (int)(rate * seconds);
		var stream = new MemoryStream();
		using (var w = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
		{
			w.Write("RIFF"u8.ToArray());
			w.Write(36 + samples * 2);
			w.Write("WAVEfmt "u8.ToArray());
			w.Write(16);
			w.Write((short)1);
			w.Write((short)1);
			w.Write(rate);
			w.Write(rate * 2);
			w.Write((short)2);
			w.Write((short)16);
			w.Write("data"u8.ToArray());
			w.Write(samples * 2);
			for (int i = 0; i < samples; i++)
			{
				double fade = 1 - (double)i / samples;
				w.Write((short)(Math.Sin(2 * Math.PI * hertz * i / rate) * 9000 * fade));
			}
		}
		stream.Position = 0;
		return stream;
	}
}

// Text that grows and shrinks to fill its box, never cut (owner 2026-11-28: big numbers on a narrow window)
internal sealed class FitText : Control
{
	private bool _bold;

	public FitText()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
			| ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
		Dock = DockStyle.Fill;
		Cursor = Cursors.Hand;
		Margin = new Padding(2);
	}

	[System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
	public bool Bold
	{
		get => _bold;
		set
		{
			if (value != _bold)
			{
				_bold = value;
				Invalidate();
			}
		}
	}

	protected override void OnTextChanged(EventArgs e)
	{
		base.OnTextChanged(e);
		Invalidate();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		if (Text.Length == 0 || Width < 8 || Height < 8)
		{
			return;
		}
		const TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
			| TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
		var style = _bold ? FontStyle.Bold : FontStyle.Regular;
		// a sample of the same length in 0s, so the size holds still while the digits change
		string sample = new string(Text.Select(c => char.IsDigit(c) ? '0' : c).ToArray());
		float size = Height * 0.62f;
		using var probe = new Font("Consolas", 100f, style, GraphicsUnit.Pixel);
		var at100 = TextRenderer.MeasureText(e.Graphics, sample, probe, Size.Empty, flags);
		size = Math.Min(size, 100f * (Width - 8) / Math.Max(1, at100.Width));
		size = Math.Max(6f, size);
		using var font = new Font("Consolas", size, style, GraphicsUnit.Pixel);
		TextRenderer.DrawText(e.Graphics, Text, font, ClientRectangle, ForeColor, flags);
	}
}
