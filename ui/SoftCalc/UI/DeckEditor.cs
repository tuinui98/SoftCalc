// DeckEditor.cs - New deck / Edit deck / View of a ready-made deck (design/random_plan.md R6)
//
// Owner 2026-11-14: the cards on the left; the chosen card's name,
// weight and picture on the right; an Add box that splits by lines,
// commas or spaces (Auto: the first of them found); Edit all as text
// ("name | weight" lines); CSV import and export (name,weight,image).
// Works on a copy: OK keeps the changes, Cancel drops them. A
// ready-made deck opens read-only.

using SoftCalc.Logic;
using SoftCalc.Persistence;

namespace SoftCalc.UI;

internal sealed class DeckEditor : Form
{
	private const int ThumbSize = 24;
	private readonly Deck _deck;
	private readonly CardStore _store;
	private readonly CardPictures _pictures;
	private readonly string _language;
	private readonly bool _readOnly;
	private readonly TextBox _name = new() { Width = 260 };
	private readonly ListView _list = new()
	{
		Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false,
		HeaderStyle = ColumnHeaderStyle.Nonclickable,
	};
	private readonly TextBox _cardName = new() { Width = 170 };
	private readonly NumericUpDown _weight = new() { Minimum = 1, Maximum = CardText.MaxWeight, Width = 70 };
	private readonly PictureBox _picture = new()
	{
		Size = new Size(96, 96), SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(3, 3, 3, 3),
	};
	private readonly Button _choose = new() { Text = "C&hoose...", AutoSize = true };
	private readonly Button _clearPicture = new() { Text = "C&lear", AutoSize = true };
	private readonly TextBox _add = new() { Width = 300, PlaceholderText = "Fried rice, Noodles, Som tam" };
	private readonly ComboBox _split = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
	private readonly Label _count = new() { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(8, 8, 3, 3) };
	private readonly Label _note = new() { AutoSize = true, Dock = DockStyle.Bottom, Padding = new Padding(0, 4, 0, 4), ForeColor = SystemColors.GrayText };
	private bool _showing;

	public Deck Result => _deck;

	public DeckEditor(Deck deck, CardStore store, CardPictures pictures, string language, bool readOnly)
	{
		_deck = new Deck { Id = deck.Id, Name = deck.Name, Thai = deck.Thai, Alphabetical = deck.Alphabetical,
			Cards = deck.Cards.Select(c => c.Copy()).ToList() };
		_store = store;
		_pictures = pictures;
		_language = language;
		_readOnly = readOnly;
		Text = readOnly ? "Ready-made deck" : string.IsNullOrEmpty(deck.Name) ? "New deck" : "Edit deck";
		Icon = SvgIcon.ProgramIcon();
		ShowInTaskbar = false;
		MinimizeBox = false;
		StartPosition = FormStartPosition.CenterParent;
		AutoScaleMode = AutoScaleMode.Dpi;
		ClientSize = new Size(620, 500);
		MinimumSize = new Size(560, 420);
		Padding = new Padding(8);

		//==================================================================
		//  Name, cards, the chosen card
		//==================================================================
		var nameRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false };
		nameRow.Controls.Add(new Label { Text = "&Name", AutoSize = true, Margin = new Padding(3, 7, 6, 3) });
		nameRow.Controls.Add(_name);
		nameRow.Controls.Add(_count);
		_name.Text = deck.Shown(language);
		_name.ReadOnly = readOnly;

