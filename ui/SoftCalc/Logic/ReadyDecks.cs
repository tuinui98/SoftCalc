// ReadyDecks.cs - The ready-made card decks, in English and Thai (design/random_plan.md R6)
//
// Owner 2026-11-14: a pack of playing cards (the two jokers start
// unticked), months, days, the provinces of Thailand, countries by
// continent (the UN members plus Palestine, Taiwan and the Vatican),
// twelve colours, the zodiac and the twelve animal years; owner
// 2026-11-15: the 928 districts, all and by province. The lists are
// embedded text files (data/decks/*.txt, "English|Thai" lines, shared with
// the web app). Ready-made
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
		decks.AddRange(Districts());
		return decks;
	}

	// The 928 districts (owner 2026-11-15; Office of the Royal Society's list, see districts.txt):
	// all of them, a name that two provinces share followed by its province, and a deck per province
	private static IEnumerable<Deck> Districts()
	{
		var thaiProvince = Rows("provinces").ToDictionary(r => r[0], r => r[1]);
		var rows = Rows("districts").ToList();
		var shared = rows.GroupBy(r => r[1]).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
		yield return new Deck
		{
			Id = Prefix + "districts", Name = "Districts: all", Thai = "อำเภอ: ทั้งหมด", Alphabetical = true,
			Cards = rows.Select(r => shared.Contains(r[1])
				? new Card { Name = $"{r[1]} ({r[0]})", Thai = $"{r[2]} ({thaiProvince[r[0]]})" }
				: new Card { Name = r[1], Thai = r[2] }).ToList(),
		};
		foreach (var province in rows.GroupBy(r => r[0]).OrderBy(g => g.Key, StringComparer.Ordinal))
		{
			yield return new Deck
			{
				Id = Prefix + "districts-" + province.Key.ToLowerInvariant().Replace(' ', '-'),
				Name = "Districts: " + province.Key, Thai = "อำเภอ: " + thaiProvince[province.Key], Alphabetical = true,
				Cards = province.Select(r => new Card { Name = r[1], Thai = r[2] }).ToList(),
			};
		}
	}

	// A ready-made card's picture is embedded in the exe (data/cards/), named "ready:<set>/<file>.png";
	// a deck of the user's own never names one (CardStore keeps only its own file names), and
	// Copy to my decks stores the picture as the user's own (owner 2026-11-24)
	public const string PicturePrefix = "ready:";

	// The embedded picture's bytes, or null
	public static byte[]? Picture(string name)
	{
		if (!name.StartsWith(PicturePrefix, StringComparison.Ordinal))
		{
			return null;
		}
		using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("cards/" + name[PicturePrefix.Length..]);
		if (stream == null)
		{
			return null;
		}
		using var bytes = new MemoryStream();
		stream.CopyTo(bytes);
		return bytes.ToArray();
	}

	// 52 cards, ace to king in each suit, and two jokers that start unticked; pictures:
	// heratexx's four-colour deck (CC0) and jokers drawn to match (data/cards/playing/README.md)
	private static Deck PlayingCards()
	{
		// the cards' files (data/cards/playing/svg): rank A 2..9 T J Q K, suit s h d c; here, not static
		// fields: the deck is made while the class's static fields are still being set
		string[] rankFiles = { "A", "2", "3", "4", "5", "6", "7", "8", "9", "T", "J", "Q", "K" };
		string[] suitFiles = { "s", "h", "d", "c" };
		var cards = Suits.SelectMany((s, suit) => Ranks.Select((r, rank) => new Card
		{
			Name = r + s, Color = SuitShades[suit][rank % 2], Image = $"{PicturePrefix}playing/{rankFiles[rank]}{suitFiles[suit]}.png",
		})).ToList();
		cards.Add(new Card { Name = "Joker (black)", Thai = "โจ๊กเกอร์ดำ", Off = true, Color = SuitShades[0][0], Image = PicturePrefix + "playing/joker_black.png" });
		cards.Add(new Card { Name = "Joker (red)", Thai = "โจ๊กเกอร์แดง", Off = true, Color = SuitShades[1][0], Image = PicturePrefix + "playing/joker_red.png" });
		return new Deck { Id = Prefix + "playing-cards", Name = "Playing cards", Thai = "ไพ่ 1 สำรับ", Cards = cards };
	}

	// The lines of data/decks/<name>.txt split at '|'; '#' starts a comment line
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
