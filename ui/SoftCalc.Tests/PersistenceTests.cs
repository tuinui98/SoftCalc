// PersistenceTests.cs - settings.json, history.jsonl merge, .pcf encodings, window fit (M7)

using System.Text;
using SoftCalc.Persistence;

namespace SoftCalc.Tests;

public sealed class PersistenceTests : IDisposable
{
	private readonly string _dir = Path.Combine(Path.GetTempPath(), "softcalc-tests-" + Guid.NewGuid().ToString("N"));

	public PersistenceTests()
	{
		Directory.CreateDirectory(_dir);
	}

	public void Dispose()
	{
		Directory.Delete(_dir, recursive: true);
	}

	private static StoredEntry Entry(string id, int minute, string input = "1+1")
	{
		return new StoredEntry(id, new DateTime(2026, 11, 5, 10, minute, 0, DateTimeKind.Utc), "result", input, input, "2");
	}

	//==================================================================
	//  History
	//==================================================================
	[Fact]
	public void MergeKeepsBothWindowsEntriesInTimeOrder()
	{
		var onDisk = new HistoryFile(new[] { Entry("a-1", 1), Entry("b-1", 3) }, null);    // window B closed first
		var mine = new[] { Entry("a-1", 1), Entry("a-2", 2), Entry("a-3", 4) };              // window A, with an old loaded id
		var merged = HistoryStore.Merge(onDisk, mine, null, 1000);
		Assert.Equal(new[] { "a-1", "a-2", "b-1", "a-3" }, merged.Entries.Select(e => e.Id));
	}

	[Fact]
	public void MergeKeepsOnlyTheNewestCap()
	{
		var mine = Enumerable.Range(1, 10).Select(i => Entry($"x-{i}", i)).ToList();
		var merged = HistoryStore.Merge(new HistoryFile(Array.Empty<StoredEntry>(), null), mine, null, 3);
		Assert.Equal(new[] { "x-8", "x-9", "x-10" }, merged.Entries.Select(e => e.Id));
	}

	[Fact]
	public void ClearInOneWindowDropsOlderEntriesFromOthers()
	{
		var onDisk = new HistoryFile(new[] { Entry("b-1", 1), Entry("b-2", 5) }, null);
		var clearedAt = new DateTime(2026, 11, 5, 10, 3, 0, DateTimeKind.Utc);
		var merged = HistoryStore.Merge(onDisk, new[] { Entry("a-1", 2), Entry("a-2", 4) }, clearedAt, 1000);
		Assert.Equal(new[] { "a-2", "b-2" }, merged.Entries.Select(e => e.Id));
		Assert.Equal(clearedAt, merged.ClearedAt);
	}

	[Fact]
	public void SerializedHistoryParsesBackAndSkipsDamagedLines()
	{
		var file = new HistoryFile(new[] { Entry("a-1", 1, "x*2"), Entry("a-2", 2) },
			new DateTime(2026, 11, 5, 9, 0, 0, DateTimeKind.Utc));
		string text = HistoryStore.Serialize(file) + "{not json\n\n";
		var back = HistoryStore.Parse(text.Split('\n'));
		Assert.Equal(file.ClearedAt, back.ClearedAt);
		Assert.Equal(file.Entries, back.Entries);
	}

	[Fact]
	public void DeletedEntryStaysGoneFromFileAndOtherWindows()
	{
		var folder = new DataFolder(_dir);
		HistoryStore.MergeAndSave(folder, new[] { Entry("a-1", 1), Entry("a-2", 2) }, null, 1000);
		var deleted = new Dictionary<string, DateTime> { ["a-1"] = Entry("a-1", 1).Time };
		HistoryStore.MergeAndSave(folder, Array.Empty<StoredEntry>(), null, 1000, deleted);    // this window deleted a loaded line
		HistoryStore.MergeAndSave(folder, new[] { Entry("a-1", 1), Entry("b-1", 3) }, null, 1000);    // another window still had it
		var file = HistoryStore.Load(folder.HistoryPath);
		Assert.Equal(new[] { "a-2", "b-1" }, file.Entries.Select(e => e.Id));
		Assert.True(file.DeletedIds.ContainsKey("a-1"));
	}

	[Fact]
	public void ClearDropsDeletedMarksItCovers()
	{
		var onDisk = new HistoryFile(new[] { Entry("a-2", 5) }, null,
			new Dictionary<string, DateTime> { ["a-1"] = Entry("a-1", 1).Time, ["a-3"] = Entry("a-3", 6).Time });
		var merged = HistoryStore.Merge(onDisk, Array.Empty<StoredEntry>(), Entry("x", 3).Time, 1000);
		Assert.Equal(new[] { "a-3" }, merged.DeletedIds.Keys);
	}

	[Fact]
	public void MergeAndSaveRereadsTheFile()
	{
		var folder = new DataFolder(_dir);
		HistoryStore.MergeAndSave(folder, new[] { Entry("b-1", 1) }, null, 1000);    // another window
		HistoryStore.MergeAndSave(folder, new[] { Entry("a-1", 2) }, null, 1000);
		Assert.Equal(new[] { "b-1", "a-1" }, HistoryStore.Load(folder.HistoryPath).Entries.Select(e => e.Id));
	}

