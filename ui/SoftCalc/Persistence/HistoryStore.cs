// HistoryStore.cs - history.jsonl: load at start, merge and write at close (E12)
//
// One JSON object per line: an entry, or a {"cleared_at": ...} mark.
// Merge keeps every window's entries: union by id, drop entries older
// than the newest cleared mark, sort by time, keep the newest `cap`.

using System.Text;
using System.Text.Json;

namespace SoftCalc.Persistence;

internal sealed record StoredEntry(string Id, DateTime Time, string Kind, string Input, string DisplayInput, string Text);

internal sealed record HistoryFile(IReadOnlyList<StoredEntry> Entries, DateTime? ClearedAt);

internal static class HistoryStore
{
	private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

	// Entry id: process start time + PID + sequence; PIDs alone are reused by Windows
	public static string MakeId(DateTime processStart, int pid, int sequence)
	{
		return $"{processStart.ToUniversalTime():yyyyMMddHHmmssfff}-{pid}-{sequence}";
	}

	public static HistoryFile Parse(IEnumerable<string> lines)
	{
		var entries = new List<StoredEntry>();
		DateTime? clearedAt = null;
		foreach (string line in lines)
		{
			if (string.IsNullOrWhiteSpace(line))
			{
				continue;
			}
			try
			{
				using var doc = JsonDocument.Parse(line);
				if (doc.RootElement.TryGetProperty("cleared_at", out var mark))
				{
					var time = mark.GetDateTime();
					clearedAt = clearedAt == null || time > clearedAt ? time : clearedAt;
					continue;
				}
				var entry = doc.RootElement.Deserialize<StoredEntry>(Options);
				if (entry != null && !string.IsNullOrEmpty(entry.Id))
				{
					entries.Add(entry);
				}
			}
			catch (JsonException)
			{
				// a damaged line is skipped; the rest still loads
			}
		}
		return new HistoryFile(entries, clearedAt);
	}

	public static HistoryFile Load(string path)
	{
		return File.Exists(path) ? Parse(File.ReadLines(path, Encoding.UTF8)) : new HistoryFile(Array.Empty<StoredEntry>(), null);
	}

	// What History shows at start: entries after the last clear, newest `cap`
	public static IReadOnlyList<StoredEntry> Visible(HistoryFile file, int cap)
	{
		return Merge(file, Array.Empty<StoredEntry>(), null, cap).Entries;
	}

	public static HistoryFile Merge(HistoryFile onDisk, IEnumerable<StoredEntry> mine, DateTime? myClearedAt, int cap)
	{
		DateTime? cleared = Max(onDisk.ClearedAt, myClearedAt);
		var byId = new Dictionary<string, StoredEntry>();
		foreach (var entry in onDisk.Entries.Concat(mine))
		{
			byId[entry.Id] = entry;
		}
		var kept = byId.Values
			.Where(e => cleared == null || e.Time > cleared)
			.OrderBy(e => e.Time)
			.ThenBy(e => e.Id, StringComparer.Ordinal)
			.ToList();
		if (kept.Count > cap)
		{
			kept = kept.Skip(kept.Count - cap).ToList();
		}
		return new HistoryFile(kept, cleared);
	}

	public static string Serialize(HistoryFile file)
	{
		var text = new StringBuilder();
		if (file.ClearedAt != null)
		{
			text.Append(JsonSerializer.Serialize(new { cleared_at = file.ClearedAt })).Append('\n');
		}
		foreach (var entry in file.Entries)
		{
			text.Append(JsonSerializer.Serialize(entry, Options)).Append('\n');
		}
		return text.ToString();
	}

	// Close-time write: re-read under the lock so other windows' entries survive
	public static void MergeAndSave(DataFolder folder, IEnumerable<StoredEntry> mine, DateTime? myClearedAt, int cap)
	{
		folder.WithLock(() =>
		{
			var merged = Merge(Load(folder.HistoryPath), mine, myClearedAt, cap);
			DataFolder.WriteAtomic(folder.HistoryPath, Serialize(merged));
		});
	}

	private static DateTime? Max(DateTime? a, DateTime? b)
	{
		if (a == null)
		{
			return b;
		}
		if (b == null)
		{
			return a;
		}
		return a > b ? a : b;
	}
}
