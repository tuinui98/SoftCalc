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

// A finished stopwatch run, kept so `:sw *` shows it after a restart (owner 2026-11-28)
export interface StopwatchRun
{
	total: number;
	laps: Lap[];
	pauses: number[];    // the time shown at each pause
	ended: number;       // ms
}

export const SOUNDS = ["once", "repeat", "off"] as const;

// The countdown page's three numbers (owner 2026-11-28): what each shows and its format, kept per kind
export const SHOWS = ["left", "since", "done", "remain", "set", "started", "ends", "clock"] as const;
export const SHOW_NAMES: Record<string, string> = {
	left: "Time left", since: "Since start", done: "% done", remain: "% left", set: "Set length", started: "Started", ends: "Ends", clock: "Clock",
};
export const FORMATS = ["ddd hh:mm:ss.s", "hhh:mm:ss.s", "mmm:ss.s", "ssss.s"] as const;
// a time of day (owner 2026-11-28: the clock beside the end time): started, ends, this device's clock
export const CLOCK_FORMATS = ["HH:mm:ss.s", "HH:mm:ss", "dd.MM.yyyy HH:mm:ss.s"] as const;

export const isClock = (show: string) => show === "started" || show === "ends" || show === "clock";

// The formats a show takes: lengths, or times of day
export function formatsOf(show: string): readonly string[]
{
	return isClock(show) ? CLOCK_FORMATS : FORMATS;
}
export type Slot = [string, string];

export function defaultSlots(kind: string): Slot[]
{
	const format = kind === "alarm" ? "ddd hh:mm:ss.s" : "hhh:mm:ss.s";
	return [["left", format], ["since", format], ["done", format]];
}

function slotsFrom(stored: unknown, kind: string): Slot[]
{
	const good = Array.isArray(stored) && stored.length === 3 && stored.every(slot => Array.isArray(slot) && slot.length === 2
		&& (SHOWS as readonly string[]).includes(slot[0]) && formatsOf(slot[0]).includes(slot[1]));
	return good ? (stored as Slot[]).map(([a, b]) => [a, b] as Slot) : defaultSlots(kind);
}

export interface TimerState
{
	before: number;               // seconds measured before the current run
	runningSince: number | null;  // ms; null when stopped or paused
	laps: Lap[];
	pauses: number[];             // the time shown at each pause of this run
	lastRun: StopwatchRun | null;
	countdowns: Countdown[];
	sound: string;                // when one is done (owner 2026-11-28: an option)
	timerSlots: Slot[];
	alarmSlots: Slot[];
	quietCountdown: boolean;      // no ticks in the last 10 seconds
}

export function newTimers(): TimerState
{
	return { before: 0, runningSince: null, laps: [], pauses: [], lastRun: null, countdowns: [], sound: "once",
		timerSlots: defaultSlots("timer"), alarmSlots: defaultSlots("alarm"), quietCountdown: false };
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
		pauses: Array.isArray(s.pauses) ? s.pauses.filter(p => typeof p === "number") : [],
		lastRun: s.lastRun && typeof s.lastRun === "object" && typeof s.lastRun.total === "number"
			? { total: s.lastRun.total, laps: Array.isArray(s.lastRun.laps) ? s.lastRun.laps : [],
				pauses: Array.isArray(s.lastRun.pauses) ? s.lastRun.pauses : [], ended: Number(s.lastRun.ended) || 0 }
			: null,
		countdowns: Array.isArray(s.countdowns)
			? s.countdowns.filter(c => typeof c?.due === "number" && (c.kind === "timer" || c.kind === "alarm")) : [],
		sound: SOUNDS.includes(s.sound as typeof SOUNDS[number]) ? s.sound! : "once",
		timerSlots: slotsFrom(s.timerSlots, "timer"),
		alarmSlots: slotsFrom(s.alarmSlots, "alarm"),
		quietCountdown: s.quietCountdown === true,
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
		s.pauses.push(s.before);
	}
}

export function reset(s: TimerState)
{
	s.before = 0;
	s.runningSince = null;
	s.laps = [];
	s.pauses = [];
}

// The time measured, and the stopwatch cleared; null when it never started
export function stop(s: TimerState, now: number): number | null
{
	if (!started(s))
	{
		return null;
	}
	const total = elapsed(s, now);
	s.lastRun = { total, laps: [...s.laps], pauses: [...s.pauses], ended: now };
	reset(s);
	return total;
}

// A new lap, or null when the stopwatch is not running (paused: the same time again, owner 2026-11-28)
export function addLap(s: TimerState, now: number): Lap | null
{
	if (!running(s))
	{
		return null;
	}
	const total = elapsed(s, now);
	const lap = { number: s.laps.length + 1, seconds: total - (s.laps.at(-1)?.total ?? 0), total };
	s.laps.push(lap);
	return lap;
}

// A new timer or alarm; a name already in use sets that one again (owner 2026-11-28). changed: it was there.
export function add(s: TimerState, kind: Countdown["kind"], label: string, name: string | null, seconds: number, now: number)
	: { countdown: Countdown; changed: boolean }
{
	const found = name === null ? undefined : s.countdowns.find(c => c.kind === kind && c.name?.toLowerCase() === name.toLowerCase());
	const countdown = found ?? { kind, label, name, due: 0, set: 0 };
	Object.assign(countdown, { label, due: now + seconds * 1000, set: now });
	if (!found)
		s.countdowns.push(countdown);
	s.countdowns.sort((a, b) => a.due - b.due);
	return { countdown, changed: found !== undefined };
}

