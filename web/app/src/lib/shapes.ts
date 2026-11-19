// shapes.ts - ⋮ > Random's Advanced tab: values that follow a shape (owner 2026-11-20, R2)
//
// A port of the desktop's Logic/RandomShapes.cs. Each shape draws x from
// 0 to 1 and has a density for the picture. Whole numbers get one slot per
// number (both ends included); decimals are stretched and rounded. Insert
// Command's engine call is built here too (randbeta, randtri, randexp,
// randpeaks), which Seed draws through the engine.

import type { RandomSource } from "./random";

export const MAX_COUNT = 10000;    // enough for a histogram that shows the shape
const PEAKS_SIZE = 20;    // a + b of each hump, as in the engine
export const DEFAULT_STRENGTH = 5;

export type ShapeKind = "beta" | "triangle" | "exponential" | "peaks";
export interface Curve { kind: ShapeKind; p: number; q: number }

// The list (owner 2026-11-12): id, name; Strength is used unless noted
export const SHAPES: [string, string][] = [
	["flat", "Flat"], ["bell", "Bell"], ["narrow", "Narrow bell"], ["right", "Right skew"], ["left", "Left skew"],
	["triangle", "Triangle"], ["u", "U shape"], ["peaks", "Two peaks"], ["rising", "Rising"], ["falling", "Falling"],
	["decay", "Decay"], ["growth", "Growth"], ["custom", "Custom (Beta a, b)"],
];

export const usesStrength = (id: string) => !["flat", "triangle", "custom"].includes(id);

export function hint(id: string): string
{
	switch (id)
	{
		case "flat": return "Every value as likely as any other.";
		case "bell": case "narrow": return "Most values near the middle. Strength: how narrow the bell is.";
		case "right": return "Small values most often, a long tail to the right. Strength: how far it leans.";
		case "left": return "Large values most often, a long tail to the left. Strength: how far it leans.";
		case "triangle": return "Rises in a straight line to Peak at, then falls.";
		case "u": return "Most values near both ends, few in the middle. Strength: how deep the dip is.";
		case "peaks": return "Two humps with a dip between. Strength: how far apart the humps are.";
		case "rising": return "The larger, the more often. Strength: how steep.";
		case "falling": return "The smaller, the more often. Strength: how steep.";
		case "decay": return "Falls away fast from From. Strength: how fast.";
		case "growth": return "Climbs fast towards To. Strength: how fast.";
		default: return "Beta(a, b): a and b above 1 give a hump, below 1 pile up at the ends.";
	}
}

const beta = (a: number, b: number): Curve => ({ kind: "beta", p: a, q: b });
const round = (value: number) => Number(value.toFixed(10));

// The shape's parameters for a Strength of 1 to 10; 5 gives the pictures the owner chose
export function curve(id: string, strength: number, a: number, b: number): Curve
{
	const s = Math.min(10, Math.max(1, strength));
	switch (id)
	{
		case "bell": return beta(round(1 + 0.6 * s), round(1 + 0.6 * s));
		case "narrow": return beta(round(2.4 * s), round(2.4 * s));
		case "right": return beta(2, round(2 + 0.6 * s));
		case "left": return beta(round(2 + 0.6 * s), 2);
		case "triangle": return { kind: "triangle", p: 0, q: 0 };
		case "u": return beta(round((11 - s) / 10), round((11 - s) / 10));
		case "peaks": return { kind: "peaks", p: round(s / 10), q: 0 };
		case "rising": return beta(round(1 + 0.2 * s), 1);
		case "falling": return beta(1, round(1 + 0.2 * s));
		case "decay": return { kind: "exponential", p: round(0.8 * s), q: 0 };
		case "growth": return { kind: "exponential", p: round(-0.8 * s), q: 0 };
		case "custom": return beta(a, b);
		default: return beta(1, 1);
	}
}

export interface Range { from: number; to: number; peak: number; whole: boolean; places: number }

// The range in order, whole numbers cut to whole
function ordered(range: Range): Range
{
	let { from, to, peak } = range;
	if (range.whole)
	{
		[from, to, peak] = [Math.trunc(from), Math.trunc(to), Math.trunc(peak)];
	}
	if (from > to)
	{
		[from, to] = [to, from];
	}
	return { ...range, from, to, peak };
}

// Where the triangle's peak sits on 0-1; whole numbers: the middle of its slot
export function peakAt(range: Range): number
{
	const r = ordered(range);
	if (r.whole)
	{
		return (r.peak - r.from + 0.5) / (r.to - r.from + 1);
	}
	return r.to === r.from ? 0.5 : (r.peak - r.from) / (r.to - r.from);
}

// The range's problem, or null
export function check(c: Curve, range: Range, count: number): string | null
{
	const r = ordered(range);
	if (![r.from, r.to, r.peak].every(Number.isFinite))
	{
		return "From, To and Peak at must be numbers.";
	}
	if (!(count >= 1 && count <= MAX_COUNT))
	{
		return `Count must be 1 to ${MAX_COUNT}.`;
	}
	if (!(range.places >= 0 && range.places <= 10))
	{
		return "Decimal places must be 0 to 10.";
	}
	if (c.kind === "triangle" && (r.peak < r.from || r.peak > r.to))
	{
		return `Peak at must be from ${r.from} to ${r.to}.`;
	}
	if (c.kind === "beta" && !(c.p > 0 && c.q > 0))
	{
		return "a and b must be above 0.";
	}
	return null;
}

