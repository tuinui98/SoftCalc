// ToolsTests.cs - M8: ECB rates (parse, calendar, fetch policy, cache), Compound table text, copy texts

using System.Text.Json.Nodes;
using SoftCalc.Engine;
using SoftCalc.Logic;
using SoftCalc.Persistence;
using SoftCalc.UI;

namespace SoftCalc.Tests;

public sealed class ToolsTests : IDisposable
{
	// Same shape as eurofxref-daily.xml, made-up rates
	internal const string Sample = """
		<?xml version="1.0" encoding="UTF-8"?>
		<gesmes:Envelope xmlns:gesmes="http://www.gesmes.org/xml/2002-08-01" xmlns="http://www.ecb.int/vocabulary/2002-08-01/eurofxref">
			<gesmes:subject>Reference rates</gesmes:subject>
			<Cube>
				<Cube time='2026-11-05'>
					<Cube currency='USD' rate='1.25'/>
					<Cube currency='THB' rate='40'/>
					<Cube currency='JPY' rate='160.5'/>
					<Cube currency='BAD' rate='x'/>
				</Cube>
			</Cube>
		</gesmes:Envelope>
		""";

	private readonly string _dir = Path.Combine(Path.GetTempPath(), "softcalc-tools-" + Guid.NewGuid().ToString("N"));

	public ToolsTests()
	{
		Directory.CreateDirectory(_dir);
	}

	public void Dispose()
	{
		Directory.Delete(_dir, recursive: true);
	}

	private static DateTime Utc(int month, int day, int hour, int minute = 0, int year = 2026)
	{
		return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
	}

	private static long Unix(DateTime utc)
	{
		return new DateTimeOffset(utc).ToUnixTimeSeconds();
	}

	//==================================================================
	//  ECB file
	//==================================================================
	[Fact]
	public void ParseReadsDateAndRatesAndSkipsBadOnes()
	{
		var rates = EcbRates.Parse(Sample)!;
		Assert.Equal(new DateOnly(2026, 11, 5), rates.Date);
		Assert.Equal(new[] { "JPY", "THB", "USD" }, rates.PerEur.Keys);
		Assert.Equal("160.5", rates.PerEur["JPY"]);
	}

	[Theory]
	[InlineData("")]
	[InlineData("<html>maintenance</html>")]
	[InlineData("<Cube><Cube time='2026-11-05'></Cube></Cube>")]
	public void ParseRefusesFilesWithoutRates(string xml)
	{
		Assert.Null(EcbRates.Parse(xml));
	}

	//==================================================================
	//  Publication calendar (CET = UTC+1 in November, UTC+2 in July)
	//==================================================================
	[Theory]
	[InlineData(11, 6, 14, 2026, 11, 5)]     // Friday 15:00 CET: Thursday's rates
	[InlineData(11, 6, 15, 2026, 11, 6)]     // Friday 16:00 CET: today's
	[InlineData(11, 7, 12, 2026, 11, 6)]     // Saturday
	[InlineData(11, 9, 10, 2026, 11, 6)]     // Monday morning
	[InlineData(12, 28, 10, 2026, 12, 24)]   // after 25 and 26 Dec
	[InlineData(4, 7, 10, 2026, 4, 2)]       // after Good Friday and Easter Monday 2026
	public void LatestPublication(int month, int day, int hour, int year, int wantMonth, int wantDay)
	{
		Assert.Equal(new DateOnly(year, wantMonth, wantDay), EcbCalendar.LatestPublication(Utc(month, day, hour)));
	}

	[Theory]
	[InlineData(2024, 3, 31)]
	[InlineData(2025, 4, 20)]
	[InlineData(2026, 4, 5)]
	public void EasterSunday(int year, int month, int day)
	{
		Assert.Equal(new DateOnly(year, month, day), EcbCalendar.EasterSunday(year));
	}

	[Fact]
	public void PublicationFollowsSummerTime()
	{
		Assert.Equal(Utc(11, 6, 15), EcbCalendar.PublicationUtc(new DateOnly(2026, 11, 6)));
		Assert.Equal(Utc(7, 1, 14), EcbCalendar.PublicationUtc(new DateOnly(2026, 7, 1)));
	}

	[Fact]
	public void LatestRatesWaitForTheNextPublication()
	{
		var thursday = new DateOnly(2026, 11, 5);
		Assert.Equal(Utc(11, 6, 15), EcbCalendar.NextCheckUtc(thursday, Utc(11, 6, 10), EcbCalendar.RetryDelay));
		var friday = new DateOnly(2026, 11, 6);
		Assert.Equal(Utc(11, 9, 15), EcbCalendar.NextCheckUtc(friday, Utc(11, 7, 10), EcbCalendar.RetryDelay));
	}

	[Fact]
	public void OldRatesRetryAfterTheDelay()
	{
		var thursday = new DateOnly(2026, 11, 5);
		var now = Utc(11, 6, 16);
		Assert.Equal(now.AddHours(1), EcbCalendar.NextCheckUtc(thursday, now, EcbCalendar.RetryDelay));
		Assert.Equal(now, EcbCalendar.NextCheckUtc(thursday, now, TimeSpan.Zero));
	}

