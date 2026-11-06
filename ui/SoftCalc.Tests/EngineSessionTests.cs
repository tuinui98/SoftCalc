// EngineSessionTests.cs - The UI's engine connection against the real engine
//
// Uses the development engine (engine/.venv) found by EngineLocator, so
// these tests also check the Python side through the same pipe the
// window uses. Skipped when no engine is available.

using System.Text.Json.Nodes;
using SoftCalc.Engine;

namespace SoftCalc.Tests;

public sealed class EngineSessionTests : IAsyncLifetime
{
	private EngineSession? _session;

	public async Task InitializeAsync()
	{
		_session = new EngineSession(EngineLocator.Locate(Path.Combine(Path.GetTempPath(), "softcalc-test-engine")));
		await _session.StartAsync();
	}

	public Task DisposeAsync()
	{
		_session?.Dispose();
		return Task.CompletedTask;
	}

	private EngineSession Session => _session ?? throw new InvalidOperationException();

	[Fact]
	public async Task EvalReturnsDisplayAndResult()
	{
		await Session.EvalAsync("x=5");
		var reply = await Session.EvalAsync("2x+1");
		Assert.Equal(("result", "2x+1", "2*5+1", "11"), (reply.Kind, reply.Input, reply.DisplayInput, reply.Text));
	}

	[Fact]
	public async Task ErrorsAndEmptyInputAreKinds()
	{
		Assert.Equal("error", (await Session.EvalAsync("1/0")).Kind);
		Assert.Equal("empty", (await Session.EvalAsync("")).Kind);
	}

	[Fact]
	public async Task NonAsciiSurvivesThePipe()
	{
		var reply = await Session.EvalAsync("ก=1");
		Assert.Equal(("error", "ก=1"), (reply.Kind, reply.Input));
	}

	[Fact]
	public async Task SettingsAreTracked()
	{
		var settings = await Session.ChangeSettingsAsync(new JsonObject { ["base"] = "hex" });
		Assert.Equal("hex", (string?)settings["base"]);
		Assert.Equal("0xFF", (await Session.EvalAsync("255")).Text);
	}

	[Fact]
	public async Task StateListsUserFunctions()
	{
		await Session.EvalAsync("grow(p, n)=p*2^n");
		var state = await Session.StateAsync();
		var grow = Assert.Single(state.Functions, f => f.Name == "grow");
		Assert.Equal(new[] { "p", "n" }, grow.Parameters);
		Assert.Equal("grow(p, n)=", grow.Head);
	}

	[Fact]
	public async Task CatalogAndMenuInsert()
	{
		var catalog = await Session.CatalogAsync();
		Assert.Contains(catalog.ConversionGroups, g => g.Key == "Length");
		var (text, caret) = await Session.MenuInsertAsync(new JsonObject
		{
			["kind"] = "function", ["name"] = "sqrt", ["text"] = "2", ["caret"] = 1,
		});
		Assert.Equal(("sqrt(2)", 7), (text, caret));
	}

	[Fact]
	public async Task DeleteRemovesAndReportsMissing()
	{
		await Session.EvalAsync("gone=1");
		Assert.True(await Session.DeleteAsync("gone"));
		Assert.False(await Session.DeleteAsync("gone"));
		Assert.Equal("error", (await Session.EvalAsync("gone")).Kind);
	}

	[Fact]
	public async Task PlotReturnsPointsAndGaps()
	{
		await Session.EvalAsync("r(x)=sqrt(x)");
		var points = await Session.PlotAsync("r", "xy", -1, 1, 3);
		Assert.Null(points[0]);
		Assert.Equal((1.0, 1.0), points[2]);
	}

	[Fact]
	public async Task ValueAtGivesFullPrecisionOrNothing()
	{
		await Session.EvalAsync("r(x)=sqrt(x)");
		var (text, value) = await Session.ValueAtAsync("r", 2);
		Assert.StartsWith("1.41421356237309504880", text);
		Assert.Equal(Math.Sqrt(2), value!.Value, 9);
		Assert.Equal((null, null), await Session.ValueAtAsync("r", -1));
	}

	[Fact]
	public async Task DeletedDefinitionStaysDeletedAfterRestart()
	{
		await Session.EvalAsync("temp=7");
		await Session.DeleteAsync("temp");
		Session.Client!.KillForTest();
		await Session.EvalAsync("1");    // lost, triggers the restart
		Assert.Equal("error", (await Session.EvalAsync("temp")).Kind);
	}

	[Fact]
	public async Task RequestBeforeStartIsNotTreatedAsLostEngine()
	{
		using var notStarted = new EngineSession(EngineLocator.Locate(Path.Combine(Path.GetTempPath(), "softcalc-test-engine")));
		bool restarted = false;
		notStarted.Restarted += (_, _) => restarted = true;
		await Assert.ThrowsAsync<EngineNotStartedException>(() => notStarted.EvalAsync("1"));
		await Assert.ThrowsAsync<EngineNotStartedException>(() => notStarted.PlotAsync("g", "xy", 0, 1, 2));
		Assert.False(restarted);
	}

