// CardDraws.cs - Draw tabs: steps that each draw one card from a pile (design/random_plan.md R7)
//
// Owner 2026-11-14: a draw tab has steps; a step pools any decks with
// extra cards typed for this draw only (not saved as a deck), and any
// card can be unticked. Weights are used or not; No repeat works like a
// real pile: a drawn card stays out until Shuffle. One Draw gives one
// line, a card from each step in turn. History (with the time) and the
// count of each card are kept; Clear draw empties the results and
// History but keeps the counts.
//
// Owner 2026-11-14 (conditions): an extra card takes a weight after "|"
// and a condition after ">>" ("Rice soup>>stop|2"); a deck card gets its
// condition in Edit tab. A condition goes on to the next step, stops the
// line, skips ahead to a step, or draws from a deck first ("Gym", "Gym*2",
// "Gym,stop"). A deck drawn this way is one level only: its own cards'
// conditions are not used.

using System.Globalization;
using System.Text.RegularExpressions;

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
	public Dictionary<string, string> Then { get; set; } = new();    // deck card key -> its condition (the ">>" text)
	public Dictionary<string, List<string>> SubDrawn { get; set; } = new();    // deck id -> keys drawn through a condition (No repeat)
	public Dictionary<string, Dictionary<string, int>> SubCounts { get; set; } = new();    // deck id -> key -> times drawn
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
	public string Theme { get; set; } = "slot";    // list, slot, dart, spin; new tabs start on slot (owner 2026-11-14)
	public int Count { get; set; } = 1;
	public bool Effects { get; set; } = true;
	public List<DrawStep> Steps { get; set; } = new();
	public List<DrawRecord> History { get; set; } = new();    // newest first
	public int Draws { get; set; }    // lines drawn since the counts were cleared (Stats)
}

// A card in a step's pool: where it comes from, its key there and its condition
internal sealed record PoolCard(string Key, Card Card, string Source, string Then = "");

// A card in a drawn line: its step, the deck id when a condition drew it (else null),
// and for the last line the cards it was drawn from (for the theme)
internal sealed record DrawPick(int Step, string? Sub, PoolCard Card, List<PoolCard>? Live, bool Weights);

internal enum ThenFlow
{
	Next,
	Stop,
	Step,
}

// A condition: draw Count cards from Deck (if any), then go on as Flow says
internal sealed record CardThen(string? Deck, int Count, ThenFlow Flow, int Step)
{
	public const int MaxCount = 10;
	public static readonly CardThen Next = new(null, 1, ThenFlow.Next, 0);
	private static readonly Regex FlowWord = new(@"^(next|stop|step\s*(\d+))$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
	private static readonly Regex Times = new(@"\*\s*(\d+)$", RegexOptions.CultureInvariant);

	public static bool IsFlow(string text)
	{
		return FlowWord.IsMatch(text.Trim());
	}

	// "", "next", "stop", "step 3", "Gym", "Gym*2", "Gym,stop", "deck:stop" (a deck named stop)
	public static (CardThen? Then, string? Error) Parse(string text)
	{
		string t = text.Trim();
		if (t.Length == 0)
		{
			return (Next, null);
		}
		var flow = (ThenFlow.Next, 0);
		int comma = t.LastIndexOf(',');
		if (FlowWord.Match(t) is { Success: true } whole)
		{
			return ReadFlow(whole) is var (f, s) && f == ThenFlow.Step && s < 1 ? (null, $"'{t}': steps start at 1.") : (new CardThen(null, 1, f, s), null);
		}
		if (comma >= 0 && FlowWord.Match(t[(comma + 1)..].Trim()) is { Success: true } tail)
		{
			flow = ReadFlow(tail);
			t = t[..comma].Trim();
		}
		if (flow is (ThenFlow.Step, < 1))
		{
			return (null, $"'{text.Trim()}': steps start at 1.");
		}
		int count = 1;
		if (Times.Match(t) is { Success: true } times)
		{
			if (!int.TryParse(times.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out count) || count < 1 || count > MaxCount)
			{
				return (null, $"'{text.Trim()}': draw 1 to {MaxCount} cards from a deck.");
			}
			t = t[..times.Index].Trim();
		}
		if (t.StartsWith("deck:", StringComparison.OrdinalIgnoreCase))
		{
			t = t[5..].Trim();
		}
		if (t.Length == 0)
		{
			return (null, $"'{text.Trim()}': name a deck to draw from.");
		}
		return (new CardThen(t, count, flow.Item1, flow.Item2), null);
	}

	private static (ThenFlow, int) ReadFlow(Match word)
	{
		string w = word.Groups[1].Value.ToLowerInvariant();
		return w == "next" ? (ThenFlow.Next, 0) : w == "stop" ? (ThenFlow.Stop, 0)
			: (ThenFlow.Step, int.TryParse(word.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int s) ? s : 0);
	}

	// The text form, as Parse reads it
	public string Format()
	{
		string flow = Flow switch
		{
			ThenFlow.Stop => "stop",
			ThenFlow.Step => $"step {Step}",
			_ => "",
		};
		if (Deck == null)
		{
			return flow;
		}
		string deck = (IsFlow(Deck) || Deck.StartsWith("deck:", StringComparison.OrdinalIgnoreCase) ? "deck:" : "") + Deck
			+ (Count > 1 ? $"*{Count}" : "");
		return flow.Length == 0 ? deck : deck + "," + flow;
	}
}

internal static class CardDraws
{
	public const string ExtraSource = "extra";
	public const int MaxSteps = 6;
	public const int MaxCount = 1000;
	public const int MaxHistory = 1000;
	public const string Joiner = " · ";
	public const string SubJoiner = " → ";
	public const string SubCards = " + ";
	public const int StageLines = 5;    // the slot machine plays at most this many lines
	public static readonly string[] Themes = { "list", "slot", "dart", "spin" };
	public static readonly string[] ThemeNames = { "List", "Slot machine", "Dartboard", "Spin wheel" };