	//==================================================================
	//  Fetch and cache
	//==================================================================
	private string CachePath => Path.Combine(_dir, "rates.xml");

	private RatesSource Source(Func<string> download, DateTime now)
	{
		return new RatesSource(CachePath, _ => Task.FromResult(download()), () => now);
	}

	[Fact]
	public async Task FetchWritesTheCacheAndWaitsForTheNextPublication()
	{
		var now = Utc(11, 6, 10);
		var payload = await Source(() => Sample, now).FetchAsync();
		Assert.True(File.Exists(CachePath));
		Assert.Equal("2026-11-05", (string?)payload["date"]);
		Assert.Equal("40", (string?)payload["rates"]?["THB"]);
		Assert.False((bool?)payload["stale"]);
		Assert.Equal(Unix(Utc(11, 6, 15)), (long?)payload["next_check"]);
	}

	[Fact]
	public async Task FailedFetchWithoutCacheGivesNoRatesForAnHour()
	{
		var now = Utc(11, 6, 10);
		var payload = await Source(() => throw new HttpRequestException("offline"), now).FetchAsync();
		Assert.Empty(payload["rates"]!.AsObject());
		Assert.True((bool?)payload["stale"]);
		Assert.Equal(Unix(now.AddHours(1)), (long?)payload["next_check"]);
		Assert.False(File.Exists(CachePath));
	}

	[Fact]
	public async Task FailedFetchKeepsTheHeldRatesMarkedStale()
	{
		bool online = true;
		var source = Source(() => online ? Sample : throw new TaskCanceledException(), Utc(11, 6, 16));
		await source.FetchAsync();
		online = false;
		var payload = await source.FetchAsync();
		Assert.Equal("2026-11-05", (string?)payload["date"]);
		Assert.True((bool?)payload["stale"]);
	}

	[Fact]
	public async Task GarbageFromTheServerDoesNotReplaceTheCache()
	{
		File.WriteAllText(CachePath, Sample);
		var source = Source(() => "<html>error</html>", Utc(11, 6, 16));
		source.LoadCached();
		var payload = await source.FetchAsync();
		Assert.True((bool?)payload["stale"]);
		Assert.Equal(Sample, File.ReadAllText(CachePath));
	}

	[Fact]
	public void CachedRatesAreDueAtOnceWhenOld()
	{
		File.WriteAllText(CachePath, Sample);
		var now = Utc(11, 9, 10);    // Monday; Friday's rates exist
		var payload = Source(() => Sample, now).LoadCached();
		Assert.Equal("2026-11-05", (string?)payload["date"]);
		Assert.Equal(Unix(now), (long?)payload["next_check"]);
	}

	[Fact]
	public void SeedRatesServeUntilTheFirstFetch()
	{
		var now = Utc(11, 9, 10);
		var source = new RatesSource(CachePath, _ => throw new HttpRequestException("offline"), () => now, () => Sample);
		var payload = source.LoadCached();
		Assert.Equal("2026-11-05", (string?)payload["date"]);
		Assert.Equal(Unix(now), (long?)payload["next_check"]);    // the first currency line still fetches
		Assert.False(File.Exists(CachePath));
	}

	[Fact]
	public async Task SeedRatesGoStaleWhenTheFetchFails()
	{
		var source = new RatesSource(CachePath, _ => throw new HttpRequestException("offline"), () => Utc(11, 9, 10), () => Sample);
		source.LoadCached();
		var payload = await source.FetchAsync();
		Assert.Equal("2026-11-05", (string?)payload["date"]);
		Assert.True((bool?)payload["stale"]);
	}

	[Fact]
	public void CacheWinsOverSeed()
	{
		File.WriteAllText(CachePath, Sample);
		var payload = new RatesSource(CachePath, _ => Task.FromResult(Sample), () => Utc(11, 6, 10), () => "<bad/>").LoadCached();
		Assert.Equal("2026-11-05", (string?)payload["date"]);
	}

	[Fact]
	public void NoSeedNoRates()
	{
		var payload = new RatesSource(CachePath, _ => Task.FromResult(Sample), () => Utc(11, 9, 10), () => null).LoadCached();
		Assert.Null((string?)payload["date"]);
	}

	[Fact]
	public void CachedLatestRatesAreNotDue()
	{
		File.WriteAllText(CachePath, Sample);
		var payload = Source(() => Sample, Utc(11, 6, 10)).LoadCached();
		Assert.Equal(Unix(Utc(11, 6, 15)), (long?)payload["next_check"]);
	}

	//==================================================================
	//  Compound Interest table text
	//==================================================================
	private static readonly ScheduleRow[] Rows =
	{
		new(1, "1000", "50", "0", "1050"),
		new(2, "1050", "52.5", "0", "1102.5"),
	};

