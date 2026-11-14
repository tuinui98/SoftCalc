// CardPictures.cs - Card pictures for lists and draws: the stored picture, or a colour swatch (design/random_plan.md R6)
//
// Pictures are read from the cards folder once and kept while the
// Random window is open; the file is read into memory so it is never
// locked. A card without a picture but with a colour gets a swatch.

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
		if (card.Image != null)
		{
			if (!_cache.TryGetValue(card.Image, out var image))
			{
				image = Read(_store.ImagePath(card.Image));
				_cache[card.Image] = image;
			}
			if (image != null)
			{
				return image;
			}
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
				list.Images.Add(key, Fit(For(card)!, size));
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
			using var stream = new MemoryStream(File.ReadAllBytes(path));
			using var loaded = Image.FromStream(stream);
			return new Bitmap(loaded);
		}
		catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
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
