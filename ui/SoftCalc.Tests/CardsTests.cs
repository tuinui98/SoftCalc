// CardsTests.cs - Card decks, ready-made decks, draw tabs and cards.json (design/random_plan.md R6-R7)

using System.Text;
using SoftCalc.Engine;
using SoftCalc.Logic;
using SoftCalc.Persistence;
using SoftCalc.UI;

namespace SoftCalc.Tests;

public sealed class CardsTests
{
	private static string TempDir()
	{
		string dir = Path.Combine(Path.GetTempPath(), "softcalc-cards-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		return dir;
	}

	private static Deck Find(string id)
	{
		return ReadyDecks.Find(ReadyDecks.Prefix + id)!;
	}

	[Fact]
	public void ReadyDecksAreComplete()
	{
		Assert.Equal(54, Find("playing-cards").Cards.Count);
		Assert.Equal(2, Find("playing-cards").Cards.Count(c => c.Off));
		Assert.Equal(12, Find("months").Cards.Count);
		Assert.Equal(new[] { "Monday", "Sunday" }, new[] { Find("days").Cards[0].Name, Find("days").Cards[^1].Name });
		Assert.Equal("จันทร์", Find("days").Cards[0].Shown("th"));
		Assert.Equal(77, Find("provinces").Cards.Count);
		Assert.Equal(12, Find("colors").Cards.Count(c => c.Color != null));
		Assert.Equal(12, Find("zodiac").Cards.Count);
		Assert.Equal("ชวด", Find("animal-years").Cards[0].Shown("th"));
		Assert.Equal(196, Find("countries").Cards.Count);    // 193 UN members, Palestine, Taiwan, the Vatican
		var continents = ReadyDecks.All.Where(d => d.Id.StartsWith(ReadyDecks.Prefix + "countries-")).ToList();
		Assert.Equal(6, continents.Count);
		Assert.Equal(196, continents.Sum(d => d.Cards.Count));
		Assert.Contains(Find("countries").Cards, c => c.Name == "Taiwan" && c.Thai == "ไต้หวัน");
		Assert.All(ReadyDecks.All, d =>
		{
			Assert.False(string.IsNullOrEmpty(d.Thai));
			Assert.Equal(d.Cards.Count, d.Cards.Select(c => c.Name).Distinct().Count());
			Assert.All(d.Cards, c => Assert.True(d.Id.EndsWith("playing-cards") || !string.IsNullOrEmpty(c.Thai), c.Name));
		});
	}

	[Fact]
	public void ThaiNamesListInThaiOrder()
	{
		var thai = Find("provinces").Listed("th").Select(c => c.Shown("th")).ToList();
		Assert.StartsWith("ก", thai[0]);
		Assert.StartsWith("อ", thai[^1]);
		Assert.Equal("Amnat Charoen", Find("provinces").Listed("en").First().Name);
		var copy = Find("days").CopyAs("x", "วัน", "th");
		Assert.Equal("จันทร์", copy.Cards[0].Name);
		Assert.Null(copy.Cards[0].Thai);
	}

	[Fact]
	public void TypedNamesSplitAutoLinesCommasSpaces()
	{
		Assert.Equal(new[] { "Fried rice", "Noodles" }, CardText.Split("Fried rice, Noodles", "auto"));
		Assert.Equal(new[] { "a, b", "c" }, CardText.Split("a, b\nc", "auto"));
		Assert.Equal(new[] { "x", "y", "z" }, CardText.Split(" x  y z ", "auto"));
		Assert.Equal(new[] { "x y" }, CardText.Split("x y", "lines"));
		Assert.Equal(new[] { "วันสิ้นเดือน", "วันเงินเดือนออก" }, CardText.Split("วันสิ้นเดือน,วันเงินเดือนออก", "auto"));
	}

	[Fact]
	public void LinesCarryWeights()
	{
		var (cards, error) = CardText.ParseLines("Fried rice | 2\r\n\r\nNoodles\nA|B | 3");
		Assert.Null(error);
		Assert.Equal(new[] { ("Fried rice", 2), ("Noodles", 1), ("A|B", 3) }, cards!.Select(c => (c.Name, c.Weight)));
		Assert.Equal("Fried rice | 2" + Environment.NewLine + "Noodles" + Environment.NewLine + "A|B | 3", CardText.FormatLines(cards!));
		Assert.Contains("Line 2", CardText.ParseLines("ok\nbad | 0").Error);
		Assert.Contains("1 to 1000", CardText.ParseLines("bad | 1.5").Error);
		Assert.NotNull(CardText.ParseLines(" | 2").Error);
	}

	[Fact]
	public void CsvReadsAndWritesQuotesAndThai()
	{
		var (rows, error) = CardText.ParseCsv("name,weight,image\r\n\"Rice, fried\",2,rice.png\r\n\"Say \"\"hi\"\"\"\r\nส้มตำ,,\r\n");
		Assert.Null(error);
		Assert.Equal(new[] { "Rice, fried", "Say \"hi\"", "ส้มตำ" }, rows!.Select(r => r.Card.Name));
		Assert.Equal(new[] { 2, 1, 1 }, rows!.Select(r => r.Card.Weight));
		Assert.Equal("rice.png", rows![0].Image);
		string csv = CardText.ToCsv(rows!.Select(r => r.Card), "en", _ => "");
		Assert.Equal(rows!.Select(r => r.Card.Name), CardText.ParseCsv(csv).Rows!.Select(r => r.Card.Name));
		Assert.Contains("Row 2", CardText.ParseCsv("a\nb,x").Error);

		Assert.Equal("ส้มตำ", CardText.ReadText(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("ส้มตำ")).ToArray()));
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		Assert.Equal("ส้มตำ", CardText.ReadText(Encoding.GetEncoding(874).GetBytes("ส้มตำ")));
		Assert.Equal("ส้มตำ", CardText.ReadText(Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("ส้มตำ")).ToArray()));
	}

	[Fact]
	public void PoolJoinsDecksAndExtrasAndLeavesOutUnticked()
	{
		// owner 2026-11-14: Days with "end of the month, payday" gives 9 cards
		var step = new DrawStep { Decks = { ReadyDecks.Prefix + "days" }, Extra = "วันสิ้นเดือน,วันเงินเดือนออก, วันสิ้นเดือน" };
		var pool = CardDraws.Pool(step, ReadyDecks.Find);
		Assert.Equal(9, pool.Count);
		step.Off.Add(CardDraws.Key(ReadyDecks.Prefix + "days", new Card { Name = "Saturday" }));
		step.Off.Add(CardDraws.Key(ReadyDecks.Prefix + "days", new Card { Name = "Sunday" }));
		Assert.Equal(7, CardDraws.Live(step, pool).Count);
		Assert.Empty(CardDraws.Pool(new DrawStep { Decks = { "gone" } }, _ => null));
	}

	[Fact]
	public void WeightsShareThePoints()
	{
		var deck = new Deck { Id = "d", Cards = { new Card { Name = "A", Weight = 3 }, new Card { Name = "B" } } };
		var step = new DrawStep { Decks = { "d" } };
		var live = CardDraws.Live(step, CardDraws.Pool(step, _ => deck));
		Assert.Equal(4, CardDraws.Total(step, live));
		Assert.Equal(new[] { "A", "A", "A", "B" }, Enumerable.Range(0, 4).Select(p => CardDraws.At(step, live, p).Card.Name));
		step.Weights = false;
		Assert.Equal(2, CardDraws.Total(step, live));
		Assert.Equal("B", CardDraws.At(step, live, 1).Card.Name);
	}

	[Fact]
	public void NoRepeatEmptiesThePileUntilShuffle()
	{
		var setup = new DrawSetup { Steps = { new DrawStep { Extra = "a,b,c", NoRepeat = true } } };
		var mixed = new MixedRandom();
		var (lines, error) = CardDraws.Draw(setup, _ => null, 3, n => (long)mixed.Below((ulong)n), "en", DateTime.UtcNow);
		Assert.Null(error);
		Assert.Equal(new[] { "a", "b", "c" }, lines.Select(l => l[0].Card.Card.Name).Order());
		var (more, why) = CardDraws.Draw(setup, _ => null, 1, n => 0, "en", DateTime.UtcNow);
		Assert.Empty(more);
		Assert.Contains("Shuffle", why);
		Assert.Equal(3, setup.History.Count);
		Assert.Equal(3, setup.Draws);
		Assert.Equal(3, setup.Steps[0].Counts.Values.Sum());
		setup.Steps[0].Drawn.Clear();
		Assert.Single(CardDraws.Draw(setup, _ => null, 1, n => 0, "en", DateTime.UtcNow).Lines);
	}

	[Fact]
	public void DrawJoinsStepsAndCounts()
	{
		var setup = new DrawSetup
		{
			Steps = { new DrawStep { Decks = { ReadyDecks.Prefix + "days" } }, new DrawStep { Extra = "rice" } },
		};
		var (lines, error) = CardDraws.Draw(setup, ReadyDecks.Find, 2, n => 0, "th", DateTime.UtcNow);
		Assert.Null(error);
		Assert.Equal(2, lines.Count);
		Assert.Equal("จันทร์ · rice", setup.History[0].Text);
		var stats = CardDraws.Stats(setup.Steps[0], CardDraws.Pool(setup.Steps[0], ReadyDecks.Find), "th");
		Assert.Equal(("จันทร์", 2, 100.0), (stats[0].Name, stats[0].Times, stats[0].Share));
		Assert.Equal(100.0 / 7, stats[0].Expected, 6);
		Assert.Contains("no cards", CardDraws.Draw(new DrawSetup { Steps = { new DrawStep() } }, _ => null, 1, n => 0, "en", DateTime.UtcNow).Error);
	}

	[Fact]
	public void DrawsFollowTheWeightsOverManyDraws()
	{
		var deck = new Deck { Id = "d", Cards = { new Card { Name = "A", Weight = 3 }, new Card { Name = "B" } } };
		var setup = new DrawSetup { Steps = { new DrawStep { Decks = { "d" } } } };
		var mixed = new MixedRandom();
		CardDraws.Draw(setup, _ => deck, 1000, n => (long)mixed.Below((ulong)n), "en", DateTime.UtcNow);
		Assert.InRange(setup.Steps[0].Counts["d/A"], 680, 820);    // 750 expected
		Assert.Equal(CardDraws.MaxHistory, setup.History.Count);
	}

	[Fact]
	public void ExtraCardsTakeWeightsAndConditions()
	{
		// owner 2026-11-14: "ราดหน้า>>stop|2", also written "ราดหน้า|2>>stop"
		Assert.Equal(("ราดหน้า", 2, "stop", (string?)null), CardDraws.ParseExtra("ราดหน้า>>stop|2"));
		Assert.Equal(("ราดหน้า", 2, "stop", (string?)null), CardDraws.ParseExtra("ราดหน้า|2>>stop"));
		Assert.Equal(("rice", 3, "", (string?)null), CardDraws.ParseExtra("rice | 3"));
		Assert.NotNull(CardDraws.ParseExtra("rice|x").Error);
		// "Gym,stop" and "step 3" split by commas or spaces join up again
		Assert.Equal(new[] { "a", "Gym>>ยิม,stop|2", "b" }, CardDraws.ExtraEntries("a, Gym>>ยิม|2, stop, b"));
		Assert.Equal(new[] { "a>>step 3", "b" }, CardDraws.ExtraEntries("a>>step 3 b"));
		Assert.Equal(new[] { "a>>b,stop", "c" }, CardDraws.ExtraEntries("a>>b,stop,c", "comma"));    // the deck editor's Add box
		var pool = CardDraws.Pool(new DrawStep { Extra = "Run, Gym>>ยิม|2, Swim>>stop" }, _ => null);
		Assert.Equal(new[] { 1, 2, 1 }, pool.Select(p => p.Card.Weight));
		Assert.Equal(new[] { "", "ยิม", "stop" }, pool.Select(p => p.Then));
		Assert.Equal("extra/Gym", pool[1].Key);
	}

	[Fact]
	public void RunsOfNumbersMakeCards()
	{
		// owner 2026-11-14: [from,to,step], words around it on each card
		Assert.Equal(new[] { "1", "2", "3" }, CardDraws.ExtraEntries("[1,3]"));
		Assert.Equal(new[] { "ปลา 1 ตัว", "ปลา 2 ตัว" }, CardDraws.ExtraEntries("ปลา [1,2] ตัว"));
		Assert.Equal(new[] { "a", "x 1", "x 2", "b" }, CardDraws.ExtraEntries("a, x [1, 2], b"));
		Assert.Equal(new[] { "0.00", "0.25", "0.50", "0.75", "1.00" }, CardDraws.ExtraEntries("[0,1,0.25]"));
		Assert.Equal(new[] { "10", "7", "4", "1" }, CardDraws.ExtraEntries("[10,1,3]"));
		Assert.Equal(new[] { "08", "09", "10" }, CardDraws.ExtraEntries("[08,10]"));
		Assert.Equal(new[] { "-1", "0", "1" }, CardDraws.ExtraEntries("[-1,1]"));
		Assert.Equal(new[] { "1>>stop|2", "2>>stop|2" }, CardDraws.ExtraEntries("[1,2]>>stop|2"));
		Assert.Equal(new[] { "a", "b" }, CardDraws.ExtraEntries("a b"));    // spaces still split without a run
		Assert.NotNull(CardDraws.Expand("[1,5,0]").Error);
		Assert.NotNull(CardDraws.Expand("[1,10000]").Error);
		Assert.Equal(new[] { "[1,x]" }, CardDraws.ExtraEntries("[1,x]"));
		var steps = new List<DrawStep> { new() { Extra = "[1,5,0]" } };
		Assert.Contains("step", CardDraws.Check(steps, 0, _ => null, _ => null, "en"));
		Assert.Equal(100, CardDraws.Pool(new DrawStep { Extra = "[1,100]" }, _ => null).Count);
	}

	[Fact]
	public void ShuffleMixesTheBoardOnly()
	{
		// owner 2026-11-14: Shuffle mixes where cards sit; the draw itself does not use the order
		var step = new DrawStep { Extra = "[1,20]" };
		var pool = CardDraws.Pool(step, _ => null);
		Assert.Same(pool, CardDraws.Arranged(step, pool));
		CardDraws.MixBoard(step, _ => null, _ => null, new Random(7));
		var arranged = CardDraws.Arranged(step, pool);
		Assert.Equal(pool.Select(p => p.Key).Order(), arranged.Select(p => p.Key).Order());
		Assert.NotEqual(pool.Select(p => p.Key), arranged.Select(p => p.Key));
		Assert.Equal(step.Order, arranged.Select(p => p.Key));
		// a card added later goes after the mixed ones
		step.Extra += ", new";
		Assert.Equal("extra/new", CardDraws.Arranged(step, CardDraws.Pool(step, _ => null))[^1].Key);
		var setup = new DrawSetup { Steps = { step } };
		Assert.Equal("1", CardDraws.Draw(setup, _ => null, 1, n => 0, "en", DateTime.UtcNow).Lines[0][0].Card.Card.Name);
	}

	[Fact]
	public void CsvKeepsTheColour()
	{
		var (rows, error) = CardText.ParseCsv("name,weight,image,color\nA,2,,#c62828\nB");
		Assert.Null(error);
		Assert.Equal("#C62828", rows![0].Card.Color);
		Assert.Null(rows[1].Card.Color);
		Assert.Contains("#RRGGBB", CardText.ParseCsv("A,1,,red").Error);
		Assert.Contains(",#C62828", CardText.ToCsv(rows.Select(r => r.Card), "en", _ => ""));
		Assert.Equal(ReadyDecks.SuitShades[1][0], ReadyDecks.Find(ReadyDecks.Prefix + "playing-cards")!.Cards[13].Color);    // A♥
	}

	[Fact]
	public void ConditionsParseAndFormat()
	{
		Assert.Equal(CardThen.Next, CardThen.Parse("").Then);
		Assert.Equal(new CardThen(null, 1, ThenFlow.Stop, 0), CardThen.Parse("STOP").Then);
		Assert.Equal(new CardThen(null, 1, ThenFlow.Step, 3), CardThen.Parse("step3").Then);
		Assert.Equal(new CardThen("ยิม", 2, ThenFlow.Stop, 0), CardThen.Parse("ยิม*2,stop").Then);
		Assert.Equal(new CardThen("stop", 1, ThenFlow.Next, 0), CardThen.Parse("deck:stop").Then);
		Assert.Equal(new CardThen("a,b", 1, ThenFlow.Next, 0), CardThen.Parse("a,b").Then);
		Assert.Null(CardThen.Parse("ยิม*0").Then);
		Assert.Null(CardThen.Parse("step 0").Then);
		foreach (string text in new[] { "stop", "step 2", "ยิม", "ยิม*2", "ยิม,stop", "deck:stop*3,step 4" })
		{
			Assert.Equal(text, CardThen.Parse(text).Then!.Format());
		}
	}

	private static (DrawSetup Setup, Func<string, Deck?> Named) Sports()
	{
		var gym = new Deck { Id = "g", Name = "ยิม", Cards = { new Card { Name = "ยกเวท" }, new Card { Name = "โยคะ" }, new Card { Name = "ลู่วิ่ง" } } };
		var setup = new DrawSetup
		{
			Steps =
			{
				new DrawStep { Extra = "วิ่ง, เข้ายิม>>ยิม*2, ว่ายน้ำ>>stop, แบดมินตัน>>step 3" },
				new DrawStep { Extra = "30 นาที, 1 ชม." },
				new DrawStep { Extra = "เช้า" },
			},
		};
		return (setup, name => name == "ยิม" ? gym : null);
	}

	[Fact]
	public void ConditionsDrawFromADeckStopAndSkip()
	{
		var (setup, named) = Sports();
		// below gives the point of each pick: Gym, then Yoga and Weights from the deck, 1 hour, morning
		Assert.Equal(5, CardDraws.MostPicks(setup, _ => null));    // 1 + 2 from the deck, 1, 1
		var picks = new Queue<long>(new long[] { 1, 1, 0, 1, 0 });
		var (lines, error) = CardDraws.Draw(setup, _ => null, 1, n => picks.Dequeue(), "th", DateTime.UtcNow, named);
		Assert.Null(error);
		Assert.Equal("เข้ายิม → โยคะ + ยกเวท · 1 ชม. · เช้า", setup.History[0].Text);
		Assert.Equal(2, setup.Steps[0].SubCounts["g"].Values.Sum());
		Assert.Equal("g", lines[0][1].Sub);
		Assert.Equal(3, lines[0][1].Live!.Count);
		Assert.Equal(2, lines[0][2].Live!.Count);    // the second card from the deck is a different one

		CardDraws.Draw(setup, _ => null, 1, n => 2, "th", DateTime.UtcNow, named);
		Assert.Equal("ว่ายน้ำ", setup.History[0].Text);
		CardDraws.Draw(setup, _ => null, 1, n => n == 4 ? 3 : 0, "th", DateTime.UtcNow, named);
		Assert.Equal("แบดมินตัน · เช้า", setup.History[0].Text);
		Assert.Contains("no deck named", CardDraws.Draw(setup, _ => null, 1, n => 1, "th", DateTime.UtcNow, _ => null).Error);

		var rows = CardDraws.SubStats(setup.Steps[0], named("ยิม")!, "th");
		Assert.Equal(3, rows.Count);
		Assert.Equal(2, rows.Sum(r => r.Times));
	}

	[Fact]
	public void ConditionDeckKeepsItsOwnPileForNoRepeat()
	{
		var (setup, named) = Sports();
		setup.Steps[0].Extra = "เข้ายิม>>ยิม";
		setup.Steps[0].NoRepeat = true;
		setup.Steps.RemoveRange(1, 2);
		var (lines, error) = CardDraws.Draw(setup, _ => null, 1, n => 0, "th", DateTime.UtcNow, named);
		Assert.Null(error);
		setup.Steps[0].Drawn.Clear();
		CardDraws.Draw(setup, _ => null, 1, n => 0, "th", DateTime.UtcNow, named);
		setup.Steps[0].Drawn.Clear();
		CardDraws.Draw(setup, _ => null, 1, n => 0, "th", DateTime.UtcNow, named);
		Assert.Equal(3, setup.Steps[0].SubDrawn["g"].Distinct().Count());
		setup.Steps[0].Drawn.Clear();
		Assert.Contains("Shuffle", CardDraws.Draw(setup, _ => null, 1, n => 0, "th", DateTime.UtcNow, named).Error);
	}

	[Fact]
	public void CheckFindsBadConditions()
	{
		var (setup, named) = Sports();
		Assert.Null(CardDraws.Check(setup.Steps, 0, _ => null, named, "th"));
		setup.Steps[0].Extra = "a>>ไม่มี";
		Assert.Contains("no deck named", CardDraws.Check(setup.Steps, 0, _ => null, named, "th"));
		setup.Steps[0].Extra = "a>>step 1";
		Assert.Contains("later step", CardDraws.Check(setup.Steps, 0, _ => null, named, "th"));
		setup.Steps[0].Extra = "a|0";
		Assert.Contains("weight", CardDraws.Check(setup.Steps, 0, _ => null, named, "th"));
		// a deck card's condition is set in Edit tab
		var deck = new Deck { Id = "d", Cards = { new Card { Name = "x" } } };
		var step = new DrawStep { Decks = { "d" }, Then = { ["d/x"] = "step 9" } };
		Assert.Contains("later step", CardDraws.Check(new List<DrawStep> { step }, 0, _ => deck, named, "en"));
	}

	[Theory]
	[InlineData(12, "12")]
	[InlineData(36, "36")]
	[InlineData(37, "10/27")]
	[InlineData(52, "7/18/27")]    // a pack of cards
	[InlineData(70, "6/14/21/29")]
	[InlineData(77, "7/15/23/32")]    // owner 2026-11-14: the 77 provinces
	[InlineData(125, "11/24/38/52")]
	public void BoardRingsMatchTheAgreedTable(int cards, string rings)
	{
		var layout = BoardRings.Layout(Enumerable.Repeat(1.0, cards).ToList());
		Assert.Equal(rings, string.Join("/", layout.Select(r => r.Cards.Count)));
		Assert.Equal(1.0, layout[^1].Outer);
		// every card has the same area
		foreach (var ring in layout)
		{
			double area = (ring.Outer * ring.Outer - ring.Inner * ring.Inner) / ring.Cards.Count;
			Assert.Equal((1 - BoardRings.Hub * BoardRings.Hub) / cards, area, 9);
		}
	}

	[Fact]
	public void BoardRingsPutTheHeaviestCardsInTheMiddle()
	{
		var weights = Enumerable.Repeat(1.0, 40).ToList();
		weights.AddRange(Enumerable.Repeat(1.0, 20));
		weights[30] = 3;
		var layout = BoardRings.Layout(weights);
		Assert.Contains(30, layout[0].Cards);
		Assert.Contains(0, layout[0].Cards);
		Assert.Equal(Enumerable.Range(0, 60), layout.SelectMany(r => r.Cards).Order());
	}

	[Fact]
	public void StoreSavesLoadsAndSanitizes()
	{
		string dir = TempDir();
		try
		{
			var store = new CardStore(new DataFolder(dir));
			// owner 2026-11-15: a first run has one draw tab to try
			Assert.Equal(("Spin", "spin"), (store.File.Draws.Single().Name, store.File.Draws[0].Theme));
			store.File.Draws.Clear();
			store.File.Language = "th";
			store.File.Decks.Add(new Deck { Id = "abc", Name = "Food", Cards = { new Card { Name = "Rice", Weight = 2 } } });
			store.File.Draws.Add(new DrawSetup { Id = "d1", Name = "Lunch", Steps = { new DrawStep { Decks = { "abc" }, Extra = "Egg" } } });
			store.Save();
			var again = new CardStore(new DataFolder(dir));
			Assert.Null(again.Warning);
			Assert.Equal("th", again.File.Language);
			Assert.Equal(2, again.Find("abc")!.Cards[0].Weight);
			Assert.Equal("Lunch", again.File.Draws[0].Name);
			Assert.NotNull(again.Find(ReadyDecks.Prefix + "months"));
			Assert.Equal("Food (2)", again.FreeName("Food"));

			File.WriteAllText(Path.Combine(dir, "cards.json"),
				"{\"language\":\"xx\",\"decks\":[{\"id\":\"ready:x\",\"name\":\"no\"},{\"id\":\"k\",\"name\":\" \",\"cards\":[{\"name\":\"a\",\"weight\":99999,\"image\":\"..\\\\evil.png\"}]}],"
				+ "\"draws\":[{\"id\":\"z\",\"theme\":\"bogus\",\"count\":0}]}");
			var odd = new CardStore(new DataFolder(dir)).File;
			Assert.Equal("en", odd.Language);
			Assert.Single(odd.Decks);
			Assert.Equal(("Deck", CardText.MaxWeight, (string?)null), (odd.Decks[0].Name, odd.Decks[0].Cards[0].Weight, odd.Decks[0].Cards[0].Image));
			Assert.Equal(("list", 1), (odd.Draws[0].Theme, odd.Draws[0].Count));

			File.WriteAllText(Path.Combine(dir, "cards.json"), "{ not json");
			var broken = new CardStore(new DataFolder(dir));
			Assert.NotNull(broken.Warning);
			Assert.True(File.Exists(Path.Combine(dir, "cards.json.bak")));
		}
		finally
		{
			Directory.Delete(dir, recursive: true);
		}
	}

	[Fact]
	public void PicturesAreShrunkAndStoredByContent()
	{
		string dir = TempDir();
		try
		{
			string source = Path.Combine(dir, "big.png");
			using (var big = new Bitmap(400, 200))
			{
				using var g = Graphics.FromImage(big);
				g.Clear(Color.Orange);
				big.Save(source, System.Drawing.Imaging.ImageFormat.Png);
			}
			var store = new CardStore(new DataFolder(dir));
			var (name, error) = store.ImportImage(source);
			Assert.Null(error);
			Assert.Matches("^[0-9a-f]{16}\\.png$", name);
			using (var stored = new Bitmap(store.ImagePath(name)!))
			{
				Assert.Equal((128, 64), (stored.Width, stored.Height));
			}
			Assert.Equal(name, store.ImportImage(source).Name);    // the same picture: the same file
			File.WriteAllText(Path.Combine(dir, "not.png"), "text");
			Assert.NotNull(store.ImportImage(Path.Combine(dir, "not.png")).Error);
			Assert.Null(store.ImagePath("../x.png"));
			Assert.NotNull(new CardStore(null).ImportImage(source).Error);
		}
		finally
		{
			Directory.Delete(dir, recursive: true);
		}
	}

	private static T Field<T>(RandomWindow window, string name)
	{
		return (T)typeof(RandomWindow).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
			.GetValue(window)!;
	}

	private static Task DrawOn(DrawPage page)
	{
		return (Task)typeof(DrawPage).GetMethod("DrawAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
			.Invoke(page, null)!;
	}

	[Fact]
	public async Task WindowShowsDrawTabsBeforeTheMoreTab()
	{
		var store = new CardStore(null);
		Assert.Single(new RandomWindow(new RandomPrefs(), _ => { }, null, store).Controls.OfType<TabControl>().Single().TabPages
			.Cast<TabPage>(), t => t.Text == "...");
		store.File.Draws.Add(new DrawSetup
		{
			Id = "ab12", Name = "กินอะไรดี", Effects = false, Count = 3,
			Steps = { new DrawStep { Decks = { ReadyDecks.Prefix + "days" }, NoRepeat = true } },
		});
		var prefs = new RandomPrefs { Tab = "draw:ab12" };
		using var window = new RandomWindow(prefs, _ => { }, null, store);
		var tabs = Field<TabControl>(window, "_tabs");
		Assert.Equal(new[] { "Simple", "Advanced", "Password", "Entropy", "กินอะไรดี", "..." }, tabs.TabPages.Cast<TabPage>().Select(t => t.Text));
		var page = Assert.IsType<DrawPage>(tabs.SelectedTab);
		await DrawOn(page);
		Assert.Equal(3, page.Setup.History.Count);
		Assert.Equal(3, page.Setup.Steps[0].Drawn.Distinct().Count());
		window.SavePrefs();
		Assert.Equal("draw:ab12", prefs.Tab);
		tabs.SelectedIndex = 5;
		window.SavePrefs();
		Assert.Equal("cards", prefs.Tab);
	}

	[Fact]
	public async Task SeedGivesTheSameCardsAgain()
	{
		var seeds = new SeedEngine(EngineLocator.Locate(Path.Combine(Path.GetTempPath(), "softcalc-test-engine")), () => 32);
		var store = new CardStore(null);
		store.File.Draws.Add(new DrawSetup
		{
			Id = "s1", Name = "Seeded", Effects = false, Count = 5,
			Steps = { new DrawStep { Decks = { ReadyDecks.Prefix + "provinces" } }, new DrawStep { Decks = { ReadyDecks.Prefix + "colors" } } },
		});
		using var window = new RandomWindow(new RandomPrefs { Tab = "draw:s1" }, _ => { }, seeds, store);
		Field<TextBox>(window, "_seed").Text = "42";
		var page = (DrawPage)Field<TabControl>(window, "_tabs").SelectedTab!;
		await DrawOn(page);
		var first = page.Setup.History.Take(5).Select(h => h.Text).ToList();
		await DrawOn(page);
		Assert.Equal(first, page.Setup.History.Take(5).Select(h => h.Text));
		Assert.Equal(10, page.Setup.History.Count);
	}

	[Fact]
	public void DrawTabPrefsAreSanitized()
	{
		string path = Path.Combine(Path.GetTempPath(), "softcalc-cards-prefs-" + Guid.NewGuid().ToString("N") + ".json");
		try
		{
			File.WriteAllText(path, "{\"random\":{\"tab\":\"draw:ab12\"}}");
			Assert.Equal("draw:ab12", SettingsStore.Load(path).Settings.Random.Tab);
			File.WriteAllText(path, "{\"random\":{\"tab\":\"draw:../x\"}}");
			Assert.Equal("numbers", SettingsStore.Load(path).Settings.Random.Tab);
		}
		finally
		{
			File.Delete(path);
		}
	}
}
