// DrawPage.cs - A draw tab of the Random window (design/random_plan.md R7)
//
// Owner 2026-11-14: Draw gives a line, a card from each step; Count
// draws several lines. Theme: List, Slot machine or Dartboard, with
// Effects while drawing (one line at a time). Shuffle puts the drawn
// cards back in the No repeat piles. Clear draw empties the results and
// History, not Stats; Clear stats empties the counts. Results, History
// (with the time) and Stats are on tabs below. Draws come from the
// Source row: System, or Seed through the window's engine.

using SoftCalc.Logic;
using SoftCalc.Persistence;

namespace SoftCalc.UI;

internal sealed class DrawPage : TabPage
{
	// Gives a below(n) for picks draws (System or Seed), or null when the seed failed
	internal delegate Task<Func<long, long>?> Picker(int picks, Label note);

	private readonly CardStore _store;
	private readonly Func<string> _language;
	private readonly Picker _picker;
	private readonly Action _save;
	private readonly Label _title = new() { AutoSize = true, Margin = new Padding(3, 6, 12, 3) };
	private readonly ComboBox _theme = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
	private readonly Label _piles = new() { AutoSize = true, Dock = DockStyle.Top, ForeColor = SystemColors.GrayText, Padding = new Padding(0, 2, 0, 4) };
	private readonly Panel _stageHolder = new() { Dock = DockStyle.Top, Height = 150 };
	private readonly SlotStage _slot = new() { Dock = DockStyle.Fill };
	private readonly DartStage _dart = new() { Dock = DockStyle.Fill };
	private readonly Button _draw = new() { Text = "&Draw", AutoSize = true, MinimumSize = new Size(90, 0) };
	private readonly NumericUpDown _count = new() { Minimum = 1, Maximum = CardDraws.MaxCount, Width = 70 };
	private readonly CheckBox _effects = new() { Text = "E&ffects", AutoSize = true, Margin = new Padding(6, 6, 6, 3) };
	private readonly Label _note = new() { AutoSize = true, Dock = DockStyle.Top, ForeColor = SystemColors.GrayText, Padding = new Padding(0, 4, 0, 4) };
	private readonly TabControl _views = new() { Dock = DockStyle.Fill };
	private readonly ListBox _results = new() { Dock = DockStyle.Fill, IntegralHeight = false, SelectionMode = SelectionMode.MultiExtended };
	private readonly ListBox _history = new() { Dock = DockStyle.Fill, IntegralHeight = false, SelectionMode = SelectionMode.MultiExtended };
	private readonly ListView _stats = new()
	{
		Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable,
	};
	private readonly Label _statsNote = new() { AutoSize = true, Dock = DockStyle.Bottom, ForeColor = SystemColors.GrayText, Padding = new Padding(0, 4, 0, 0) };
	private bool _busy;

	public DrawSetup Setup { get; }

	public DrawPage(DrawSetup setup, CardStore store, CardPictures pictures, Func<string> language, Picker picker, Action save,
		Action<DrawPage> edit, Action<DrawPage> delete)
	{
		Setup = setup;
		_store = store;
		_language = language;
		_picker = picker;
		_save = save;
		Padding = new Padding(8);
		_slot.Pictures = pictures;
		_dart.Pictures = pictures;

		//==================================================================
		//  Top: name, theme, Edit tab, Delete tab
		//==================================================================
		var top = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true };
		_title.Font = new Font(Font, FontStyle.Bold);
		_theme.Items.AddRange(CardDraws.ThemeNames);
		_theme.SelectedIndex = Math.Max(0, Array.IndexOf(CardDraws.Themes, setup.Theme));
		_theme.SelectedIndexChanged += (_, _) =>
		{
			Setup.Theme = CardDraws.Themes[_theme.SelectedIndex];
			ShowTheme();
			_save();
		};
		var editTab = new Button { Text = "&Edit tab...", AutoSize = true };
		editTab.Click += (_, _) => edit(this);
		var deleteTab = new Button { Text = "Delete ta&b", AutoSize = true };
		deleteTab.Click += (_, _) => delete(this);
		top.Controls.AddRange(new Control[] { _title, new Label { Text = "T&heme", AutoSize = true, Margin = new Padding(3, 6, 3, 3) },
			_theme, _effects, editTab, deleteTab });

