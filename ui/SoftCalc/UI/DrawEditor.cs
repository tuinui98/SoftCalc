// DrawEditor.cs - New draw / Edit tab: the name and the steps of a draw tab (design/random_plan.md R7)
//
// Owner 2026-11-14: each step pools any decks (the user's own and the
// ready-made ones) with extra cards typed for this draw only, which are
// not saved as a deck; each card can be unticked (Days without Saturday
// and Sunday); Use weights and No repeat are set per step. Works on a
// copy of the steps: OK keeps them, Cancel drops them.

using SoftCalc.Logic;
using SoftCalc.Persistence;

namespace SoftCalc.UI;

internal sealed class DrawEditor : Form
{
	private readonly CardStore _store;
	private readonly string _language;
	private readonly Func<Deck?> _newDeck;
	private readonly List<DrawStep> _steps;
	private readonly TextBox _name = new() { Width = 260 };
	private readonly ListBox _stepList = new() { Dock = DockStyle.Fill, IntegralHeight = false };
	private readonly GroupBox _stepBox = new() { Dock = DockStyle.Fill, Padding = new Padding(6) };
	private readonly ListBox _decks = new() { Width = 260, Height = 64, IntegralHeight = false };
	private readonly ComboBox _deckChoice = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220, MaxDropDownItems = 20 };
	private readonly TextBox _extra = new()
	{
		Multiline = true, Height = 44, Width = 420, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true,
		PlaceholderText = "Cards for this draw only, e.g.  End of the month, Payday",
	};
	private readonly CheckedListBox _cardList = new() { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false };
	private readonly Label _cardCount = new() { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(3, 8, 3, 3) };
	private readonly CheckBox _weights = new() { Text = "Use &weights", AutoSize = true };
	private readonly CheckBox _noRepeat = new() { Text = "No &repeat (like a pile: a drawn card stays out until Shuffle)", AutoSize = true };
	private readonly Label _error = new() { AutoSize = true, Dock = DockStyle.Bottom, ForeColor = Theme.Error, Padding = new Padding(0, 4, 0, 4) };
	private List<PoolCard> _pool = new();
	private bool _showing;

	public string ResultName => _name.Text.Trim();
	public List<DrawStep> ResultSteps => _steps;

	public DrawEditor(string title, string name, IEnumerable<DrawStep> steps, CardStore store, string language, Func<Deck?> newDeck)
	{
		_store = store;
		_language = language;
		_newDeck = newDeck;
		_steps = steps.Select(Copy).ToList();
		if (_steps.Count == 0)
		{
			_steps.Add(new DrawStep());
		}
		Text = title;
		Icon = SvgIcon.ProgramIcon();
		ShowInTaskbar = false;
		MinimizeBox = false;
		StartPosition = FormStartPosition.CenterParent;
		AutoScaleMode = AutoScaleMode.Dpi;
		ClientSize = new Size(720, 540);
		MinimumSize = new Size(640, 460);
		Padding = new Padding(8);

		//==================================================================
		//  Name and steps
		//==================================================================
		var nameRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false, Padding = new Padding(0, 0, 0, 6) };
		nameRow.Controls.Add(new Label { Text = "&Name", AutoSize = true, Margin = new Padding(3, 7, 6, 3) });
		nameRow.Controls.Add(_name);
		_name.Text = name;

		var stepButtons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Bottom, WrapContents = true };
		var addStep = new Button { Text = "Add &step", AutoSize = true };
		addStep.Click += (_, _) => AddStep();
		var removeStep = new Button { Text = "Remove", AutoSize = true };
		removeStep.Click += (_, _) => RemoveStep();
		var up = new Button { Text = "▲", AutoSize = true };
		up.Click += (_, _) => MoveStep(-1);
		var down = new Button { Text = "▼", AutoSize = true };
		down.Click += (_, _) => MoveStep(1);
		stepButtons.Controls.AddRange(new Control[] { addStep, removeStep, up, down });
		var stepsBox = new GroupBox { Text = "Steps", Dock = DockStyle.Left, Width = 170, Padding = new Padding(6) };
		stepsBox.Controls.Add(_stepList);
		stepsBox.Controls.Add(stepButtons);
		var stepNote = new Label
		{
			Dock = DockStyle.Bottom, AutoSize = true, MaximumSize = new Size(156, 0), ForeColor = SystemColors.GrayText,
			Text = "One Draw gives a card from each step, in turn.", Padding = new Padding(0, 4, 0, 4),
		};
		stepsBox.Controls.Add(stepNote);
		_stepList.SelectedIndexChanged += (_, _) => ShowStep();

		//==================================================================
		//  The chosen step
		//==================================================================
		var deckRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2 };
		deckRow.Controls.Add(Caption("&Decks"), 0, 0);
		var deckButtons = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty, WrapContents = true, MaximumSize = new Size(460, 0) };
		var addDeck = new Button { Text = "&Add", AutoSize = true };
		addDeck.Click += (_, _) => AddDeck();
		var removeDeck = new Button { Text = "R&emove deck", AutoSize = true };
		removeDeck.Click += (_, _) => RemoveDeck();
		var newDeckButton = new Button { Text = "New dec&k...", AutoSize = true };
		newDeckButton.Click += (_, _) => MakeDeck();
		deckButtons.Controls.AddRange(new Control[] { _deckChoice, addDeck, removeDeck, newDeckButton });
		var deckColumn = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = Padding.Empty, WrapContents = false };
		deckColumn.Controls.Add(_decks);
		deckColumn.Controls.Add(deckButtons);
		deckRow.Controls.Add(deckColumn, 1, 0);
		deckRow.Controls.Add(Caption("E&xtra"), 0, 1);
		deckRow.Controls.Add(_extra, 1, 1);
		new ToolTip().SetToolTip(_extra, "Split by lines, else commas, else spaces. Not saved as a deck.");
		_extra.TextChanged += (_, _) =>
		{
			if (!_showing && Current is { } step)
			{
				step.Extra = _extra.Text;
				FillCards();
				FillSteps();
			}
		};

		var cardTop = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false };
		var all = new Button { Text = "A&ll", AutoSize = true };
		all.Click += (_, _) => TickAll(true);
		var none = new Button { Text = "N&one", AutoSize = true };
		none.Click += (_, _) => TickAll(false);
		cardTop.Controls.AddRange(new Control[] { Caption("&Cards"), all, none, _cardCount });
		_cardList.ItemCheck += (_, e) =>
		{
			if (_showing || Current is not { } step)
			{
				return;
			}
			string key = _pool[e.Index].Key;
			step.Off.Remove(key);
			if (e.NewValue == CheckState.Unchecked)
			{
				step.Off.Add(key);
			}
			BeginInvoke(() => ShowCount());
		};
		var options = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Bottom, FlowDirection = FlowDirection.TopDown, WrapContents = false };
		options.Controls.Add(_weights);
		options.Controls.Add(_noRepeat);
		_weights.CheckedChanged += (_, _) => SetOption(s => s.Weights = _weights.Checked);
		_noRepeat.CheckedChanged += (_, _) => SetOption(s => s.NoRepeat = _noRepeat.Checked);

		_stepBox.Controls.Add(_cardList);
		_stepBox.Controls.Add(cardTop);
		_stepBox.Controls.Add(options);
		_stepBox.Controls.Add(deckRow);

		var ok = new Button { Text = "OK", AutoSize = true, MinimumSize = new Size(80, 0) };
		ok.Click += (_, _) => Finish();
		var cancel = new Button { Text = "Cancel", AutoSize = true, MinimumSize = new Size(80, 0), DialogResult = DialogResult.Cancel };
		var okRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft };
		okRow.Controls.Add(cancel);
		okRow.Controls.Add(ok);
		AcceptButton = ok;
		CancelButton = cancel;

		var middle = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 0, 0) };
		middle.Controls.Add(_stepBox);
		Controls.Add(middle);
		Controls.Add(stepsBox);
		Controls.Add(_error);
		Controls.Add(okRow);
		Controls.Add(nameRow);
		FillDeckChoice();
		FillSteps();
		_stepList.SelectedIndex = 0;
		ShowStep();
	}

	private DrawStep? Current => _stepList.SelectedIndex >= 0 && _stepList.SelectedIndex < _steps.Count ? _steps[_stepList.SelectedIndex] : null;

	private static DrawStep Copy(DrawStep s)
	{
		return new DrawStep
		{
			Decks = s.Decks.ToList(), Extra = s.Extra, Off = s.Off.ToList(), Weights = s.Weights, NoRepeat = s.NoRepeat,
			Drawn = s.Drawn.ToList(), Counts = new Dictionary<string, int>(s.Counts),
		};
	}

	//==================================================================
	//  Steps
	//==================================================================
	private void FillSteps()
	{
		int selected = _stepList.SelectedIndex;
		_showing = true;
		_stepList.BeginUpdate();
		_stepList.Items.Clear();
		for (int i = 0; i < _steps.Count; i++)
		{
			_stepList.Items.Add($"{i + 1}. {Summary(_steps[i])}");
		}
		_stepList.EndUpdate();
		_stepList.SelectedIndex = Math.Clamp(selected, 0, _steps.Count - 1);
		_showing = false;
		if (_stepList.SelectedIndex != selected)
		{
			ShowStep();
		}
	}

	// "Days + 2 extra" for a step
	private string Summary(DrawStep step)
	{
		var parts = step.Decks.Select(id => _store.Find(id)?.Shown(_language) ?? "(deck gone)").ToList();
		int extra = CardText.Split(step.Extra, "auto").Count;
		if (extra > 0)
		{
			parts.Add($"{extra} extra");
		}
		return parts.Count == 0 ? "(no cards)" : string.Join(" + ", parts);
	}

	private void AddStep()
	{
		if (_steps.Count >= CardDraws.MaxSteps)
		{
			_error.Text = $"At most {CardDraws.MaxSteps} steps.";
			return;
		}
		_steps.Add(new DrawStep());
		FillSteps();
		_stepList.SelectedIndex = _steps.Count - 1;
		ShowStep();
	}

	private void RemoveStep()
	{
		if (_steps.Count <= 1 || Current is not { } step)
		{
			return;
		}
		int index = _stepList.SelectedIndex;
		_steps.Remove(step);
		FillSteps();
		_stepList.SelectedIndex = Math.Min(index, _steps.Count - 1);
		ShowStep();
	}

	private void MoveStep(int by)
	{
		int index = _stepList.SelectedIndex, target = index + by;
		if (index < 0 || target < 0 || target >= _steps.Count)
		{
			return;
		}
		(_steps[index], _steps[target]) = (_steps[target], _steps[index]);
		FillSteps();
		_stepList.SelectedIndex = target;
		ShowStep();
	}

	private void ShowStep()
	{
		if (_showing || Current is not { } step)
		{
			return;
		}
		_showing = true;
		_stepBox.Text = $"Step {_stepList.SelectedIndex + 1}";
		_decks.Items.Clear();
		foreach (string id in step.Decks)
		{
			_decks.Items.Add(new Choice(id, _store.Find(id)?.Shown(_language) ?? "(deck gone)"));
		}
		_extra.Text = step.Extra;
		_weights.Checked = step.Weights;
		_noRepeat.Checked = step.NoRepeat;
		_showing = false;
		FillCards();
	}

	private void SetOption(Action<DrawStep> change)
	{
		if (!_showing && Current is { } step)
		{
			change(step);
			FillCards();
		}
	}

	//==================================================================
	//  Decks and cards
	//==================================================================
	private sealed record Choice(string Id, string Text)
	{
		public override string ToString() => Text;
	}

	private void FillDeckChoice()
	{
		_deckChoice.Items.Clear();
		foreach (var deck in _store.File.Decks)
		{
			_deckChoice.Items.Add(new Choice(deck.Id, deck.Name));
		}
		foreach (var deck in ReadyDecks.All)
		{
			_deckChoice.Items.Add(new Choice(deck.Id, deck.Shown(_language) + "  (ready-made)"));
		}
		if (_deckChoice.Items.Count > 0)
		{
			_deckChoice.SelectedIndex = 0;
		}
	}

	private void AddDeck()
	{
		if (_deckChoice.SelectedItem is Choice choice)
		{
			AddDeck(choice.Id);
		}
	}

	private void AddDeck(string id)
	{
		if (Current is not { } step || step.Decks.Contains(id) || _store.Find(id) is not { } deck)
		{
			return;
		}
		step.Decks.Add(id);
		// cards that start unticked (the jokers)
		step.Off.AddRange(deck.Cards.Where(c => c.Off).Select(c => CardDraws.Key(id, c)));
		ShowStep();
		FillSteps();
	}

	private void RemoveDeck()
	{
		if (Current is not { } step || _decks.SelectedItem is not Choice choice)
		{
			return;
		}
		step.Decks.Remove(choice.Id);
		step.Off.RemoveAll(k => k.StartsWith(choice.Id + "/", StringComparison.Ordinal));
		ShowStep();
		FillSteps();
	}

	private void MakeDeck()
	{
		if (_newDeck() is { } deck)
		{
			FillDeckChoice();
			AddDeck(deck.Id);
		}
	}

	private void FillCards()
	{
		if (Current is not { } step)
		{
			return;
		}
		_showing = true;
		_pool = CardDraws.Pool(step, _store.Find);
		var off = new HashSet<string>(step.Off);
		_cardList.BeginUpdate();
		_cardList.Items.Clear();
		foreach (var p in _pool)
		{
			string weight = step.Weights && p.Card.Weight > 1 ? $"   ×{p.Card.Weight}" : "";
			_cardList.Items.Add(p.Card.Shown(_language) + weight, !off.Contains(p.Key));
		}
		_cardList.EndUpdate();
		_showing = false;
		ShowCount();
	}

	private void ShowCount()
	{
		_cardCount.Text = $"{_cardList.CheckedItems.Count} of {_pool.Count} ticked";
	}

	private void TickAll(bool tick)
	{
		if (Current is not { } step)
		{
			return;
		}
		var keys = _pool.Select(p => p.Key).ToHashSet();
		step.Off.RemoveAll(keys.Contains);
		if (!tick)
		{
			step.Off.AddRange(keys);
		}
		FillCards();
	}

	//==================================================================
	//  OK
	//==================================================================
	private void Finish()
	{
		if (ResultName.Length == 0)
		{
			_error.Text = "The draw needs a name.";
			_name.Focus();
			return;
		}
		for (int i = 0; i < _steps.Count; i++)
		{
			var step = _steps[i];
			if (CardDraws.Live(new DrawStep { Off = step.Off }, CardDraws.Pool(step, _store.Find)).Count == 0)
			{
				_error.Text = $"Step {i + 1} has no ticked cards: add a deck or extra cards.";
				_stepList.SelectedIndex = i;
				return;
			}
			// keys that left the pool no longer count as drawn or unticked
			var keys = CardDraws.Pool(step, _store.Find).Select(p => p.Key).ToHashSet();
			step.Off.RemoveAll(k => !keys.Contains(k));
			step.Drawn.RemoveAll(k => !keys.Contains(k));
			step.Off = step.Off.Distinct().ToList();
		}
		DialogResult = DialogResult.OK;
	}

	private static Label Caption(string text)
	{
		return new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(3, 7, 6, 3) };
	}
}
