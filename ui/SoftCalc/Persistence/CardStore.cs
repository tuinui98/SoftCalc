// CardStore.cs - cards.json and the cards\ pictures: the user's decks and draw tabs (design/random_plan.md R6-R7)
//
// Kept apart from settings.json (owner 2026-11-14) since decks, history
// and counts grow. Written at once after each change, under the data
// folder's lock; between two Random windows the last write wins. A
// picture is shrunk to at most 128 px, stored as PNG under a name made
// from its content, and dropped once no deck uses it. An unreadable file
// is kept as cards.json.bak.

using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SoftCalc.Logic;

namespace SoftCalc.Persistence;

internal sealed class CardFile
{
	public int Version { get; set; } = 1;
	public string Language { get; set; } = "en";    // names of the ready-made cards: en or th
	public List<Deck> Decks { get; set; } = new();
	public List<DrawSetup> Draws { get; set; } = new();
}

internal sealed class CardStore
{
	public const int ImageSize = 128;
	public const int MaxDecks = 500;
	public const int MaxDraws = 50;
	private static readonly Regex ImageName = new(@"^[0-9a-f]{16}\.png$");
	private static readonly JsonSerializerOptions Options = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};
	private readonly DataFolder? _folder;

	public CardFile File { get; }
	public string? Warning { get; }

	// folder null: kept in memory only (tests, help pictures)
	public CardStore(DataFolder? folder)
	{
		_folder = folder;
		(File, Warning) = folder == null ? (new CardFile(), null) : Load(Path);
	}

	private string Path => System.IO.Path.Combine(_folder!.Root, "cards.json");
	public string? ImagesDir => _folder == null ? null : System.IO.Path.Combine(_folder.Root, "cards");

	public static (CardFile File, string? Warning) Load(string path)
	{
		if (!System.IO.File.Exists(path))
		{
			return (new CardFile(), null);
		}
		try
		{
			var file = JsonSerializer.Deserialize<CardFile>(System.IO.File.ReadAllText(path), Options);
			if (file != null)
			{
				Sanitize(file);
				return (file, null);
			}
		}
		catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException)
		{
		}
		System.IO.File.Copy(path, path + ".bak", overwrite: true);
		return (new CardFile(), "The card decks could not be read (the old file is kept as cards.json.bak).");
	}

	public void Save()
	{
		if (_folder == null)
		{
			return;
		}
		string json = JsonSerializer.Serialize(File, Options);
		_folder.WithLock(() => DataFolder.WriteAtomic(Path, json));
		PruneImages();
	}

	// A deck of the user's own, a ready-made one, or null when it is gone
	public Deck? Find(string id)
	{
		return id.StartsWith(ReadyDecks.Prefix, StringComparison.Ordinal) ? ReadyDecks.Find(id) : File.Decks.FirstOrDefault(d => d.Id == id);
	}

	// A deck by its name, for the draw conditions: my decks first, then the ready-made ones (English or Thai)
	public Deck? Named(string name)
	{
		return File.Decks.FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
			?? ReadyDecks.All.FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
				|| (d.Thai?.Equals(name, StringComparison.OrdinalIgnoreCase) ?? false));
	}

	public static string NewId()
	{
		return Guid.NewGuid().ToString("N")[..12];
	}

	// A name not taken yet by the user's decks: "Food", "Food (2)", ...
	public string FreeName(string name)
	{
		string candidate = name;
		for (int n = 2; File.Decks.Any(d => d.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)); n++)
		{
			candidate = $"{name} ({n})";
		}
		return candidate;
	}

	//==================================================================
	//  Pictures
	//==================================================================
	public string? ImagePath(string? name)
	{
		return name == null || ImagesDir == null || !ImageName.IsMatch(name) ? null : System.IO.Path.Combine(ImagesDir, name);
	}

	// A picture file shrunk and stored; returns its file name, or why not
	public (string? Name, string? Error) ImportImage(string source)
	{
		if (ImagesDir == null)
		{
			return (null, "Pictures need the data folder.");
		}
		try
		{
			using var original = new Bitmap(new MemoryStream(System.IO.File.ReadAllBytes(source)));
			double scale = Math.Min(1.0, (double)ImageSize / Math.Max(original.Width, original.Height));
			int width = Math.Max(1, (int)Math.Round(original.Width * scale));
			int height = Math.Max(1, (int)Math.Round(original.Height * scale));
			using var small = new Bitmap(width, height, PixelFormat.Format32bppArgb);
			using (var g = Graphics.FromImage(small))
			{
				g.InterpolationMode = InterpolationMode.HighQualityBicubic;
				g.PixelOffsetMode = PixelOffsetMode.HighQuality;
				g.DrawImage(original, 0, 0, width, height);
			}
			using var png = new MemoryStream();
			small.Save(png, ImageFormat.Png);
			byte[] bytes = png.ToArray();
			string name = Convert.ToHexString(SHA256.HashData(bytes))[..16].ToLowerInvariant() + ".png";
			Directory.CreateDirectory(ImagesDir);
			string target = System.IO.Path.Combine(ImagesDir, name);
			if (!System.IO.File.Exists(target))
			{
				DataFolder.WriteAtomic(target, bytes);
			}
			return (name, null);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or ExternalException
			or OutOfMemoryException)
		{
			return (null, $"'{System.IO.Path.GetFileName(source)}' could not be read as a picture.");
		}
	}

	// Pictures no deck uses; a picture added in the last hour stays, as another
	// window may have a deck open that uses it
	private void PruneImages()
	{
		if (ImagesDir == null || !Directory.Exists(ImagesDir))
		{
			return;
		}
		var used = new HashSet<string>(File.Decks.SelectMany(d => d.Cards).Select(c => c.Image ?? ""));
		foreach (string path in Directory.GetFiles(ImagesDir, "*.png"))
		{
			string name = System.IO.Path.GetFileName(path);
			if (ImageName.IsMatch(name) && !used.Contains(name) && System.IO.File.GetLastWriteTimeUtc(path) < DateTime.UtcNow.AddHours(-1))
			{
				try
				{
					System.IO.File.Delete(path);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
				}
			}
		}
	}

	//==================================================================
	//  Sanitize
	//==================================================================
	private static void Sanitize(CardFile file)
	{
		if (file.Language is not ("en" or "th"))
		{
			file.Language = "en";
		}
		file.Decks = (file.Decks ?? new()).Where(d => d != null && !string.IsNullOrEmpty(d.Id) && !d.Ready)
			.DistinctBy(d => d.Id).Take(MaxDecks).ToList();
		foreach (var deck in file.Decks)
		{
			deck.Name = Trim(deck.Name, "Deck");
			deck.Thai = null;
			deck.Cards = (deck.Cards ?? new()).Where(c => c != null && !string.IsNullOrWhiteSpace(c.Name))
				.Take(CardText.MaxCards).ToList();
			foreach (var card in deck.Cards)
			{
				card.Name = Trim(card.Name, "Card");
				card.Thai = null;
				card.Weight = Math.Clamp(card.Weight, 1, CardText.MaxWeight);
				if (card.Image != null && !ImageName.IsMatch(card.Image))
				{
					card.Image = null;
				}
				if (card.Color != null && !Regex.IsMatch(card.Color, "^#[0-9A-Fa-f]{6}$"))
				{
					card.Color = null;
				}
			}
		}
		file.Draws = (file.Draws ?? new()).Where(d => d != null && !string.IsNullOrEmpty(d.Id)).DistinctBy(d => d.Id)
			.Take(MaxDraws).ToList();
		foreach (var draw in file.Draws)
		{
			draw.Name = Trim(draw.Name, "Custom random");
			if (!CardDraws.Themes.Contains(draw.Theme))
			{
				draw.Theme = "list";
			}
			draw.Count = Math.Clamp(draw.Count, 1, CardDraws.MaxCount);
			draw.StageHeight = Math.Clamp(draw.StageHeight, 0, 2000);
			draw.Steps = (draw.Steps ?? new()).Where(s => s != null).Take(CardDraws.MaxSteps).ToList();
			foreach (var step in draw.Steps)
			{
				step.Decks = (step.Decks ?? new()).Where(id => !string.IsNullOrEmpty(id)).ToList();
				step.Extra ??= "";
				step.Off = (step.Off ?? new()).Where(k => k != null).ToList();
				step.Drawn = (step.Drawn ?? new()).Where(k => k != null).ToList();
				step.Counts = (step.Counts ?? new()).Where(kv => kv.Key != null && kv.Value > 0).ToDictionary();
				step.Order = (step.Order ?? new()).Where(k => k != null).Take(2 * CardText.MaxCards).ToList();
				step.Then = (step.Then ?? new()).Where(kv => kv.Key != null && !string.IsNullOrWhiteSpace(kv.Value)).ToDictionary();
				step.SubDrawn = (step.SubDrawn ?? new()).Where(kv => kv.Key != null && kv.Value != null)
					.ToDictionary(kv => kv.Key, kv => kv.Value.Where(k => k != null).ToList());
				step.SubCounts = (step.SubCounts ?? new()).Where(kv => kv.Key != null && kv.Value != null)
					.ToDictionary(kv => kv.Key, kv => kv.Value.Where(c => c.Key != null && c.Value > 0).ToDictionary());
			}
			draw.History = (draw.History ?? new()).Where(h => h?.Text != null).Take(CardDraws.MaxHistory).ToList();
			draw.Draws = Math.Max(0, draw.Draws);
		}
	}

	private static string Trim(string? name, string fallback)
	{
		string t = (name ?? "").Trim();
		return t.Length == 0 ? fallback : t.Length > CardText.MaxName ? t[..CardText.MaxName] : t;
	}
}
