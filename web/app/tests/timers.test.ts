// timers.test.ts - The stopwatch, timers and alarms the app keeps, as the desktop's TimerStateTests (owner 2026-11-28)

import { describe, expect, it } from "vitest";
import {
	add, addLap, cancel, clock, elapsed, left, newTimers, pause, readout, start, started, stop, takeDue, timersFrom,
} from "../src/lib/timers";

const T0 = Date.UTC(2026, 10, 28, 7, 0, 0);
const at = (seconds: number) => T0 + seconds * 1000;

describe("timers", () =>
{
	it("a stopwatch runs, pauses, laps and stops", () =>
	{
		const s = newTimers();
		expect(stop(s, T0)).toBeNull();
		start(s, T0);
		expect(elapsed(s, at(12.5))).toBeCloseTo(12.5);
		pause(s, at(10));
		expect(elapsed(s, at(99))).toBeCloseTo(10);    // paused: the time stays
		start(s, at(100));
		expect(addLap(s, at(105))).toEqual({ number: 1, seconds: 15, total: 15 });
		expect(addLap(s, at(112))).toEqual({ number: 2, seconds: 7, total: 22 });
		expect(stop(s, at(115))).toBeCloseTo(25);
		expect([started(s), s.laps.length]).toEqual([false, 0]);
	});

	it("timers and alarms come due in order and cancel", () =>
	{
		const s = newTimers();
		add(s, "timer", "5m", "tea", 300, T0);
		add(s, "alarm", "14:30:00", null, 60, T0);
		add(s, "timer", "1h", null, 3600, T0);
		expect(s.countdowns[0].kind).toBe("alarm");    // nearest first
		expect(cancel(s, "timer", "TEA")).toBe(1);       // names ignore case
		expect(takeDue(s, at(61)).length).toBe(1);
		expect(takeDue(s, at(62)).length).toBe(0);
		expect(cancel(s, "timer", null)).toBe(1);
		expect(s.countdowns).toEqual([]);
	});

	it("the readout shows the stopwatch and the nearest", () =>
	{
		const s = newTimers();
		expect(readout(s, T0)).toBe("");
		start(s, T0);
		add(s, "timer", "5m", null, 300, T0);
		add(s, "alarm", "8:00", null, 3600, T0);
		expect(readout(s, at(83.45))).toBe("⏱ 0:01:23.4   ⌛ 3:37 +1");
		pause(s, at(90));
		expect(readout(s, at(95))).toMatch(/^⏸ 0:01:30\.0/);
	});

	it.each([[83.456, 1, "0:01:23.4"], [83.456, 2, "0:01:23.45"], [3725, 1, "1:02:05.0"], [0, 0, "0:00:00"]])(
		"clock(%f, %i) is %s", (seconds, places, want) =>
		{
			expect(clock(seconds, places)).toBe(want);
		});

	it("time left rounds up", () =>
	{
		const countdown = { kind: "timer" as const, label: "", name: null, due: at(3851.2), set: T0 };
		expect(left(countdown, T0)).toBe("1:04:12");
		expect(left(countdown, at(3850.5))).toBe("0:01");
	});

	it("survives storage; anything odd starts afresh", () =>
	{
		const s = newTimers();
		start(s, T0);
		addLap(s, at(3));
		add(s, "timer", "5m", "tea", 300, T0);
		const back = timersFrom(JSON.parse(JSON.stringify(s)));
		expect([elapsed(back, at(10)), back.countdowns[0].name, back.laps.length]).toEqual([10, "tea", 1]);
		expect(timersFrom("junk")).toEqual(newTimers());
		expect(timersFrom({ sound: "loud", countdowns: [{ kind: "x", due: 1 }] })).toEqual(newTimers());
	});
});
