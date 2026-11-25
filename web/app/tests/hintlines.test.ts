// hintlines.test.ts - One or two hint lines, as the desktop's HintLinesTests (owner 2026-11-26)

import { describe, expect, it } from "vitest";
import { hintLines } from "../src/lib/hintlines";

describe("hint lines", () =>
{
	it("takes two lines when one shows less than three quarters", () =>
	{
		expect(hintLines(400, 500)).toBe(1);
		expect(hintLines(400, 300)).toBe(1);
		expect(hintLines(400, 299)).toBe(2);
		expect(hintLines(0, 100)).toBe(1);
	});
});
