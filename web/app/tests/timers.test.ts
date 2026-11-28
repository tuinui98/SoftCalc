// timers.test.ts - The stopwatch, timers and alarms the app keeps, as the desktop's TimerStateTests (owner 2026-11-28)

import { describe, expect, it } from "vitest";
import {
	add, addLap, cancel, clock, elapsed, left, marks, nameHint, newTimers, pause, readout, start, started, stop, takeDue, timersFrom,
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

describe("names, runs and the name hint (owner 2026-11-28)", () =>
{
	it("a name in use sets its time again; without a name each is new", () =>
	{
		const s = newTimers();
		const first = add(s, "timer", "5m", "tea", 300, T0);
		const again = add(s, "timer", "10m", "TEA", 600, T0);
		add(s, "alarm", "8:00", "tea", 60, T0);
		add(s, "timer", "1m", null, 60, T0);
		add(s, "timer", "1m", null, 60, T0);
		expect(again.countdown).toBe(first.countdown);
		expect([first.changed, again.changed, again.countdown.label, again.countdown.due]).toEqual([false, true, "10m", at(600)]);
		expect(s.countdowns.filter(c => c.kind === "timer").length).toBe(3);
	});

	it("the last run keeps its laps and pauses in order", () =>
	{
		const s = newTimers();
		expect(s.lastRun).toBeNull();
		start(s, T0);
		addLap(s, at(12.3));
		pause(s, at(20));
		start(s, at(30));
		addLap(s, at(51.5));
		stop(s, at(60));
		expect([s.lastRun!.total, s.lastRun!.laps.length, s.lastRun!.pauses.length, s.pauses.length]).toEqual([50, 2, 1, 0]);
		expect(marks(s.lastRun!.laps, s.lastRun!.pauses).map(m => m[0])).toEqual(["Lap 1  0:00:12.30", "Paused at 0:00:20.0", "Lap 2  0:00:29.20"]);
	});

	it("the name hint lists the names in use", () =>
	{
		const s = newTimers();
		add(s, "timer", "5m", "tea", 300, T0);
		add(s, "timer", "9m", "eggs", 540, T0);
		expect(nameHint(s, ':timer 10m "')).toBe("timer names: tea, eggs  (one of them sets its time again)");
		expect(nameHint(s, ':timer 10m "E')).toMatch(/^timer names: eggs/);
		expect(nameHint(s, ':alarm 7:00 "')).toBe("alarm names: none yet; a new name starts a new alarm");
		expect(nameHint(s, ':timer 10m "tea" ')).toBeNull();
		expect(nameHint(s, ':sw "')).toBeNull();
	});
});
