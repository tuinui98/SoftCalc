// DrawPage.cs - A draw tab of the Random window (design/random_plan.md R7)
//
// Owner 2026-11-14: Draw gives a line, a card from each step; Count
// draws several lines. The theme (List, Slot machine or Dartboard) and
// Effects are set in Edit tab; effects play when drawing one line.
// Shuffle puts the drawn cards back in the No repeat piles. Clear draw
// empties the results; Clear history and Clear stats ask first. Results, History
// (with the time) and Stats are on tabs below. Draws come from the
// Source row: System, or Seed through the window's engine.
// A card's condition may stop the line, skip to a step, or draw from a
// deck too: "Gym → Yoga", with its own reel or board and Stats rows.

using SoftCalc.Logic;
using SoftCalc.Persistence;

namespace SoftCalc.UI;

internal sealed class DrawPage : TabPage
{
	// Gives a below(n) for picks draws (System or Seed), or null when the seed failed
	internal delegate Task<Func<long, long>?> Picker(int picks, Label note);

	private readonly CardStore _store;
	private readonly Func<string> _language;
	private readonly Action<string> _insert;
	private readonly Picker _picker;
	private readonly Action _save;
	private readonly Label _title = new() { AutoSize = true, Margin = new Padding(3, 6, 12, 3) };
	private readonly Label _piles = new() { AutoSize = true, Dock = DockStyle.Top, ForeColor = SystemColors.GrayText, Padding = new Padding(0, 2, 0, 4) };
	private readonly Panel _stageHolder = new() { Dock = DockStyle.Top, Height = 150 };
	// owner 2026-11-14: drag the bar under the theme to make it taller and Results shorter, live
	private readonly Panel _stageBar = new() { Dock = DockStyle.Top, Height = 7, Cursor = Cursors.HSplit };
	private readonly Panel _viewsPanel = new() { Dock = DockStyle.Fill };
	private int _dragFrom = -1, _dragHeight, _dragMax;    // screen y where the drag began, the height then, the most it may grow to
	private readonly SlotStage _slot = new() { Dock = DockStyle.Fill };
	private readonly DartStage _dart = new() { Dock = DockStyle.Fill };
	private readonly SpinStage _spin = new() { Dock = DockStyle.Fill };
	private readonly Button _draw = new() { Text = "&Draw", AutoSize = true, MinimumSize = new Size(90, 0) };
	private readonly NumericUpDown _count = new() { Minimum = 1, Maximum = CardDraws.MaxCount, Width = 70 };
	private readonly Label _note = new() { AutoSize = true, Dock = DockStyle.Top, ForeColor = SystemColors.GrayText, Padding = new Padding(0, 4, 0, 4) };
	// Results, History and Stats: a row of toggle buttons over three panels (a nested TabControl's frame is white in dark mode)
	private readonly Panel[] _views = { ViewPanel(true), ViewPanel(false), ViewPanel(false) };
	private readonly Button[] _viewButtons = new Button[3];
	private int _view;    // 0 Results, 1 History, 2 Stats
	private Font? _boldFont;
	private Font _bold => _boldFont ??= new Font(Font, FontStyle.Bold);
	private readonly ListBox _results = new() { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle, SelectionMode = SelectionMode.MultiExtended };
	private readonly ListBox _history = new() { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.FixedSingle, SelectionMode = SelectionMode.MultiExtended };
	private readonly ListView _stats = new()
	{
		Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable,
	};
	private readonly Label _statsNote = new() { AutoSize = true, Dock = DockStyle.Bottom, ForeColor = SystemColors.GrayText, Padding = new Padding(0, 4, 0, 0) };
	private bool _busy;
	private List<StageStep>? _lastStage;    // the theme's last result, shown again in the new order after Shuffle
	private bool _skip;    // a click on Results ends the List effect
	// Owner 2026-11-24: each Results line shows its cards' pictures small; the chosen line's show large
	// beside Results (the List theme; the others have their stage)
	private readonly CardPictures _pictures;
	private readonly List<List<Card>> _lineCards = new();
	private readonly Panel _shown = new DoubleBufferedPanel { Dock = DockStyle.Right, Width = 220, Visible = false };
	private const int Thumb = 36;

	public DrawSetup Setup { get; }

