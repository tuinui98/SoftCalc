// graph.ts - The Graph's geometry, without drawing: ranges, grid steps, line runs, zoom and pan, trace hits
//
// The rules follow the desktop's PlotMath.cs and PlotView.Trace.cs, so a
// graph looks and traces the same on a phone. The web app's own part is
// the two-finger gesture: one finger traces (owner 2026-11-19), two move
// and zoom.

export interface Rect
{
	minX: number;
	maxX: number;
	minY: number;
	maxY: number;
}

// A drawing area on the canvas, in CSS pixels
export interface Area
{
	left: number;
	top: number;
	width: number;
	height: number;
}

export interface Point
{
	x: number;
	y: number;
}

export type Mode = "xy" | "polar";

// What the Graph keeps between starts (calc.ts saves it as "graphView")
export interface GraphState
{
	mode: Mode;
	grid: boolean;
	square: boolean;    // 1:1: one unit as long across as up
	shown: string[];    // lines drawn, by name (a function's name or an equation's text)
	selected: string | null;    // drawn thicker, wins a trace tie
	colours: Record<string, string>;    // "#rrggbb" picked for a line; the rest by place
	xy: Rect;
	polar: { minT: number; maxT: number; maxR: number };
}

export const MIN_SPAN = 1e-9;
export const MAX_SPAN = 1e12;

// The desktop's first run (SettingsStore.ForNewUser): degrees, so a full turn each way
export const FIRST_EQUATIONS = ["y=sin(x)", "y=x/360"];

export function firstState(): GraphState
{
	return {
		mode: "xy", grid: true, square: false, shown: [...FIRST_EQUATIONS], selected: FIRST_EQUATIONS[0], colours: {},
		xy: { minX: -360, maxX: 360, minY: -2, maxY: 2 },
		polar: { minT: -Math.PI, maxT: Math.PI, maxR: 10 },
	};
}

// Reset: the start range of the mode, as the desktop's Home
export const RESET_XY: Rect = { minX: -10, maxX: 10, minY: -10, maxY: 10 };
export const RESET_POLAR = { minT: -Math.PI, maxT: Math.PI, maxR: 10 };

// A saved state that may be old or damaged: what is usable, the rest from the first run
export function stateFrom(saved: unknown): GraphState
{
	const first = firstState();
	if (typeof saved !== "object" || saved === null)
	{
		return first;
	}
	const s = saved as Partial<GraphState>;
	const finite = (...values: unknown[]) => values.every(v => typeof v === "number" && Number.isFinite(v));
	const xy = s.xy && finite(s.xy.minX, s.xy.maxX, s.xy.minY, s.xy.maxY) && s.xy.minX < s.xy.maxX && s.xy.minY < s.xy.maxY
		? { ...s.xy } : first.xy;
	const polar = s.polar && finite(s.polar.minT, s.polar.maxT, s.polar.maxR) && s.polar.minT < s.polar.maxT && s.polar.maxR > 0
		? { ...s.polar } : first.polar;
	return {
		mode: s.mode === "polar" ? "polar" : "xy",
		grid: s.grid !== false,
		square: s.square === true,
		shown: Array.isArray(s.shown) ? s.shown.filter(name => typeof name === "string") : first.shown,
		selected: typeof s.selected === "string" ? s.selected : null,
		colours: typeof s.colours === "object" && s.colours !== null ? { ...s.colours } : {},
		xy, polar,
	};
}

export function same(a: string | null | undefined, b: string | null | undefined): boolean
{
	return (a ?? "").toLowerCase() === (b ?? "").toLowerCase() && a !== null && a !== undefined;
}

//==================================================================
//  Colours: the desktop's palette by place (Theme.Series)
//==================================================================
const SERIES = ["#e6781e", "#28a046", "#c83c8c", "#8264d2", "#14a0af", "#b49614"];

// Place 0 takes the page's selection colour (the desktop's Highlight); null means that one
export function lineColour(state: GraphState, graphable: string[], name: string): string | null
{
	const picked = Object.entries(state.colours).find(([key]) => same(key, name));
	if (picked)
	{
		return picked[1];
	}
	const index = graphable.findIndex(item => same(item, name));
	return index <= 0 ? null : SERIES[(index - 1) % SERIES.length];
}

// The colours to pick from (owner 2026-11-19: a short list, no colour wheel): the palette, then six more
export const PICK_COLOURS = [...SERIES, "#d23c3c", "#2f6fd6", "#8c5a32", "#e66eaa", "#3cb48c", "#787878"];