	[Fact]
	public void CsvHasHeaderAndCrlfRows()
	{
		Assert.Equal("Period,Start balance,Interest,Contribution,End balance\r\n1,1000,50,0,1050\r\n2,1050,52.5,0,1102.5\r\n",
			CompoundTable.ToCsv(Rows));
	}

	[Theory]
	[InlineData("1.5", "1.5")]
	[InlineData("1,5", "\"1,5\"")]
	[InlineData("a\"b", "\"a\"\"b\"")]
	public void CsvQuotesWhenNeeded(string cell, string want)
	{
		Assert.Equal(want, CompoundTable.Quote(cell));
	}

	[Fact]
	public void TabsForTheClipboard()
	{
		Assert.Equal("1\t1000\t50\t0\t1050\r\n2\t1050\t52.5\t0\t1102.5\r\n", CompoundTable.ToTabs(Rows, header: false));
		Assert.StartsWith("Period\tStart balance", CompoundTable.ToTabs(Rows, header: true));
	}

	[Theory]
	[InlineData("years: must be positive", "years", "must be positive")]
	[InlineData("principal: Unknown variable 'x'", "principal", "Unknown variable 'x'")]
	[InlineData("'compounding' must be one of [...]", null, "'compounding' must be one of [...]")]
	[InlineData("Syntax error: oops", null, "Syntax error: oops")]
	public void ErrorsGoToTheirField(string error, string? field, string message)
	{
		Assert.Equal((field, message), CompoundTable.RouteError(error));
	}

	[Theory]
	[InlineData(950, "950")]
	[InlineData(1500, "1.5k")]
	[InlineData(2000000, "2M")]
	public void ChartLabelsAreShort(double value, string want)
	{
		Assert.Equal(want, CompoundChart.Short(value));
	}

	[Theory]
	[InlineData(10, 5, 2)]
	[InlineData(1234, 5, 500)]
	[InlineData(0.9, 4, 0.5)]
	public void ChartStepsAreRound(double range, int ticks, double want)
	{
		Assert.Equal(want, CompoundChart.NiceStep(range, ticks), 9);
	}

	//==================================================================
	//  History copy texts and stored notes
	//==================================================================
	[Fact]
	public void CopyTextCarriesTheRateNote()
	{
		var entry = new HistoryEntry("result", "100 [usd->thb]", "100 [usd->thb]", "3200") { Note = "ECB 2026-11-05" };
		Assert.Equal("100 [usd->thb]\r\n\t3200\t(ECB 2026-11-05)", HistoryView.CopyText(entry));
		var plain = new HistoryEntry("result", "1+1", "1+1", "2");
		Assert.Equal("1+1\r\n\t2", HistoryView.CopyText(plain));
	}

	[Fact]
	public void AllTextLeavesNoticesOut()
	{
		var view = new HistoryView();
		view.Append(new HistoryEntry("result", "1+1", "1+1", "2"));
		view.Append(new HistoryEntry("notice", "", "(engine restarted)", "why"));
		view.Append(new HistoryEntry("error", "1/0", "1/0", "ERROR:Divide by zero"));
		Assert.Equal("1+1\r\n\t2\r\n1/0\r\n\tERROR:Divide by zero\r\n", view.AllText());
	}

	[Fact]
	public void NotesSurviveTheHistoryFileAndOldFilesStillLoad()
	{
		var time = new DateTime(2026, 11, 6, 10, 0, 0, DateTimeKind.Utc);
		var entry = new StoredEntry("a", time, "result", "1 [usd->thb]", "1 [usd->thb]", "32", "ECB 2026-11-05");
		var file = HistoryStore.Parse(HistoryStore.Serialize(new HistoryFile(new[] { entry }, null)).Split('\n'));
		Assert.Equal("ECB 2026-11-05", file.Entries[0].Note);
		var old = HistoryStore.Parse(new[] { """{"id":"b","time":"2026-11-05T10:00:00Z","kind":"result","input":"1","display_input":"1","text":"1"}""" });
		Assert.Equal("", old.Entries[0].Note ?? "");
	}

	[Fact]
	public void ToolSettingsRoundTrip()
	{
		string path = Path.Combine(_dir, "settings.json");
		var settings = new AppSettings { AlwaysOnTop = true };
		settings.Compound.Years = "30";
		settings.Compound.Compounding = "continuous";
		SettingsStore.Save(path, settings);
		var (loaded, warning) = SettingsStore.Load(path);
		Assert.Null(warning);
		Assert.True(loaded.AlwaysOnTop);
		Assert.Equal(("30", "continuous"), (loaded.Compound.Years, loaded.Compound.Compounding));
	}

	[Fact]
	public void BadCompoundChoicesGoBackToDefaults()
	{
		string path = Path.Combine(_dir, "settings.json");
		File.WriteAllText(path, """{"compound": {"compounding": "hourly", "contribution_freq": "continuous"}}""");
		var (loaded, _) = SettingsStore.Load(path);
		Assert.Equal(("monthly", "monthly"), (loaded.Compound.Compounding, loaded.Compound.ContributionFreq));
	}
}
