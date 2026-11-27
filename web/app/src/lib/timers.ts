// timers.ts - The stopwatch, timers and alarms the app keeps (owner 2026-11-27/28)
//
// The engine reads `:sw`, `:timer`, `:alarm` lines and sends an action
// (engine timers.py); this keeps the clock: one stopwatch with its laps,
// and a list of timers and alarms, each with the moment it is due. Times
// are wall-clock milliseconds, so the state is stored and a running
// stopwatch or a timer survives closing the app. Every function takes
// "now", so the tests need no waiting. The desktop's TimerState.cs is the
// same.

export interface Lap
{
	number: number;
	seconds: number;    // this lap
	total: number;      // since the start
}

export interface Countdown
{
	kind: "timer" | "alarm";
	label: string;          // 5m, 0:15:00, 28.11.2026 14:30:00
	name: string | null;
	due: number;            // ms
	set: number;            // ms, when it was set
}

export const SOUNDS = ["once", "repeat", "off"] as const;

export interface TimerState
{
	before: number;               // seconds measured before the current run
	runningSince: number | null;  // ms; null when stopped or paused
	laps: Lap[];
	countdowns: Countdown[];
	sound: string;                // when one is done (owner 2026-11-28: an option)
}

export function newTimers(): TimerState
{
	return { before: 0, runningSince: null, laps: [], countdowns: [], sound: "once" };
}

// A stored state, checked: anything odd starts afresh
export function timersFrom(stored: unknown): TimerState
{
	const fresh = newTimers();
	if (!stored || typeof stored !== "object")
	{
		return fresh;
	}
	const s = stored as Partial<TimerState>;
	return {
		before: typeof s.before === "number" && s.before >= 0 ? s.before : 0,
		runningSince: typeof s.runningSince === "number" ? s.runningSince : null,
		laps: Array.isArray(s.laps) ? s.laps.filter(lap => typeof lap?.total === "number") : [],
		countdowns: Array.isArray(s.countdowns)
			? s.countdowns.filter(c => typeof c?.due === "number" && (c.kind === "timer" || c.kind === "alarm")) : [],
		sound: SOUNDS.includes(s.sound as typeof SOUNDS[number]) ? s.sound! : "once",
	};
}

export const running = (s: TimerState) => s.runningSince !== null;
export const started = (s: TimerState) => running(s) || s.before > 0 || s.laps.length > 0;
export const active = (s: TimerState) => started(s) || s.countdowns.length > 0;

export function elapsed(s: TimerState, now: number): number
{
	return s.before + (s.runningSince !== null ? Math.max(0, (now - s.runningSince) / 1000) : 0);
}

export function start(s: TimerState, now: number)
{
	s.runningSince ??= now;
}

export function pause(s: TimerState, now: number)
{
	if (s.runningSince !== null)
	{
		s.before = elapsed(s, now);
		s.runningSince = null;
	}
}

export function reset(s: TimerState)
{
	s.before = 0;
	s.runningSince = null;
	s.laps = [];
}

// The time measured, and the stopwatch cleared; null when it never started
export function stop(s: TimerState, now: number): number | null
{
	if (!started(s))
	{
		return null;
	}
	const total = elapsed(s, now);
	reset(s);
	return total;
}

// A new lap, or null when the stopwatch has not started
export function addLap(s: TimerState, now: number): Lap | null
{
	if (!started(s))
	{
		return null;
	}
	const total = elapsed(s, now);
	const lap = { number: s.laps.length + 1, seconds: total - (s.laps.at(-1)?.total ?? 0), total };
	s.laps.push(lap);
	return lap;
}

export function add(s: TimerState, kind: Countdown["kind"], label: string, name: string | null, seconds: number, now: number): Countdown
{
	const countdown = { kind, label, name, due: now + seconds * 1000, set: now };
	s.countdowns.push(countdown);
	s.countdowns.sort((a, b) => a.due - b.due);
	return countdown;
}

// Cancels the timers or alarms of a kind, all or by name; how many went
export function cancel(s: TimerState, kind: string, name: string | null): number
{
	const before = s.countdowns.length;
	s.countdowns = s.countdowns.filter(c => !(c.kind === kind && (name === null || c.name?.toLowerCase() === name.toLowerCase())));
	return before - s.countdowns.length;
}

export function cancelOne(s: TimerState, countdown: Countdown): number
{
	const before = s.countdowns.length;
	s.countdowns = s.countdowns.filter(c => c !== countdown);
	return before - s.countdowns.length;
}

// Those due by now, taken off the list
export function takeDue(s: TimerState, now: number): Countdown[]
{
	const due = s.countdowns.filter(c => c.due <= now);
	s.countdowns = s.countdowns.filter(c => c.due > now);
	return due;
}

// The faint text at the right of the hint line: the stopwatch, the nearest countdown, and how many more
export function readout(s: TimerState, now: number): string
{
	const parts: string[] = [];
	if (started(s))
	{
		parts.push((running(s) ? "⏱ " : "⏸ ") + clock(elapsed(s, now), 1));
	}
	if (s.countdowns.length > 0)
	{
		const next = s.countdowns[0];
		parts.push((next.kind === "alarm" ? "⏰ " : "⌛ ") + left(next, now) + (s.countdowns.length > 1 ? ` +${s.countdowns.length - 1}` : ""));
	}
	return parts.join("   ");
}

// Time left, whole seconds rounded up: 4:12, 1:04:12
export function left(countdown: Countdown, now: number): string
{
	const seconds = Math.ceil(Math.max(0, (countdown.due - now) / 1000));
	const hours = Math.floor(seconds / 3600);
	const minutes = Math.floor(seconds / 60) % 60;
	const rest = String(seconds % 60).padStart(2, "0");
	return hours > 0 ? `${hours}:${String(minutes).padStart(2, "0")}:${rest}` : `${minutes}:${rest}`;
}

// h:mm:ss with places decimals, as the engine writes a duration: 0:01:23.4
export function clock(seconds: number, places: number): string
{
	const scale = 10 ** places;
	const rounded = Math.floor(Math.max(0, seconds) * scale + 1e-9) / scale;
	const hours = Math.floor(rounded / 3600);
	const minutes = Math.floor(rounded / 60) % 60;
	const rest = rounded - hours * 3600 - minutes * 60;
	const text = rest.toFixed(places).padStart(places > 0 ? places + 3 : 2, "0");
	return `${hours}:${String(minutes).padStart(2, "0")}:${text}`;
}

// What a countdown is called in History: Timer 5m "tea"
export function title(countdown: Countdown): string
{
	return `${countdown.kind === "alarm" ? "Alarm" : "Timer"} ${countdown.label}` + (countdown.name !== null ? ` "${countdown.name}"` : "");
}

// The local clock time of a moment: 14:05:00, or with the date for an alarm
export function at(ms: number, withDate: boolean): string
{
	const d = new Date(ms);
	const two = (n: number) => String(n).padStart(2, "0");
	const time = `${two(d.getHours())}:${two(d.getMinutes())}:${two(d.getSeconds())}`;
	return withDate ? `${two(d.getDate())}.${two(d.getMonth() + 1)}.${d.getFullYear()} ${time}` : time;
}