//==================================================================
//  A line renamed or gone: its tick, colour and selection follow (desktop GraphSettings.Rename)
//==================================================================
export function renameLine(state: GraphState, old: string, next: string)
{
	state.shown = state.shown.map(name => same(name, old) ? next : name);
	const colour = Object.entries(state.colours).find(([key]) => same(key, old));
	if (colour)
	{
		state.colours = Object.fromEntries(Object.entries(state.colours).filter(([key]) => !same(key, old)));
		state.colours[next] = colour[1];
	}
	if (same(state.selected, old))
	{
		state.selected = next;
	}
}

export function forgetLine(state: GraphState, name: string)
{
	state.shown = state.shown.filter(item => !same(item, name));
	state.colours = Object.fromEntries(Object.entries(state.colours).filter(([key]) => !same(key, name)));
	if (same(state.selected, name))
	{
		state.selected = state.shown[0] ?? null;
	}
}

// A function for the Graph: one parameter (f(x)=...); its name and parameter, or null
export function functionHead(text: string): { name: string; param: string } | null
{
	const match = /^\s*([A-Za-z_][A-Za-z0-9_]*)\s*\(\s*([A-Za-z_][A-Za-z0-9_]*)\s*\)\s*=/.exec(text);
	return match ? { name: match[1], param: match[2] } : null;
}

// The first of f, g, h, p, q, r, then f1, f2, ... not taken
export function freeName(taken: string[]): string
{
	const used = new Set(taken.map(name => name.toLowerCase()));
	for (const name of ["f", "g", "h", "p", "q", "r"])
	{
		if (!used.has(name))
		{
			return name;
		}
	}
	let n = 1;
	while (used.has("f" + n))
	{
		n++;
	}
	return "f" + n;
}

//==================================================================
//  Mapping, grid and tick labels
//==================================================================
export function toScreen(x: number, y: number, world: Rect, area: Area): Point
{
	return {
		x: area.left + (x - world.minX) / (world.maxX - world.minX) * area.width,
		y: area.top + area.height - (y - world.minY) / (world.maxY - world.minY) * area.height,
	};
}

export function toWorld(at: Point, world: Rect, area: Area): Point
{
	return {
		x: world.minX + (at.x - area.left) / area.width * (world.maxX - world.minX),
		y: world.minY + (area.top + area.height - at.y) / area.height * (world.maxY - world.minY),
	};
}

// A round step (1, 2 or 5 x 10^n) giving at most `count` intervals over span
export function niceStep(span: number, count: number): number
{
	if (!(span > 0) || count < 1)
	{
		return 1;
	}
	const raw = span / count;
	const power = Math.pow(10, Math.floor(Math.log10(raw)));
	for (const m of [1, 2, 5, 10])
	{
		if (m * power >= raw)
		{
			return m * power;
		}
	}
	return 10 * power;
}

// Multiples of step inside [min, max]; none when there would be more than 200
export function gridLines(min: number, max: number, step: number): number[]
{
	const lines: number[] = [];
	if (!(step > 0) || (max - min) / step > 200)
	{
		return lines;
	}
	for (let k = Math.ceil(min / step); k * step <= max + step * 1e-9; k++)
	{
		lines.push(k * step);
	}
	return lines;
}

// Tick text with just enough decimals for the step; no "-0"
export function formatTick(value: number, step: number): string
{
	const decimals = Math.min(12, Math.max(0, Math.ceil(-Math.log10(step) - 1e-9)));
	if (Math.abs(value) < step * 1e-6)
	{
		value = 0;
	}
	const size = Math.max(Math.abs(value), step);
	if (size >= 1e7 || size < 1e-5)
	{
		return exponent(value);
	}
	return value.toFixed(decimals);
}

// C#'s "0.###E+0": 1.5E+7, 2E-6
function exponent(value: number): string
{
	if (value === 0)
	{
		return "0E+0";
	}
	const [mantissa, power] = value.toExponential(3).split("e");
	const trimmed = mantissa.replace(/\.?0+$/, "");
	const p = Number(power);
	return `${trimmed}E${p < 0 ? "-" : "+"}${Math.abs(p)}`;
}

//==================================================================
//  Lines: runs between gaps and poles
//==================================================================
export type Sample = [number, number] | null;

// Breaks at undefined points and at jumps over two view heights (tan's poles)
export function runs(points: Sample[], world: Rect): [number, number][][]
{
	const result: [number, number][][] = [];
	let current: [number, number][] = [];
	const jump = (world.maxY - world.minY) * 2;
	for (const point of points)
	{
		const breakHere = point === null || (current.length > 0 && Math.abs(point[1] - current[current.length - 1][1]) > jump);
		if (breakHere && current.length > 0)
		{
			result.push(current);
			current = [];
		}
		if (point !== null)
		{
			current.push(point);
		}
	}
	if (current.length > 0)
	{
		result.push(current);
	}
	return result;
}

