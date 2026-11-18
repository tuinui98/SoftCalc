// calc.test.ts - The web app's calculator rules (ported from the desktop tests) and its saving
//
// A fake engine answers a few lines by table, so these run without
// Pyodide; the real engine has its own tests (test_engine.mjs).

import { describe, expect, it } from "vitest";
import { Calculator, clearEntry, memoryAdd, type EngineLike } from "../src/lib/calc";
import type { Answer } from "../src/lib/engine";
import { MemoryStore } from "../src/lib/store";

class FakeEngine implements EngineLike
{
	readonly ready = Promise.resolve("test");
	sent: { op: string; args: Record<string, unknown> }[] = [];
	settings: Record<string, unknown> = { precision: 32, angle: "rad", base: "dec", rpn: false };
	definitions = "";

	async send(op: string, args: Record<string, unknown> = {}): Promise<Answer>
	{
		this.sent.push({ op, args });
		const ok = (more: Record<string, unknown> = {}) => ({ id: 1, ok: true, ...more }) as Answer;
		switch (op)
		{
			case "settings":
				Object.assign(this.settings, args);
				return ok(this.settings);
			case "catalog":
				return ok({ functions: { Trig: ["sin"] }, conversions: { Length: ["m", "km"] } });
			case "state":
				return ok({ variables: this.definitions.includes("memory") ? [{ name: "memory" }] : [] });
			case "pcf_export":
				return ok({ text: this.definitions });
			case "pcf_import":
				this.definitions = String(args.text);
				return ok({ skipped: [] });
			case "menu_insert":
			{
				const text = String(args.text), caret = Number(args.caret), piece = `${args.name}()`;
				return ok({ text: text.slice(0, caret) + piece + text.slice(caret), caret: caret + piece.length - 1 });
			}
			case "eval":
				return ok(this.evaluate(String(args.input)));
			case "hint":
			{
				const left = String(args.text).slice(0, Number(args.caret));
				return ok({ text: left.length === 0 ? null : `${this.settings.base}:${left}` });
			}
			default:
				return ok();
		}
	}

	private evaluate(input: string): Record<string, unknown>
	{
		if (/^\w+\s*=/.test(input))
		{
			this.definitions += input + "\n";
			return { kind: "definition", input, display_input: input, text: "Variable Defined" };
		}
		if (/^[\d+\-*/(). ]+$/.test(input))
		{
			return { kind: "result", input, display_input: input, text: String(Function(`return (${input})`)()) };
		}
		return { kind: "error", input, display_input: input, text: "ERROR:Unknown" };
	}
}

async function started(store = new MemoryStore(), engine = new FakeEngine())
{
	const calc = new Calculator(engine, store);
	await calc.started;
	return { calc, engine, store };
}

async function press(calc: Calculator, ...ids: string[])
{
	for (const id of ids)
	{
		await calc.press(id);
	}
}

describe("keypad rules (desktop KeypadRulesTests, InputRulesTests)", () =>
{
	it.each([
		["12+345", "12+"], ["12+", "12"], ["sin(30", "sin("], ["2*pi ", "2*"], ["3.25", ""], ["", ""],
	])("C on %j leaves %j", (text, expected) =>
	{
		expect(clearEntry(text)).toBe(expected);
	});

	it.each([
		["5", false, false, "memory=(5)"], ["5", true, false, "memory=memory+(5)"],
		["2*3", true, true, "memory=memory-(2*3)"], ["", true, false, "memory=memory+answer"],
		["  ", false, true, "memory=-answer"],
	] as const)("M+/M- on %j (memory %s, minus %s) is %j", (input, has, minus, expected) =>
	{
		expect(memoryAdd(input, has, minus)).toBe(expected);
	});
});