	public DrawPage(DrawSetup setup, CardStore store, CardPictures pictures, Func<string> language, Picker picker, Action save,
		Action<DrawPage> edit, Action<DrawPage> delete, Action<string> insert)
	{
		Setup = setup;
		_insert = insert;
		_store = store;
		_language = language;
		_picker = picker;
		_save = save;
		Padding = new Padding(8);
		_pictures = pictures;
		_slot.Pictures = pictures;
		_dart.Pictures = pictures;
		_spin.Pictures = pictures;

		//==================================================================
		//  Top: name, Edit tab, Delete tab
		//==================================================================
		var top = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true };
		_title.Font = new Font(Font, FontStyle.Bold);
		var editTab = new Button { Text = "&Edit tab...", AutoSize = true };
		editTab.Click += (_, _) => edit(this);
		var deleteTab = new Button { Text = "Delete ta&b", AutoSize = true };
		deleteTab.Click += (_, _) => delete(this);
		top.Controls.AddRange(new Control[] { _title, editTab, deleteTab });

		//==================================================================
		//  Draw buttons
		//==================================================================
		var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true, Margin = new Padding(0, 6, 0, 0) };
		_count.Value = Math.Clamp(setup.Count, 1, CardDraws.MaxCount);
		_count.ValueChanged += (_, _) => Setup.Count = (int)_count.Value;
		_draw.Click += async (_, _) => await DrawAsync();
		var shuffle = new Button { Text = "&Shuffle", AutoSize = true };
		new ToolTip().SetToolTip(shuffle, "Put the drawn cards back in the No repeat piles");
		shuffle.Click += (_, _) => Shuffle();
		var clearDraw = new Button { Text = "C&lear draw", AutoSize = true };
		new ToolTip().SetToolTip(clearDraw, "Clear the results on screen; History and Stats stay");
		clearDraw.Click += (_, _) => ClearDraw();
		var copy = new Button { Text = "Cop&y", AutoSize = true };
		copy.Click += (_, _) => CopyLines();
		var command = new Button { Text = "Insert Co&mmand", AutoSize = true };
		new ToolTip().SetToolTip(command, "Put the card() line for this deck, Count and No repeat in Input, to run there");
		command.Click += (_, _) => InsertCommand();
		buttons.Controls.AddRange(new Control[] { _draw, new Label { Text = "&Count", AutoSize = true, Margin = new Padding(8, 6, 3, 3) },
			_count, shuffle, clearDraw, copy, command });

		//==================================================================
		//  Results, History, Stats
		//==================================================================
		_results.Font = new Font(Font.FontFamily, 11f);
		_results.MouseDown += (_, _) => _skip = true;
		_results.DrawMode = DrawMode.OwnerDrawFixed;
		_results.DrawItem += DrawResult;
		_results.SelectedIndexChanged += (_, _) => _shown.Invalidate();
		_shown.Paint += PaintShown;
		_shown.Resize += (_, _) => _shown.Invalidate();
		var (resultsPage, historyPage, statsPage) = (_views[0], _views[1], _views[2]);
		resultsPage.Controls.Add(_results);
		resultsPage.Controls.Add(_shown);
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
		statsPage.Controls.Add(_stats);
		statsPage.Controls.Add(_statsNote);
		statsPage.Controls.Add(clearStats);
		var viewRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false, Margin = Padding.Empty };
		string[] names = { "Results", "History", "Stats" };
		for (int i = 0; i < 3; i++)
		{
			int view = i;
			_viewButtons[i] = new Button { Text = names[i], AutoSize = true, MinimumSize = new Size(76, 0), Margin = new Padding(0, 3, 2, 3) };
			_viewButtons[i].Click += (_, _) => ShowView(view);
			viewRow.Controls.Add(_viewButtons[i]);
		}
		_results.ContextMenuStrip = Menu(("&Copy", () => Copy(_results, all: false)), ("Copy &all", () => Copy(_results, all: true)),
			("C&lear draw", ClearDraw));
		_history.ContextMenuStrip = Menu(("&Copy", () => Copy(_history, all: false)), ("Copy &all", () => Copy(_history, all: true)),
			("Clear &history...", ClearHistory));
		_stats.ContextMenuStrip = Menu(("Copy &all", CopyStats), ("Clear s&tats...", ClearStats));

		_stageHolder.Controls.Add(_slot);
		_stageHolder.Controls.Add(_dart);
		_stageHolder.Controls.Add(_spin);
		_viewsPanel.Controls.AddRange(_views);
		_viewsPanel.Controls.Add(viewRow);
		Controls.Add(_viewsPanel);
		_viewButtons[0].Font = _bold;    // Results shows first; toggling Visible here would make its handle early
		Controls.Add(_note);
		Controls.Add(buttons);
		Controls.Add(_stageBar);
		Controls.Add(_stageHolder);
		_stageBar.MouseDown += (_, e) =>
		{
			if (e.Button == MouseButtons.Left)
			{
				_dragFrom = Cursor.Position.Y;
				_dragHeight = _stageHolder.Height;
				_dragMax = Math.Max(80, _stageHolder.Height + _viewsPanel.Height - 60);    // Results keeps 60 px
			}
		};
		_stageBar.MouseMove += (_, _) =>
		{
			if (_dragFrom >= 0)
			{
				_stageHolder.Height = Math.Clamp(_dragHeight + Cursor.Position.Y - _dragFrom, 80, _dragMax);
				Update();
			}
		};
		_stageBar.MouseUp += (_, _) =>
		{
			if (_dragFrom >= 0)
			{
				_dragFrom = -1;
				Setup.StageHeight = _stageHolder.Height;
				_save();
			}
		};
		_stageBar.Paint += (_, e) =>
		{
			// a grip in the middle of the bar
			int x = _stageBar.Width / 2, y = _stageBar.Height / 2;
			using var grip = new Pen(SystemColors.ControlDark, 1f);
			e.Graphics.DrawLine(grip, x - 16, y - 1, x + 16, y - 1);
			e.Graphics.DrawLine(grip, x - 16, y + 1, x + 16, y + 1);
		};
		_stageBar.Resize += (_, _) => _stageBar.Invalidate();
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
		_spin.Language = Language;
		ShowPiles();
		ShowTheme();
		if (changed)
		{
			_slot.Clear();
			_dart.Clear();
			_spin.Clear();
			FillHistory();
			FillStats();
		}
	}

	private void ShowTheme()
	{
		_stageHolder.Visible = Setup.Theme != "list";
		_stageBar.Visible = Setup.Theme != "list";
		_slot.Visible = Setup.Theme == "slot";
		_dart.Visible = Setup.Theme == "dart";
		_spin.Visible = Setup.Theme == "spin";
		_shown.Visible = Setup.Theme == "list" && ResultPictures;
		_stageHolder.Height = StageHeight();
	}

	// The height dragged to, else the theme's own
	private int StageHeight()
	{
		return Setup.StageHeight > 0 ? Setup.StageHeight : Setup.Theme is "dart" or "spin" ? 220 : 160;
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
			var below = await _picker(count * CardDraws.MostPicks(Setup, _store.Find), _note);
			if (below == null || IsDisposed)
			{
				return;
			}
			var (lines, error) = CardDraws.Draw(Setup, _store.Find, count, below, Language, DateTime.UtcNow, _store.Named);
			_save();
			ShowView(0);
			if (Setup.Theme == "list" && Setup.Effects && lines.Count == 1)
			{
				await RollAsync(live);
			}
			if (lines.Count > 0 && Setup.Theme != "list")
			{
				// a reel or board for each card of the last line, a condition's cards too; the slot
				// machine shows up to 5 lines (owner 2026-11-14): one card each side by side, else a row each
				bool rows = Setup.Theme == "slot" && lines.Count <= CardDraws.StageLines;
				var shown = rows ? lines : lines.GetRange(lines.Count - 1, 1);
				bool single = shown.All(l => l.Count == 1);
				var stageSteps = shown.SelectMany((l, row) => l.Select(p => new StageStep(
					CardDraws.Arranged(Setup.Steps[p.Step], p.Live ?? new List<PoolCard> { p.Card }), p.Weights, p.Card, single ? 0 : row, p.Step))).ToList();
				_lastStage = stageSteps;
				var stage = Setup.Theme switch { "slot" => (DrawStage)_slot, "spin" => _spin, _ => _dart };
				if (Setup.Theme == "slot")
				{
					int rowCount = stageSteps.Max(s => s.Row) + 1;
					_stageHolder.Height = Math.Max(StageHeight(), rowCount == 1 ? 0 : 16 + 72 * rowCount);
				}
				if (Setup.Effects && (lines.Count == 1 || rows))
				{
					// owner 2026-11-14: no spoilers, Results and History wait for the reels or the dart
					_results.Items.Clear();
					await stage.PlayAsync(stageSteps);
				}
				else
				{
					stage.ShowStill(stageSteps);
				}
			}
			if (IsDisposed)
			{
				return;
			}
			ShowResults(lines.Select(l => (CardDraws.Text(l, Language), l.Select(p => p.Card.Card).ToList())).ToList());
			FillHistory();
			ShowPiles();
			if (error != null)
			{
				_note.ForeColor = Theme.Error;
				_note.Text = (lines.Count > 0 ? $"Drew {lines.Count}. " : "") + error;
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

	//==================================================================
	//  Results with pictures (owner 2026-11-24)
	//==================================================================
	private bool ResultPictures => _lineCards.Any(line => line.Any(card => card.Image != null && _pictures.Picture(card.Image) != null));

	private void ShowResults(List<(string Text, List<Card> Cards)> lines)
	{
		_lineCards.Clear();
		_lineCards.AddRange(lines.Select(l => l.Cards));
		bool pictures = ResultPictures;
		_results.ItemHeight = pictures ? LogicalToDeviceUnits(Thumb + 6) : _results.Font.Height + 4;
		_results.BeginUpdate();
		_results.Items.Clear();
		_results.Items.AddRange(lines.Select(l => (object)l.Text).ToArray());
		_results.EndUpdate();
		_shown.Visible = pictures && Setup.Theme == "list";
		_shown.Width = Math.Max(LogicalToDeviceUnits(160), _views[0].Width * 2 / 5);
		if (_results.Items.Count > 0 && pictures)
		{
			_results.SelectedIndex = 0;
		}
		_shown.Invalidate();
	}

	// A Results line: its cards' pictures small (on white), then the text
	private void DrawResult(object? sender, DrawItemEventArgs e)
	{
		if (e.Index < 0)
		{
			return;
		}
		e.DrawBackground();
		var bounds = e.Bounds;
		int x = bounds.Left + 4;
		if (e.Index < _lineCards.Count && ResultPictures)
		{
			int size = LogicalToDeviceUnits(Thumb);
			// only cards with a picture take a place; a colour alone is for the stages
			foreach (var picture in _lineCards[e.Index].Where(c => c.Image != null).Select(c => _pictures.Flat(c.Image!)).OfType<Image>().Take(6))
			{
				var slot = new Rectangle(x, bounds.Top + (bounds.Height - size) / 2, size, size);
				using var fitted = CardPictures.Fit(picture, size);
				e.Graphics.FillRectangle(Brushes.White, slot);
				e.Graphics.DrawImage(fitted, slot);
				x += size + 4;
			}
			x += 4;
		}
		var text = new Rectangle(x, bounds.Top, bounds.Right - x, bounds.Height);
		TextRenderer.DrawText(e.Graphics, _results.Items[e.Index].ToString(), e.Font, text, e.ForeColor,
			TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
		e.DrawFocusRectangle();
	}

	// The chosen line's cards large, side by side, the line's text under them
	private void PaintShown(object? sender, PaintEventArgs e)
	{
		int index = _results.SelectedIndex;
		if (index < 0 || index >= _lineCards.Count)
		{
			return;
		}
		var faces = _lineCards[index].Where(c => c.Image != null).Select(c => _pictures.Flat(c.Image!)).OfType<Image>().ToList();
		var area = _shown.ClientRectangle;
		area.Inflate(-6, -6);
		int textHeight = Font.Height * 2;
		var pictures = new Rectangle(area.Left, area.Top, area.Width, Math.Max(0, area.Height - textHeight));
		int count = Math.Max(1, faces.Count);
		int width = (pictures.Width - 6 * (count - 1)) / count;
		e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
		for (int i = 0; i < faces.Count; i++)
		{
			var picture = faces[i];
			var slot = new Rectangle(pictures.Left + i * (width + 6), pictures.Top, width, pictures.Height);
			double scale = Math.Min((double)slot.Width / picture.Width, (double)slot.Height / picture.Height);
			int w = (int)(picture.Width * scale), h = (int)(picture.Height * scale);
			e.Graphics.DrawImage(picture, slot.Left + (slot.Width - w) / 2, slot.Top + (slot.Height - h) / 2, w, h);
		}
		var name = new Rectangle(area.Left, pictures.Bottom, area.Width, textHeight);
		TextRenderer.DrawText(e.Graphics, _results.Items[index].ToString(), _bold, name, ForeColor,
			TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
	}

	private sealed class DoubleBufferedPanel : Panel
	{
		public DoubleBufferedPanel()
		{
			DoubleBuffered = true;
			ResizeRedraw = true;
		}
	}

	// The List effect: random lines from the piles roll in Results, slowing down, before the drawn one
	private async Task RollAsync(List<List<PoolCard>> live)
	{
		if (live.Any(l => l.Count == 0))
		{
			return;
		}
		_skip = false;
		var look = new Random();
		double wait = 30;
		for (var clock = System.Diagnostics.Stopwatch.StartNew(); !_skip && !IsDisposed && clock.Elapsed.TotalSeconds < 1.2; wait *= 1.12)
		{
			var line = live.Select(l => l[look.Next(l.Count)]).ToList();
			ShowResults(new() { (CardDraws.Text(line, Language), line.Select(p => p.Card).ToList()) });
			await Task.Delay((int)wait);
		}
	}

	private void Shuffle()
	{
		// owner 2026-11-14: Shuffle also mixes where the cards sit on the dartboard and the spin wheel
		var look = new Random();
		foreach (var step in Setup.Steps)
		{
			step.Drawn.Clear();
			step.SubDrawn.Clear();
			CardDraws.MixBoard(step, _store.Find, _store.Named, look);
		}
		_save();
		ShowPiles();
		if (_lastStage != null && Setup.Theme is "dart" or "spin" && !_busy)
		{
			var stage = Setup.Theme == "spin" ? (DrawStage)_spin : _dart;
			_lastStage = _lastStage.Where(s => s.Step < Setup.Steps.Count)
				.Select(s => s with { Live = CardDraws.Arranged(Setup.Steps[s.Step], s.Live) }).ToList();
			stage.ShowStill(_lastStage);
		}
		_note.ForeColor = SystemColors.GrayText;
		_note.Text = "Shuffled: every card is back in the piles, and the boards are mixed.";
	}

	private void ClearDraw()
	{
		ShowResults(new());
		_slot.Clear();
		_dart.Clear();
		_spin.Clear();
		_lastStage = null;
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
			step.SubCounts.Clear();
		}
		Setup.Draws = 0;
		_save();
		FillStats();
	}

	// card() takes one deck (owner 2026-11-24); its draws leave out only the cards saved unticked
	private void InsertCommand()
	{
		var step = Setup.Steps.Count == 1 ? Setup.Steps[0] : null;
		var deck = step is { Decks.Count: 1 } && string.IsNullOrWhiteSpace(step.Extra) ? _store.Find(step.Decks[0]) : null;
		if (deck == null)
		{
			_note.Text = "Insert Command needs a tab of one step with one deck and no extra cards.";
			return;
		}
		_insert(CardText.Command(deck.Shown(_language()), Setup.Count, step!.NoRepeat));
	}

	private void CopyLines()
	{
		Copy(_view == 1 ? _history : _results, all: false);
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

	private void ShowView(int view)
	{
		if (view == _view)
		{
			return;
		}
		_view = view;
		for (int i = 0; i < _views.Length; i++)
		{
			_views[i].Visible = i == view;
			// the chosen view: a flat button with a coloured frame
			_viewButtons[i].Font = i == view ? _bold : Font;
		}
		if (view == 2)
		{
			FillStats();
		}
	}

	private static Panel ViewPanel(bool shown)
	{
		return new Panel { Dock = DockStyle.Fill, Visible = shown, Padding = new Padding(0, 2, 0, 0) };
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

	private void AddStats(string step, List<(string Name, int Times, double Share, double Expected)> rows)
	{
		foreach (var (name, times, share, expected) in rows)
		{
			var item = new ListViewItem(step);
			item.SubItems.Add(name);
			item.SubItems.Add(times.ToString());
			item.SubItems.Add($"{share:0.0}%");
			item.SubItems.Add($"{expected:0.0}%");
			_stats.Items.Add(item);
		}
	}

	private void FillStats()
	{
		_stats.BeginUpdate();
		_stats.Items.Clear();
		for (int i = 0; i < Setup.Steps.Count; i++)
		{
			var step = Setup.Steps[i];
			AddStats((i + 1).ToString(), CardDraws.Stats(step, CardDraws.Pool(step, _store.Find), Language));
			// the decks its conditions draw from: "1 → Gym"
			foreach (var deck in CardDraws.SubDecks(step, _store.Find, _store.Named))
			{
				AddStats($"{i + 1}{CardDraws.SubJoiner}{deck.Shown(Language)}", CardDraws.SubStats(step, deck, Language));
			}
		}
		_stats.EndUpdate();
		_stats.Columns[0].Width = _stats.Items.Cast<ListViewItem>().Any(it => it.Text.Length > 3) ? 110 : 44;
		_statsNote.Text = $"{Setup.Draws} draws since the stats were cleared. Expected: the share each card's weight gives "
			+ "(No repeat changes it within a pile).";
	}
}
