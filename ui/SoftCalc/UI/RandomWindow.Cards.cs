// RandomWindow.Cards.cs - The "..." tab: new draw tabs and the card decks (design/random_plan.md R6)
//
// Owner 2026-11-14: the tab strip ends in a "..." tab; it makes a new
// draw tab, and holds the decks: the user's own (New, Edit, Copy,
// Delete, Import CSV, Export CSV, and all of them in one zip with their
// pictures: Import decks, Export all decks, also in the File menu) and
// the ready-made ones (View, Copy to my decks, Export CSV), named in
// English or Thai. Decks are saved in cards.json at once (CardStore).
// Owner 2026-11-24: the decks show as covers (DeckCovers), or as the
// list they were; Cover... sets a deck's own cover picture.

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
	private readonly RadioButton _showCovers = ViewButton("Co&vers");
	private readonly RadioButton _showList = ViewButton("Lis&t");
	private const int CoverWidth = 80;    // a row of covers fits the window's first size (560 x 520)
	private const int CoverHeight = 60;

	private bool ShowingCovers => _cards.File.DeckView == "covers";

	// The size a row of covers in each list needs; a smaller window grows to it once (owner 2026-11-24)
	private const int CoversWidth = 620;
	private const int CoversHeight = 640;
	private bool _grownForCovers;

	private void GrowForCovers()
	{
		if (_grownForCovers || !ShowingCovers || WindowState != FormWindowState.Normal)
		{
			return;
		}
		_grownForCovers = true;
		var wanted = new Size(Math.Max(Width, LogicalToDeviceUnits(CoversWidth)), Math.Max(Height, LogicalToDeviceUnits(CoversHeight)));
		if (wanted != Size)
		{
			var area = Screen.FromControl(this).WorkingArea;
			Bounds = WindowPlacement.FitOnScreen(new Rectangle(Location, wanted), new[] { area });
		}
	}

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
		_showCovers.Checked = ShowingCovers;
		_showList.Checked = !ShowingCovers;
		_showCovers.CheckedChanged += (_, _) =>
		{
			_cards.File.DeckView = _showCovers.Checked ? "covers" : "list";
			SaveCards();
			FillDecks();
			GrowForCovers();
		};
		_tabs.SelectedIndexChanged += (_, _) =>
		{
			if (_tabs.SelectedTab == _morePage)
			{
				GrowForCovers();
			}
		};
		top.Controls.AddRange(new Control[] { _newDraw, newDeck,
			new Label { Text = "Ready-made names", AutoSize = true, Margin = new Padding(12, 8, 3, 3) }, _language, _showCovers, _showList });
		_showCovers.Margin = new Padding(12, 3, 0, 3);
		new ToolTip().SetToolTip(_language, "The language of the ready-made cards, in decks and draws");

		// My decks
		// One row of buttons, so the covers keep their room; Copy and the files go under More
		var myButtons = Buttons(
			("&Edit...", EditDeck), ("De&lete", DeleteDeck), ("Co&ver...", ChooseCover),
			("Insert Co&mmand", () => InsertDeckCommand(_myDecks)));
		var more = new Button { Text = "M&ore ▾", AutoSize = true };
		var files = new ContextMenuStrip();
		files.Items.Add("&Copy", null, (_, _) => CopyDeck());
		files.Items.Add(new ToolStripSeparator());
		files.Items.Add("&Import CSV...", null, (_, _) => ImportDeck());
		files.Items.Add("E&xport CSV...", null, (_, _) => ExportSelected(_myDecks));
		files.Items.Add(new ToolStripSeparator());
		files.Items.Add("Import deck&s...", null, (_, _) => ImportDecks());
		files.Items.Add("Export &all decks...", null, (_, _) => ExportDecks());
		more.Click += (_, _) => files.Show(more, new Point(0, more.Height));
		myButtons.Controls.Add(more);
		_myDecks.DoubleClick += (_, _) => EditDeck();
		var mine = Group("My decks", _myDecks, myButtons);

		// Ready-made decks
		var readyButtons = Buttons(("&View...", ViewDeck), ("Copy to my dec&ks", CopyReady),
			("Ex&port CSV...", () => ExportSelected(_readyDecks)), ("Insert Comma&nd", () => InsertDeckCommand(_readyDecks)));
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
		bool covers = ShowingCovers;
		var old = list.LargeImageList;
		list.BeginUpdate();
		list.Items.Clear();
		list.View = covers ? View.LargeIcon : View.Details;
		list.LargeImageList = covers ? CoverImages(decks) : null;
		foreach (var deck in decks)
		{
			string name = deck.Shown(Language);
			var item = new ListViewItem(covers ? $"{name} · {deck.Cards.Count}" : name)
			{
				Tag = deck, ImageKey = covers ? deck.Id : "", ToolTipText = $"{name}: {deck.Cards.Count} cards",
			};
			item.SubItems.Add(deck.Cards.Count.ToString());
			list.Items.Add(item);
			item.Selected = deck.Id == selected;
		}
		list.EndUpdate();
		old?.Dispose();
	}

	// Each deck's cover, keyed by its id (CardPictures.Cover)
	private ImageList CoverImages(IEnumerable<Deck> decks)
	{
		var images = new ImageList
		{
			ImageSize = new Size(LogicalToDeviceUnits(CoverWidth), LogicalToDeviceUnits(CoverHeight)), ColorDepth = ColorDepth.Depth32Bit,
		};
		foreach (var deck in decks)
		{
			images.Images.Add(deck.Id, _pictures.Cover(deck, Language, images.ImageSize.Width, images.ImageSize.Height));
		}
		return images;
	}

	// Cover...: a picture of the user's choosing for the chosen deck, or Auto (DeckCovers)
	private void ChooseCover()
	{
		if (Chosen(_myDecks) is not { } deck)
		{
			return;
		}
		var menu = new ContextMenuStrip();
		menu.Items.Add("Choose a &picture...", null, (_, _) =>
		{
			using var dialog = new OpenFileDialog
			{
				Title = $"Cover for '{deck.Name}'", Filter = "Pictures|*.png;*.jpg;*.jpeg;*.gif;*.bmp|All files|*.*",
			};
			if (dialog.ShowDialog(this) != DialogResult.OK)
			{
				return;
			}
			var (name, error) = _cards.ImportImage(dialog.FileName);
			if (name == null)
			{
				Tell(_cardsNote, error ?? "The picture could not be read.", error: true);
				return;
			}
			SetCover(deck, name);
		});
		var auto = menu.Items.Add("&Auto: the first card's picture, else its colours or first letter", null, (_, _) => SetCover(deck, null));
		auto.Enabled = deck.Cover != null;
		menu.Closed += (_, _) => BeginInvoke(menu.Dispose);
		var button = _myDecks.Parent!.Controls.OfType<FlowLayoutPanel>().SelectMany(f => f.Controls.OfType<Button>())
			.FirstOrDefault(b => b.Text == "Co&ver...");
		if (button != null)
		{
			menu.Show(button, new Point(0, button.Height));
		}
		else
		{
			menu.Show(Cursor.Position);
		}
	}

	private void SetCover(Deck deck, string? picture)
	{
		deck.Cover = picture;
		SaveCards();
		FillDecks();
		Tell(_cardsNote, picture == null ? $"'{deck.Name}' shows its first card's picture again." : $"Cover of '{deck.Name}' set.");
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

	//==================================================================
	//  All my decks in one zip (owner 2026-11-23; DeckTransfer)
	//==================================================================
	internal CardStore Cards => _cards;

	private void ExportDecks()
	{
		if (SaveDecksZip(this, _cards) is { } said)
		{
			Tell(_cardsNote, said);
		}
	}

	private void ImportDecks()
	{
		if (OpenDecksZip(this, _cards) is { } said)
		{
			DecksImported();
			Tell(_cardsNote, said.Text, error: said.Error);
		}
	}

	// After decks came in (here or through the File menu)
	internal void DecksImported()
	{
		FillDecks();
		DecksChanged();
	}

	// Save dialog and zip; returns what to tell, or null when cancelled
	internal static string? SaveDecksZip(IWin32Window owner, CardStore store)
	{
		if (store.File.Decks.Count == 0)
		{
			return "No decks of my own to export: New deck or Copy to my decks makes one.";
		}
		using var dialog = new SaveFileDialog
		{
			Title = "Export all my decks", Filter = "SoftCalc decks (*.zip)|*.zip", DefaultExt = "zip",
			FileName = DeckTransfer.ExportName(DateTime.Now),
		};
		if (dialog.ShowDialog(owner) != DialogResult.OK)
		{
			return null;
		}
		try
		{
			DataFolder.WriteAtomic(dialog.FileName, DeckTransfer.Write(store.File.Decks, store));
			return $"{store.File.Decks.Count} deck(s) saved to {Path.GetFileName(dialog.FileName)}.";
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return "Not exported: " + ex.Message;
		}
	}

	// Open dialog, read and add; saves cards.json. Returns what to tell, or null when cancelled
	internal static (string Text, bool Error)? OpenDecksZip(IWin32Window owner, CardStore store)
	{
		using var dialog = new OpenFileDialog
		{
			Title = "Import decks", Filter = "SoftCalc decks (*.zip)|*.zip|All files (*.*)|*.*", Multiselect = true,
		};
		if (dialog.ShowDialog(owner) != DialogResult.OK)
		{
			return null;
		}
		var said = new List<string>();
		bool error = false;
		foreach (string path in dialog.FileNames)
		{
			var (read, problem) = DeckTransfer.Read(path);
			if (read == null)
			{
				said.Add($"{Path.GetFileName(path)}: {problem}");
				error = true;
				continue;
			}
			said.Add($"{Path.GetFileName(path)}: {DeckTransfer.Import(store, read).Report()}");
		}
		try
		{
			store.Save();
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return ("The decks could not be saved: " + ex.Message, true);
		}
		return (string.Join(" ", said), error);
	}

	// card("Deck") in Input (owner 2026-11-24)
	private void InsertDeckCommand(ListView list)
	{
		if (Chosen(list) is { } deck)
		{
			_insert(CardText.Command(deck.Shown(Language)));
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
			HeaderStyle = ColumnHeaderStyle.Nonclickable, ShowItemToolTips = true,
		};
		list.Columns.Add("Deck", 300);
		list.Columns.Add("Cards", 70, HorizontalAlignment.Right);
		return list;
	}

	private static RadioButton ViewButton(string text)
	{
		return new RadioButton { Text = text, Appearance = Appearance.Button, AutoSize = true, Margin = new Padding(0, 3, 0, 3) };
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
