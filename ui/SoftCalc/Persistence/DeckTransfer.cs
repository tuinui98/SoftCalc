// DeckTransfer.cs - Export Decks / Import Decks: the user's own decks and their pictures in one zip (owner 2026-11-23)
//
// The zip holds decks.json and pictures/<name>.png, each picture once
// however many cards use it; the desktop, web and Android apps write and
// read the same file (web/app/src/lib/decks.ts). Only the user's own decks
// go out; draw tabs do not. Import adds, never replaces: a deck of the same
// name and the same cards is skipped, one of the same name with other
// cards comes in as "name (2)". A zip from elsewhere is read with limits
// (its size, its entries, each entry's size) and only decks.json and
// pictures/<16 hex>.png are read, so no name in it becomes a path.

using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SoftCalc.Logic;

namespace SoftCalc.Persistence;

internal sealed class DeckZip
{
	public string Format { get; set; } = "";
	public int Version { get; set; }
	public List<ZipDeck> Decks { get; set; } = new();
}

internal sealed class ZipDeck
{
	public string Name { get; set; } = "";
	public List<Card> Cards { get; set; } = new();
}

// What a zip held: its decks (not yet checked) and the pictures they use
internal sealed record DecksRead(List<ZipDeck> Decks, Dictionary<string, byte[]> Pictures);

internal sealed record DecksImported(int Added, int Renamed, int Same, int OverLimit, int PicturesLost)
{
	public string Report()
	{
		var parts = new List<string> { $"{Added} deck(s) added" };
		if (Renamed > 0)
		{
			parts.Add($"{Renamed} of them renamed, as a deck of that name has other cards");
		}
		if (Same > 0)
		{
			parts.Add($"{Same} skipped, already in my decks");
		}
		if (OverLimit > 0)
		{
			parts.Add($"{OverLimit} left out: at most {CardStore.MaxDecks} decks");
		}
		if (PicturesLost > 0)
		{
			parts.Add($"{PicturesLost} picture(s) could not be read");
		}
		return string.Join("; ", parts) + ".";
	}
}

internal static class DeckTransfer
{
	public const string Format = "softcalc-decks";
	public const int Version = 1;
	public const string DecksEntry = "decks.json";
	public const string PicturesFolder = "pictures/";
	public const long MaxZipBytes = 64L * 1024 * 1024;
	public const int MaxEntries = 20000;
	public const int MaxJsonBytes = 32 * 1024 * 1024;
	public const int MaxPictureBytes = 1024 * 1024;
	public const long MaxTotalBytes = 256L * 1024 * 1024;

