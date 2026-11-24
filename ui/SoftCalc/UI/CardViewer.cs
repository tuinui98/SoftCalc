// CardViewer.cs - A deck's cards one at a time, large (owner 2026-11-24, deck covers)
//
// Opened from the deck editor (View large, a double-click or Space). The
// card's picture fills the window, or its colour when it has none; the
// arrow buttons and the Left and Right keys go through the deck, Esc
// closes. Use as cover sets the deck's cover to the card's picture (not
// for a ready-made deck).

using SoftCalc.Logic;

namespace SoftCalc.UI;

internal sealed class CardViewer : Form
{
	private readonly IReadOnlyList<Card> _cards;
	private readonly CardPictures _pictures;
	private readonly string _language;
	private readonly Action<Card>? _useAsCover;
	private readonly PictureBox _picture = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };
	private readonly Label _name = new() { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
	private readonly Label _where = new() { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = SystemColors.GrayText };
	private readonly Button _previous = new() { Text = "◀", AutoSize = true, MinimumSize = new Size(44, 0), AccessibleName = "Previous card" };
	private readonly Button _next = new() { Text = "▶", AutoSize = true, MinimumSize = new Size(44, 0), AccessibleName = "Next card" };
	private readonly Button _cover = new() { Text = "Use as &cover", AutoSize = true };
	private Bitmap? _swatch;
	private int _index;

	public int Index => _index;

	// useAsCover null: a ready-made deck, or no picture to set
	public CardViewer(string deckName, IReadOnlyList<Card> cards, int index, CardPictures pictures, string language, Action<Card>? useAsCover)
	{
		_cards = cards;
		_pictures = pictures;
		_language = language;
		_useAsCover = useAsCover;
		_index = Math.Clamp(index, 0, Math.Max(0, cards.Count - 1));
		Text = deckName;
		Icon = SvgIcon.ProgramIcon();
		ShowInTaskbar = false;
		MinimizeBox = false;
		StartPosition = FormStartPosition.CenterParent;
		AutoScaleMode = AutoScaleMode.Dpi;
		ClientSize = new Size(520, 580);
		MinimumSize = new Size(320, 360);
		Padding = new Padding(10);
		KeyPreview = true;

		var big = new Font(Font.FontFamily, Font.Size * 1.5f, FontStyle.Bold);
		_name.Font = big;
		var stage = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
		stage.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
		stage.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
		stage.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
		_previous.Anchor = AnchorStyles.None;
		_next.Anchor = AnchorStyles.None;
		stage.Controls.Add(_previous, 0, 0);
		stage.Controls.Add(_picture, 1, 0);
		stage.Controls.Add(_next, 2, 0);

		var close = new Button { Text = "Close", AutoSize = true, MinimumSize = new Size(80, 0), DialogResult = DialogResult.Cancel };
		var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft };
		buttons.Controls.Add(close);
		buttons.Controls.Add(_cover);
		_cover.Visible = useAsCover != null;
		CancelButton = close;

		var lines = new TableLayoutPanel { Dock = DockStyle.Bottom, ColumnCount = 1, RowCount = 2, Height = big.Height * 2 + Font.Height + 16 };
		lines.RowStyles.Add(new RowStyle(SizeType.Absolute, big.Height * 2));
		lines.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
		lines.Controls.Add(_name, 0, 0);
		lines.Controls.Add(_where, 0, 1);

		Controls.Add(stage);
		Controls.Add(lines);
		Controls.Add(buttons);

		_previous.Click += (_, _) => Go(-1);
		_next.Click += (_, _) => Go(1);
		_cover.Click += (_, _) =>
		{
			if (_cards.Count > 0)
			{
				_useAsCover?.Invoke(_cards[_index]);
				ShowCard();
			}
		};
		KeyDown += (_, e) =>
		{
			if (e.KeyCode is Keys.Left or Keys.Right)
			{
				Go(e.KeyCode == Keys.Left ? -1 : 1);
				e.Handled = true;
			}
		};
		FormClosed += (_, _) =>
		{
			_swatch?.Dispose();
			big.Dispose();
		};
		ShowCard();
	}

	private void Go(int by)
	{
		if (_cards.Count == 0)
		{
			return;
		}
		_index = (_index + by + _cards.Count) % _cards.Count;
		ShowCard();
	}

	private void ShowCard()
	{
		if (_cards.Count == 0)
		{
			_name.Text = "No cards";
			_where.Text = "";
			_previous.Enabled = _next.Enabled = _cover.Enabled = false;
			return;
		}
		var card = _cards[_index];
		_swatch?.Dispose();
		_swatch = null;
		if (card.Image != null && _pictures.Flat(card.Image) is { } picture)
		{
			_picture.Image = picture;
		}
		else if (card.Color != null)
		{
			// a card with a colour and no picture shows its colour, as on the dartboard
			_swatch = new Bitmap(64, 64);
			using (var g = Graphics.FromImage(_swatch))
			{
				g.Clear(ColorTranslator.FromHtml(card.Color));
			}
			_picture.Image = _swatch;
		}
		else
		{
			_picture.Image = null;
		}
		_name.Text = card.Shown(_language);
		string weight = card.Weight > 1 ? $"weight {card.Weight} · " : "";
		_where.Text = $"{weight}card {_index + 1} of {_cards.Count} · ← → for the next card, Esc to close";
		_previous.Enabled = _next.Enabled = _cards.Count > 1;
		_cover.Enabled = card.Image != null && _pictures.Picture(card.Image) != null;
	}
}
