// CardDraws.cs - Draw tabs: steps that each draw one card from a pile (design/random_plan.md R7)
//
// Owner 2026-11-14: a draw tab has steps; a step pools any decks with
// extra cards typed for this draw only (not saved as a deck), and any
// card can be unticked. Weights are used or not; No repeat works like a
// real pile: a drawn card stays out until Shuffle. One Draw gives one
// line, a card from each step in turn. History (with the time) and the
// count of each card are kept; Clear draw empties the results and
// History but keeps the counts.

using System.Globalization;

namespace SoftCalc.Logic;

internal sealed class DrawStep
{
	public List<string> Decks { get; set; } = new();    // deck ids, the ready-made ones with ReadyDecks.Prefix
	public string Extra { get; set; } = "";    // cards for this draw only, split like the Add box (Auto)
	public List<string> Off { get; set; } = new();    // keys of unticked cards
	public bool Weights { get; set; } = true;
	public bool NoRepeat { get; set; }
	public List<string> Drawn { get; set; } = new();    // No repeat: keys out of the pile until Shuffle
	public Dictionary<string, int> Counts { get; set; } = new();    // key -> times drawn (Stats)
}

internal sealed class DrawRecord
{
	public DateTime Time { get; set; }
	public string Text { get; set; } = "";
}

internal sealed class DrawSetup
{
	public string Id { get; set; } = "";
	public string Name { get; set; } = "";
	public string Theme { get; set; } = "slot";    // list, slot, dart; new tabs start on slot (owner 2026-11-14)
	public int Count { get; set; } = 1;
	public bool Effects { get; set; } = true;
	public List<DrawStep> Steps { get; set; } = new();
	public List<DrawRecord> History { get; set; } = new();    // newest first
	public int Draws { get; set; }    // lines drawn since the counts were cleared (Stats)
}

// A card in a step's pool: where it comes from and its key there
internal sealed record PoolCard(string Key, Card Card, string Source);

internal static class CardDraws
{
	public const string ExtraSource = "extra";
	public const int MaxSteps = 6;
	public const int MaxCount = 1000;
	public const int MaxHistory = 1000;
	public const string Joiner = " · ";
	public static readonly string[] Themes = { "list", "slot", "dart" };
	public static readonly string[] ThemeNames = { "List", "Slot machine", "Dartboard" };

	public static string Key(string source, Card card)
	{
		return source + "/" + card.Name;
	}

	// Every card of the step's decks and its extra cards, ticked or not; a
	// deck that is gone is skipped, a name twice in the extras counts once
	public static List<PoolCard> Pool(DrawStep step, Func<string, Deck?> find)
	{
		var pool = new List<PoolCard>();
		foreach (string id in step.Decks.Distinct())
		{
			if (find(id) is { } deck)
			{
				pool.AddRange(deck.Cards.Select(c => new PoolCard(Key(id, c), c, id)));
			}
		}
		var extras = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string name in CardText.Split(step.Extra, "auto"))
		{
			if (name.Length <= CardText.MaxName && extras.Add(name))
			{
				var card = new Card { Name = name };
				pool.Add(new PoolCard(Key(ExtraSource, card), card, ExtraSource));
			}
		}
		return pool;
	}

	// The cards a draw can give now: ticked, and for No repeat not drawn yet
	public static List<PoolCard> Live(DrawStep step, List<PoolCard> pool)
	{
		var off = new HashSet<string>(step.Off);
		var drawn = step.NoRepeat ? new HashSet<string>(step.Drawn) : new HashSet<string>();
		return pool.Where(p => !off.Contains(p.Key) && !drawn.Contains(p.Key)).ToList();
	}

	public static long Total(DrawStep step, List<PoolCard> live)
	{
		return live.Sum(p => step.Weights ? (long)p.Card.Weight : 1L);
	}

	// The card at a point from 0 to Total - 1, each card owning as many points as its weight
	public static PoolCard At(DrawStep step, List<PoolCard> live, long point)
	{
		foreach (var p in live)
		{
			point -= step.Weights ? p.Card.Weight : 1;
			if (point < 0)
			{
				return p;
			}
		}
		return live[^1];
	}

	// count lines, a card from each step; below(n) gives 0 to n - 1. Changes the
	// steps (Drawn, Counts) and the history. Stops early, with the reason, when
	// a pile runs out.
	public static (List<List<PoolCard>> Lines, string? Error) Draw(DrawSetup setup, Func<string, Deck?> find, int count,
		Func<long, long> below, string language, DateTime now)
	{
		var lines = new List<List<PoolCard>>();
		var pools = setup.Steps.Select(s => Pool(s, find)).ToList();
		if (setup.Steps.Count == 0)
		{
			return (lines, "No steps: press Edit tab to choose the cards.");
		}
		for (int n = 0; n < count; n++)
		{
			var line = new List<PoolCard>();
			for (int i = 0; i < setup.Steps.Count; i++)
			{
				var step = setup.Steps[i];
				var live = Live(step, pools[i]);
				if (live.Count == 0)
				{
					string why = pools[i].Count == 0 ? "has no cards" : step.NoRepeat && Live(new DrawStep { Off = step.Off }, pools[i]).Count > 0
						? "has drawn every card: press Shuffle" : "has every card unticked";
					return (lines, $"Step {i + 1} {why}.");
				}
				var card = At(step, live, below(Total(step, live)));
				if (step.NoRepeat)
				{
					step.Drawn.Add(card.Key);
				}
				line.Add(card);
			}
			foreach (var (step, card) in setup.Steps.Zip(line))
			{
				step.Counts[card.Key] = step.Counts.GetValueOrDefault(card.Key) + 1;
			}
			setup.Draws++;
			setup.History.Insert(0, new DrawRecord { Time = now, Text = Text(line, language) });
			lines.Add(line);
		}
		if (setup.History.Count > MaxHistory)
		{
			setup.History.RemoveRange(MaxHistory, setup.History.Count - MaxHistory);
		}
		return (lines, null);
	}

	public static string Text(IEnumerable<PoolCard> line, string language)
	{
		return string.Join(Joiner, line.Select(p => p.Card.Shown(language)));
	}

	// Stats rows of a step: shown name, times drawn, its share and the share its weight gives
	public static List<(string Name, int Times, double Share, double Expected)> Stats(DrawStep step, List<PoolCard> pool,
		string language)
	{
		var ticked = pool.Where(p => !step.Off.Contains(p.Key)).ToList();
		long total = Math.Max(1, ticked.Sum(p => step.Weights ? (long)p.Card.Weight : 1L));
		int drawn = Math.Max(1, step.Counts.Values.Sum());
		return ticked.Select(p => (p.Card.Shown(language), step.Counts.GetValueOrDefault(p.Key),
				100.0 * step.Counts.GetValueOrDefault(p.Key) / drawn, 100.0 * (step.Weights ? p.Card.Weight : 1) / total))
			.OrderByDescending(r => r.Item2).ToList();
	}

	public static string Time(DateTime time)
	{
		return time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
	}
}
