// CardPictures.cs - Card pictures for lists and draws: the stored picture, or a colour swatch (design/random_plan.md R6)
//
// Pictures are read from the cards folder once and kept while the
// Random window is open; the file is read into memory so it is never
// locked. A card without a picture but with a colour gets a swatch.
// Lists, covers and the large view show a picture on white (Shown, Flat:
// owner 2026-11-24, a transparent PNG drawn in black vanished on the dark
// theme); the draw stages keep the picture as it is (For), over the card's
// colour.

using SoftCalc.Logic;
using SoftCalc.Persistence;

namespace SoftCalc.UI;

internal sealed class CardPictures : IDisposable
{
	private readonly CardStore _store;
	private readonly Dictionary<string, Image?> _cache = new();

	public CardPictures(CardStore store)
	{
		_store = store;
	}

	// The card's picture, a swatch for its colour, or null
	public Image? For(Card card)
	{
		if (card.Image != null && Picture(card.Image) is { } image)
		{
			return image;
		}
		if (card.Color != null)
		{
			string key = "color:" + card.Color;
			if (!_cache.TryGetValue(key, out var swatch))
			{
				swatch = Swatch(ColorTranslator.FromHtml(card.Color));
				_cache[key] = swatch;
			}
			return swatch;
		}
		return null;
	}

	// What lists and the editor show for a card: its picture on white, else its colour swatch
	public Image? Shown(Card card)
	{
		return card.Image != null && Flat(card.Image) is { } flat ? flat : For(card) is { } swatch && card.Color != null ? swatch : null;
	}

	// The picture on a white ground, so a transparent one reads on any theme
	public Image? Flat(string name)
	{
		string key = "flat:" + name;
		if (!_cache.TryGetValue(key, out var flat))
		{
			if (Picture(name) is { } image)
			{
				var white = new Bitmap(image.Width, image.Height);
				using (var g = Graphics.FromImage(white))
				{
					g.Clear(Color.White);
					g.DrawImage(image, 0, 0, image.Width, image.Height);
				}
				flat = white;
			}
			_cache[key] = flat;
		}
		return flat;
	}

	// A stored picture by its file name, or null when its file is gone or unreadable
	public Image? Picture(string name)
	{
		if (!_cache.TryGetValue(name, out var image))
		{
			image = ReadyDecks.Picture(name) is { } embedded ? Read(embedded) : Read(_store.ImagePath(name));
			_cache[name] = image;
		}
		return image;
	}

	//==================================================================
	//  Deck covers (owner 2026-11-24): DeckCovers says what, this draws it
	//==================================================================
	public DeckCover CoverOf(Deck deck, string language)
	{
		return DeckCovers.Of(deck, language, name => Picture(name) != null);
	}

	// The deck's cover, width x height: a picture filled in (cut to fit), the colours as a
	// 2 x 2 mosaic, or the first letter in white on the deck's own colour
	public Bitmap Cover(Deck deck, string language, int width, int height)
	{
		var cover = CoverOf(deck, language);
		var bitmap = new Bitmap(width, height);
		using var g = Graphics.FromImage(bitmap);
		g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
		g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
		var area = new Rectangle(0, 0, width, height);
		switch (cover.Kind)
		{
			case CoverKind.Picture:
			{
				var image = Flat(cover.Image!)!;
				double scale = Math.Max((double)width / image.Width, (double)height / image.Height);
				int w = (int)Math.Ceiling(image.Width * scale), h = (int)Math.Ceiling(image.Height * scale);
				g.DrawImage(image, (width - w) / 2, (height - h) / 2, w, h);
				break;
			}
			case CoverKind.Colours:
			{
				int half = width / 2, middle = height / 2;
				var cells = new[] { new Rectangle(0, 0, half, middle), new Rectangle(half, 0, width - half, middle),
					new Rectangle(0, middle, half, height - middle), new Rectangle(half, middle, width - half, height - middle) };
				for (int i = 0; i < cells.Length; i++)
				{
					using var fill = new SolidBrush(ColorTranslator.FromHtml(cover.Colours[i % cover.Colours.Count]));
					g.FillRectangle(fill, cells[i]);
				}
				using var line = new Pen(Color.White, Math.Max(1, width / 48));
				g.DrawLine(line, half, 0, half, height);
				g.DrawLine(line, 0, middle, width, middle);
				break;
			}
			default:
			{
				using var back = new SolidBrush(ColorTranslator.FromHtml(cover.Back));
				g.FillRectangle(back, area);
				using var font = new Font("Segoe UI Semibold", height * 0.42f, GraphicsUnit.Pixel);
				TextRenderer.DrawText(g, cover.Letter, font, area, Color.White,
					TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
				break;
			}
		}
		return bitmap;
	}

	// The key of the card's picture in an ImageList, or null
	public string? Key(Card card)
	{
		return For(card) == null ? null : card.Image != null && _cache.GetValueOrDefault(card.Image) != null
			? card.Image : "color:" + card.Color;
	}

	// An ImageList of size px squares holding the pictures of these cards
	public ImageList List(IEnumerable<Card> cards, int size)
	{
		var list = new ImageList { ImageSize = new Size(size, size), ColorDepth = ColorDepth.Depth32Bit };
		foreach (var card in cards)
		{
			if (Key(card) is { } key && !list.Images.ContainsKey(key))
			{
				list.Images.Add(key, Fit(Shown(card)!, size));
			}
		}
		return list;
	}

	// The picture centred in a size px square, its sides kept in proportion
	public static Bitmap Fit(Image image, int size)
	{
		var square = new Bitmap(size, size);
		using var g = Graphics.FromImage(square);
		g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
		double scale = Math.Min((double)size / image.Width, (double)size / image.Height);
		int w = Math.Max(1, (int)(image.Width * scale)), h = Math.Max(1, (int)(image.Height * scale));
		g.DrawImage(image, (size - w) / 2, (size - h) / 2, w, h);
		return square;
	}

	public void Forget(string? name)
	{
		if (name != null && _cache.Remove(name, out var image))
		{
			image?.Dispose();
		}
	}

	public void Dispose()
	{
		foreach (var image in _cache.Values)
		{
			image?.Dispose();
		}
		_cache.Clear();
	}

	private static Image? Read(string? path)
	{
		if (path == null || !File.Exists(path))
		{
			return null;
		}
		try
		{
			return Read(File.ReadAllBytes(path));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return null;
		}
	}

	private static Image? Read(byte[] bytes)
	{
		try
		{
			using var stream = new MemoryStream(bytes);
			using var loaded = Image.FromStream(stream);
			return new Bitmap(loaded);
		}
		catch (ArgumentException)
		{
			return null;
		}
	}

	private static Bitmap Swatch(Color color)
	{
		var swatch = new Bitmap(32, 32);
		using var g = Graphics.FromImage(swatch);
		using var fill = new SolidBrush(color);
		g.FillRectangle(fill, 2, 2, 28, 28);
		g.DrawRectangle(Pens.Gray, 2, 2, 27, 27);
		return swatch;
	}
}
