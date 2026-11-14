// RandomWindow.Cards.cs - The "..." tab: new draw tabs and the card decks (design/random_plan.md R6)
//
// Owner 2026-11-14: the tab strip ends in a "..." tab; it makes a new
// draw tab, and holds the decks: the user's own (New, Edit, Copy,
// Delete, Import CSV, Export CSV) and the ready-made ones (View, Copy to
// my decks, Export CSV), named in English or Thai. Decks are saved in
// cards.json at once (CardStore).

using SoftCalc.Logic;
using SoftCalc.Persistence;

namespace SoftCalc.UI;

internal sealed partial class RandomWindow
{
	private readonly CardStore _cards;
	private readonly CardPictures _pictures;
	private readonly TabPage _morePage = new("...") { Padding = new Padding(8), ToolTipText = "New draw tab, card decks" };
	private readonly ListView _myDecks = DeckList();
	private readonly ListView _readyDecks = DeckList();
	private readonly ComboBox _language = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
	private readonly Label _cardsNote = Note();
	private readonly Button _newDraw = new() { Text = "New &draw...", AutoSize = true, MinimumSize = new Size(100, 0) };

	private string Language => _cards.File.Language;

	private TabPage MorePage()
	{
		var page = _morePage;
		var top = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true };
		var newDeck = new Button { Text = "New dec&k...", AutoSize = true, MinimumSize = new Size(100, 0) };
		newDeck.Click += (_, _) => NewDeck();
		_language.Items.AddRange(new object[] { "English", "ไทย" });
		_language.SelectedIndex = Language == "th" ? 1 : 0;
		_language.SelectedIndexChanged += (_, _) =>
		{
			_cards.File.Language = _language.SelectedIndex == 1 ? "th" : "en";
			SaveCards();
			FillDecks();
			LanguageChanged();
		};
		top.Controls.AddRange(new Control[] { _newDraw, newDeck,
			new Label { Text = "Ready-made names", AutoSize = true, Margin = new Padding(18, 8, 3, 3) }, _language });
		new ToolTip().SetToolTip(_language, "The language of the ready-made cards, in decks and draws");

		// My decks
		var myButtons = Buttons(
			("&Edit...", EditDeck), ("&Copy", CopyDeck), ("De&lete", DeleteDeck),
			("&Import CSV...", ImportDeck), ("E&xport CSV...", () => ExportSelected(_myDecks)));
		_myDecks.DoubleClick += (_, _) => EditDeck();
		var mine = Group("My decks", _myDecks, myButtons);

		// Ready-made decks
		var readyButtons = Buttons(("&View...", ViewDeck), ("Copy to my dec&ks", CopyReady),
			("Ex&port CSV...", () => ExportSelected(_readyDecks)));
		_readyDecks.DoubleClick += (_, _) => ViewDeck();
		var ready = Group("Ready-made decks", _readyDecks, readyButtons);