// A run's laps and pauses in order, as History lists them: ["Lap 1  0:00:12.30", "total 0:00:12.30"]
export function marks(laps: Lap[], pauses: number[]): [string, string][]
{
	return [
		...laps.map(l => ({ total: l.total, order: 0, title: `Lap ${l.number}  ${clock(l.seconds, 2)}`, detail: `total ${clock(l.total, 2)}` })),
		...pauses.map(p => ({ total: p, order: 1, title: `Paused at ${clock(p, 1)}`, detail: "" })),
	].sort((a, b) => a.total - b.total || a.order - b.order).map(m => [m.title, m.detail]);
}

// The hint for a name being typed in quotes: `:timer 5m "te` lists the timers' names (owner 2026-11-28)
export function nameHint(s: TimerState, left: string): string | null
{
	const match = /^\s*:(timer|alarm)\b[^"]*"([^"]*)$/i.exec(left);
	if (!match)
	{
		return null;
	}
	const kind = match[1].toLowerCase();
	const typed = match[2].toLowerCase();
	const names = [...new Set(s.countdowns.filter(c => c.kind === kind && c.name !== null && c.name.toLowerCase().startsWith(typed))
		.map(c => c.name!))];
	if (names.length === 0)
	{
		return `${kind} names: none${typed.length > 0 ? " like that" : " yet"}; a new name starts a new ${kind}`;
	}
	return `${kind} names: ${names.join(", ")}  (one of them sets its time again)`;
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

//==================================================================
//  The countdown page (owner 2026-11-28): its numbers and lines, as TimerState.cs
//==================================================================
const pad = (n: number) => String(n).padStart(2, "0");

// A length in one of FORMATS; up rounds tenths down, a countdown rounds them up (it never shows 0 early)
export function span(seconds: number, format: string, roundUp: boolean): string
{
	const tenths = roundUp ? Math.ceil(Math.max(0, seconds) * 10 - 1e-9) : Math.floor(Math.max(0, seconds) * 10 + 1e-9);
	const whole = Math.floor(tenths / 10), tenth = tenths % 10;
	const s = whole % 60, m = Math.floor(whole / 60) % 60, h = Math.floor(whole / 3600), d = Math.floor(whole / 86400);
	switch (format)
	{
		case "ddd hh:mm:ss.s": return `${d} ${pad(h % 24)}:${pad(m)}:${pad(s)}.${tenth}`;
		case "hhh:mm:ss.s": return `${h}:${pad(m)}:${pad(s)}.${tenth}`;
		case "mmm:ss.s": return `${Math.floor(whole / 60)}:${pad(s)}.${tenth}`;
		default: return `${whole}.${tenth}`;
	}
}

// The Set line: no days or hours when they are 0, at least mm:ss.ss
export function length(seconds: number): string
{
	const hundredths = Math.round(Math.max(0, seconds) * 100);
	const whole = Math.floor(hundredths / 100), hund = pad(hundredths % 100);
	const s = whole % 60, m = Math.floor(whole / 60) % 60, h = Math.floor(whole / 3600) % 24, d = Math.floor(whole / 86400);
	if (d > 0)
		return `${d} ${pad(h)}:${pad(m)}:${pad(s)}.${hund}`;
	return h > 0 ? `${h}:${pad(m)}:${pad(s)}.${hund}` : `${pad(m)}:${pad(s)}.${hund}`;
}

// Local date and time to a tenth: 28.11.2026 14:03:12.4
export function stamp(ms: number): string
{
	return `${at(ms, true)}.${Math.floor(new Date(ms).getMilliseconds() / 100)}`;
}

// One number of the countdown page: T- 0:04:12.3, then T+ once it is past; 15.9 %
export function slot(countdown: Countdown, show: string, format: string, now: number): string
{
	const total = Math.max(0.001, (countdown.due - countdown.set) / 1000);
	const since = (now - countdown.set) / 1000, left = (countdown.due - now) / 1000;
	const done = Math.min(100, Math.max(0, since / total * 100));
	switch (show)
	{
		case "left": return left > 0 ? `T- ${span(left, format, true)}` : `T+ ${span(-left, format, false)}`;
		case "since": return span(since, format, false);
		case "done": return `${done.toFixed(1)} %`;
		case "remain": return `${(100 - done).toFixed(1)} %`;
		case "started": return timeOfDay(countdown.set, format);
		case "ends": return timeOfDay(countdown.due, format);
		case "clock": return timeOfDay(now, format);
		default: return span(total, format, false);
	}
}

// A moment in one of CLOCK_FORMATS, local time: 14:03:12.4
export function timeOfDay(ms: number, format: string): string
{
	const full = stamp(ms);    // dd.MM.yyyy HH:mm:ss.s
	if (format === "dd.MM.yyyy HH:mm:ss.s")
		return full;
	return format === "HH:mm:ss" ? full.slice(11, 19) : full.slice(11);
}
