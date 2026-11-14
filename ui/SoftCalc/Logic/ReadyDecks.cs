// ReadyDecks.cs - The ready-made card decks, in English and Thai (design/random_plan.md R6)
//
// Owner 2026-11-14: a pack of playing cards (the two jokers start
// unticked), months, days, the provinces of Thailand, countries by
// continent (the UN members plus Palestine, Taiwan and the Vatican),
// twelve colours, the zodiac and the twelve animal years. The lists are
// embedded text files (Decks/*.txt, "English|Thai" lines). Ready-made
// decks cannot be edited; Copy to my decks makes one that can.

using System.Reflection;

namespace SoftCalc.Logic;

internal static class ReadyDecks
{
	public const string Prefix = "ready:";
	public static readonly string[] Suits = { "♠", "♥", "♦", "♣" };
	public static readonly string[] Ranks = { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };
	// Four-colour pack (owner 2026-11-14): spades black, hearts red, diamonds blue, clubs green,
	// two shades in turn so cards side by side on a board stay apart
	public static readonly string[][] SuitShades =
	{
		new[] { "#202020", "#4A4A4A" }, new[] { "#C62828", "#E05353" }, new[] { "#1E5AA8", "#4A86D0" }, new[] { "#2E7D32", "#55A35A" },
	};

	private static readonly (string File, string Id, string English, string Thai, bool Alphabetical)[] Lists =
	{
		("months", "months", "Months", "เดือน", false),
		("days", "days", "Days of the week", "วันในสัปดาห์", false),
		("provinces", "provinces", "Provinces of Thailand", "จังหวัดในประเทศไทย", true),
		("colors", "colors", "Colours", "สี", false),
		("zodiac", "zodiac", "Zodiac signs", "ราศี", false),
		("chinese-zodiac", "animal-years", "Animal years", "ปีนักษัตร", false),
	};

	private static readonly (string English, string Thai)[] Continents =
	{
		("Africa", "แอฟริกา"), ("Asia", "เอเชีย"), ("Europe", "ยุโรป"),
		("North America", "อเมริกาเหนือ"), ("South America", "อเมริกาใต้"), ("Oceania", "โอเชียเนีย"),
	};

	public static IReadOnlyList<Deck> All { get; } = Build();

	public static Deck? Find(string id)
	{
		return All.FirstOrDefault(d => d.Id == id);
	}

	private static List<Deck> Build()
	{
		var decks = new List<Deck> { PlayingCards() };
		foreach (var (file, id, english, thai, alphabetical) in Lists)
		{
			var cards = Rows(file).Select(r => new Card { Name = r[0], Thai = r[1], Color = r.Length > 2 ? r[2] : null }).ToList();
			decks.Add(new Deck { Id = Prefix + id, Name = english, Thai = thai, Cards = cards, Alphabetical = alphabetical });
		}
		var countries = Rows("countries").ToList();
		decks.Add(new Deck
		{
			Id = Prefix + "countries", Name = "Countries: all", Thai = "ประเทศ: ทั้งหมด", Alphabetical = true,
			Cards = countries.Select(r => new Card { Name = r[1], Thai = r[2] }).ToList(),
		});
		foreach (var (english, thai) in Continents)
		{
			decks.Add(new Deck
			{
				Id = Prefix + "countries-" + english.ToLowerInvariant().Replace(' ', '-'),
				Name = "Countries: " + english, Thai = "ประเทศ: " + thai, Alphabetical = true,
				Cards = countries.Where(r => r[0] == english).Select(r => new Card { Name = r[1], Thai = r[2] }).ToList(),
			});
		}
		return decks;
	}

	// 52 cards, ace to king in each suit, and two jokers that start unticked
	private static Deck PlayingCards()
	{
		var cards = Suits.SelectMany((s, suit) => Ranks.Select((r, rank) => new Card { Name = r + s, Color = SuitShades[suit][rank % 2] }))
			.ToList();
		cards.Add(new Card { Name = "Joker (black)", Thai = "โจ๊กเกอร์ดำ", Off = true, Color = SuitShades[0][0] });
		cards.Add(new Card { Name = "Joker (red)", Thai = "โจ๊กเกอร์แดง", Off = true, Color = SuitShades[1][0] });
		return new Deck { Id = Prefix + "playing-cards", Name = "Playing cards", Thai = "ไพ่ 1 สำรับ", Cards = cards };
	}

	// The lines of Decks/<name>.txt split at '|'; '#' starts a comment line
	private static IEnumerable<string[]> Rows(string name)
	{
		using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"decks/{name}.txt")
			?? throw new InvalidOperationException($"missing deck {name}");
		using var reader = new StreamReader(stream);
		var rows = new List<string[]>();
		while (reader.ReadLine() is { } line)
		{
			if (line.Length > 0 && !line.StartsWith('#'))
			{
				rows.Add(line.Split('|'));
			}
		}
		return rows;
	}
}