	private static readonly JsonSerializerOptions Options = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,    // no nulls, no "off": false
	};

	public static string ExportName(DateTime localTime)
	{
		return HistoryStore.ExportName("decks", "zip", localTime);
	}

	//==================================================================
	//  Export
	//==================================================================
	// The decks and the pictures they use; a picture whose file is gone leaves its card without one
	public static byte[] Write(IEnumerable<Deck> decks, CardStore store)
	{
		var pictures = new Dictionary<string, string>();
		var model = new DeckZip { Format = Format, Version = Version };
		foreach (var deck in decks)
		{
			var cards = new List<Card>();
			foreach (var card in deck.Cards)
			{
				string? image = card.Image;
				if (image != null && !pictures.ContainsKey(image))
				{
					string? path = store.ImagePath(image);
					if (path != null && File.Exists(path))
					{
						pictures[image] = path;
					}
					else
					{
						image = null;
					}
				}
				cards.Add(new Card { Name = card.Name, Weight = card.Weight, Image = image, Color = card.Color, Off = card.Off });
			}
			model.Decks.Add(new ZipDeck { Name = deck.Name, Cards = cards });
		}
		using var output = new MemoryStream();
		using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
		{
			var json = zip.CreateEntry(DecksEntry, CompressionLevel.Optimal);
			using (var stream = json.Open())
			{
				JsonSerializer.Serialize(stream, model, Options);
			}
			foreach (var (name, path) in pictures)
			{
				// PNGs are packed already
				var entry = zip.CreateEntry(PicturesFolder + name, CompressionLevel.NoCompression);
				using var stream = entry.Open();
				stream.Write(File.ReadAllBytes(path));
			}
		}
		return output.ToArray();
	}

	//==================================================================
	//  Read (with limits)
	//==================================================================
	public static (DecksRead? Read, string? Error) Read(string path)
	{
		try
		{
			if (new FileInfo(path).Length > MaxZipBytes)
			{
				return (null, $"The file is larger than {MaxZipBytes / (1024 * 1024)} MB.");
			}
			using var stream = File.OpenRead(path);
			return Read(stream);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return (null, "The file could not be read: " + ex.Message);
		}
	}

	public static (DecksRead? Read, string? Error) Read(Stream stream)
	{
		const string NotDecks = "This is not a SoftCalc decks file.";
		try
		{
			using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
			if (zip.Entries.Count > MaxEntries)
			{
				return (null, $"The file holds more than {MaxEntries} entries.");
			}
			var jsonEntry = zip.Entries.FirstOrDefault(e => e.FullName == DecksEntry);
			if (jsonEntry == null)
			{
				return (null, NotDecks);
			}
			long total = 0;
			byte[]? json = ReadEntry(jsonEntry, MaxJsonBytes, ref total);
			if (json == null)
			{
				return (null, "decks.json is too large.");
			}
			var model = JsonSerializer.Deserialize<DeckZip>(json, Options);
			if (model == null || model.Format != Format)
			{
				return (null, NotDecks);
			}
			if (model.Version > Version)
			{
				return (null, "The file was made by a newer SoftCalc.");
			}
			var decks = (model.Decks ?? new()).Where(d => d != null).ToList();
			var wanted = decks.SelectMany(d => d.Cards ?? new()).Select(c => c?.Image).Where(i => i != null).ToHashSet();
			var pictures = new Dictionary<string, byte[]>();
			foreach (var entry in zip.Entries)
			{
				if (!entry.FullName.StartsWith(PicturesFolder, StringComparison.Ordinal))
				{
					continue;
				}
				string name = entry.FullName[PicturesFolder.Length..];
				if (!CardStore.IsImageName(name) || !wanted.Contains(name) || pictures.ContainsKey(name))
				{
					continue;
				}
				if (ReadEntry(entry, MaxPictureBytes, ref total) is { } bytes)
				{
					pictures[name] = bytes;
				}
				else if (total > MaxTotalBytes)
				{
					return (null, "The file unpacks to too much.");
				}
			}
			return (new DecksRead(decks, pictures), null);
		}
		catch (InvalidDataException)
		{
			return (null, "The file is not a zip file, or it is damaged.");
		}
		catch (JsonException)
		{
			return (null, "decks.json in the file could not be read.");
		}
	}

	// An entry's bytes, or null when it is larger than cap (counting what is unpacked, not what it claims)
	private static byte[]? ReadEntry(ZipArchiveEntry entry, int cap, ref long total)
	{
		if (entry.Length > cap)
		{
			return null;
		}
		using var stream = entry.Open();
		using var bytes = new MemoryStream();
		var buffer = new byte[81920];
		int read;
		while ((read = stream.Read(buffer)) > 0)
		{
			total += read;
			if (bytes.Length + read > cap || total > MaxTotalBytes)
			{
				return null;
			}
			bytes.Write(buffer, 0, read);
		}
		return bytes.ToArray();
	}

	//==================================================================
	//  Import: add to my decks
	//==================================================================
	// Adds the decks to the store (not saved here) and stores their pictures
	public static DecksImported Import(CardStore store, DecksRead read)
	{
		var stored = new Dictionary<string, string?>();    // a picture's name in the zip -> in the cards folder
		int added = 0, renamed = 0, same = 0, overLimit = 0;
		foreach (var source in read.Decks)
		{
			var deck = new Deck { Id = CardStore.NewId(), Name = source.Name, Cards = source.Cards ?? new() };
			CardStore.SanitizeDeck(deck);
			var cards = new List<Card>();
			CardText.AddNew(cards, deck.Cards.Select(c => c.Copy()));
			foreach (var card in cards.Where(c => c.Image != null))
			{
				if (!stored.TryGetValue(card.Image!, out string? name))
				{
					name = read.Pictures.TryGetValue(card.Image!, out var bytes) ? store.StoreImage(bytes, keepSmallPng: true).Name : null;
					stored[card.Image!] = name;
				}
				card.Image = name;
			}
			deck.Cards = cards;
			if (deck.Cards.Count == 0)
			{
				continue;
			}
			if (store.File.Decks.Any(d => SameName(d.Name, deck.Name) && SameCards(d.Cards, deck.Cards)))
			{
				same++;
				continue;
			}
			if (store.File.Decks.Count >= CardStore.MaxDecks)
			{
				overLimit++;
				continue;
			}
			string free = store.FreeName(deck.Name);
			if (free != deck.Name)
			{
				renamed++;
				deck.Name = free;
			}
			store.File.Decks.Add(deck);
			added++;
		}
		return new DecksImported(added, renamed, same, overLimit, stored.Count(kv => kv.Value == null));
	}

	// "Food" is "Food", or "Food (2)" from an earlier import
	private static bool SameName(string have, string name)
	{
		return have.Equals(name, StringComparison.OrdinalIgnoreCase)
			|| (have.StartsWith(name + " (", StringComparison.OrdinalIgnoreCase) && Regex.IsMatch(have[(name.Length + 2)..], @"^\d+\)$"));
	}

	private static bool SameCards(List<Card> a, List<Card> b)
	{
		return a.Count == b.Count && a.Zip(b).All(p => p.First.Name == p.Second.Name && p.First.Weight == p.Second.Weight
			&& p.First.Image == p.Second.Image && p.First.Color == p.Second.Color && p.First.Off == p.Second.Off);
	}
}
