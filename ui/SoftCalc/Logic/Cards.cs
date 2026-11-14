// Cards.cs - Card decks for the Random window: cards, decks and their text forms (design/random_plan.md R6)
//
// Owner 2026-11-14: the main job of a draw is a pick from a pile of
// cards. A deck holds cards with a name, a weight and an optional
// picture. Cards are typed in a box (split by lines, commas or spaces),
// edited as "name | weight" lines, or read from and written to CSV
// (name,weight,image). Ready-made decks (ReadyDecks.cs) carry an English
// and a Thai name for each card.

using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace SoftCalc.Logic;

internal sealed class Card
{
	public string Name { get; set; } = "";
	public string? Thai { get; set; }    // ready-made decks: the Thai name; Name (English) stays the card's key
	public int Weight { get; set; } = 1;
	public string? Image { get; set; }    // a picture's file name in the cards folder
	public string? Color { get; set; }    // "#RRGGBB": a swatch (the ready-made colours)
	public bool Off { get; set; }    // starts unticked when the deck is added to a draw (the jokers)

	public string Shown(string language)
	{
		return language == "th" && !string.IsNullOrEmpty(Thai) ? Thai : Name;
	}

	public Card Copy()
	{
		return (Card)MemberwiseClone();
	}
}

internal sealed class Deck
{
	public string Id { get; set; } = "";
	public string Name { get; set; } = "";
	public string? Thai { get; set; }
	public List<Card> Cards { get; set; } = new();

	[JsonIgnore]
	public bool Ready => Id.StartsWith(ReadyDecks.Prefix, StringComparison.Ordinal);

	[JsonIgnore]
	public bool Alphabetical { get; init; }    // ready-made lists shown in the order of the language's alphabet

	public string Shown(string language)
	{
		return language == "th" && !string.IsNullOrEmpty(Thai) ? Thai : Name;
	}

	// The cards in the order they are listed for this language
	public IEnumerable<Card> Listed(string language)
	{
		if (!Alphabetical)
		{
			return Cards;
		}
		var order = StringComparer.Create(CultureInfo.GetCultureInfo(language == "th" ? "th-TH" : "en-US"), ignoreCase: true);
		return Cards.OrderBy(c => c.Shown(language), order);
	}

	// A deck of the user's own from this one: names in the language shown
	public Deck CopyAs(string id, string name, string language)
	{
		var cards = Listed(language).Select(c =>
		{
			var copy = c.Copy();
			copy.Name = c.Shown(language);
			copy.Thai = null;
			return copy;
		});
		return new Deck { Id = id, Name = name, Cards = cards.ToList() };
	}
}

internal static class CardText
{
	public const int MaxCards = 5000;
	public const int MaxName = 200;
	public const int MaxWeight = 1000;
	public static readonly string[] Splits = { "auto", "lines", "comma", "space" };
	public static readonly string[] SplitNames = { "Auto", "Lines", "Commas", "Spaces" };

	// Names typed in a box; Auto (owner 2026-11-14): lines if there is a line
	// break, else commas if there is a comma, else spaces
	public static List<string> Split(string text, string mode)
	{
		if (mode == "auto")
		{
			mode = text.Contains('\n') ? "lines" : text.Contains(',') ? "comma" : "space";
		}
		char[] separators = mode switch
		{
			"lines" => new[] { '\r', '\n' },
			"comma" => new[] { ',', '\r', '\n' },
			_ => new[] { ' ', '\t', '\r', '\n' },
		};
		return text.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(n => n.Length > 0).ToList();
	}

	// One card per line, a weight after the last bar: "Fried rice | 2"
	public static (List<Card>? Cards, string? Error) ParseLines(string text)
	{
		var cards = new List<Card>();
		int number = 0;
		foreach (string raw in text.Split('\n'))
		{
			number++;
			string line = raw.Trim();
			if (line.Length == 0)
			{
				continue;
			}
			string name = line;
			int weight = 1;
			int bar = line.LastIndexOf('|');
			if (bar >= 0)
			{
				name = line[..bar].Trim();
				var (value, error) = Weight(line[(bar + 1)..]);
				if (error != null)
				{
					return (null, $"Line {number}: {error}");
				}
				weight = value;
			}
			var (card, nameError) = Make(name, weight);
			if (card == null)
			{
				return (null, $"Line {number}: {nameError}");
			}
			cards.Add(card);
		}
		return Limit(cards);
	}

	public static string FormatLines(IEnumerable<Card> cards)
	{
		return string.Join(Environment.NewLine, cards.Select(c => c.Weight == 1 ? c.Name : $"{c.Name} | {c.Weight}"));
	}

