// TimerStateTests.cs - The stopwatch, timers and alarms the UI keeps (owner 2026-11-28); web/app/tests/timers.test.ts is the same

using SoftCalc.Logic;

namespace SoftCalc.Tests;

public class TimerStateTests
{
	private static readonly DateTime T0 = new(2026, 11, 28, 7, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void StopwatchRunsPausesLapsAndStops()
	{
		var state = new TimerState();
		Assert.Null(state.Stop(T0));
		state.Start(T0);
		Assert.Equal(12.5, state.Elapsed(T0.AddSeconds(12.5)), 6);
		state.Pause(T0.AddSeconds(10));
		Assert.Equal(10, state.Elapsed(T0.AddSeconds(99)), 6);    // paused: the time stays
		Assert.Null(state.AddLap(T0.AddSeconds(99)));               // no lap while paused (owner 2026-11-28)
		state.Start(T0.AddSeconds(100));
		var lap1 = state.AddLap(T0.AddSeconds(105))!;
		var lap2 = state.AddLap(T0.AddSeconds(112))!;
		Assert.Equal((1, 15.0, 15.0), (lap1.Number, lap1.Seconds, lap1.Total));
		Assert.Equal((2, 7.0, 22.0), (lap2.Number, lap2.Seconds, lap2.Total));
		Assert.Equal(25, state.Stop(T0.AddSeconds(115))!.Value, 6);
		Assert.False(state.Started);
		Assert.Empty(state.Laps);
	}

	[Fact]
	public void CountdownsComeDueInOrderAndCancel()
	{
		var state = new TimerState();
		state.Add("timer", "5m", "tea", 300, T0);
		state.Add("alarm", "14:30:00", null, 60, T0);
		state.Add("timer", "1h", null, 3600, T0);
		Assert.Equal("alarm", state.Countdowns[0].Kind);    // nearest first
		Assert.Equal(1, state.Cancel("timer", "TEA"));      // names ignore case
		Assert.Single(state.TakeDue(T0.AddSeconds(61)));
		Assert.Empty(state.TakeDue(T0.AddSeconds(62)));
		Assert.Single(state.Countdowns);
		Assert.Equal(1, state.Cancel("timer", null));
		Assert.False(state.Active);
	}

	[Fact]
	public void ReadoutShowsTheStopwatchAndTheNearest()
	{
		var state = new TimerState();
		Assert.Equal("", state.Readout(T0));
		state.Start(T0);
		state.Add("timer", "5m", null, 300, T0);
		state.Add("alarm", "8:00", null, 3600, T0);
		Assert.Equal("⏱ 0:01:23.4   ⌛ 3:37 +1", state.Readout(T0.AddSeconds(83.45)));
		state.Pause(T0.AddSeconds(90));
		Assert.StartsWith("⏸ 0:01:30.0", state.Readout(T0.AddSeconds(95)));
	}

	[Theory]
	[InlineData(83.456, 1, "0:01:23.4")]
	[InlineData(83.456, 2, "0:01:23.45")]
	[InlineData(3725, 1, "1:02:05.0")]
	[InlineData(0, 0, "0:00:00")]
	public void ClockReadsAsADuration(double seconds, int places, string want)
	{
		Assert.Equal(want, TimerState.Clock(seconds, places));
	}

	[Fact]
	public void LeftRoundsUp()
	{
		var countdown = new Countdown { Due = T0.AddSeconds(3851.2) };
		Assert.Equal("1:04:12", TimerState.Left(countdown, T0));
		Assert.Equal("0:01", TimerState.Left(countdown, T0.AddSeconds(3850.5)));
	}

	[Fact]
	public void ANameInUseSetsItsTimeAgain()    // owner 2026-11-28; without a name each is new
	{
		var state = new TimerState();
		var (first, changed1) = state.Add("timer", "5m", "tea", 300, T0);
		var (again, changed2) = state.Add("timer", "10m", "TEA", 600, T0);
		state.Add("alarm", "8:00", "tea", 60, T0);    // an alarm is another list
		state.Add("timer", "1m", null, 60, T0);
		state.Add("timer", "1m", null, 60, T0);
		Assert.Same(first, again);
		Assert.Equal((false, true), (changed1, changed2));
		Assert.Equal(("10m", T0.AddSeconds(600)), (again.Label, again.Due));
		Assert.Equal(3, state.Of("timer").Count());
	}

	[Fact]
	public void AnAlarmRingsOnTheSecondItNames()    // owner 2026-11-28: 10:50 rang at 10:50:00.7
	{
		var now = new DateTime(2026, 11, 28, 10, 49, 12, 700, DateTimeKind.Local).ToUniversalTime();
		double seconds = TimerState.SecondsTo("28.11.2026 10:50:00", 48, now);
		Assert.Equal(47.3, seconds, 6);
		var state = new TimerState();
		var (alarm, _) = state.Add("alarm", "10:50:00", null, seconds, now);
		Assert.Equal(new DateTime(2026, 11, 28, 10, 50, 0, DateTimeKind.Local), alarm.Due.ToLocalTime());
		Assert.Equal(48, TimerState.SecondsTo("junk", 48, now));
	}

	[Fact]
	public void PausedAtZeroIsStillStarted()    // found by the web test: a pause right after the start
	{
		var state = new TimerState();
		state.Start(T0);
		state.Pause(T0);
		Assert.True(state.Started);
		Assert.Null(state.AddLap(T0));
		Assert.Equal(0, state.Stop(T0));
	}

	[Fact]
	public void LastRunKeepsLapsAndPausesInOrder()
	{
		var state = new TimerState();
		Assert.Null(state.LastRun);
		state.Start(T0);
		state.AddLap(T0.AddSeconds(12.3));
		state.Pause(T0.AddSeconds(20));
		state.Start(T0.AddSeconds(30));
		state.AddLap(T0.AddSeconds(51.5));
		state.Stop(T0.AddSeconds(60));
		var run = state.LastRun!;
		Assert.Equal((50.0, 2, 1), (run.Total, run.Laps.Count, run.Pauses.Count));
		Assert.Empty(state.Pauses);
		Assert.Equal(new[] { "Lap 1  0:00:12.30", "Paused at 0:00:20.0", "Lap 2  0:00:29.20" },
			TimerState.Marks(run.Laps, run.Pauses).Select(m => m.Title));
	}

	[Fact]
	public void NameHintListsTheNamesInUse()
	{
		var state = new TimerState();
		state.Add("timer", "5m", "tea", 300, T0);
		state.Add("timer", "9m", "eggs", 540, T0);
		Assert.Equal("timer names: tea, eggs  (one of them sets its time again)", state.NameHint(":timer 10m \""));
		Assert.StartsWith("timer names: eggs", state.NameHint(":timer 10m \"E"));
		Assert.Equal("alarm names: none yet; a new name starts a new alarm", state.NameHint(":alarm 7:00 \""));
		Assert.Null(state.NameHint(":timer 10m \"tea\" "));    // the quotes are closed
		Assert.Null(state.NameHint(":sw \""));
	}

	[Theory]    // the countdown page's formats (owner 2026-11-28); web timers.test.ts has the same
	[InlineData(93784.56, "ddd hh:mm:ss.s", false, "1 02:03:04.5")]
	[InlineData(93784.56, "hhh:mm:ss.s", false, "26:03:04.5")]
	[InlineData(93784.56, "mmm:ss.s", false, "1563:04.5")]
	[InlineData(93784.56, "ssss.s", false, "93784.5")]
	[InlineData(252.21, "hhh:mm:ss.s", true, "0:04:12.3")]    // a countdown rounds up
	[InlineData(0, "mmm:ss.s", false, "0:00.0")]
	public void SpanFormats(double seconds, string format, bool roundUp, string want)
	{
		Assert.Equal(want, TimerState.Span(seconds, format, roundUp));
	}

	[Theory]
	[InlineData(25, "00:25.00")]
	[InlineData(7500.5, "2:05:00.50")]
	[InlineData(93900, "1 02:05:00.00")]
	public void LengthDropsZeroDaysAndHours(double seconds, string want)
	{
		Assert.Equal(want, TimerState.Length(seconds));
	}

	[Fact]
	public void SlotsCountDownThenOn()
	{
		var countdown = new Countdown { Set = T0, Due = T0.AddSeconds(100) };
		Assert.Equal("T- 0:01:30.0", TimerState.Slot(countdown, "left", "hhh:mm:ss.s", T0.AddSeconds(10)));
		Assert.Equal("T+ 0:00:05.2", TimerState.Slot(countdown, "left", "hhh:mm:ss.s", T0.AddSeconds(105.25)));
		Assert.Equal("0:00:10.0", TimerState.Slot(countdown, "since", "hhh:mm:ss.s", T0.AddSeconds(10)));
		Assert.Equal("25.0 %", TimerState.Slot(countdown, "done", "", T0.AddSeconds(25)));
		Assert.Equal("75.0 %", TimerState.Slot(countdown, "remain", "", T0.AddSeconds(25)));
		Assert.Equal("1:40.0", TimerState.Slot(countdown, "set", "mmm:ss.s", T0.AddSeconds(25)));
		// times of day, local (owner 2026-11-28: the clock beside the end time)
		string ends = T0.AddSeconds(100).ToLocalTime().ToString("HH:mm:ss");
		Assert.Equal(ends + ".0", TimerState.Slot(countdown, "ends", "HH:mm:ss.s", T0));
		Assert.Equal(ends, TimerState.Slot(countdown, "ends", "HH:mm:ss", T0));
		Assert.Equal(T0.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") + ".0", TimerState.Slot(countdown, "started", "dd.MM.yyyy HH:mm:ss.s", T0));
		Assert.Equal(T0.AddSeconds(3.45).ToLocalTime().ToString("HH:mm:ss") + ".4", TimerState.Slot(countdown, "clock", "HH:mm:ss.s", T0.AddSeconds(3.45)));
	}

	[Fact]
	public void SurvivesSettingsJson()
	{
		var state = new TimerState();
		state.Start(T0);
		state.AddLap(T0.AddSeconds(3));
		state.Add("timer", "5m", "tea", 300, T0);
		string json = System.Text.Json.JsonSerializer.Serialize(state);
		var back = System.Text.Json.JsonSerializer.Deserialize<TimerState>(json)!;
		Assert.Equal(10, back.Elapsed(T0.AddSeconds(10)), 6);
		Assert.Equal("tea", back.Countdowns[0].Name);
		Assert.Single(back.Laps);
	}
}
