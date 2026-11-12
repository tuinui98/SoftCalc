// RandomWindow.cs - Tools > Random: Numbers and Password tabs (design/random_plan.md R1-R2)
//
// Numbers: whole numbers, decimals, a coin with a bias, dice. Password:
// length, character kinds, look-alikes, a favourite word mixed into the
// secure source. Results live only in the lists: nothing goes to
// History or to disk, and the lists are cleared when the window closes.
// A copied password stays out of the clipboard history and is wiped
// after 30 s (SecretClipboard). Custom list tabs come later.

using System.Globalization;
using SoftCalc.Logic;
using SoftCalc.Persistence;

namespace SoftCalc.UI;

internal sealed class RandomWindow : Form
{
	private readonly RandomPrefs _prefs;
	private readonly Action<string> _insert;
	private readonly IRandomSource _secure = new SecureRandom();
	private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };

	// Numbers tab
	private readonly RadioButton _whole = Kind("&Whole numbers", "whole");
	private readonly RadioButton _decimal = Kind("&Decimals", "decimal");
	private readonly RadioButton _coin = Kind("C&oin", "coin");
	private readonly RadioButton _dice = Kind("D&ice", "dice");
	private readonly NumericUpDown _from = Number(-RandomTools.Limit, RandomTools.Limit, 0);
	private readonly NumericUpDown _to = Number(-RandomTools.Limit, RandomTools.Limit, 0);
	private readonly NumericUpDown _count = Number(1, RandomTools.MaxCount, 0);
	private readonly NumericUpDown _places = Number(0, RandomTools.MaxPlaces, 0);
	private readonly NumericUpDown _heads = Number(0, 100, 2);
	private readonly NumericUpDown _sides = Number(2, 1000, 0);
	private readonly TextBox _headWord = new() { Width = 90, PlaceholderText = RandomTools.DefaultHead };
	private readonly TextBox _tailWord = new() { Width = 90, PlaceholderText = RandomTools.DefaultTail };
	private readonly CheckBox _noRepeats = new() { Text = "No &repeats", AutoSize = true };
	private readonly Button _sort = new() { Text = "S&ort", AutoSize = true };
	private readonly ListBox _numbers = Results(false);
	private readonly Label _numbersNote = Note();
	private readonly Dictionary<Control, Label> _captions = new();

	// Password tab
	private readonly NumericUpDown _length = Number(4, 128, 0);
	private readonly CheckBox _upper = new() { Text = "&Upper case A-Z", AutoSize = true };
	private readonly CheckBox _lower = new() { Text = "&Lower case a-z", AutoSize = true };
	private readonly CheckBox _digits = new() { Text = "Di&gits 0-9", AutoSize = true };
	private readonly CheckBox _symbols = new() { Text = "&Symbols", AutoSize = true };
	private readonly TextBox _symbolSet = new() { Width = 260 };
	private readonly List<(CheckBox Box, string Chars)> _symbolGroups = new();
	private bool _settingGroups;
	private readonly CheckBox _noLookAlikes = new() { Text = $"Leave out look-&alikes ({RandomTools.LookAlikes})", AutoSize = true };
	private readonly NumericUpDown _passwordCount = Number(1, 50, 0);
	private readonly TextBox _word = new() { Width = 200, PlaceholderText = "optional" };
	private readonly ListBox _passwords = Results(true);
	private readonly Label _strength = Note();

	public RandomWindow(RandomPrefs prefs, Action<string> insert)
	{
		_prefs = prefs;
		_insert = insert;
		Text = "Random";
		Icon = SvgIcon.ProgramIcon();
		ShowInTaskbar = false;
		AutoScaleMode = AutoScaleMode.Dpi;
		Size = new Size(560, 520);
		MinimumSize = new Size(460, 440);
		StartPosition = FormStartPosition.CenterParent;
		if (prefs.Window is { HasPosition: true } w)
		{
			StartPosition = FormStartPosition.Manual;
			Bounds = WindowPlacement.FitOnScreen(new Rectangle(w.X, w.Y, w.Width, w.Height),
				Screen.AllScreens.Select(s => s.WorkingArea).ToList());
		}
		_tabs.TabPages.Add(NumbersTab());
		_tabs.TabPages.Add(PasswordTab());
		Controls.Add(_tabs);
		LoadPrefs();
		FormClosing += (_, _) => SavePrefs();
		FormClosed += (_, _) =>
		{
			_numbers.Items.Clear();
			_passwords.Items.Clear();
			_word.Clear();
		};
	}

	//==================================================================
	//  Numbers tab
	//==================================================================
	private TabPage NumbersTab()
	{
		var page = new TabPage("Numbers") { Padding = new Padding(8) };
		var kinds = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true };
		kinds.Controls.AddRange(new Control[] { _whole, _decimal, _coin, _dice });
		foreach (var kind in new[] { _whole, _decimal, _coin, _dice })
		{
			kind.CheckedChanged += (_, _) => ShowKind();
		}
		_places.ValueChanged += (_, _) => ShowKind();

		var fields = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 4 };
		AddPair(fields, "&From", _from, "&To", _to);
		// Count, then the one setting of the chosen kind beside it
		int row = fields.RowCount++;
		var countCaption = Caption("&Count");
		fields.Controls.Add(countCaption, 0, row);
		fields.Controls.Add(_count, 1, row);
		var extra = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty, WrapContents = false };
		foreach (var (text, box) in new[] { ("&Places", _places), ("&Heads %", _heads), ("Si&des", _sides) })
		{
			var caption = Caption(text);
			_captions[box] = caption;
			extra.Controls.Add(caption);
			extra.Controls.Add(box);
		}
		fields.Controls.Add(extra, 2, row);
		fields.SetColumnSpan(extra, 2);
		// Coin: the words shown for heads and tails (owner 2026-11-12)
		int wordsRow = fields.RowCount++;
		var headCaption = Caption("Hea&d word");
		var tailCaption = Caption("Tai&l word");
		_captions[_headWord] = headCaption;
		_captions[_tailWord] = tailCaption;
		fields.Controls.Add(headCaption, 0, wordsRow);
		fields.Controls.Add(_headWord, 1, wordsRow);
		fields.Controls.Add(tailCaption, 2, wordsRow);
		fields.Controls.Add(_tailWord, 3, wordsRow);
		var checks = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty };
		checks.Controls.Add(_noRepeats);
		fields.Controls.Add(checks, 1, fields.RowCount++);
		fields.SetColumnSpan(checks, 3);

		var draw = new Button { Text = "&Generate", AutoSize = true, MinimumSize = new Size(90, 0) };
		draw.Click += (_, _) => DrawNumbers();
		var copy = new Button { Text = "Cop&y", AutoSize = true };
		copy.Click += (_, _) => CopyLines(_numbers, all: false, secret: false);
		var copyAll = new Button { Text = "Copy &All", AutoSize = true };
		copyAll.Click += (_, _) => CopyLines(_numbers, all: true, secret: false);
		var insert = new Button { Text = "&Insert into Input", AutoSize = true };
		insert.Click += (_, _) => InsertSelected();
		_numbers.DoubleClick += (_, _) => InsertSelected();
		var command = new Button { Text = "Insert Co&mmand", AutoSize = true };
		new ToolTip().SetToolTip(command, "Put the rand() line for these settings in Input, to run there");
		command.Click += (_, _) => _insert(Command());
		var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 6, 0, 0) };
		_sort.Click += (_, _) => SortNumbers();
		buttons.Controls.AddRange(new Control[] { draw, _sort, copy, copyAll, insert, command });

		page.Controls.Add(_numbers);
		page.Controls.Add(_numbersNote);
		page.Controls.Add(buttons);
		page.Controls.Add(fields);
		page.Controls.Add(kinds);
		return page;
	}

	private void ShowKind()
	{
		bool range = _whole.Checked || _decimal.Checked;
		SetShown(_from, range);
		SetShown(_to, range);
		SetShown(_places, _decimal.Checked);
		SetShown(_heads, _coin.Checked);
		SetShown(_headWord, _coin.Checked);
		SetShown(_tailWord, _coin.Checked);
		SetShown(_sides, _dice.Checked);
		_noRepeats.Visible = _whole.Checked;
		_sort.Visible = range;
		int places = _decimal.Checked ? (int)_places.Value : 0;    // whole numbers: From and To take no fraction
		_from.DecimalPlaces = places;
		_to.DecimalPlaces = places;
	}

	private void SetShown(Control control, bool shown)
	{
		control.Visible = shown;
		_captions[control].Visible = shown;
	}

	private void DrawNumbers()
	{
		_numbers.Items.Clear();
		_numbersNote.ForeColor = SystemColors.GrayText;
		int count = (int)_count.Value;
		if (_whole.Checked)
		{
			var (values, error) = RandomTools.Integers(_secure, (long)_from.Value, (long)_to.Value, count, _noRepeats.Checked, sort: false);
			ShowNumbers(values?.Select(v => v.ToString(CultureInfo.InvariantCulture)), error, null);
		}
		else if (_decimal.Checked)
		{
			var (values, error) = RandomTools.Decimals(_secure, _from.Value, _to.Value, (int)_places.Value, count, sort: false);
			ShowNumbers(values?.Select(RandomTools.Text), error, null);
		}
		else if (_coin.Checked)
		{
			var flips = RandomTools.Coins(_secure, _heads.Value, count);
			int heads = flips.Count(f => f);
			string head = Word(_headWord, RandomTools.DefaultHead), tail = Word(_tailWord, RandomTools.DefaultTail);
			ShowNumbers(flips.Select(f => f ? head : tail), null, $"{head} {heads}, {tail} {flips.Count - heads}");
		}
		else
		{
			var dice = RandomTools.Dice(_secure, (int)_sides.Value, count);
			ShowNumbers(dice.Select(d => d.ToString(CultureInfo.InvariantCulture)), null, $"Sum {dice.Sum()}");
		}
	}

	private void ShowNumbers(IEnumerable<string>? lines, string? error, string? note)
	{
		if (error != null || lines == null)
		{
			_numbersNote.ForeColor = Theme.Error;
			_numbersNote.Text = error ?? "";
			return;
		}
		_numbers.Items.AddRange(lines.Cast<object>().ToArray());
		_numbers.SelectedIndex = 0;
		_numbersNote.Text = note ?? "";
	}

	// Sort (owner 2026-11-12: a button, not a setting): the list in order, smallest first
	private void SortNumbers()
	{
		var values = _numbers.Items.Cast<string>()
			.Select(t => (Text: t, Ok: decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var v), Value: v))
			.ToList();
		if (values.Count < 2 || values.Any(v => !v.Ok))
		{
			return;
		}
		_numbers.BeginUpdate();
		_numbers.Items.Clear();
		_numbers.Items.AddRange(values.OrderBy(v => v.Value).Select(v => (object)v.Text).ToArray());
		_numbers.EndUpdate();
	}

	// A drawn number (several: joined by spaces) at the Input's caret
	private void InsertSelected()
	{
		var picked = _numbers.SelectedItems.Cast<string>()
			.Where(t => decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out _)).ToList();    // coin words stay out
		if (picked.Count > 0)
		{
			_insert(string.Join(" ", picked));
		}
	}

	private static string Word(TextBox box, string fallback)
	{
		string word = box.Text.Trim();
		return word.Length > 0 ? word : fallback;
	}

	// The rand() line that draws one value with these settings
	private string Command()
	{
		string kind = (string)new[] { _whole, _decimal, _coin, _dice }.First(k => k.Checked).Tag!;
		return RandomTools.Command(kind, _from.Value, _to.Value, (int)_places.Value, _heads.Value, (int)_sides.Value);
	}

	//==================================================================
	//  Password tab
	//==================================================================
	private TabPage PasswordTab()
	{
		var page = new TabPage("Password") { Padding = new Padding(8) };
		var fields = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 4 };
		AddPair(fields, "&Length", _length, "&Count", _passwordCount);
		var kinds = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty };
		kinds.Controls.AddRange(new Control[] { _upper, _lower, _digits });
		fields.Controls.Add(kinds, 0, fields.RowCount++);
		fields.SetColumnSpan(kinds, 4);
		var symbols = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty };
		symbols.Controls.AddRange(new Control[] { _symbols, _symbolSet });
		fields.Controls.Add(symbols, 0, fields.RowCount++);
		fields.SetColumnSpan(symbols, 4);
		// Symbol groups (owner 2026-11-12): a tick adds the group to the box, untick removes it
		var groups = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(20, 0, 0, 0), MaximumSize = new Size(500, 0) };
		var tips = new ToolTip();
		foreach (var (name, chars) in RandomTools.SymbolGroups)
		{
			var box = new CheckBox { Text = $"{name}  {chars}", AutoSize = true, UseMnemonic = false };
			tips.SetToolTip(box, $"Tick to add {chars} to the symbols, untick to take them out");
			box.CheckedChanged += (_, _) =>
			{
				if (!_settingGroups)
				{
					_symbolSet.Text = RandomTools.ToggleSymbols(_symbolSet.Text, chars, box.Checked);
				}
			};
			_symbolGroups.Add((box, chars));
			groups.Controls.Add(box);
		}
		fields.Controls.Add(groups, 0, fields.RowCount++);
		fields.SetColumnSpan(groups, 4);
		fields.Controls.Add(_noLookAlikes, 0, fields.RowCount++);
		fields.SetColumnSpan(_noLookAlikes, 4);
		var wordRow = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty };
		wordRow.Controls.Add(new Label { Text = "Favourite &word", AutoSize = true, Margin = new Padding(3, 6, 6, 3) });
		wordRow.Controls.Add(_word);
		fields.Controls.Add(wordRow, 0, fields.RowCount++);
		fields.SetColumnSpan(wordRow, 4);
		var wordNote = Note();
		wordNote.Text = "The word is mixed into the secure random source: it never weakens a password, "
			+ "and the same word does not give the same password. It is not saved.";
		wordNote.Dock = DockStyle.None;
		wordNote.MaximumSize = new Size(480, 0);
		fields.Controls.Add(wordNote, 0, fields.RowCount++);
		fields.SetColumnSpan(wordNote, 4);
		foreach (var box in new[] { _upper, _lower, _digits, _symbols, _noLookAlikes })
		{
			box.CheckedChanged += (_, _) => ShowStrength();
		}
		_length.ValueChanged += (_, _) => ShowStrength();
		_symbolSet.TextChanged += (_, _) =>
		{
			ShowStrength();
			TickGroups();
		};

		var draw = new Button { Text = "&Generate", AutoSize = true, MinimumSize = new Size(90, 0) };
		draw.Click += (_, _) => DrawPasswords();
		var copy = new Button { Text = "Cop&y", AutoSize = true };
		copy.Click += (_, _) => CopyLines(_passwords, all: false, secret: true);
		var copyAll = new Button { Text = "Copy &All", AutoSize = true };
		copyAll.Click += (_, _) => CopyLines(_passwords, all: true, secret: true);
		_passwords.DoubleClick += (_, _) => CopyLines(_passwords, all: false, secret: true);
		var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 6, 0, 0) };
		buttons.Controls.AddRange(new Control[] { draw, copy, copyAll });

		page.Controls.Add(_passwords);
		page.Controls.Add(_strength);
		page.Controls.Add(buttons);
		page.Controls.Add(fields);
		return page;
	}

	// A group is ticked while all of its symbols are in the box (also after a hand edit)
	private void TickGroups()
	{
		_settingGroups = true;
		foreach (var (box, chars) in _symbolGroups)
		{
			box.Checked = chars.All(_symbolSet.Text.Contains);
		}
		_settingGroups = false;
	}

	private PasswordOptions Options()
	{
		return new PasswordOptions((int)_length.Value, _upper.Checked, _lower.Checked, _digits.Checked, _symbols.Checked,
			_symbolSet.Text, _noLookAlikes.Checked);
	}

	private void ShowStrength()
	{
		var options = Options();
		int pool = RandomTools.PoolSize(options);
		_strength.ForeColor = SystemColors.GrayText;
		_strength.Text = pool == 0 ? "No characters chosen."
			: $"{pool} characters, {options.Length} long: about {RandomTools.StrengthBits(options):0} bits";
	}

	private void DrawPasswords()
	{
		_passwords.Items.Clear();
		var options = Options();
		IRandomSource source = _word.Text.Length > 0 ? new MixedRandom(_word.Text) : _secure;
		for (int i = 0; i < (int)_passwordCount.Value; i++)
		{
			var (password, error) = RandomTools.Password(source, options);
			if (password == null)
			{
				_strength.ForeColor = Theme.Error;
				_strength.Text = error;
				return;
			}
			_passwords.Items.Add(password);
		}
		_passwords.SelectedIndex = 0;
		ShowStrength();
	}

	//==================================================================
	//  Copy
	//==================================================================
	private void CopyLines(ListBox list, bool all, bool secret)
	{
		var lines = (all ? list.Items.Cast<string>() : list.SelectedItems.Cast<string>()).ToList();
		if (lines.Count == 0)
		{
			return;
		}
		string text = string.Join(Environment.NewLine, lines);
		if (secret)
		{
			SecretClipboard.Copy(text);
			_strength.ForeColor = SystemColors.GrayText;
			_strength.Text = $"Copied {lines.Count}; the clipboard is cleared in {SecretClipboard.Seconds} s.";
		}
		else
		{
			Clipboard.SetText(text);
		}
	}

	//==================================================================
	//  Settings
	//==================================================================
	private void LoadPrefs()
	{
		var p = _prefs;
		(_from.Value, _to.Value, _count.Value) = (p.From, p.To, p.Count);
		(_places.Value, _heads.Value, _sides.Value) = (p.Places, p.HeadsPercent, p.Sides);
		_noRepeats.Checked = p.NoRepeats;
		(_headWord.Text, _tailWord.Text) = (p.HeadWord, p.TailWord);
		var kind = new[] { _whole, _decimal, _coin, _dice }.FirstOrDefault(k => (string)k.Tag! == p.Kind) ?? _whole;
		kind.Checked = true;
		ShowKind();
		(_length.Value, _passwordCount.Value) = (p.PasswordLength, p.PasswordCount);
		(_upper.Checked, _lower.Checked, _digits.Checked, _symbols.Checked) = (p.Upper, p.Lower, p.Digits, p.Symbols);
		(_symbolSet.Text, _noLookAlikes.Checked) = (p.SymbolSet, p.NoLookAlikes);
		TickGroups();
		_tabs.SelectedIndex = p.Tab == "password" ? 1 : 0;
		ShowStrength();
	}

	internal void SavePrefs()
	{
		var p = _prefs;
		(p.From, p.To, p.Count) = (_from.Value, _to.Value, (int)_count.Value);
		(p.Places, p.HeadsPercent, p.Sides) = ((int)_places.Value, _heads.Value, (int)_sides.Value);
		p.NoRepeats = _noRepeats.Checked;
		(p.HeadWord, p.TailWord) = (_headWord.Text.Trim(), _tailWord.Text.Trim());
		p.Kind = (string)new[] { _whole, _decimal, _coin, _dice }.First(k => k.Checked).Tag!;
		(p.PasswordLength, p.PasswordCount) = ((int)_length.Value, (int)_passwordCount.Value);
		(p.Upper, p.Lower, p.Digits, p.Symbols) = (_upper.Checked, _lower.Checked, _digits.Checked, _symbols.Checked);
		(p.SymbolSet, p.NoLookAlikes) = (_symbolSet.Text, _noLookAlikes.Checked);
		p.Tab = _tabs.SelectedIndex == 1 ? "password" : "numbers";
		var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
		p.Window = new WindowPlacement { X = bounds.X, Y = bounds.Y, Width = bounds.Width, Height = bounds.Height };
	}

	//==================================================================
	//  Building blocks
	//==================================================================
	private void AddPair(TableLayoutPanel fields, string first, Control a, string second, Control b)
	{
		int row = fields.RowCount++;
		var left = Caption(first);
		var right = Caption(second);
		_captions[a] = left;
		_captions[b] = right;
		fields.Controls.Add(left, 0, row);
		fields.Controls.Add(a, 1, row);
		fields.Controls.Add(right, 2, row);
		fields.Controls.Add(b, 3, row);
	}

	private static Label Caption(string text)
	{
		return new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 6, 3) };
	}

	private static RadioButton Kind(string text, string tag)
	{
		return new RadioButton { Text = text, Tag = tag, AutoSize = true, Margin = new Padding(3, 3, 12, 3) };
	}

	private static NumericUpDown Number(decimal min, decimal max, int places)
	{
		return new NumericUpDown { Minimum = min, Maximum = max, DecimalPlaces = places, Width = 130, ThousandsSeparator = false };
	}

	private static ListBox Results(bool monospace)
	{
		var list = new ListBox
		{
			Dock = DockStyle.Fill, SelectionMode = SelectionMode.MultiExtended, IntegralHeight = false,
			Margin = new Padding(0, 6, 0, 0),
		};
		if (monospace)
		{
			list.Font = new Font(FontFamily.GenericMonospace, 10.5f);
		}
		return list;
	}

	private static Label Note()
	{
		return new Label { AutoSize = true, Dock = DockStyle.Top, ForeColor = SystemColors.GrayText, Padding = new Padding(0, 4, 0, 4) };
	}
}

