// shapes.test.ts - ⋮ > Random's Advanced tab (desktop RandomShapesTests)

import { describe, expect, it } from "vitest";
import { secure } from "../src/lib/random";
import { bars, check, curve, line, peakAt, shapeCommand, summary, values } from "../src/lib/shapes";

const WHOLE = { from: 1, to: 10, peak: 3, whole: true, places: 2 };

describe("random shapes", () =>
{
	it("Strength 5 gives the pictures the owner chose", () =>
	{
		expect(curve("bell", 5, 0, 0)).toEqual({ kind: "beta", p: 4, q: 4 });
		expect(curve("right", 5, 0, 0)).toEqual({ kind: "beta", p: 2, q: 5 });
		expect(curve("u", 5, 0, 0)).toEqual({ kind: "beta", p: 0.6, q: 0.6 });
		expect(curve("peaks", 5, 0, 0)).toEqual({ kind: "peaks", p: 0.5, q: 0 });
		expect(curve("growth", 5, 0, 0)).toEqual({ kind: "exponential", p: -4, q: 0 });
	});

	it("values stay in range, a whole range takes both ends", () =>
	{
		for (const id of ["flat", "bell", "u", "peaks", "decay", "triangle"])
		{
			const drawn = values(secure, curve(id, 5, 0, 0), WHOLE, 500).map(Number);
			expect(Math.min(...drawn)).toBeGreaterThanOrEqual(1);
			expect(Math.max(...drawn)).toBeLessThanOrEqual(10);
		}
		const flat = new Set(values(secure, curve("flat", 5, 0, 0), WHOLE, 2000));
		expect(flat.size).toBe(10);
		expect(values(secure, curve("bell", 5, 0, 0), { ...WHOLE, whole: false }, 3).every(text => /^\d+\.\d\d$/.test(text))).toBe(true);
	});

	it("checks the peak and Custom's a and b", () =>
	{
		expect(check(curve("triangle", 5, 0, 0), { ...WHOLE, peak: 11 }, 10)).toBe("Peak at must be from 1 to 10.");
		expect(check(curve("custom", 5, 0, 2), WHOLE, 10)).toBe("a and b must be above 0.");
		expect(check(curve("bell", 5, 0, 0), WHOLE, 10001)).toBe("Count must be 1 to 10000.");
		expect(peakAt(WHOLE)).toBeCloseTo(2.5 / 10);
	});

	it("the picture's line and bars have area 1", () =>
	{
		const ys = line(curve("bell", 5, 0, 0), 0.5);
		expect(ys.reduce((a, b) => a + b, 0) / ys.length).toBeCloseTo(1);
		const histogram = bars(["1", "1", "10"], WHOLE);
		expect(histogram).toHaveLength(10);
		expect(histogram[0]).toBeCloseTo(2 / 3 * 10);
	});

	it("Insert Command is the engine call", () =>
	{
		expect(shapeCommand(curve("bell", 5, 0, 0), WHOLE)).toBe("randbeta(4, 4, 1, 10)");
		expect(shapeCommand(curve("triangle", 5, 0, 0), { ...WHOLE, whole: false })).toBe("round(randtri(3, 1.0, 10), 2)");
		expect(shapeCommand(curve("decay", 5, 0, 0), WHOLE)).toBe("randexp(4, 1, 10)");
		expect(summary(["1", "2", "6"], 0)).toBe("3 values: mean 3, lowest 1, highest 6");
	});
});