	[Fact]
	public async Task RestoreResultsFeedsAnswerAndRefs()
	{
		Assert.Equal(2, await Session.RestoreResultsAsync(new[] { "10", "Tuesday, 27 October 2026", "0xFF" }));
		Assert.Equal("256", (await Session.EvalAsync("answer+1")).Text);
		Assert.Equal("10", (await Session.EvalAsync("$3")).Text);
	}

	[Fact]
	public async Task ImportReportsSkippedLinesAndRaisesChanged()
	{
		string? changed = null;
		Session.DefinitionsChanged += (_, text) => changed = text;
		var skipped = await Session.ImportDefinitionsAsync("zzv=\r\nk=3\r\nh(x) = x*k\r\n");
		Assert.Single(skipped);
		Assert.Contains("h(x) = x*k", changed);
		Assert.Equal("6", (await Session.EvalAsync("h(2)")).Text);
	}

	[Fact]
	public async Task CrashIsRecoveredWithDefinitionsAndSettings()
	{
		string? notice = null;
		Session.Restarted += (_, reason) => notice = reason;
		await Session.EvalAsync("keep=41");
		await Session.ChangeSettingsAsync(new JsonObject { ["precision"] = 64 });

		Session.Client!.KillForTest();
		var lost = await Session.EvalAsync("keep+1");
		Assert.Equal("error", lost.Kind);
		Assert.NotNull(notice);

		Assert.Equal("42", (await Session.EvalAsync("keep+1")).Text);
		Assert.Equal(64, (int?)Session.Settings["precision"]);
		Assert.Equal(64 + 2, (await Session.EvalAsync("1/3")).Text.Length);
	}

	//==================================================================
	//  M8: currency rates on demand, Compound Interest schedule
	//==================================================================
	private static JsonObject Rates(bool stale = false)
	{
		var later = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
		return new JsonObject
		{
			["date"] = "2026-11-05", ["rates"] = new JsonObject { ["USD"] = "1.25", ["THB"] = "40" },
			["stale"] = stale, ["next_check"] = later,
		};
	}

	[Fact]
	public async Task CurrencyFetchesOnceThenEvaluatesAgain()
	{
		int calls = 0;
		Session.RatesProvider = () =>
		{
			calls++;
			return Task.FromResult(Rates());
		};
		var reply = await Session.EvalAsync("2 [usd->thb]");
		Assert.Equal(("result", "64", "ECB 2026-11-05"), (reply.Kind, reply.Text, reply.Note));
		await Session.EvalAsync("3 [usd->thb]");
		Assert.Equal(1, calls);
		Assert.Equal("", (await Session.EvalAsync("1+1")).Note);
	}

	[Fact]
	public async Task NoRatesAvailableIsAnError()
	{
		var none = new JsonObject
		{
			["date"] = null, ["rates"] = new JsonObject(), ["stale"] = true,
			["next_check"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
		};
		Session.RatesProvider = () => Task.FromResult(none);
		var reply = await Session.EvalAsync("2 [usd->thb]");
		Assert.Equal(("error", "ERROR:Currency rates unavailable"), (reply.Kind, reply.Text));
	}

	[Fact]
	public async Task RatesSurviveARestart()
	{
		await Session.SetRatesAsync(Rates(stale: true));
		Session.Client!.KillForTest();
		await Session.EvalAsync("1");
		Session.RatesProvider = () => throw new InvalidOperationException("should not fetch");
		var reply = await Session.EvalAsync("1 [usd->thb]");
		Assert.Equal(("32", "ECB 2026-11-05, offline"), (reply.Text, reply.Note));
	}

	[Fact]
	public async Task ScheduleGivesTotalsRowsAndSeries()
	{
		var reply = await Session.ScheduleAsync(new JsonObject
		{
			["principal"] = "1000", ["annual_rate"] = "12", ["years"] = "1", ["compounding"] = "quarterly",
		});
		Assert.Equal(4, reply.Rows.Count);
		Assert.Equal((4, 1000.0), (reply.PeriodsPerYear, reply.Principal));
		Assert.Equal(reply.FutureValue, reply.Rows[^1].End);
		Assert.Equal(4, reply.Interest.Count);
	}

	[Fact]
	public async Task ScheduleErrorsNameTheField()
	{
		var error = await Assert.ThrowsAsync<EngineRequestException>(() => Session.ScheduleAsync(new JsonObject
		{
			["principal"] = "1000", ["annual_rate"] = "5", ["years"] = "-1",
		}));
		Assert.StartsWith("years:", error.Message);
	}
}