// Copies a secret: kept out of Windows clipboard history and cloud sync,
// and cleared after a while if it is still on the clipboard (owner 2026-11-12)
internal static class SecretClipboard
{
	public const int Seconds = 30;
	private static readonly System.Windows.Forms.Timer Wipe = new() { Interval = Seconds * 1000 };
	private static string? _copied;

	static SecretClipboard()
	{
		Wipe.Tick += (_, _) => WipeNow();
	}

	public static void Copy(string text)
	{
		var data = new DataObject();
		data.SetData(DataFormats.UnicodeText, text);
		data.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream(new byte[] { 1, 0, 0, 0 }));
		data.SetData("CanIncludeInClipboardHistory", new MemoryStream(new byte[] { 0, 0, 0, 0 }));
		data.SetData("CanUploadToCloudClipboard", new MemoryStream(new byte[] { 0, 0, 0, 0 }));
		Clipboard.SetDataObject(data, copy: true);
		_copied = text;
		Wipe.Stop();
		Wipe.Start();
	}

	// Clears the clipboard only if it still holds what was copied here
	public static void WipeNow()
	{
		Wipe.Stop();
		try
		{
			if (_copied != null && Clipboard.ContainsText() && Clipboard.GetText() == _copied)
			{
				Clipboard.Clear();
			}
		}
		catch (System.Runtime.InteropServices.ExternalException)
		{
			// another program holds the clipboard; leave it
		}
		_copied = null;
	}
}
