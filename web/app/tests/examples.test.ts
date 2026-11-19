// examples.test.ts - Graph > Examples, from the shared list (desktop GraphExamplesTests)

import { describe, expect, it } from "vitest";
import { definition, EQUATIONS, exampleName, formLine, FUNCTIONS, usesAngles, withDefaults } from "../src/lib/examples";
import { functionHead } from "../src/lib/graph";

describe("graph examples", () =>
{
	it("are well formed and distinct", () =>
	{
		const all = [...FUNCTIONS, ...EQUATIONS];
		expect(FUNCTIONS.length).toBe(16);
		expect(EQUATIONS.length).toBe(16);
		expect(new Set(all.map(e => e.title)).size).toBe(all.length);
		for (const e of FUNCTIONS)
		{
			expect(functionHead(definition(e, []))).not.toBeNull();
		}
		for (const e of EQUATIONS)
		{
			expect(definition(e, [])).toContain("=");
			expect((e.view.maxX - e.view.minX) / (e.view.maxY - e.view.minY)).toBeCloseTo(4 / 3);
		}
	});

	it("put the defaults in as numbers, whole names only", () =>
	{
		expect(definition(FUNCTIONS[0], [])).toBe("parabola(x)=1*(x-0)^2+0");
		expect(definition(EQUATIONS[0], [])).toBe("(x-0)^2+(y-0)^2=2^2");
		expect(formLine(FUNCTIONS[0])).toBe("parabola(x)=a*(x-h)^2+k");
		expect(withDefaults("abs(x)+a*x+b", [["a", "-1"], ["b", "1/3"]])).toBe("abs(x)+(-1)*x+(1/3)");
		expect(withDefaults("ab+a2", [["a", "-1"]])).toBe("ab+a2");
	});

	it("a taken name gets a number; trig ones are marked", () =>
	{
		expect(exampleName("sinc", ["f"])).toBe("sinc");
		expect(exampleName("sinc", ["SINC", "sinc2"])).toBe("sinc3");
		expect([...FUNCTIONS, ...EQUATIONS].filter(usesAngles).map(e => e.title))
			.toEqual(["Sine", "Cosine", "Tangent", "Sinc", "x sin(1/x)", "Two waves", "Egg crate"]);
	});
});
