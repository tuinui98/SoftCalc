// DeckCovers.cs - What a deck's cover shows (owner 2026-11-24, deck covers)
//
// A cover is, in this order: the picture the user chose for the deck
// (Deck.Cover); the first card's picture; up to four of its cards'
// colours as a 2 x 2 mosaic; else the first letter of its name on a
// colour of its own (the same deck always gets the same one). Pictures
// whose file is gone are passed over. Drawing is the UI's (CardPictures).

using System.Globalization;

namespace SoftCalc.Logic;

internal enum CoverKind
{
	Picture,
	Colours,
	Letter,
}

internal sealed record DeckCover(CoverKind Kind, string? Image, IReadOnlyList<string> Colours, string Letter, string Back)
{
	public bool Chosen { get; init; }    // the picture is one the user set, not worked out
}

internal static class DeckCovers
{
	public const int MosaicColours = 4;

	// Letter covers: dark enough for white text, told apart by lightness too
	private static readonly string[] Backs = { "#2F3A4A", "#4A5D78", "#356A9A", "#2E7D6B", "#7A4E2D", "#8E3B46", "#5B4A8A", "#3E6B3A" };

	// exists(name): the picture's file is there to show
	public static DeckCover Of(Deck deck, string language, Func<string, bool> exists)
	{
		if (deck.Cover != null && exists(deck.Cover))
		{
			return new DeckCover(CoverKind.Picture, deck.Cover, Array.Empty<string>(), "", "") { Chosen = true };
		}
		if (deck.Cards.FirstOrDefault(c => c.Image != null && exists(c.Image)) is { } pictured)
		{
			return new DeckCover(CoverKind.Picture, pictured.Image, Array.Empty<string>(), "", "");
		}
		var colours = deck.Cards.Where(c => c.Color != null).Select(c => c.Color!.ToUpperInvariant()).Distinct()
			.Take(MosaicColours).ToList();
		if (colours.Count > 0)
		{
			return new DeckCover(CoverKind.Colours, null, colours, "", "");
		}
		return new DeckCover(CoverKind.Letter, null, Array.Empty<string>(), Initial(deck.Shown(language)), BackFor(deck.Id));
	}

	// The first letter as a reader sees it (a Thai letter with its marks; ไ in ไทย)
	public static string Initial(string name)
	{
		string trimmed = name.Trim();
		if (trimmed.Length == 0)
		{
			return "?";
		}
		return StringInfo.GetNextTextElement(trimmed).ToUpper(CultureInfo.InvariantCulture);
	}

	// The same deck, the same colour: a hash of its id that does not change between runs
	public static string BackFor(string id)
	{
		uint hash = 2166136261;
		foreach (char c in id)
		{
			hash = (hash ^ c) * 16777619;
		}
		return Backs[hash % (uint)Backs.Length];
	}
}