describe("keys", () =>
{
	it("types digits and operators, = calculates and clears Input", async () =>
	{
		const { calc } = await started();
		await press(calc, "1", "2", "+", "3", "=");
		expect(calc.entries.at(-1)).toMatchObject({ kind: "result", input: "12+3", text: "15" });
		expect(calc.text).toBe("");
	});

	it("an operator right after a result starts with answer", async () =>
	{
		const { calc } = await started();
		await press(calc, "2", "=", "*");
		expect(calc.text).toBe("answer*");
	});

	it("= on an empty Input repeats the last line", async () =>
	{
		const { calc } = await started();
		await press(calc, "4", "=", "=");
		expect(calc.entries.map(entry => entry.input)).toEqual(["4", "4"]);
	});

	it("an error keeps the line in Input", async () =>
	{
		const { calc } = await started();
		calc.typed("foo", 3);
		await calc.press("=");
		expect(calc.entries.at(-1)?.kind).toBe("error");
		expect(calc.text).toBe("foo");
	});

	it("AC clears, C takes the last number, % appends, ⌫ takes one character", async () =>
	{
		const { calc } = await started();
		await press(calc, "1", "2", "+", "3", "4", "C");
		expect(calc.text).toBe("12+");
		await press(calc, "5", "%");
		expect(calc.text).toBe("12+5%");
		calc.backspace();
		expect(calc.text).toBe("12+5");
		await calc.press("AC");
		expect(calc.text).toBe("");
	});

	it("2nd gives the second meaning of the next key only", async () =>
	{
		const { calc } = await started();
		await calc.press("2nd");
		expect(calc.resolve({ id: "fn:sin", second: "fn:asin" })).toBe("fn:asin");
		expect(calc.second).toBe(false);
		expect(calc.resolve({ id: "fn:sin", second: "fn:asin" })).toBe("fn:sin");
	});

	it("a function key goes through menu_insert, the caret inside the brackets", async () =>
	{
		const { calc } = await started();
		await calc.press("fn:sin");
		expect(calc.text).toBe("sin()");
		expect(calc.caret).toBe(4);
	});

	it("STO then x stores the line (an empty line stores ans)", async () =>
	{
		const { calc } = await started();
		await press(calc, "5", "*", "3", "sto", "x");
		expect(calc.entries.at(-1)?.input).toBe("x=5*3");
		await press(calc, "sto", "y");
		expect(calc.entries.at(-1)?.input).toBe("y=ans");
	});

	it("M+ defines memory, then adds to it", async () =>
	{
		const { calc } = await started();
		await press(calc, "5", "M+");
		expect(calc.entries.at(-1)?.input).toBe("memory=(5)");
		await press(calc, "2", "M+");
		expect(calc.entries.at(-1)?.input).toBe("memory=memory+(2)");
	});

	it("the base key steps dec, hex, oct, bin and keeps the line", async () =>
	{
		const { calc, engine } = await started();
		await press(calc, "1", "5", "base");
		expect(calc.settings.base).toBe("hex");
		expect(calc.text).toBe("15");
		expect(calc.entries.at(-1)?.input).toBe("15");
		await press(calc, "base", "base", "base");
		expect(engine.settings.base).toBe("dec");
	});
});

describe("History walk (desktop InputRecallTests, owner 2026-11-16)", () =>
{
	async function withLines()
	{
		const { calc } = await started();
		for (const line of ["10*1", "10*2", "10*3"])
		{
			calc.typed(line, line.length);
			await calc.press("=");
		}
		calc.typed("draft", 5);
		return calc;
	}

	it("entry (Up) walks back from the newest; Down past the newest brings the draft back", async () =>
	{
		const calc = await withLines();
		await calc.press("entry");
		expect([calc.text, calc.highlight]).toEqual(["10*3", 2]);
		calc.walk(true);
		expect([calc.text, calc.highlight]).toEqual(["10*2", 1]);
		calc.walk(false);
		calc.walk(false);
		expect([calc.text, calc.highlight]).toEqual(["draft", -1]);
	});

	it("a tap recalls the line and Up goes on from it; a second tap keeps the line, drops the highlight", async () =>
	{
		const calc = await withLines();
		calc.tapEntry(1);
		expect([calc.text, calc.highlight]).toEqual(["10*2", 1]);
		calc.walk(true);
		expect([calc.text, calc.highlight]).toEqual(["10*1", 0]);
		calc.tapEntry(0);
		expect([calc.text, calc.highlight]).toEqual(["10*1", -1]);
	});

	it("Esc brings back the draft", async () =>
	{
		const calc = await withLines();
		calc.tapEntry(2);
		calc.cancelWalk();
		expect([calc.text, calc.highlight]).toEqual(["draft", -1]);
	});

	it("typing ends the walk", async () =>
	{
		const calc = await withLines();
		calc.tapEntry(0);
		calc.typed("10*1+", 5);
		expect(calc.highlight).toBe(-1);
	});
});

