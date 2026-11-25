// hintlines.test.ts - One or two hint lines, as the desktop's HintLinesTests (owner 2026-11-26)

import { describe, expect, it } from "vitest";
import { hintLines, MIN_CHARS, SAMPLE } from "../src/lib/hintlines";

describe("hint lines", () =>
{
	it("takes two lines when the line holds fewer than MIN_CHARS characters", () =>
	{
		expect(hintLines(500, 600)).toBe(1);
		expect(hintLines(500, 500)).toBe(1);
		expect(hintLines(500, 499)).toBe(2);
		expect(SAMPLE.length).toBe(MIN_CHARS);
	});
});