//==================================================================
//  Zoom, pan, fit, 1:1
//==================================================================
// [min, max] scaled by factor around `at`; null when the span leaves the limits
export function zoom(min: number, max: number, at: number, factor: number): [number, number] | null
{
	const span = (max - min) * factor;
	if (!(span >= MIN_SPAN && span <= MAX_SPAN))
	{
		return null;
	}
	const newMin = at - (at - min) * factor;
	return [newMin, newMin + span];
}

// Which way a two-finger pinch zooms, fixed when it starts: along one axis when the fingers
// lie within 20 degrees of it (the desktop's Shift and Ctrl wheel), else both
export type PinchKind = "both" | "x" | "y";

export function pinchKind(a: Point, b: Point, square: boolean): PinchKind
{
	if (square)
	{
		return "both";
	}
	const angle = Math.abs(Math.atan2(b.y - a.y, b.x - a.x)) * 180 / Math.PI;
	const fromFlat = Math.min(angle, 180 - angle);
	return fromFlat <= 20 ? "x" : fromFlat >= 70 ? "y" : "both";
}

// The range two fingers make from where they started: the world point under the start
// midpoint stays under the midpoint now, and the span follows the fingers' distance
export function pinch(start: Rect, area: Area, from: [Point, Point], now: [Point, Point], kind: PinchKind): Rect
{
	const mid = (pair: [Point, Point]) => ({ x: (pair[0].x + pair[1].x) / 2, y: (pair[0].y + pair[1].y) / 2 });
	const m0 = mid(from), m1 = mid(now);
	const spread = (pair: [Point, Point], axis: "x" | "y" | "both") => axis === "both"
		? Math.hypot(pair[1].x - pair[0].x, pair[1].y - pair[0].y)
		: Math.abs(pair[1][axis] - pair[0][axis]);
	const factor = (axis: "x" | "y") =>
	{
		if (kind !== "both" && kind !== axis)
		{
			return 1;
		}
		const d0 = spread(from, kind), d1 = spread(now, kind);
		return d0 > 10 && d1 > 10 ? d0 / d1 : 1;
	};
	const width = start.maxX - start.minX, height = start.maxY - start.minY;
	const fx = clampFactor(width, factor("x")), fy = clampFactor(height, factor("y"));
	const anchor = toWorld(m0, start, area);
	const spanX = width * fx, spanY = height * fy;
	const minX = anchor.x - (m1.x - area.left) / area.width * spanX;
	const minY = anchor.y - (area.top + area.height - m1.y) / area.height * spanY;
	return { minX, maxX: minX + spanX, minY, maxY: minY + spanY };
}

function clampFactor(span: number, factor: number): number
{
	return Math.min(MAX_SPAN / span, Math.max(MIN_SPAN / span, factor));
}

// Y range showing the values: 2nd to 98th percentile (cuts poles), 5% margin
export function fitRange(values: number[]): [number, number] | null
{
	const finite = values.filter(Number.isFinite).sort((a, b) => a - b);
	if (finite.length === 0)
	{
		return null;
	}
	const cut = finite.length >= 50 ? Math.floor(finite.length / 50) : 0;
	const low = finite[cut], high = finite[finite.length - 1 - cut];
	if (high - low < MIN_SPAN * 10)
	{
		const pad = Math.max(1, Math.abs(low) * 0.1);
		return [low - pad, high + pad];
	}
	const margin = (high - low) * 0.05;
	return [low - margin, high + margin];
}

// 1:1 around the centre: the axis with fewer units per pixel widens to the other's
export function square(world: Rect, width: number, height: number): Rect
{
	const per = Math.max((world.maxX - world.minX) / Math.max(1, width), (world.maxY - world.minY) / Math.max(1, height));
	const cx = (world.minX + world.maxX) / 2, cy = (world.minY + world.maxY) / 2;
	return { minX: cx - per * width / 2, maxX: cx + per * width / 2, minY: cy - per * height / 2, maxY: cy + per * height / 2 };
}

// The world rectangle drawn: the XY range, or a square around the pole
export function viewOf(state: GraphState): Rect
{
	return state.mode === "xy" ? state.xy
		: { minX: -state.polar.maxR, maxX: state.polar.maxR, minY: -state.polar.maxR, maxY: state.polar.maxR };
}

