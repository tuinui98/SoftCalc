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
