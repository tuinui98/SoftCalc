// DeckCoversTests.cs - What a deck's cover shows (owner 2026-11-24, deck covers)

using SoftCalc.Logic;

namespace SoftCalc.Tests;

public class DeckCoversTests
{
	private static readonly HashSet<string> Files = new() { "aaaaaaaaaaaaaaaa.png", "bbbbbbbbbbbbbbbb.png" };

	private static DeckCover Of(Deck deck) => DeckCovers.Of(deck, "en", Files.Contains);

	[Fact]
	public void ADecksOwnPictureComesFirst()
	{
		var deck = new Deck { Id = "d1", Name = "Food", Cover = "bbbbbbbbbbbbbbbb.png", Cards = { new Card { Name = "Rice", Image = "aaaaaaaaaaaaaaaa.png" } } };
		var cover = Of(deck);
		Assert.Equal((CoverKind.Picture, "bbbbbbbbbbbbbbbb.png", true), (cover.Kind, cover.Image, cover.Chosen));
	}

	[Fact]
	public void ElseTheFirstCardWithAPictureWhoseFileIsThere()
	{
		var deck = new Deck
		{
			Id = "d1", Name = "Food", Cover = "cccccccccccccccc.png",    // gone: passed over
			Cards = { new Card { Name = "Soup", Image = "dddddddddddddddd.png" }, new Card { Name = "Rice", Image = "aaaaaaaaaaaaaaaa.png" } },
		};
		var cover = Of(deck);
		Assert.Equal((CoverKind.Picture, "aaaaaaaaaaaaaaaa.png", false), (cover.Kind, cover.Image, cover.Chosen));
	}

	[Fact]
	public void ElseUpToFourColoursOfItsCards()
	{
		var deck = new Deck { Id = "d1", Name = "Gym" };
		foreach (string colour in new[] { "#ff0000", "#00FF00", "#FF0000", "#0000ff", "#111111", "#222222" })
		{
			deck.Cards.Add(new Card { Name = colour + deck.Cards.Count, Color = colour });
		}
		var cover = Of(deck);
		Assert.Equal(CoverKind.Colours, cover.Kind);
		Assert.Equal(new[] { "#FF0000", "#00FF00", "#0000FF", "#111111" }, cover.Colours);
	}

	[Fact]
	public void ElseItsFirstLetterOnAColourOfItsOwn()
	{
		var deck = new Deck { Id = "abc123", Name = "chores", Cards = { new Card { Name = "Dishes" } } };
		var cover = Of(deck);
		Assert.Equal((CoverKind.Letter, "C"), (cover.Kind, cover.Letter));
		Assert.Equal(cover.Back, Of(new Deck { Id = "abc123", Name = "Other" }).Back);    // the same deck, the same colour
		Assert.Equal("ไ", DeckCovers.Initial("ไทย"));
		Assert.Equal("กิ", DeckCovers.Initial("กิน"));    // a Thai letter with its vowel mark
		Assert.Equal("?", DeckCovers.Initial("  "));
	}

	[Fact]
	public void ReadyMadeDecksGetACover()
	{
		foreach (var deck in ReadyDecks.All)
		{
			var cover = DeckCovers.Of(deck, "th", _ => false);
			Assert.True(cover.Kind is CoverKind.Colours or CoverKind.Letter, deck.Name);
		}
		var cards = ReadyDecks.All.First(d => d.Name == "Playing cards");
		Assert.Equal(CoverKind.Colours, DeckCovers.Of(cards, "en", _ => false).Kind);    // the four suit colours
	}
}
