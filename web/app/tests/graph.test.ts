// graph.test.ts - The Graph's geometry: ticks, runs, two-finger zoom and pan, fit, 1:1, trace hits

import { describe, expect, it } from "vitest";
import {
	firstState, fitRange, formatTick, gridLines, lineColour, niceStep, pinch, pinchKind, runs, square, stateFrom,
	toScreen, toWorld, traceTarget, type Area, type Series,
} from "../src/lib/graph";

const AREA: Area = { left: 0, top: 0, width: 400, height: 300 };
const WORLD = { minX: -10, maxX: 10, minY: -5, maxY: 10 };

describe("ticks and grid", () =>
{
	it("steps are 1, 2 or 5 times a power of ten", () =>
	{
		expect(niceStep(20, 8)).toBe(5);
		expect(niceStep(720, 6)).toBe(200);
		expect(niceStep(0.03, 10)).toBeCloseTo(0.005);
	});

	it("labels carry the step's decimals, no -0, and very large or small numbers as E", () =>
	{
		expect(formatTick(0.25, 0.05)).toBe("0.25");
		expect(formatTick(-1e-12, 0.5)).toBe("0.0");
		expect(formatTick(-3, 1)).toBe("-3");
		expect(formatTick(15000000, 5000000)).toBe("1.5E+7");
		expect(formatTick(0.000002, 0.000001)).toBe("2E-6");
	});

	it("grid lines are the step's multiples in range, none when too dense", () =>
	{
		expect(gridLines(-1, 1.2, 0.5)).toEqual([-1, -0.5, 0, 0.5, 1]);
		expect(gridLines(0, 1000, 1)).toEqual([]);
	});

	it("maps world and screen both ways", () =>
	{
		const p = toScreen(0, 0, WORLD, AREA);
		expect(p).toEqual({ x: 200, y: 200 });
		expect(toWorld(p, WORLD, AREA)).toEqual({ x: 0, y: 0 });
	});
});

describe("lines", () =>
{
	it("break at undefined points and at poles", () =>
	{
		const points: ([number, number] | null)[] = [[0, 0], [1, 1], null, [2, 2], [3, 100], [4, 101]];
		expect(runs(points, { minX: 0, maxX: 4, minY: 0, maxY: 10 })).toEqual([[[0, 0], [1, 1]], [[2, 2]], [[3, 100], [4, 101]]]);
	});

	it("colours go by place, the first takes the page's own, a picked one wins", () =>
	{
		const state = firstState();
		const graphable = ["f", "y=sin(x)", "y=x"];
		expect(lineColour(state, graphable, "f")).toBeNull();
		expect(lineColour(state, graphable, "Y=SIN(X)")).toBe("#e6781e");
		state.colours["y=x"] = "#123456";
		expect(lineColour(state, graphable, "y=x")).toBe("#123456");
	});
});

describe("two fingers", () =>
{
	it("moving both pans: the point under them follows", () =>
	{
		const moved = pinch(WORLD, AREA, [{ x: 100, y: 100 }, { x: 200, y: 100 }], [{ x: 140, y: 130 }, { x: 240, y: 130 }], "both");
		const before = toWorld({ x: 150, y: 100 }, WORLD, AREA);
		const after = toWorld({ x: 190, y: 130 }, moved, AREA);
		expect(after.x).toBeCloseTo(before.x);
		expect(after.y).toBeCloseTo(before.y);
		expect(moved.maxX - moved.minX).toBeCloseTo(20);
	});

	it("spreading zooms in around them; along one axis only that axis", () =>
	{
		const both = pinch(WORLD, AREA, [{ x: 150, y: 150 }, { x: 250, y: 150 }], [{ x: 100, y: 150 }, { x: 300, y: 150 }], "both");
		expect(both.maxX - both.minX).toBeCloseTo(10);
		expect(both.maxY - both.minY).toBeCloseTo(7.5);
		const onlyX = pinch(WORLD, AREA, [{ x: 150, y: 150 }, { x: 250, y: 150 }], [{ x: 100, y: 150 }, { x: 300, y: 150 }], "x");
		expect(onlyX.maxX - onlyX.minX).toBeCloseTo(10);
		expect(onlyX.maxY - onlyX.minY).toBeCloseTo(15);
	});

	it("the kind is fixed by the fingers' angle at the start; 1:1 always zooms both", () =>
	{
		expect(pinchKind({ x: 0, y: 0 }, { x: 100, y: 10 }, false)).toBe("x");
		expect(pinchKind({ x: 0, y: 0 }, { x: 10, y: 100 }, false)).toBe("y");
		expect(pinchKind({ x: 0, y: 0 }, { x: 100, y: 100 }, false)).toBe("both");
		expect(pinchKind({ x: 0, y: 0 }, { x: 100, y: 0 }, true)).toBe("both");
	});
});

describe("fit and 1:1", () =>
{
	it("fit leaves out poles and adds a margin", () =>
	{
		const values = [...Array.from({ length: 98 }, (_, i) => i / 97), 1e9, -1e9];
		const [low, high] = fitRange(values)!;
		expect(low).toBeCloseTo(-0.05, 1);
		expect(high).toBeCloseTo(1.05, 1);
		expect(fitRange([NaN])).toBeNull();
	});

	it("1:1 widens the axis with fewer units per pixel, around the centre", () =>
	{
		const shown = square({ minX: -10, maxX: 10, minY: -10, maxY: 10 }, 400, 200);
		expect(shown).toEqual({ minX: -20, maxX: 20, minY: -10, maxY: 10 });
	});
});

describe("trace", () =>
{
	const line: Series = { name: "f", equation: false, points: Array.from({ length: 21 }, (_, i) => [i - 10, i - 10] as [number, number]), start: -10, end: 10, segments: [] };
	const circle: Series = { name: "x^2+y^2=4", equation: true, points: [], start: 0, end: 0, segments: [[-2, 0, -1, 1.7]] };

	it("a function's x is the shown text, rounded to a pixel", () =>
	{
		const at = toScreen(2.3456, 2.3456, WORLD, AREA);
		const target = traceTarget([line], { x: at.x, y: at.y + 5 }, WORLD, AREA, "xy", null, null)!;
		expect(target.line).toBe("f");
		expect(target.fixed).toMatch(/^2\.\d\d$/);
		expect(target.t).toBe(Number(target.fixed));
	});

	it("nothing near the finger traces nothing; an equation fixes the axis it runs along", () =>
	{
		expect(traceTarget([line], { x: 10, y: 290 }, WORLD, AREA, "xy", null, null)).toBeNull();
		const at = toScreen(-1.5, 0.85, WORLD, AREA);
		const target = traceTarget([circle], at, WORLD, AREA, "xy", null, null)!;
		expect(target.equation).toBe(true);
		expect(target.axis).toBe("y");    // steep on screen: y is fixed, x solved
	});
});

describe("saved state", () =>
{
	it("keeps what is usable and takes the first run's for the rest", () =>
	{
		const back = stateFrom({ mode: "polar", xy: { minX: 5, maxX: 1, minY: 0, maxY: 1 }, shown: ["f", 3], colours: null });
		expect(back.mode).toBe("polar");
		expect(back.xy).toEqual(firstState().xy);
		expect(back.shown).toEqual(["f"]);
		expect(back.colours).toEqual({});
		expect(stateFrom("junk")).toEqual(firstState());
	});
});
