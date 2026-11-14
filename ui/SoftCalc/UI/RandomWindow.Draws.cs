// RandomWindow.Draws.cs - The draw tabs: New draw, Edit tab, Delete tab, and their source (design/random_plan.md R7)
//
// Owner 2026-11-14: a draw tab sits before the "..." tab; New draw on
// "..." asks for its name (Custom random to start) and steps. Draws use
// the Source row like the other tabs: System (with any Entropy mix), or
// Seed, where the window's engine draws rand() once per pick, so the same
// seed and the same piles give the same cards. Nothing goes to the
// calculator's History; the tab keeps its own (cards.json).

using System.Globalization;
using SoftCalc.Logic;
using SoftCalc.Persistence;

namespace SoftCalc.UI;

internal sealed partial class RandomWindow
{
	private const string DefaultDrawName = "Custom random";

	private void AddDrawPages()
	{
		foreach (var setup in _cards.File.Draws)
		{
			_tabs.TabPages.Insert(_tabs.TabPages.IndexOf(_morePage), MakePage(setup));
		}
		_newDraw.Click += (_, _) => NewDraw();
	}

	private DrawPage MakePage(DrawSetup setup)
	{
		return new DrawPage(setup, _cards, _pictures, () => Language, PickerAsync, SaveCards, EditDraw, DeleteDraw);
	}

	private IEnumerable<DrawPage> DrawPages => _tabs.TabPages.OfType<DrawPage>();

	internal void NewDraw()
	{
		if (_cards.File.Draws.Count >= CardStore.MaxDraws)
		{
			Tell(_cardsNote, $"At most {CardStore.MaxDraws} draw tabs.", error: true);
			return;
		}
		var fresh = new DrawSetup();
		using var editor = new DrawEditor("New draw", DefaultDrawName, Array.Empty<DrawStep>(), _cards, Language, NewDeck,
			fresh.Theme, fresh.Effects);
		if (editor.ShowDialog(this) != DialogResult.OK)
		{
			FillDecks();
			return;
		}
		var setup = new DrawSetup
		{
			Id = CardStore.NewId(), Name = editor.ResultName, Steps = editor.ResultSteps, Theme = editor.ResultTheme,
			Effects = editor.ResultEffects,
		};
		_cards.File.Draws.Add(setup);
		SaveCards();
		FillDecks();
		var page = MakePage(setup);
		_tabs.TabPages.Insert(_tabs.TabPages.IndexOf(_morePage), page);
		_tabs.SelectedTab = page;
	}

	private void EditDraw(DrawPage page)
	{
		using var editor = new DrawEditor("Edit tab", page.Setup.Name, page.Setup.Steps, _cards, Language, NewDeck,
			page.Setup.Theme, page.Setup.Effects);
		if (editor.ShowDialog(this) == DialogResult.OK)
		{
			page.Setup.Name = editor.ResultName;
			page.Setup.Steps = editor.ResultSteps;
			if (page.Setup.Theme != editor.ResultTheme)
			{
				page.Setup.StageHeight = 0;    // a new theme starts at its own height
			}
			page.Setup.Theme = editor.ResultTheme;
			page.Setup.Effects = editor.ResultEffects;
			SaveCards();
			page.Reload(changed: true);
		}
		FillDecks();
	}

	private void DeleteDraw(DrawPage page)
	{
		var setup = page.Setup;
		if (MessageBox.Show(this, $"Delete the draw tab '{setup.Name}' with its History and Stats? This cannot be undone. "
			+ "Its decks stay.", "Delete tab", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2)
			!= DialogResult.OK)
		{
			return;
		}
		int index = _tabs.TabPages.IndexOf(page);
		_cards.File.Draws.Remove(setup);
		SaveCards();
		_tabs.TabPages.Remove(page);
		page.Dispose();
		_tabs.SelectedIndex = Math.Max(0, index - 1);
	}

	// A deck edited or deleted, or the names' language: the draw tabs show it
	private void DecksChanged()
	{
		foreach (var page in DrawPages)
		{
			page.Reload(changed: true);
		}
	}

	private void LanguageChanged()
	{
		DecksChanged();
	}

	// below(n) for picks draws: System, or rand() drawn by the engine after rands(seed)
	private async Task<Func<long, long>?> PickerAsync(int picks, Label note)
	{
		if (SeedInUse is not { } seed)
		{
			return n => (long)_secure.Below((ulong)n);
		}
		if (_drawing)
		{
			return null;
		}
		var texts = await SeedDrawsAsync(seed, "rand()", picks, unique: false, note);
		if (texts == null)
		{
			return null;
		}
		var values = new Queue<decimal>();
		foreach (string text in texts)
		{
			if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal u) || u < 0 || u >= 1)
			{
				note.ForeColor = Theme.Error;
				note.Text = $"The engine gave '{text}' for rand().";
				return null;
			}
			values.Enqueue(u);
		}
		note.ForeColor = SystemColors.GrayText;
		note.Text = $"Seed {seed}: the same seed and the same piles give the same cards.";
		return n => Math.Min(n - 1, (long)decimal.Floor(values.Dequeue() * n));
	}

	// The tab as saved: numbers ... entropy, "cards" for "...", or "draw:<id>"
	private string SelectedTabId()
	{
		return _tabs.SelectedTab switch
		{
			DrawPage page => "draw:" + page.Setup.Id,
			var tab when tab == _morePage => "cards",
			_ => TabIds[Math.Clamp(_tabs.SelectedIndex, 0, TabIds.Length - 1)],
		};
	}

	private void SelectTabId(string id)
	{
		TabPage? tab = id == "cards" ? _morePage
			: id.StartsWith("draw:", StringComparison.Ordinal) ? DrawPages.FirstOrDefault(p => "draw:" + p.Setup.Id == id)
			: Array.IndexOf(TabIds, id) is int i and >= 0 ? _tabs.TabPages[i] : null;
		_tabs.SelectedTab = tab ?? _tabs.TabPages[0];
	}
}