	public static string Key(string source, Card card)
	{
		return source + "/" + card.Name;
	}

	//==================================================================
	//  Extra cards: "name", "name|2", "name>>stop", "name>>stop|2", "name|2>>stop"
	//==================================================================
	public static List<string> ExtraEntries(string extra, string mode = "auto")
	{
		var entries = new List<string>();
		foreach (string piece in CardText.Split(extra, mode))
		{
			// "Gym>>Gym,stop" split by commas, and "x>>step 3" split by spaces, join up again
			if (entries.Count > 0 && entries[^1].Contains(">>"))
			{
				string bare = piece.Split('|')[0].Trim();
				if (CardThen.IsFlow(bare) && !entries[^1][(entries[^1].IndexOf(">>") + 2)..].Contains(','))
				{
					entries[^1] = Glue(entries[^1], "," + piece);
					continue;
				}
				if (piece.Length > 0 && char.IsAsciiDigit(piece[0]) && Regex.IsMatch(entries[^1], @"(>>|,)\s*step$", RegexOptions.IgnoreCase))
				{
					entries[^1] = Glue(entries[^1], " " + piece);
					continue;
				}
			}
			entries.Add(piece);
		}
		return entries;
	}

	// Joins a piece to an entry, keeping a weight on the entry ("a|2>>b" + ",stop") before the flow
	private static string Glue(string entry, string piece)
	{
		int arrows = entry.IndexOf(">>", StringComparison.Ordinal), bar = entry.LastIndexOf('|');
		return bar > arrows ? entry[..bar] + piece + entry[bar..] : entry + piece;
	}