		_list.Columns.Add("Card", 300);
		_list.Columns.Add("Weight", 60, HorizontalAlignment.Right);
		_list.SelectedIndexChanged += (_, _) => ShowCard();
		_list.KeyDown += (_, e) =>
		{
			if (e.KeyCode == Keys.Delete && !_readOnly)
			{
				RemoveCards();
			}
		};
		var card = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Right, Padding = new Padding(8, 0, 0, 0) };
		card.Controls.Add(Caption("Car&d"), 0, 0);
		card.Controls.Add(_cardName, 1, 0);
		card.Controls.Add(Caption("&Weight"), 0, 1);
		card.Controls.Add(_weight, 1, 1);
		card.Controls.Add(Caption("Picture"), 0, 2);
		card.Controls.Add(_picture, 1, 2);
		var pictureButtons = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty };
		pictureButtons.Controls.AddRange(new Control[] { _choose, _clearPicture });
		card.Controls.Add(pictureButtons, 1, 3);
		var weightNote = new Label
		{
			AutoSize = true, MaximumSize = new Size(200, 0), ForeColor = SystemColors.GrayText,
			Text = "A card of weight 2 comes up twice as often as one of weight 1 (when the draw uses weights).",
		};
		card.Controls.Add(weightNote, 0, 4);
		card.SetColumnSpan(weightNote, 2);
		_cardName.TextChanged += (_, _) => EditCard(c => c.Name = _cardName.Text.Trim());
		_weight.ValueChanged += (_, _) => EditCard(c => c.Weight = (int)_weight.Value);
		_choose.Click += (_, _) => ChoosePicture();
		_clearPicture.Click += (_, _) => EditCard(c => c.Image = null, redraw: true);

		var listButtons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Bottom, WrapContents = true };
		var remove = new Button { Text = "&Remove", AutoSize = true };
		remove.Click += (_, _) => RemoveCards();
		var up = new Button { Text = "▲", AutoSize = true };
		up.Click += (_, _) => MoveCard(-1);
		var down = new Button { Text = "▼", AutoSize = true };
		down.Click += (_, _) => MoveCard(1);
		listButtons.Controls.AddRange(new Control[] { remove, up, down });

		//==================================================================
		//  Add, text, CSV, OK
		//==================================================================
		var addRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Bottom, WrapContents = true };
		_split.Items.AddRange(CardText.SplitNames);
		_split.SelectedIndex = 0;
		var addButton = new Button { Text = "&Add", AutoSize = true };
		addButton.Click += (_, _) => AddTyped();
		_add.KeyDown += (_, e) =>
		{
			if (e.KeyCode == Keys.Enter)
			{
				e.SuppressKeyPress = true;
				AddTyped();
			}
		};
		addRow.Controls.Add(new Label { Text = "Add", AutoSize = true, Margin = new Padding(3, 7, 6, 3) });
		addRow.Controls.AddRange(new Control[] { _add, new Label { Text = "Split", AutoSize = true, Margin = new Padding(6, 7, 3, 3) }, _split, addButton });
		new ToolTip().SetToolTip(_add, "Several cards at once: one per line, or split by commas or spaces. "
			+ "A name with a space needs lines or commas.");

		var fileRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Bottom, WrapContents = true };
		var asText = new Button { Text = "Edit all as &text...", AutoSize = true };
		asText.Click += (_, _) => EditAsText();
		var import = new Button { Text = "&Import CSV...", AutoSize = true };
		import.Click += (_, _) => ImportCsv();
		var export = new Button { Text = "E&xport CSV...", AutoSize = true };
		export.Click += (_, _) => ExportDeck(this, _deck, _store, _language, _note);
		fileRow.Controls.AddRange(new Control[] { asText, import, export });

		var ok = new Button { Text = "OK", AutoSize = true, MinimumSize = new Size(80, 0) };
		ok.Click += (_, _) => Finish();
		var cancel = new Button { Text = "Cancel", AutoSize = true, MinimumSize = new Size(80, 0), DialogResult = DialogResult.Cancel };
		var okRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft };
		okRow.Controls.Add(cancel);
		okRow.Controls.Add(ok);
		AcceptButton = readOnly ? cancel : ok;
		CancelButton = cancel;
		if (readOnly)
		{
			ok.Visible = false;
			cancel.Text = "Close";
			foreach (var control in new Control[] { _cardName, _weight, _choose, _clearPicture, remove, up, down, addRow, asText, import })
			{
				control.Enabled = false;
			}
		}

		Controls.Add(_list);
		Controls.Add(card);
		Controls.Add(listButtons);
		Controls.Add(addRow);
		Controls.Add(fileRow);
		Controls.Add(_note);
		Controls.Add(okRow);
		Controls.Add(nameRow);
		FormClosed += (_, _) => _list.SmallImageList?.Dispose();
		FillList(0);
	}

	//==================================================================
	//  The list
	//==================================================================
	private IEnumerable<Card> Ordered => _readOnly ? _deck.Listed(_language) : _deck.Cards;

	private void FillList(int select)
	{
		var old = _list.SmallImageList;
		_list.BeginUpdate();
		_list.Items.Clear();
		_list.SmallImageList = _pictures.List(_deck.Cards, ThumbSize);
		foreach (var card in Ordered)
		{
			var item = new ListViewItem(card.Shown(_language)) { Tag = card, ImageKey = _pictures.Key(card) ?? "" };
			item.SubItems.Add(card.Weight.ToString());
			_list.Items.Add(item);
		}
		_list.EndUpdate();
		old?.Dispose();
		if (_list.Items.Count > 0)
		{
			int index = Math.Clamp(select, 0, _list.Items.Count - 1);
			_list.Items[index].Selected = true;
			_list.Items[index].Focused = true;
			_list.EnsureVisible(index);
		}
		_count.Text = $"{_deck.Cards.Count} cards";
		ShowCard();
	}

	private Card? Selected => _list.SelectedItems.Count == 1 ? (Card)_list.SelectedItems[0].Tag! : null;

	private void ShowCard()
	{
		_showing = true;
		var card = Selected;
		bool editable = card != null && !_readOnly;
		_cardName.Text = card?.Shown(_language) ?? "";
		_weight.Value = card?.Weight ?? 1;
		_picture.Image = card == null ? null : _pictures.For(card);
		_cardName.Enabled = editable;
		_weight.Enabled = editable;
		_choose.Enabled = editable;
		_clearPicture.Enabled = editable && card!.Image != null;
		_showing = false;
	}

	private void EditCard(Action<Card> change, bool redraw = false)
	{
		if (_showing || Selected is not { } card)
		{
			return;
		}
		change(card);
		var item = _list.SelectedItems[0];
		if (redraw)
		{
			FillList(item.Index);
			return;
		}
		item.Text = card.Name;
		item.SubItems[1].Text = card.Weight.ToString();
	}

	private void ChoosePicture()
	{
		using var dialog = new OpenFileDialog
		{
			Title = "Picture for the card",
			Filter = "Pictures|*.png;*.jpg;*.jpeg;*.gif;*.bmp|All files|*.*",
		};
		if (dialog.ShowDialog(this) != DialogResult.OK)
		{
			return;
		}
		var (name, error) = _store.ImportImage(dialog.FileName);
		Say(error);
		if (name != null)
		{
			EditCard(c => c.Image = name, redraw: true);
		}
	}

	private void RemoveCards()
	{
		var gone = _list.SelectedItems.Cast<ListViewItem>().Select(i => (Card)i.Tag!).ToList();
		if (gone.Count == 0)
		{
			return;
		}
		int first = _list.SelectedIndices[0];
		_deck.Cards.RemoveAll(gone.Contains);
		FillList(first);
	}

	private void MoveCard(int by)
	{
		if (Selected is not { } card)
		{
			return;
		}
		int index = _deck.Cards.IndexOf(card), target = index + by;
		if (target < 0 || target >= _deck.Cards.Count)
		{
			return;
		}
		_deck.Cards.RemoveAt(index);
		_deck.Cards.Insert(target, card);
		FillList(target);
	}

	//==================================================================
	//  Adding cards
	//==================================================================
	private void AddTyped()
	{
		var names = CardText.Split(_add.Text, CardText.Splits[Math.Max(0, _split.SelectedIndex)]);
		var made = new List<Card>();
		foreach (string name in names)
		{
			var (card, error) = CardText.Make(name, 1);
			if (card == null)
			{
				Say($"'{Short(name)}': {error}");
				return;
			}
			made.Add(card);
		}
		int skipped = CardText.AddNew(_deck.Cards, made);
		_add.Clear();
		FillList(_deck.Cards.Count - 1);
		SayAdded(made.Count - skipped, skipped);
	}

	private void EditAsText()
	{
		using var dialog = new Form
		{
			Text = "Edit all as text", Icon = SvgIcon.ProgramIcon(), ShowInTaskbar = false, MinimizeBox = false,
			StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(420, 420), Padding = new Padding(8),
			AutoScaleMode = AutoScaleMode.Dpi,
		};
		var box = new TextBox
		{
			Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, AcceptsReturn = true,
			Text = CardText.FormatLines(_deck.Cards),
		};
		var hint = new Label { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 6),
			Text = "One card per line; a weight after a bar, e.g.  Fried rice | 2" };
		var error = new Label { AutoSize = true, Dock = DockStyle.Bottom, ForeColor = Theme.Error, Padding = new Padding(0, 4, 0, 0) };
		var ok = new Button { Text = "OK", AutoSize = true, MinimumSize = new Size(80, 0) };
		var cancel = new Button { Text = "Cancel", AutoSize = true, MinimumSize = new Size(80, 0), DialogResult = DialogResult.Cancel };
		var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft };
		buttons.Controls.AddRange(new Control[] { cancel, ok });
		dialog.Controls.AddRange(new Control[] { box, hint, error, buttons });
		dialog.CancelButton = cancel;
		List<Card>? parsed = null;
		ok.Click += (_, _) =>
		{
			var (cards, message) = CardText.ParseLines(box.Text);
			if (cards == null)
			{
				error.Text = message;
				return;
			}
			parsed = cards;
			dialog.DialogResult = DialogResult.OK;
		};
		if (dialog.ShowDialog(this) != DialogResult.OK || parsed == null)
		{
			return;
		}
		// A card keeps its picture when its name stays
		var old = _deck.Cards.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(),
			StringComparer.OrdinalIgnoreCase);
		var kept = new List<Card>();
		foreach (var card in parsed)
		{
			if (old.TryGetValue(card.Name, out var before))
			{
				(card.Image, card.Color) = (before.Image, before.Color);
			}
		}
		int skipped = CardText.AddNew(kept, parsed);
		_deck.Cards = kept;
		FillList(0);
		Say(skipped > 0 ? $"{skipped} cards with a name already above were left out." : null);
	}

	private void ImportCsv()
	{
		var rows = ReadCsv(this, _store, _note);
		if (rows == null)
		{
			return;
		}
		int skipped = CardText.AddNew(_deck.Cards, rows);
		FillList(_deck.Cards.Count - 1);
		SayAdded(rows.Count - skipped, skipped);
	}

	// Reads a CSV the user picks: cards with their pictures stored; null if cancelled or unreadable
	internal static List<Card>? ReadCsv(IWin32Window owner, CardStore store, Label note, Action<string>? named = null)
	{
		using var dialog = new OpenFileDialog
		{
			Title = "Import cards", Filter = "CSV or text files|*.csv;*.txt|All files|*.*",
		};
		if (dialog.ShowDialog(owner) != DialogResult.OK)
		{
			return null;
		}
		string text;
		try
		{
			text = CardText.ReadText(File.ReadAllBytes(dialog.FileName));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Show(note, ex.Message);
			return null;
		}
		var (rows, error) = CardText.ParseCsv(text);
		if (rows == null)
		{
			Show(note, error);
			return null;
		}
		string folder = Path.GetDirectoryName(dialog.FileName) ?? "";
		var cards = new List<Card>();
		var missing = new List<string>();
		foreach (var (card, image) in rows)
		{
			if (image.Length > 0)
			{
				string path = Path.IsPathRooted(image) ? image : Path.Combine(folder, image);
				var (name, _) = File.Exists(path) ? store.ImportImage(path) : (null, null);
				card.Image = name;
				if (name == null)
				{
					missing.Add(Path.GetFileName(image));
				}
			}
			cards.Add(card);
		}
		Show(note, missing.Count == 0 ? null : $"Pictures not found or not readable: {string.Join(", ", missing.Take(5))}"
			+ (missing.Count > 5 ? $" and {missing.Count - 5} more" : ""));
		named?.Invoke(Path.GetFileNameWithoutExtension(dialog.FileName));
		return cards;
	}

	// Writes the deck as CSV (UTF-8 with a BOM, so Excel shows Thai)
	internal static void ExportDeck(IWin32Window owner, Deck deck, CardStore store, string language, Label note)
	{
		using var dialog = new SaveFileDialog
		{
			Title = "Export cards", Filter = "CSV files|*.csv", FileName = MakeFileName(deck.Shown(language)) + ".csv",
		};
		if (dialog.ShowDialog(owner) != DialogResult.OK)
		{
			return;
		}
		string csv = CardText.ToCsv(deck.Listed(language), language, c => store.ImagePath(c.Image) ?? "");
		try
		{
			DataFolder.WriteAtomic(dialog.FileName, System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray());
			note.ForeColor = SystemColors.GrayText;
			note.Text = $"Exported {deck.Cards.Count} cards to {Path.GetFileName(dialog.FileName)}.";
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Show(note, ex.Message);
		}
	}

	private static string MakeFileName(string name)
	{
		var bad = Path.GetInvalidFileNameChars();
		string safe = new(name.Select(c => bad.Contains(c) ? '_' : c).ToArray());
		return safe.Length == 0 ? "cards" : safe;
	}

	//==================================================================
	//  OK
	//==================================================================
	private void Finish()
	{
		string name = _name.Text.Trim();
		if (name.Length == 0)
		{
			Say("The deck needs a name.");
			_name.Focus();
			return;
		}
		if (_deck.Cards.FirstOrDefault(c => c.Name.Length == 0) is not null)
		{
			Say("Every card needs a name.");
			return;
		}
		var twice = _deck.Cards.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
		if (twice != null)
		{
			Say($"Two cards are named '{Short(twice.Key)}'.");
			return;
		}
		_deck.Name = name.Length > CardText.MaxName ? name[..CardText.MaxName] : name;
		DialogResult = DialogResult.OK;
	}

	private void SayAdded(int added, int skipped)
	{
		_note.ForeColor = SystemColors.GrayText;
		_note.Text = $"Added {added} cards." + (skipped > 0 ? $" {skipped} left out: the name is in the deck already." : "");
	}

	private void Say(string? error)
	{
		Show(_note, error);
	}

	private static void Show(Label note, string? error)
	{
		note.ForeColor = Theme.Error;
		note.Text = error ?? "";
	}

	private static string Short(string name)
	{
		return name.Length > 40 ? name[..40] + "..." : name;
	}

	private static Label Caption(string text)
	{
		return new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 6, 3) };
	}
}