	// CSV rows: name, weight (optional), image path (optional); a first row
	// starting with "name" is a header
	public static (List<(Card Card, string Image)>? Rows, string? Error) ParseCsv(string text)
	{
		var rows = new List<(Card, string)>();
		var records = CsvRecords(text).ToList();
		for (int i = 0; i < records.Count; i++)
		{
			var fields = records[i];
			if (fields.All(f => f.Trim().Length == 0))
			{
				continue;
			}
			if (i == 0 && fields[0].Trim().Equals("name", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			int weight = 1;
			if (fields.Count > 1 && fields[1].Trim().Length > 0)
			{
				var (value, error) = Weight(fields[1]);
				if (error != null)
				{
					return (null, $"Row {i + 1}: {error}");
				}
				weight = value;
			}
			var (card, nameError) = Make(fields[0].Trim(), weight);
			if (card == null)
			{
				return (null, $"Row {i + 1}: {nameError}");
			}
			rows.Add((card, fields.Count > 2 ? fields[2].Trim() : ""));
		}
		return rows.Count > MaxCards ? (null, $"At most {MaxCards} cards in a deck.") : (rows, null);
	}

	public static string ToCsv(IEnumerable<Card> cards, string language, Func<Card, string> imagePath)
	{
		var text = new StringBuilder("name,weight,image\r\n");
		foreach (var card in cards)
		{
			text.Append(Quote(card.Shown(language))).Append(',')
				.Append(card.Weight.ToString(CultureInfo.InvariantCulture)).Append(',')
				.Append(Quote(imagePath(card))).Append("\r\n");
		}
		return text.ToString();
	}

	// A text file's bytes: UTF-8 (with or without a BOM), UTF-16 with a BOM,
	// else the Thai Windows code page that Excel uses for "CSV" on a Thai system
	public static string ReadText(byte[] bytes)
	{
		if (bytes.Length >= 2 && (bytes[0], bytes[1]) is (0xFF, 0xFE) or (0xFE, 0xFF))
		{
			return new StreamReader(new MemoryStream(bytes), detectEncodingFromByteOrderMarks: true).ReadToEnd();
		}
		try
		{
			string text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
			return text.TrimStart('﻿');
		}
		catch (DecoderFallbackException)
		{
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			return Encoding.GetEncoding(874).GetString(bytes);
		}
	}

	// Adds cards whose name is not in the deck yet; returns how many were left out
	public static int AddNew(List<Card> into, IEnumerable<Card> add)
	{
		var names = new HashSet<string>(into.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
		int skipped = 0;
		foreach (var card in add)
		{
			if (into.Count >= MaxCards || !names.Add(card.Name))
			{
				skipped++;
				continue;
			}
			into.Add(card);
		}
		return skipped;
	}

	public static (Card? Card, string? Error) Make(string name, int weight)
	{
		if (name.Length == 0)
		{
			return (null, "a card needs a name.");
		}
		if (name.Length > MaxName)
		{
			return (null, $"a name may have at most {MaxName} characters.");
		}
		return (new Card { Name = name, Weight = weight }, null);
	}

	public static (int Value, string? Error) Weight(string text)
	{
		string t = text.Trim();
		if (t.Length == 0)
		{
			return (1, null);
		}
		if (!int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < 1 || value > MaxWeight)
		{
			return (0, $"the weight '{t}' must be a whole number from 1 to {MaxWeight}.");
		}
		return (value, null);
	}

	private static (List<Card>?, string?) Limit(List<Card> cards)
	{
		return cards.Count > MaxCards ? (null, $"At most {MaxCards} cards in a deck.") : (cards, null);
	}

	private static string Quote(string field)
	{
		return field.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0 ? field : "\"" + field.Replace("\"", "\"\"") + "\"";
	}

	// RFC 4180: fields split by commas, quoted fields may hold commas, quotes ("") and line breaks
	private static IEnumerable<List<string>> CsvRecords(string text)
	{
		var fields = new List<string>();
		var field = new StringBuilder();
		bool quoted = false;
		for (int i = 0; i < text.Length; i++)
		{
			char c = text[i];
			if (quoted)
			{
				if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
				{
					field.Append('"');
					i++;
				}
				else if (c == '"')
				{
					quoted = false;
				}
				else
				{
					field.Append(c);
				}
			}
			else if (c == '"')
			{
				quoted = true;
			}
			else if (c == ',')
			{
				fields.Add(field.ToString());
				field.Clear();
			}
			else if (c == '\n' || c == '\r')
			{
				if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
				{
					i++;
				}
				fields.Add(field.ToString());
				field.Clear();
				yield return fields;
				fields = new List<string>();
			}
			else
			{
				field.Append(c);
			}
		}
		if (field.Length > 0 || fields.Count > 0)
		{
			fields.Add(field.ToString());
			yield return fields;
		}
	}
}