	public static (string Name, int Weight, string Then, string? Error) ParseExtra(string entry)
	{
		string left = entry, then = "";
		int arrows = entry.IndexOf(">>", StringComparison.Ordinal);
		if (arrows >= 0)
		{
			left = entry[..arrows];
			then = entry[(arrows + 2)..];
		}
		int weight = 1;
		string? error = null;
		int bar = left.LastIndexOf('|');
		string weightText = "";
		if (bar >= 0)
		{
			weightText = left[(bar + 1)..];
			left = left[..bar];
		}
		else if (then.LastIndexOf('|') is int thenBar and >= 0)
		{
			weightText = then[(thenBar + 1)..];
			then = then[..thenBar];
		}
		if (weightText.Length > 0 || bar >= 0)
		{
			(weight, error) = CardText.Weight(weightText);
			weight = Math.Max(1, weight);
		}
		return (left.Trim(), weight, then.Trim(), error);
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
				pool.AddRange(deck.Cards.Select(c =>
				{
					string key = Key(id, c);
					return new PoolCard(key, c, id, step.Then.GetValueOrDefault(key, ""));
				}));
			}
		}
		var extras = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string entry in ExtraEntries(step.Extra))
		{
			var (name, weight, then, _) = ParseExtra(entry);
			if (name.Length > 0 && name.Length <= CardText.MaxName && extras.Add(name))
			{
				var card = new Card { Name = name, Weight = weight };
				pool.Add(new PoolCard(Key(ExtraSource, card), card, ExtraSource, then));
			}
		}
		return pool;
	}

	// The first mistake in a step's extra cards and conditions, or null
	public static string? Check(List<DrawStep> steps, int index, Func<string, Deck?> find, Func<string, Deck?> named, string language)
	{
		var step = steps[index];
		foreach (string entry in ExtraEntries(step.Extra))
		{
			var (name, _, _, error) = ParseExtra(entry);
			if (error != null)
			{
				return $"Step {index + 1}, extra '{entry}': {error}";
			}
			if (name.Length == 0)
			{
				return $"Step {index + 1}, extra '{entry}': a card needs a name.";
			}
		}
		foreach (var p in Pool(step, find).Where(p => p.Then.Length > 0))
		{
			string where = $"Step {index + 1}, card '{p.Card.Shown(language)}'";
			var (then, error) = CardThen.Parse(p.Then);
			if (then == null)
			{
				return $"{where}: {error}";
			}
			if (then.Deck != null && named(then.Deck) == null)
			{
				return $"{where}: no deck named '{then.Deck}'.";
			}
			if (then.Flow == ThenFlow.Step && (then.Step <= index + 1 || then.Step > steps.Count))
			{
				return $"{where}: 'step {then.Step}' must be a later step, {index + 2} to {steps.Count}.";
			}
		}
		return null;
	}

	// The cards a draw can give now: ticked, and for No repeat not drawn yet
	public static List<PoolCard> Live(DrawStep step, List<PoolCard> pool)
	{
		var off = new HashSet<string>(step.Off);
		var drawn = step.NoRepeat ? new HashSet<string>(step.Drawn) : new HashSet<string>();
		return pool.Where(p => !off.Contains(p.Key) && !drawn.Contains(p.Key)).ToList();
	}

	// A deck drawn through a condition: its cards that start ticked (no jokers), and for No repeat not drawn yet
	public static List<PoolCard> SubPool(Deck deck)
	{
		return deck.Cards.Where(c => !c.Off).Select(c => new PoolCard(Key(deck.Id, c), c, deck.Id)).ToList();
	}

	public static List<PoolCard> SubLive(DrawStep step, Deck deck)
	{
		var drawn = step.NoRepeat && step.SubDrawn.TryGetValue(deck.Id, out var keys) ? new HashSet<string>(keys) : new HashSet<string>();
		return SubPool(deck).Where(p => !drawn.Contains(p.Key)).ToList();
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

	// The most picks one line can need: a card per step and the cards its conditions draw
	public static int MostPicks(DrawSetup setup, Func<string, Deck?> find)
	{
		return setup.Steps.Sum(s => 1 + Pool(s, find).Select(p => CardThen.Parse(p.Then).Then is { Deck: not null } t ? t.Count : 0)
			.DefaultIfEmpty(0).Max());
	}

	// count lines, a card from each step; below(n) gives 0 to n - 1. Changes the
	// steps (Drawn, Counts) and the history. Stops early, with the reason, when
	// a pile runs out. named finds a deck by its name, for the conditions.
	public static (List<List<DrawPick>> Lines, string? Error) Draw(DrawSetup setup, Func<string, Deck?> find, int count,
		Func<long, long> below, string language, DateTime now, Func<string, Deck?>? named = null)
	{
		var lines = new List<List<DrawPick>>();
		var pools = setup.Steps.Select(s => Pool(s, find)).ToList();
		if (setup.Steps.Count == 0)
		{
			return (lines, "No steps: press Edit tab to choose the cards.");
		}
		for (int n = 0; n < count; n++)
		{
			bool last = n >= count - StageLines;    // keeps the cards drawn from, for the theme
			var line = new List<DrawPick>();
			for (int i = 0; i < setup.Steps.Count;)
			{
				//==================================================================
				//  BLOCK 1 — Step card : one card from the step's pile
				//==================================================================
				var step = setup.Steps[i];
				var live = Live(step, pools[i]);
				if (live.Count == 0)
				{
					string why = pools[i].Count == 0 ? "has no cards" : step.NoRepeat && Live(new DrawStep { Off = step.Off }, pools[i]).Count > 0
						? "has drawn every card: press Shuffle" : "has every card unticked";
					return (Finish(setup, lines), $"Step {i + 1} {why}.");
				}
				var card = At(step, live, below(Total(step, live)));
				if (step.NoRepeat)
				{
					step.Drawn.Add(card.Key);
				}
				line.Add(new DrawPick(i, null, card, last ? live : null, step.Weights));
				var (then, error) = CardThen.Parse(card.Then);
				if (then == null)
				{
					return (Finish(setup, lines), $"Step {i + 1}, card '{card.Card.Shown(language)}': {error}");
				}
				// end BLOCK 1

				//==================================================================
				//  BLOCK 2 — Condition deck : cards from the deck it names, all different
				//==================================================================
				if (then.Deck != null)
				{
					if (named?.Invoke(then.Deck) is not { } deck)
					{
						return (Finish(setup, lines), $"Step {i + 1}: no deck named '{then.Deck}'.");
					}
					var subLive = SubLive(step, deck);
					for (int k = 0; k < then.Count; k++)
					{
						if (subLive.Count == 0)
						{
							string why = SubPool(deck).Count == 0 ? "has no cards" : step.NoRepeat ? "has drawn every card: press Shuffle"
								: $"has fewer than {then.Count} cards";
							return (Finish(setup, lines), $"Step {i + 1}{SubJoiner}{deck.Shown(language)} {why}.");
						}
						var sub = At(step, subLive, below(Total(step, subLive)));
						line.Add(new DrawPick(i, deck.Id, sub, last ? subLive.ToList() : null, step.Weights));
						subLive.Remove(sub);
						if (step.NoRepeat)
						{
							step.SubDrawn.TryAdd(deck.Id, new List<string>());
							step.SubDrawn[deck.Id].Add(sub.Key);
						}
					}
				}
				// end BLOCK 2

				//==================================================================
				//  BLOCK 3 — Flow : the next step, a later one, or the end of the line
				//==================================================================
				i = then.Flow switch
				{
					ThenFlow.Stop => setup.Steps.Count,
					ThenFlow.Step when then.Step - 1 > i => then.Step - 1,
					_ => i + 1,
				};
				// end BLOCK 3
			}
			foreach (var pick in line)
			{
				var step = setup.Steps[pick.Step];
				var counts = pick.Sub == null ? step.Counts : step.SubCounts.TryGetValue(pick.Sub, out var c) ? c
					: step.SubCounts[pick.Sub] = new Dictionary<string, int>();
				counts[pick.Card.Key] = counts.GetValueOrDefault(pick.Card.Key) + 1;
			}
			setup.Draws++;
			setup.History.Insert(0, new DrawRecord { Time = now, Text = Text(line, language) });
			lines.Add(line);
		}
		return (Finish(setup, lines), null);
	}

	private static List<List<DrawPick>> Finish(DrawSetup setup, List<List<DrawPick>> lines)
	{
		if (setup.History.Count > MaxHistory)
		{
			setup.History.RemoveRange(MaxHistory, setup.History.Count - MaxHistory);
		}
		return lines;
	}

	// "Gym → Yoga + Treadmill · 1 hour"
	public static string Text(IEnumerable<DrawPick> line, string language)
	{
		var parts = new List<string>();
		var subs = new List<string>();
		foreach (var pick in line)
		{
			if (pick.Sub == null)
			{
				Flush(parts, subs);
				parts.Add(pick.Card.Card.Shown(language));
			}
			else
			{
				subs.Add(pick.Card.Card.Shown(language));
			}
		}
		Flush(parts, subs);
		return string.Join(Joiner, parts);
	}

	private static void Flush(List<string> parts, List<string> subs)
	{
		if (subs.Count > 0 && parts.Count > 0)
		{
			parts[^1] += SubJoiner + string.Join(SubCards, subs);
		}
		subs.Clear();
	}

	public static string Text(IEnumerable<PoolCard> line, string language)
	{
		return string.Join(Joiner, line.Select(p => p.Card.Shown(language)));
	}

	// Stats rows of a step: shown name, times drawn, its share and the share its weight gives
	public static List<(string Name, int Times, double Share, double Expected)> Stats(DrawStep step, List<PoolCard> pool,
		string language)
	{
		return Rows(pool.Where(p => !step.Off.Contains(p.Key)).ToList(), step.Counts, step.Weights, language);
	}

	// Stats rows of a deck drawn through a step's conditions
	public static List<(string Name, int Times, double Share, double Expected)> SubStats(DrawStep step, Deck deck, string language)
	{
		return Rows(SubPool(deck), step.SubCounts.GetValueOrDefault(deck.Id) ?? new Dictionary<string, int>(), step.Weights, language);
	}

	// The decks a step's conditions draw from, by id, in the order first named
	public static List<Deck> SubDecks(DrawStep step, Func<string, Deck?> find, Func<string, Deck?> named)
	{
		return Pool(step, find).Select(p => CardThen.Parse(p.Then).Then?.Deck).Where(d => d != null).Select(d => named(d!))
			.Where(d => d != null).DistinctBy(d => d!.Id).Select(d => d!).ToList();
	}

	private static List<(string Name, int Times, double Share, double Expected)> Rows(List<PoolCard> ticked,
		Dictionary<string, int> counts, bool weights, string language)
	{
		long total = Math.Max(1, ticked.Sum(p => weights ? (long)p.Card.Weight : 1L));
		int drawn = Math.Max(1, counts.Values.Sum());
		return ticked.Select(p => (p.Card.Shown(language), counts.GetValueOrDefault(p.Key),
				100.0 * counts.GetValueOrDefault(p.Key) / drawn, 100.0 * (weights ? p.Card.Weight : 1) / total))
			.OrderByDescending(r => r.Item2).ToList();
	}

	public static string Time(DateTime time)
	{
		return time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
	}
}
