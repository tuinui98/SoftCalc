// random.test.ts - ⋮ > Random's Simple and Password generators (desktop RandomToolsTests)

import { describe, expect, it } from "vitest";
import {
	coins, command, decimals, dice, integers, kinds, password, poolSize, prefsFrom, firstPrefs, scaled, secure, signed,
	strengthBits, toggleSymbols, unbiased, unscaled, type PasswordOptions, type RandomSource,
} from "../src/lib/random";

// Hands out the given numbers in turn, each below the bound asked for
function fixed(...numbers: number[]): RandomSource
{
	let i = 0;
	return { below: (bound: bigint) => BigInt(numbers[i++ % numbers.length]) % bound };
}

const OPTIONS: PasswordOptions = { length: 12, upper: true, lower: true, digits: true, symbols: false, symbolSet: "", noLookAlikes: false };

describe("random: numbers", () =>
{
	it("drops the uneven top of the 64-bit range", () =>
	{
		const draws = [2n ** 64n - 1n, 7n];
		expect(unbiased(10n, () => draws.shift()!)).toBe(7n);
	});

	it("whole numbers include both ends; no repeats is refused when the range is too small", () =>
	{
		expect(integers(fixed(0, 4), "5", "1", 2, false).values).toEqual(["1", "5"]);
		expect(integers(fixed(0, 0, 1), "1", "2", 2, true).values).toEqual(["1", "2"]);
		expect(integers(fixed(0), "1", "3", 4, true).error).toBe("Only 3 different numbers from 1 to 3.");
		expect(integers(fixed(0), "1.5", "3", 1, false).error).toMatch(/whole/);
	});

	it("decimals step by the places, exactly", () =>
	{
		expect(decimals(fixed(0, 100, 37), "0", "1", 2, 3, ).values).toEqual(["0.00", "1.00", "0.37"]);
		expect(scaled("-12.5", 2)).toBe(-1250n);
		expect(scaled("1.234", 2)).toBeNull();
		expect(unscaled(-5n, 2)).toBe("-0.05");
	});

	it("coins follow the chance; dice count from 1", () =>
	{
		expect(coins(fixed(2999, 3000), 30, 2)).toEqual([true, false]);
		expect(dice(fixed(0, 5), 6, 2)).toEqual([1, 6]);
	});

	it("Insert Command writes the rand() line", () =>
	{
		expect(command("whole", "10", "1", 2, 50, 6)).toBe("rand(1, 10)");
		expect(command("decimal", "1", "10", 2, 50, 6)).toBe("round(rand(1.0, 10), 2)");
		expect(command("coin", "1", "2", 2, 50, 6)).toBe("round(rand())");
		expect(command("coin", "1", "2", 2, 30, 6)).toBe("floor(rand()+0.3)");
		expect(command("dice", "1", "2", 2, 50, 20)).toBe("rand(1, 20)");
	});

	it("the secure source stays below the bound", () =>
	{
		for (let i = 0; i < 200; i++)
		{
			expect(secure.below(3n) < 3n).toBe(true);
		}
	});
});

describe("random: passwords", () =>
{
	it("has one of each kind, length as asked", () =>
	{
		const made = password(secure, OPTIONS).password!;
		expect(made).toHaveLength(12);
		expect(made).toMatch(/[A-Z]/);
		expect(made).toMatch(/[a-z]/);
		expect(made).toMatch(/\d/);
	});

	it("look-alikes and doubles go; strength is length x log2(pool)", () =>
	{
		const options = { ...OPTIONS, upper: false, lower: false, symbols: true, symbolSet: "!!0 ", noLookAlikes: true };
		expect(kinds(options)).toEqual(["23456789", "!"]);
		expect(poolSize(OPTIONS)).toBe(62);
		expect(strengthBits({ ...OPTIONS, length: 2, upper: false, lower: false })).toBeCloseTo(2 * Math.log2(10));
		expect(password(secure, { ...OPTIONS, length: 2 }).error).toBe("Length must be at least 3 for 3 kinds.");
	});

	it("a signature goes in as typed and counts in Length", () =>
	{
		const end = signed(secure, OPTIONS, "Ab", "end").password!;
		expect(end).toHaveLength(12);
		expect(end.endsWith("Ab")).toBe(true);
		expect(signed(secure, OPTIONS, "Ab", "start").password!.startsWith("Ab")).toBe(true);
		expect(signed(secure, { ...OPTIONS, length: 4 }, "Ab", "end").error).toBe("Length must be at least 5: the signature is 2 long.");
	});

	it("a symbol group comes out and goes back at the end", () =>
	{
		expect(toggleSymbols("!@-_+", "-_", false)).toBe("!@+");
		expect(toggleSymbols("!-@", "-_", true)).toBe("!@-_");
	});

	it("saved options keep their types; the rest are the first values", () =>
	{
		expect(prefsFrom({ count: "5", sides: 20, tab: "password" })).toEqual({ ...firstPrefs(), sides: 20, tab: "password" });
	});
});
