// exitguard.test.ts - Back on the main screen: the first shows the note, a second soon after leaves

import { describe, expect, it } from "vitest";
import { EXIT_MS, ExitGuard } from "../src/lib/exitguard";

describe("exit guard", () =>
{
	it("leaves on a second Back within the time, asks again after it or after a tap", () =>
	{
		const guard = new ExitGuard();
		let shown = 0;
		guard.onshow = () => { shown++; };
		expect(guard.press(1000)).toBe(false);
		expect(guard.press(1000 + EXIT_MS - 1)).toBe(true);
		expect(guard.press(5000 + EXIT_MS)).toBe(false);
		guard.reset();
		expect(guard.press(5001 + EXIT_MS)).toBe(false);
		expect(shown).toBe(3);
	});
});