describe("saving between visits", () =>
{
	it("History, settings and definitions come back, results as $n", async () =>
	{
		const store = new MemoryStore();
		const first = await started(store);
		await press(first.calc, "6", "*", "7", "=");
		first.calc.typed("rate=5", 6);
		await first.calc.press("=");
		await first.calc.setSetting("precision", 64);
		await new Promise(resolve => setTimeout(resolve, 0));

		const again = await started(store);
		expect(again.calc.entries.map(entry => entry.input)).toEqual(["6*7", "rate=5"]);
		expect(again.calc.settings.precision).toBe(64);
		expect(again.engine.definitions).toContain("rate=5");
		const restored = again.engine.sent.find(message => message.op === "restore_results");
		expect(restored?.args.texts).toEqual(["42"]);
	});

	it("the angle starts in degrees when nothing was saved", async () =>
	{
		const { calc } = await started();
		expect(calc.settings.angle).toBe("deg");
	});

	it("Clear History empties the saved History", async () =>
	{
		const store = new MemoryStore();
		const { calc } = await started(store);
		await press(calc, "1", "=");
		calc.clearHistory();
		await new Promise(resolve => setTimeout(resolve, 0));
		expect(store.data.entries).toEqual([]);
	});

	it("a memory saved last time is added to, not replaced", async () =>
	{
		const store = new MemoryStore();
		store.data.definitions = "memory=3\n";
		const { calc } = await started(store);
		await press(calc, "2", "M+");
		expect(calc.entries.at(-1)?.input).toBe("memory=memory+(2)");
	});
});

describe("hint line (engine `hint`, owner 2026-11-18)", () =>
{
	const settle = () => new Promise(resolve => setTimeout(resolve, 0));

	it("follows the text and the caret, and is empty for no text", async () =>
	{
		const { calc } = await started();
		await press(calc, "1", "2");
		await settle();
		expect(calc.hint).toBe("dec:12");
		calc.moveCaret(1);
		await settle();
		expect(calc.hint).toBe("dec:1");
		await calc.press("AC");
		await settle();
		expect(calc.hint).toBe("");
	});

	it("asks once per text and caret", async () =>
	{
		const { calc, engine } = await started();
		calc.typed("sq", 2);
		calc.moveCaret(2);
		await settle();
		expect(engine.sent.filter(message => message.op === "hint")).toHaveLength(1);
	});

	it("asks again in a new base, for the same line", async () =>
	{
		const { calc } = await started();
		await press(calc, "1", "5", "base");
		await settle();
		expect(calc.hint).toBe("hex:15");
	});

	it("an older reply does not overwrite a newer one", async () =>
	{
		const { calc } = await started();
		calc.typed("1", 1);
		calc.typed("12", 2);
		await settle();
		expect(calc.hint).toBe("dec:12");
	});

	it("Show Input Hints off empties the line and stops asking", async () =>
	{
		const { calc, engine } = await started();
		calc.typed("12", 2);
		await settle();
		calc.setShowHints(false);
		calc.typed("123", 3);
		await settle();
		expect(calc.hint).toBe("");
		expect(engine.sent.filter(message => message.op === "hint")).toHaveLength(1);
	});
});