		//==================================================================
		//  Draw buttons
		//==================================================================
		var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true, Margin = new Padding(0, 6, 0, 0) };
		_count.Value = Math.Clamp(setup.Count, 1, CardDraws.MaxCount);
		_count.ValueChanged += (_, _) => Setup.Count = (int)_count.Value;
		_effects.Checked = setup.Effects;
		_effects.CheckedChanged += (_, _) => Setup.Effects = _effects.Checked;
		new ToolTip().SetToolTip(_effects, "Slot machine and Dartboard play while drawing one line; click the picture to skip");
		_draw.Click += async (_, _) => await DrawAsync();
		var shuffle = new Button { Text = "&Shuffle", AutoSize = true };
		new ToolTip().SetToolTip(shuffle, "Put the drawn cards back in the No repeat piles");
		shuffle.Click += (_, _) => Shuffle();
		var clearDraw = new Button { Text = "C&lear draw", AutoSize = true };
		new ToolTip().SetToolTip(clearDraw, "Clear the results on screen; History and Stats stay");
		clearDraw.Click += (_, _) => ClearDraw();
		var copy = new Button { Text = "Cop&y", AutoSize = true };
		copy.Click += (_, _) => CopyLines();
		buttons.Controls.AddRange(new Control[] { _draw, new Label { Text = "&Count", AutoSize = true, Margin = new Padding(8, 6, 3, 3) },
			_count, shuffle, clearDraw, copy });

		//==================================================================
		//  Results, History, Stats
		//==================================================================
		_results.Font = new Font(Font.FontFamily, 11f);
		var resultsPage = new TabPage("Results");
		resultsPage.Controls.Add(_results);
		var historyPage = new TabPage("History");
		historyPage.Controls.Add(_history);
		var clearHistory = new Button { Text = "Clear &history...", AutoSize = true, Dock = DockStyle.Bottom };
		clearHistory.Click += (_, _) => ClearHistory();
		historyPage.Controls.Add(clearHistory);
		_stats.Columns.Add("Step", 44);
		_stats.Columns.Add("Card", 200);
		_stats.Columns.Add("Times", 60, HorizontalAlignment.Right);
		_stats.Columns.Add("Share", 70, HorizontalAlignment.Right);
		_stats.Columns.Add("Expected", 70, HorizontalAlignment.Right);
		var clearStats = new Button { Text = "Clear s&tats...", AutoSize = true, Dock = DockStyle.Bottom };
		clearStats.Click += (_, _) => ClearStats();
		var statsPage = new TabPage("Stats");
		statsPage.Controls.Add(_stats);
		statsPage.Controls.Add(_statsNote);
		statsPage.Controls.Add(clearStats);
		_views.TabPages.AddRange(new[] { resultsPage, historyPage, statsPage });
		_results.ContextMenuStrip = Menu(("&Copy", () => Copy(_results, all: false)), ("Copy &all", () => Copy(_results, all: true)),
			("C&lear draw", ClearDraw));
		_history.ContextMenuStrip = Menu(("&Copy", () => Copy(_history, all: false)), ("Copy &all", () => Copy(_history, all: true)),
			("Clear &history...", ClearHistory));
		_stats.ContextMenuStrip = Menu(("Copy &all", CopyStats), ("Clear s&tats...", ClearStats));
		_views.SelectedIndexChanged += (_, _) =>
		{
			if (_views.SelectedTab == statsPage)
			{
				FillStats();
			}
		};

		_stageHolder.Controls.Add(_slot);
		_stageHolder.Controls.Add(_dart);
		Controls.Add(_views);
		Controls.Add(_note);
		Controls.Add(buttons);
		Controls.Add(_stageHolder);
		Controls.Add(_piles);
		Controls.Add(top);
		Reload(changed: true);
	}

	private string Language => _language();

	// After Edit tab, a deck change or the language: names, piles, history and stats again
	public void Reload(bool changed)
	{
		Text = Setup.Name.Length > 24 ? Setup.Name[..23] + "…" : Setup.Name;
		ToolTipText = Setup.Name;
		_title.Text = Setup.Name;
		_slot.Language = Language;
		_dart.Language = Language;
		ShowPiles();
		ShowTheme();
		if (changed)
		{
			_slot.Clear();
			_dart.Clear();
			FillHistory();
			FillStats();
		}
	}

	private void ShowTheme()
	{
		_stageHolder.Visible = Setup.Theme != "list";
		_slot.Visible = Setup.Theme == "slot";
		_dart.Visible = Setup.Theme == "dart";
		_stageHolder.Height = Setup.Theme == "dart" ? 210 : 160;
	}

	// "Step 1: 9 cards, 7 left" per step
	private void ShowPiles()
	{
		var parts = new List<string>();
		for (int i = 0; i < Setup.Steps.Count; i++)
		{
			var step = Setup.Steps[i];
			var pool = CardDraws.Pool(step, _store.Find);
			int ticked = CardDraws.Live(new DrawStep { Off = step.Off }, pool).Count;
			string left = step.NoRepeat ? $", {CardDraws.Live(step, pool).Count} left" : "";
			string weights = step.Weights && pool.Any(p => p.Card.Weight > 1) ? ", weighted" : "";
			parts.Add($"Step {i + 1}: {ticked} cards{left}{weights}");
		}
		_piles.Text = parts.Count == 0 ? "No steps: press Edit tab." : string.Join("   ", parts);
	}

	//==================================================================
	//  Draw
	//==================================================================
	private async Task DrawAsync()
	{
		if (_busy)
		{
			return;
		}
		_busy = true;
		_draw.Enabled = false;
		try
		{
			_note.ForeColor = SystemColors.GrayText;
			_note.Text = "";
			int count = (int)_count.Value;
			// before the draw: what each step can give, for the effect
			var live = Setup.Steps.Select(s => CardDraws.Live(s, CardDraws.Pool(s, _store.Find))).ToList();
			var below = await _picker(count * Setup.Steps.Count, _note);
			if (below == null || IsDisposed)
			{
				return;
			}
			var (lines, error) = CardDraws.Draw(Setup, _store.Find, count, below, Language, DateTime.UtcNow);
			_save();
			_results.BeginUpdate();
			_results.Items.Clear();
			_results.Items.AddRange(lines.Select(l => (object)CardDraws.Text(l, Language)).ToArray());
			_results.EndUpdate();
			_views.SelectedIndex = 0;
			FillHistory();
			ShowPiles();
			if (error != null)
			{
				_note.ForeColor = Theme.Error;
				_note.Text = (lines.Count > 0 ? $"Drew {lines.Count}. " : "") + error;
			}
			if (lines.Count > 0 && Setup.Theme != "list")
			{
				var last = lines[^1];
				var stageSteps = last.Select((card, i) =>
					new StageStep(lines.Count == 1 ? live[i] : CardDraws.Live(new DrawStep { Off = Setup.Steps[i].Off },
						CardDraws.Pool(Setup.Steps[i], _store.Find)), Setup.Steps[i].Weights, card)).ToList();
				var stage = Setup.Theme == "slot" ? (DrawStage)_slot : _dart;
				if (Setup.Effects && lines.Count == 1)
				{
					await stage.PlayAsync(stageSteps);
				}
				else
				{
					stage.ShowStill(stageSteps);
				}
			}
		}
		finally
		{
			_busy = false;
			if (!IsDisposed)
			{
				_draw.Enabled = true;
			}
		}
	}

	private void Shuffle()
	{
		foreach (var step in Setup.Steps)
		{
			step.Drawn.Clear();
		}
		_save();
		ShowPiles();
		_note.ForeColor = SystemColors.GrayText;
		_note.Text = "Shuffled: every card is back in the piles.";
	}

	private void ClearDraw()
	{
		_results.Items.Clear();
		_slot.Clear();
		_dart.Clear();
		_note.ForeColor = SystemColors.GrayText;
		_note.Text = "Results cleared; History and Stats stay.";
	}

	private void ClearHistory()
	{
		if (MessageBox.Show(FindForm(), $"Clear the History of '{Setup.Name}'? This cannot be undone. Stats stay.", "Clear history",
			MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.OK)
		{
			return;
		}
		Setup.History.Clear();
		_save();
		FillHistory();
	}

	private void ClearStats()
	{
		if (MessageBox.Show(FindForm(), $"Clear the stats of '{Setup.Name}'? This cannot be undone.", "Clear stats",
			MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.OK)
		{
			return;
		}
		foreach (var step in Setup.Steps)
		{
			step.Counts.Clear();
		}
		Setup.Draws = 0;
		_save();
		FillStats();
	}

	private void CopyLines()
	{
		Copy(_views.SelectedIndex == 1 ? _history : _results, all: false);
	}

	// The selected lines, or all of them when none is selected or all is asked
	private static void Copy(ListBox list, bool all)
	{
		var lines = (!all && list.SelectedItems.Count > 0 ? list.SelectedItems.Cast<string>() : list.Items.Cast<string>()).ToList();
		if (lines.Count > 0)
		{
			Clipboard.SetText(string.Join(Environment.NewLine, lines));
		}
	}

	// The stats as tab-separated lines, with the column names
	private void CopyStats()
	{
		var lines = new List<string> { string.Join('\t', _stats.Columns.Cast<ColumnHeader>().Select(c => c.Text)) };
		lines.AddRange(_stats.Items.Cast<ListViewItem>().Select(i => string.Join('\t', i.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(s => s.Text))));
		if (lines.Count > 1)
		{
			Clipboard.SetText(string.Join(Environment.NewLine, lines));
		}
	}

	private static ContextMenuStrip Menu(params (string Text, Action Click)[] items)
	{
		var menu = new ContextMenuStrip();
		foreach (var (text, click) in items)
		{
			menu.Items.Add(text, null, (_, _) => click());
		}
		return menu;
	}

	//==================================================================
	//  History and Stats
	//==================================================================
	private void FillHistory()
	{
		_history.BeginUpdate();
		_history.Items.Clear();
		_history.Items.AddRange(Setup.History.Select(h => (object)$"{CardDraws.Time(h.Time)}   {h.Text}").ToArray());
		_history.EndUpdate();
	}

	private void FillStats()
	{
		_stats.BeginUpdate();
		_stats.Items.Clear();
		for (int i = 0; i < Setup.Steps.Count; i++)
		{
			var step = Setup.Steps[i];
			foreach (var (name, times, share, expected) in CardDraws.Stats(step, CardDraws.Pool(step, _store.Find), Language))
			{
				var item = new ListViewItem((i + 1).ToString());
				item.SubItems.Add(name);
				item.SubItems.Add(times.ToString());
				item.SubItems.Add($"{share:0.0}%");
				item.SubItems.Add($"{expected:0.0}%");
				_stats.Items.Add(item);
			}
		}
		_stats.EndUpdate();
		_statsNote.Text = $"{Setup.Draws} draws since the stats were cleared. Expected: the share each card's weight gives "
			+ "(No repeat changes it within a pile).";
	}
}
