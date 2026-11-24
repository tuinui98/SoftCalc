// DeckTransferTests.cs - Export Decks / Import Decks: one zip with decks.json and the pictures (owner 2026-11-23)

using System.IO.Compression;
using System.Text;
using SoftCalc.Logic;
using SoftCalc.Persistence;

namespace SoftCalc.Tests;

public sealed class DeckTransferTests : IDisposable
{
	private readonly string _dir = Path.Combine(Path.GetTempPath(), "softcalc-decks-" + Guid.NewGuid().ToString("N"));

	public DeckTransferTests()
	{
		Directory.CreateDirectory(_dir);
	}

	public void Dispose()
	{
		Directory.Delete(_dir, recursive: true);
	}

	private CardStore Store(string name)
	{
		string root = Path.Combine(_dir, name);
		Directory.CreateDirectory(root);
		var store = new CardStore(new DataFolder(root));
		store.File.Decks.Clear();
		return store;
	}

	private string Picture(CardStore store, Color color, int size = 40)
	{
		string path = Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".png");
		using (var bitmap = new Bitmap(size, size))
		{
			using var g = Graphics.FromImage(bitmap);
			g.Clear(color);
			bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
		}
		return store.ImportImage(path).Name!;
	}

	// Food (a picture on two cards, a colour, a weight, an unticked card) and Days
	private CardStore WithDecks()
	{
		var store = Store("from");
		string red = Picture(store, Color.Red);
		store.File.Decks.Add(new Deck
		{
			Id = CardStore.NewId(), Name = "Food", Cards =
			{
				new Card { Name = "Rice", Weight = 3, Image = red },
				new Card { Name = "Noodles", Image = red, Color = "#112233" },
				new Card { Name = "Soup", Off = true },
			},
		});
		store.File.Decks.Add(new Deck { Id = CardStore.NewId(), Name = "Days", Cards = { new Card { Name = "Mon" }, new Card { Name = "Tue" } } });
		return store;
	}

	private static DecksRead ReadBack(byte[] zip)
	{
		var (read, error) = DeckTransfer.Read(new MemoryStream(zip));
		Assert.Null(error);
		return read!;
	}

	private static byte[] Zip(params (string Name, byte[] Bytes)[] entries)
	{
		using var output = new MemoryStream();
		using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
		{
			foreach (var (name, bytes) in entries)
			{
				using var stream = zip.CreateEntry(name).Open();
				stream.Write(bytes);
			}
		}
		return output.ToArray();
	}

	private static byte[] Json(string text)
	{
		return Encoding.UTF8.GetBytes(text);
	}

	[Fact]
	public void DecksAndPicturesGoOutAndBack()
	{
		var from = WithDecks();
		byte[] zip = DeckTransfer.Write(from.File.Decks, from);
		using (var archive = new ZipArchive(new MemoryStream(zip)))
		{
			// the shared picture once
			Assert.Equal(new[] { "decks.json", "pictures/" + from.File.Decks[0].Cards[0].Image }, archive.Entries.Select(e => e.FullName));
			string json = new StreamReader(archive.GetEntry("decks.json")!.Open()).ReadToEnd();
			Assert.Contains("\"format\": \"softcalc-decks\"", json);
			Assert.DoesNotContain("\"id\"", json);
			Assert.DoesNotContain("thai", json);
			Assert.DoesNotContain("false", json);
			Assert.Contains("\"off\": true", json);
		}
		var to = Store("to");
		var done = DeckTransfer.Import(to, ReadBack(zip));
		Assert.Equal(new DecksImported(2, 0, 0, 0, 0), done);
		var food = to.File.Decks[0];
		Assert.Equal("Food", food.Name);
		Assert.NotEqual(from.File.Decks[0].Id, food.Id);
		Assert.Equal(new[] { ("Rice", 3, false, (string?)null), ("Noodles", 1, false, "#112233"), ("Soup", 1, true, null) },
			food.Cards.Select(c => (c.Name, c.Weight, c.Off, c.Color)));
		// a small PNG is kept as it was, so its name is the same
		Assert.Equal(from.File.Decks[0].Cards[0].Image, food.Cards[0].Image);
		Assert.True(File.Exists(to.ImagePath(food.Cards[0].Image)));
		Assert.Null(food.Cards[2].Image);
	}

	[Fact]
	public void ImportSkipsTheSameDeckAndRenamesAnother()
	{
		var from = WithDecks();
		var to = Store("to");
		DeckTransfer.Import(to, ReadBack(DeckTransfer.Write(from.File.Decks, from)));
		Assert.Equal(new DecksImported(0, 0, 2, 0, 0), DeckTransfer.Import(to, ReadBack(DeckTransfer.Write(from.File.Decks, from))));

		from.File.Decks[1].Cards.Add(new Card { Name = "Wed" });
		var changed = DeckTransfer.Import(to, ReadBack(DeckTransfer.Write(from.File.Decks, from)));
		Assert.Equal(new DecksImported(1, 1, 1, 0, 0), changed);
		Assert.Equal(new[] { "Food", "Days", "Days (2)" }, to.File.Decks.Select(d => d.Name));
		// the renamed one again: skipped, not "Days (3)"
		Assert.Equal(2, DeckTransfer.Import(to, ReadBack(DeckTransfer.Write(from.File.Decks, from))).Same);
	}

	[Fact]
	public void ImportStopsAtTheDeckLimit()
	{
		var to = Store("to");
		for (int i = 0; i < CardStore.MaxDecks - 1; i++)
		{
			to.File.Decks.Add(new Deck { Id = CardStore.NewId(), Name = "D" + i, Cards = { new Card { Name = "x" } } });
		}
		var from = WithDecks();
		Assert.Equal(new DecksImported(1, 0, 0, 1, 0), DeckTransfer.Import(to, ReadBack(DeckTransfer.Write(from.File.Decks, from))));
		Assert.Equal(CardStore.MaxDecks, to.File.Decks.Count);
	}

	[Fact]
	public void ReadRefusesWhatIsNotADecksFile()
	{
		Assert.NotNull(DeckTransfer.Read(new MemoryStream(Json("not a zip"))).Error);
		Assert.NotNull(DeckTransfer.Read(new MemoryStream(Zip(("other.json", Json("{}"))))).Error);
		Assert.NotNull(DeckTransfer.Read(new MemoryStream(Zip(("decks.json", Json("{\"format\":\"x\",\"version\":1}"))))).Error);
		Assert.NotNull(DeckTransfer.Read(new MemoryStream(Zip(("decks.json", Json("{broken"))))).Error);
		Assert.Equal("The file was made by a newer SoftCalc.",
			DeckTransfer.Read(new MemoryStream(Zip(("decks.json", Json("{\"format\":\"softcalc-decks\",\"version\":2}"))))).Error);
	}

	[Fact]
	public void ReadTakesOnlyWellNamedSmallPictures()
	{
		var store = Store("pics");
		string name = Picture(store, Color.Blue);
		byte[] png = File.ReadAllBytes(store.ImagePath(name)!);
		const string Big = "0123456789abcdef.png";
		string json = "{\"format\":\"softcalc-decks\",\"version\":1,\"decks\":[{\"name\":\" Odd \",\"cards\":["
			+ $"{{\"name\":\"a\",\"image\":\"{name}\"}},{{\"name\":\"b\",\"image\":\"../evil.png\"}},"
			+ $"{{\"name\":\"c\",\"image\":\"{Big}\",\"weight\":99999,\"color\":\"red\"}},{{\"name\":\"A\"}},{{\"name\":\" \"}}]}}]}}";
		var read = ReadBack(Zip(("decks.json", Json(json)), ("pictures/" + name, png), ("pictures/../evil.png", png),
			("pictures/" + Big, new byte[DeckTransfer.MaxPictureBytes + 1])));
		Assert.Equal(new[] { name }, read.Pictures.Keys);
		var to = Store("to");
		Assert.Equal(new DecksImported(1, 0, 0, 0, 1), DeckTransfer.Import(to, read));
		var deck = to.File.Decks.Single();
		Assert.Equal("Odd", deck.Name);
		// "A" is "a" again; a blank name goes
		Assert.Equal(new[] { ("a", 1, name), ("b", 1, (string?)null), ("c", CardText.MaxWeight, null) },
			deck.Cards.Select(c => (c.Name, c.Weight, c.Image)));
		Assert.Null(deck.Cards[2].Color);
	}

	[Fact]
	public void ABigPictureIsShrunk()
	{
		var store = Store("pics");
		string path = Path.Combine(_dir, "big.png");
		using (var bitmap = new Bitmap(1200, 600))
		{
			bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
		}
		byte[] png = File.ReadAllBytes(path);
		string json = "{\"format\":\"softcalc-decks\",\"version\":1,\"decks\":[{\"name\":\"Big\",\"cards\":[{\"name\":\"a\",\"image\":\"0123456789abcdef.png\"}]}]}";
		var to = Store("to");
		DeckTransfer.Import(to, ReadBack(Zip(("decks.json", Json(json)), ("pictures/0123456789abcdef.png", png))));
		using var stored = new Bitmap(to.ImagePath(to.File.Decks[0].Cards[0].Image)!);
		Assert.Equal((512, 256), (stored.Width, stored.Height));
	}

	[Fact]
	public void ADecksOwnCoverGoesOutAndComesBack()
	{
		var from = WithDecks();
		string blue = Picture(from, Color.Blue);
		from.File.Decks[0].Cover = blue;
		from.File.Decks[1].Cover = "0123456789abcdef.png";    // its file is gone: left out
		var read = ReadBack(DeckTransfer.Write(from.File.Decks, from));
		Assert.Equal((blue, null), (read.Decks[0].Cover, read.Decks[1].Cover));
		Assert.True(read.Pictures.ContainsKey(blue));

		var to = Store("to");
		DeckTransfer.Import(to, read);
		string? cover = to.File.Decks[0].Cover;
		Assert.NotNull(cover);
		Assert.True(File.Exists(to.ImagePath(cover)));
		Assert.Null(to.File.Decks[1].Cover);
		to.Save();    // a cover alone keeps its picture when unused pictures are dropped
		Assert.True(File.Exists(to.ImagePath(cover)));
	}

	[Fact]
	public void ACoverThatIsNotAPictureNameIsDropped()
	{
		var deck = new Deck { Name = "x", Cards = { new Card { Name = "a" } }, Cover = "../../secret.png" };
		CardStore.SanitizeDeck(deck);
		Assert.Null(deck.Cover);
	}

	[Fact]
	public void CopyToMyDecksStoresTheEmbeddedPicturesAsTheUsersOwn()
	{
		var store = Store("copy");
		var ready = ReadyDecks.All.First(d => d.Name == "Playing cards");
		var mine = store.OwnPictures(ready.CopyAs(CardStore.NewId(), "Cards", "en"));
		Assert.All(mine.Cards, c => Assert.True(CardStore.IsImageName(c.Image!), c.Name));
		Assert.Equal(54, mine.Cards.Select(c => c.Image).Distinct().Count());
		Assert.True(File.Exists(store.ImagePath(mine.Cards[0].Image)));
		Assert.StartsWith("ready:", ready.Cards[0].Image);    // the ready-made deck itself is unchanged

		var memory = new CardStore(null);    // no data folder: the cards go without pictures
		Assert.All(memory.OwnPictures(ready.CopyAs("x", "Cards", "en")).Cards, c => Assert.Null(c.Image));
	}
}