// count values as text; check() first
export function values(source: RandomSource, c: Curve, range: Range, count: number): string[]
{
	const r = ordered(range);
	const at = peakAt(range);
	const slots = r.to - r.from + 1;
	const out: string[] = [];
	for (let i = 0; i < count; i++)
	{
		const x = draw(source, c, at);
		out.push(r.whole ? String(r.from + Math.min(Math.floor(x * slots), slots - 1)) : (r.from + x * (r.to - r.from)).toFixed(r.places));
	}
	return out;
}

// Strictly between 0 and 1, 53 random bits
function uniform(source: RandomSource): number
{
	const top = 2n ** 53n;
	return Number(source.below(top - 1n) + 1n) / Number(top);
}

export function draw(source: RandomSource, c: Curve, at: number): number
{
	switch (c.kind)
	{
		case "triangle":
		{
			const u = uniform(source);
			return u < at ? Math.sqrt(u * at) : 1 - Math.sqrt((1 - u) * (1 - at));
		}
		case "exponential":
		{
			const v = uniform(source);
			return c.p === 0 ? v : -Math.log(1 - v * (1 - Math.exp(-c.p))) / c.p;
		}
		case "peaks":
		{
			const middle = source.below(2n) === 0n ? (1 - c.p) / 2 : (1 + c.p) / 2;
			return betaDraw(source, 1 + middle * (PEAKS_SIZE - 2), 1 + (1 - middle) * (PEAKS_SIZE - 2));
		}
		default:
			return betaDraw(source, c.p, c.q);
	}
}

function betaDraw(source: RandomSource, a: number, b: number): number
{
	const x = gamma(source, a);
	return x / (x + gamma(source, b));
}

// Marsaglia and Tsang (2000); alpha below 1 boosted to alpha + 1
function gamma(source: RandomSource, alpha: number): number
{
	if (alpha < 1)
	{
		return gamma(source, alpha + 1) * Math.pow(uniform(source), 1 / alpha);
	}
	const d = alpha - 1 / 3, c = 1 / Math.sqrt(9 * d);
	for (;;)
	{
		const x = Math.sqrt(-2 * Math.log(uniform(source))) * Math.cos(2 * Math.PI * uniform(source));
		let v = 1 + c * x;
		if (v <= 0)
		{
			continue;
		}
		v = v * v * v;
		if (Math.log(uniform(source)) < x * x / 2 + d - d * v + d * Math.log(v))
		{
			return d * v;
		}
	}
}

//==================================================================
//  The picture
//==================================================================
function betaShape(x: number, a: number, b: number): number
{
	return Math.pow(x, a - 1) * Math.pow(1 - x, b - 1);
}

export function density(c: Curve, x: number, at: number): number
{
	switch (c.kind)
	{
		case "triangle":
			return x < at ? (at <= 0 ? 0 : x / at) : (at >= 1 ? 0 : (1 - x) / (1 - at));
		case "exponential":
			return Math.exp(-c.p * x);
		case "peaks":
		{
			// the humps are mirror images, Beta(a, b) and Beta(b, a), so they share a scale
			const a = 1 + (1 - c.p) / 2 * (PEAKS_SIZE - 2), b = PEAKS_SIZE - a;
			return betaShape(x, a, b) + betaShape(x, b, a);
		}
		default:
			return betaShape(x, c.p, c.q);
	}
}

// The density at points + 1 places from 0 to 1, area 1; the ends a little inside, where a U shape is infinite
export function line(c: Curve, at: number, points = 200): number[]
{
	const ys = Array.from({ length: points + 1 }, (_, i) => density(c, Math.min(0.996, Math.max(0.004, i / points)), at));
	const area = ys.reduce((sum, y) => sum + y, 0) / ys.length;
	return area > 0 && Number.isFinite(area) ? ys.map(y => y / area) : ys;
}

// The results as bars over 0-1, scaled like line (area 1): one bar per whole number when there are few, else 40
export function bars(texts: string[], range: Range): number[]
{
	const r = ordered(range);
	const slots = r.whole ? r.to - r.from + 1 : 0;
	const count = r.whole && slots <= 60 ? slots : 40;
	const out = new Array<number>(count).fill(0);
	if (texts.length === 0 || (!r.whole && r.to === r.from))
	{
		return out;
	}
	for (const text of texts)
	{
		const v = Number(text);
		const x = r.whole ? (v - r.from + 0.5) / slots : (v - r.from) / (r.to - r.from);
		out[Math.min(count - 1, Math.max(0, Math.floor(x * count)))]++;
	}
	return out.map(b => b / texts.length * count);
}

// The line under the buttons after Generate
export function summary(texts: string[], places: number): string
{
	const numbers = texts.map(Number);
	const mean = numbers.reduce((sum, v) => sum + v, 0) / numbers.length;
	return `${numbers.length} values: mean ${Number(mean.toFixed(Math.max(2, places)))}, lowest ${Math.min(...numbers)}, highest ${Math.max(...numbers)}`;
}

//==================================================================
//  Insert Command: the engine call that draws one value the same way
//==================================================================
export function shapeCommand(c: Curve, range: Range): string
{
	const r = ordered(range);
	const plain = (value: number) => String(Number(value.toPrecision(15)));
	const start = plain(r.from);
	const span = r.whole ? `${start}, ${plain(r.to)}` : `${start.includes(".") ? start : start + ".0"}, ${plain(r.to)}`;
	const call = c.kind === "triangle" ? `randtri(${plain(r.peak)}, ${span})`
		: c.kind === "exponential" ? `randexp(${plain(c.p)}, ${span})`
		: c.kind === "peaks" ? `randpeaks(${plain(c.p)}, ${span})`
		: `randbeta(${plain(c.p)}, ${plain(c.q)}, ${span})`;
	return r.whole ? call : `round(${call}, ${r.places})`;
}