	[Fact]
	public void IdsDifferByProcessStartPidAndSequence()
	{
		var start = new DateTime(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc);
		var ids = new[]
		{
			HistoryStore.MakeId(start, 100, 1), HistoryStore.MakeId(start, 100, 2),
			HistoryStore.MakeId(start, 200, 1), HistoryStore.MakeId(start.AddMinutes(1), 100, 1),
		};
		Assert.Equal(ids.Length, ids.Distinct().Count());
	}

	//==================================================================
	//  Settings
	//==================================================================
	[Fact]
	public void MissingSettingsGiveDefaults()
	{
		var (settings, warning) = SettingsStore.Load(Path.Combine(_dir, "none.json"));
		Assert.Null(warning);
		Assert.Equal(("History", 400, 651, 1000), (settings.View, settings.Window.Width, settings.Window.Height, settings.HistoryCap));
	}

	[Fact]
	public void SettingsRoundTrip()
	{
		string path = Path.Combine(_dir, "settings.json");
		var original = new AppSettings { Precision = 128, Angle = "deg", Base = "hex", Rpn = true, Decimals = 2, View = "Advanced" };
		original.Window = new WindowPlacement { X = 10, Y = 20, Width = 500, Height = 700, Maximized = true };
		original.Graph.Mode = "polar";
		original.Graph.Function = "wave";
		SettingsStore.Save(path, original);
		var (back, warning) = SettingsStore.Load(path);
		Assert.Null(warning);
		Assert.Equal((128, "deg", "hex", true, 2, "Advanced"), (back.Precision, back.Angle, back.Base, back.Rpn, back.Decimals, back.View));
		Assert.Equal((10, 20, 500, 700, true), (back.Window.X, back.Window.Y, back.Window.Width, back.Window.Height, back.Window.Maximized));
		Assert.Equal(("polar", "wave"), (back.Graph.Mode, back.Graph.Function));
	}

	[Fact]
	public void CorruptSettingsAreKeptAsBakAndReset()
	{
		string path = Path.Combine(_dir, "settings.json");
		File.WriteAllText(path, "{ broken");
		var (settings, warning) = SettingsStore.Load(path);
		Assert.NotNull(warning);
		Assert.Equal(32, settings.Precision);
		Assert.Equal("{ broken", File.ReadAllText(path + ".bak"));
	}

	[Fact]
	public void OutOfRangeSettingsFallBack()
	{
		string path = Path.Combine(_dir, "settings.json");
		File.WriteAllText(path, "{\"precision\": 33, \"angle\": \"turns\", \"view\": \"Odd\", \"history_cap\": 0}");
		var (settings, _) = SettingsStore.Load(path);
		Assert.Equal((32, "rad", "History", 1000), (settings.Precision, settings.Angle, settings.View, settings.HistoryCap));
	}

	//==================================================================
	//  .pcf files
	//==================================================================
	[Fact]
	public void PcfIsSavedAsUtf16LeWithoutBom()
	{
		byte[] bytes = PcfFile.Encode("a=1\r\n");
		Assert.Equal(new byte[] { (byte)'a', 0, (byte)'=', 0, (byte)'1', 0, 13, 0, 10, 0 }, bytes);
	}

	[Theory]
	[InlineData("utf16le")]
	[InlineData("utf16le-bom")]
	[InlineData("utf16be-bom")]
	[InlineData("utf8")]
	[InlineData("utf8-bom")]
	public void PcfReadsCommonEncodings(string encoding)
	{
		const string text = "rate=0.05\r\ng(x) = x\r\n";
		byte[] bytes = encoding switch
		{
			"utf16le" => Encoding.Unicode.GetBytes(text),
			"utf16le-bom" => Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(text)).ToArray(),
			"utf16be-bom" => Encoding.BigEndianUnicode.GetPreamble().Concat(Encoding.BigEndianUnicode.GetBytes(text)).ToArray(),
			"utf8" => Encoding.UTF8.GetBytes(text),
			_ => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray(),
		};
		Assert.Equal(text, PcfFile.Decode(bytes));
	}

	//==================================================================
	//  Window placement
	//==================================================================
	[Fact]
	public void VisibleWindowStaysWhereItWas()
	{
		var window = new Rectangle(100, 100, 400, 651);
		Assert.Equal(window, WindowPlacement.FitOnScreen(window, new[] { new Rectangle(0, 0, 1920, 1040) }));
	}

	[Fact]
	public void WindowOnARemovedMonitorMovesToThePrimary()
	{
		var window = new Rectangle(2500, 100, 400, 651);    // was on a second monitor
		var fitted = WindowPlacement.FitOnScreen(window, new[] { new Rectangle(0, 0, 1920, 1040) });
		Assert.True(new Rectangle(0, 0, 1920, 1040).Contains(fitted));
		Assert.Equal(new Size(400, 651), fitted.Size);
	}
}
