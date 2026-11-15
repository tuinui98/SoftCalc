// CryptoWatchStore.cs - crypto.json: the watched addresses, what was last seen, recent events (design/crypto_plan.md C2a)
//
// Addresses and labels only: no key, no seed. Written after each round of
// checks and after each change in the Watch tab.

using System.Text.Json;
using System.Text.Json.Serialization;
using SoftCalc.Logic;

namespace SoftCalc.Persistence;

internal sealed class CryptoWatchFile
{
	public List<WatchEntry> Entries { get; set; } = new();
	public List<WatchEvent> Events { get; set; } = new();    // newest first
}

internal sealed class CryptoWatchStore
{
	public const int MaxEntries = 50;
	public const int MaxEvents = 200;
	private static readonly JsonSerializerOptions Options = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
	};
	private readonly DataFolder? _folder;

	public CryptoWatchFile File { get; }
	public string? Warning { get; }

	// folder null: kept in memory only (tests)
	public CryptoWatchStore(DataFolder? folder)
	{
		_folder = folder;
		(File, Warning) = folder == null ? (new CryptoWatchFile(), null) : Load(folder.CryptoWatchPath);
	}

	public static (CryptoWatchFile File, string? Warning) Load(string path)
	{
		if (!System.IO.File.Exists(path))
		{
			return (new CryptoWatchFile(), null);
		}
		try
		{
			var file = JsonSerializer.Deserialize<CryptoWatchFile>(System.IO.File.ReadAllText(path), Options);
			if (file != null)
			{
				Sanitize(file);
				return (file, null);
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
		{
			return (new CryptoWatchFile(), $"crypto.json could not be read ({ex.Message}); the watch list starts empty and the file is "
				+ "replaced at the next change.");
		}
		return (new CryptoWatchFile(), null);
	}

	private static void Sanitize(CryptoWatchFile file)
	{
		file.Entries ??= new();
		file.Events ??= new();
		file.Entries = file.Entries
			.Where(e => e != null && CryptoAddress.Check(e.Address ?? "") is { Valid: true } check && !IsWallet(check))
			.Take(MaxEntries)
			.ToList();
		foreach (var entry in file.Entries)
		{
			entry.Label ??= "";
			entry.Balances ??= new();
			entry.Seen ??= new();
			entry.Counterparties ??= new();
			entry.Status ??= "";
		}
		file.Events = file.Events.Where(e => e is { Label: not null, Text: not null }).Take(MaxEvents).ToList();
	}

	// An xpub/ypub/zpub: watched in C2b, not here
	public static bool IsWallet(AddressCheck check)
	{
		return check.Kind?.Contains("extended public key") == true;
	}

	public void AddEvents(IEnumerable<WatchEvent> events)
	{
		File.Events.InsertRange(0, events.Reverse());
		if (File.Events.Count > MaxEvents)
		{
			File.Events.RemoveRange(MaxEvents, File.Events.Count - MaxEvents);
		}
	}

	public void Save()
	{
		if (_folder == null)
		{
			return;
		}
		string json = JsonSerializer.Serialize(File, Options);
		try
		{
			_folder.WithLock(() => DataFolder.WriteAtomic(_folder.CryptoWatchPath, json));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// the list still works for this session
		}
	}
}