//==================================================================
//  Trace: the nearest point on a drawn line
//==================================================================
// A drawn line: a function's samples over [start, end] (x, or theta in polar), or an equation's segments
export interface Series
{
	name: string;
	equation: boolean;
	points: Sample[];
	start: number;
	end: number;
	segments: [number, number, number, number][];
}

// Distance in pixels, the world point, the parameter there (x or theta) and the line's screen direction
export interface Hit
{
	distance: number;
	x: number;
	y: number;
	t: number;
	dx: number;
	dy: number;
}

export function nearest(line: Series, at: Point, world: Rect, area: Area): Hit | null
{
	let best: Hit | null = null;
	const consider = (x1: number, y1: number, x2: number, y2: number, t1: number, t2: number) =>
	{
		const a = toScreen(x1, y1, world, area), b = toScreen(x2, y2, world, area);
		const dx = b.x - a.x, dy = b.y - a.y;
		if (Math.abs(dy) > area.height / 2 || Math.abs(dx) > area.width / 2)
		{
			return;    // a jump (tan's pole), not a line
		}
		const length = dx * dx + dy * dy;
		const u = length === 0 ? 0 : Math.min(1, Math.max(0, ((at.x - a.x) * dx + (at.y - a.y) * dy) / length));
		const px = a.x + u * dx, py = a.y + u * dy;
		const distance = Math.hypot(at.x - px, at.y - py);
		if (best === null || distance < best.distance)
		{
			best = { distance, x: x1 + u * (x2 - x1), y: y1 + u * (y2 - y1), t: t1 + u * (t2 - t1), dx, dy };
		}
	};
	const step = line.points.length > 1 ? (line.end - line.start) / (line.points.length - 1) : 0;
	for (let i = 0; i + 1 < line.points.length; i++)
	{
		const p = line.points[i], q = line.points[i + 1];
		if (p && q)
		{
			consider(p[0], p[1], q[0], q[1], line.start + i * step, line.start + (i + 1) * step);
		}
	}
	for (const [x1, y1, x2, y2] of line.segments)
	{
		consider(x1, y1, x2, y2, 0, 0);
	}
	return best;
}

// What to ask the engine for the line nearest the finger. A function: its x (polar: angle)
// as the status line shows it, sent as text so it is exact. An equation: the coordinate along
// which the line runs is fixed, the other solved near the finger.
export interface TraceTarget
{
	line: string;
	equation: boolean;
	t: number;
	step: number;
	axis: "x" | "y";
	fixed: string;
	guess: number;
	window: number;
}

export const SNAP_PIXELS = 36;    // a finger is wider than the desktop's mouse (20 px)
const STAY_PIXELS = 6;    // the traced line stays until another is nearer by more
const THETA_STEP = 0.001;

export function traceTarget(series: Series[], at: Point, world: Rect, area: Area, mode: Mode,
	selected: string | null, current: string | null): TraceTarget | null
{
	const hits = series
		.map(line => ({ line, hit: nearest(line, at, world, area) }))
		.filter((item): item is { line: Series; hit: Hit } => item.hit !== null && item.hit.distance <= SNAP_PIXELS);
	if (hits.length === 0)
	{
		return null;
	}
	let best = hits.reduce((a, b) => b.hit.distance < a.hit.distance ? b : a);
	const chosen = hits.find(item => same(item.line.name, selected));
	if (chosen && chosen.hit.distance <= best.hit.distance + 1)
	{
		best = chosen;
	}
	const kept = hits.find(item => same(item.line.name, current));
	if (kept && kept.hit.distance <= best.hit.distance + STAY_PIXELS)
	{
		best = kept;
	}
	const { line, hit } = best;
	const pixelX = (world.maxX - world.minX) / area.width, pixelY = (world.maxY - world.minY) / area.height;
	if (line.equation)
	{
		const fixX = Math.abs(hit.dx) >= Math.abs(hit.dy);
		return {
			line: line.name, equation: true, t: 0, step: fixX ? pixelX : pixelY, axis: fixX ? "x" : "y",
			fixed: formatTick(fixX ? hit.x : hit.y, fixX ? pixelX : pixelY),
			guess: fixX ? hit.y : hit.x, window: 6 * (fixX ? pixelY : pixelX),
		};
	}
	const step = mode === "polar" ? THETA_STEP : pixelX;
	const shown = formatTick(mode === "polar" ? hit.t : hit.x, step);
	return { line: line.name, equation: false, t: Number(shown), step, axis: "x", fixed: shown, guess: 0, window: 0 };
}

export function sameTarget(a: TraceTarget | null, b: TraceTarget | null): boolean
{
	return a !== null && b !== null && a.line === b.line && a.t === b.t && a.axis === b.axis && a.fixed === b.fixed;
}