		var both = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
		both.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
		both.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
		both.Controls.Add(mine, 0, 0);
		both.Controls.Add(ready, 0, 1);
		page.Controls.Add(both);
		page.Controls.Add(_cardsNote);
		page.Controls.Add(top);
		if (_cards.Warning != null)
		{
			Tell(_cardsNote, _cards.Warning, error: true);
		}
		FillDecks();
		return page;
	}

	private void FillDecks()
	{
		Fill(_myDecks, _cards.File.Decks);
		Fill(_readyDecks, ReadyDecks.All);
	}

	private void Fill(ListView list, IEnumerable<Deck> decks)
	{
		string? selected = list.SelectedItems.Count > 0 ? ((Deck)list.SelectedItems[0].Tag!).Id : null;
		list.BeginUpdate();
		list.Items.Clear();
		foreach (var deck in decks)
		{
			var item = new ListViewItem(deck.Shown(Language)) { Tag = deck };
			item.SubItems.Add(deck.Cards.Count.ToString());
			list.Items.Add(item);
			item.Selected = deck.Id == selected;
		}
		list.EndUpdate();
	}

	private static Deck? Chosen(ListView list)
	{
		return list.SelectedItems.Count > 0 ? (Deck)list.SelectedItems[0].Tag! : null;
	}

	private void SaveCards()
	{
		try
		{
			_cards.Save();
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Tell(_cardsNote, "The decks could not be saved: " + ex.Message, error: true);
		}
	}

	//==================================================================
	//  My decks
	//==================================================================
	// The deck editor; returns the edited deck after OK, else null
	internal Deck? OpenDeckEditor(Deck deck, bool readOnly)
	{
		using var editor = new DeckEditor(deck, _cards, _pictures, Language, readOnly);
		return editor.ShowDialog(this) == DialogResult.OK && !readOnly ? editor.Result : null;
	}

	// A new deck of my own, from start's cards when given (Save as deck)
	internal Deck? NewDeck(Deck? start = null)
	{
		if (_cards.File.Decks.Count >= CardStore.MaxDecks)
		{
			Tell(_cardsNote, $"At most {CardStore.MaxDecks} decks.", error: true);
			return null;
		}
		var made = OpenDeckEditor(start ?? new Deck { Id = CardStore.NewId() }, readOnly: false);
		if (made != null)
		{
			_cards.File.Decks.Add(made);
			SaveCards();
			FillDecks();
			Tell(_cardsNote, $"Deck '{made.Name}' saved, {made.Cards.Count} cards.");
		}
		return made;
	}

	private void EditDeck()
	{
		if (Chosen(_myDecks) is not { } deck)
		{
			return;
		}
		var edited = OpenDeckEditor(deck, readOnly: false);
		if (edited != null)
		{
			_cards.File.Decks[_cards.File.Decks.IndexOf(deck)] = edited;
			SaveCards();
			FillDecks();
			DecksChanged();
		}
	}

	private void CopyDeck()
	{
		if (Chosen(_myDecks) is { } deck)
		{
			AddDeck(deck.CopyAs(CardStore.NewId(), _cards.FreeName(deck.Name), Language));
		}
	}

	private void CopyReady()
	{
		if (Chosen(_readyDecks) is { } deck)
		{
			AddDeck(deck.CopyAs(CardStore.NewId(), _cards.FreeName(deck.Shown(Language)), Language));
		}
	}

	private void AddDeck(Deck deck)
	{
		if (_cards.File.Decks.Count >= CardStore.MaxDecks)
		{
			Tell(_cardsNote, $"At most {CardStore.MaxDecks} decks.", error: true);
			return;
		}
		_cards.File.Decks.Add(deck);
		SaveCards();
		FillDecks();
		Tell(_cardsNote, $"Deck '{deck.Name}' added to my decks.");
	}

	private void DeleteDeck()
	{
		if (Chosen(_myDecks) is not { } deck)
		{
			return;
		}
		var users = _cards.File.Draws.Where(d => d.Steps.Any(s => s.Decks.Contains(deck.Id))).Select(d => d.Name).ToList();
		string used = users.Count == 0 ? "" : $"\n\nIt is used by: {string.Join(", ", users)}. Those draws lose its cards.";
		if (MessageBox.Show(this, $"Delete the deck '{deck.Name}' and its {deck.Cards.Count} cards? This cannot be undone.{used}",
			"Delete deck", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.OK)
		{
			return;
		}
		_cards.File.Decks.Remove(deck);
		SaveCards();
		FillDecks();
		DecksChanged();
		Tell(_cardsNote, $"Deck '{deck.Name}' deleted.");
	}

	private void ImportDeck()
	{
		string name = "Imported";
		var cards = DeckEditor.ReadCsv(this, _cards, _cardsNote, n => name = n);
		if (cards == null)
		{
			return;
		}
		var deck = new Deck { Id = CardStore.NewId(), Name = _cards.FreeName(name) };
		int skipped = CardText.AddNew(deck.Cards, cards);
		AddDeck(deck);
		if (skipped > 0)
		{
			Tell(_cardsNote, $"Deck '{deck.Name}' added; {skipped} cards with a name already in it were left out.");
		}
	}

	private void ExportSelected(ListView list)
	{
		if (Chosen(list) is { } deck)
		{
			DeckEditor.ExportDeck(this, deck, _cards, Language, _cardsNote);
		}
	}

	private void ViewDeck()
	{
		if (Chosen(_readyDecks) is { } deck)
		{
			OpenDeckEditor(deck, readOnly: true);
		}
	}

	//==================================================================
	//  Building blocks
	//==================================================================
	private static ListView DeckList()
	{
		var list = new ListView
		{
			Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false,
			HeaderStyle = ColumnHeaderStyle.Nonclickable,
		};
		list.Columns.Add("Deck", 300);
		list.Columns.Add("Cards", 70, HorizontalAlignment.Right);
		return list;
	}

	private static FlowLayoutPanel Buttons(params (string Text, Action Click)[] buttons)
	{
		var row = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Bottom, WrapContents = true };
		foreach (var (text, click) in buttons)
		{
			var button = new Button { Text = text, AutoSize = true };
			button.Click += (_, _) => click();
			row.Controls.Add(button);
		}
		return row;
	}

	private static GroupBox Group(string text, Control list, Control buttons)
	{
		var box = new GroupBox { Text = text, Dock = DockStyle.Fill, Padding = new Padding(6) };
		box.Controls.Add(list);
		box.Controls.Add(buttons);
		return box;
	}

	private static void Tell(Label note, string text, bool error = false)
	{
		note.ForeColor = error ? Theme.Error : SystemColors.GrayText;
		note.Text = text;
	}
}
