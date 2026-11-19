// calc.test.ts - The web app's calculator rules (ported from the desktop tests) and its saving
//
// A fake engine answers a few lines by table, so these run without
// Pyodide; the real engine has its own tests (test_engine.mjs).

import { describe, expect, it } from "vitest";
import { Calculator, clearEntry, memoryAdd, stepDecimals, type EngineLike } from "../src/lib/calc";
import type { Answer } from "../src/lib/engine";
import { MemoryStore } from "../src/lib/store";
import { pcfBytes } from "../src/lib/transfer";

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
			{
				// "name=value" lines are variables, "f(x)=..." lines functions
				const lines = this.definitions.split("\n").filter(line => line.includes("="));
				const variables = lines.filter(line => !/^\w+\(/.test(line)).map(line =>
				{
					const [name, value] = line.split("=");
					return { name, value };
				});
				const functions = lines.filter(line => /^\w+\(/.test(line)).map(line => ({ name: line.split("(")[0], source: line }));
				return ok({ variables, functions });
			}
			case "delete":
			{
				const before = this.definitions;
				this.definitions = before.split("\n").filter(line => !line.startsWith(`${args.name}=`) && !line.startsWith(`${args.name}(`)).join("\n");
				return before === this.definitions ? { id: 1, ok: false, error: "no such name" } as Answer : ok({ deleted: "variable" });
			}
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
		if (/^\w+(\([^)]*\))?\s*=/.test(input))
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

	it("a function key types its name and a bracket at the caret (owner 2026-11-19)", async () =>
	{
		const { calc } = await started();
		calc.typed("2*", 2);
		await calc.press("fn:sin");
		expect(calc.text).toBe("2*sin(");
		calc.typed("2*sin(+1", 6);
		await calc.press("fn:cos");
		expect([calc.text, calc.caret]).toEqual(["2*sin(cos(+1", 10]);
	});

	it("2nd ( ) and ans type [ ] and -> for a conversion", async () =>
	{
		const { calc } = await started();
		await press(calc, "5");
		for (const key of [{ id: "ins:(", second: "ins:[" }, { id: "ins:ans", second: "ins:->" }, { id: "ins:)", second: "ins:]" }])
		{
			await calc.press("2nd");
			await calc.press(calc.resolve(key));
		}
		expect(calc.text).toBe("5[->]");
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

	it("Up walks back from the newest; Down past the newest brings the draft back", async () =>
	{
		const calc = await withLines();
		calc.walk(true);
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

	it("History shows while the engine loads; = then waits for it (owner 2026-11-19)", async () =>
	{
		const store = new MemoryStore();
		const first = await started(store);
		await press(first.calc, "6", "*", "7", "=");
		await new Promise(resolve => setTimeout(resolve, 0));

		let load!: (version: string) => void;
		const slow = new FakeEngine();
		(slow as { ready: Promise<string> }).ready = new Promise(resolve => { load = resolve; });
		const calc = new Calculator(slow, store);
		await new Promise(resolve => setTimeout(resolve, 0));
		expect(calc.ready).toBe(false);
		expect(calc.entries.map(entry => entry.input)).toEqual(["6*7"]);

		await press(calc, "1", "+", "2");
		const equals = calc.press("=");
		const twice = calc.press("=");     // a second = while waiting adds nothing
		load("test");
		await Promise.all([equals, twice]);
		expect(calc.entries.map(entry => entry.input)).toEqual(["6*7", "1+2"]);
		expect(calc.entries.at(-1)?.text).toBe("3");
		expect(slow.sent.findIndex(message => message.op === "restore_results"))
			.toBeLessThan(slow.sent.findIndex(message => message.op === "eval"));
	});

	it("Import of a history.jsonl merges by id, saves, and $n counts the merged results", async () =>
	{
		const store = new MemoryStore();
		const { calc, engine } = await started(store);
		await press(calc, "6", "*", "7", "=");
		const mine = calc.historyLines()[0];
		const file = [
			JSON.stringify({ id: mine.id, time: mine.time, kind: "result", input: "6*7", display_input: "6*7", text: "42", note: "" }),
			JSON.stringify({ id: "20200101000000000-1-1", time: "2020-01-01T00:00:00.0000000Z", kind: "result",
				input: "2+3", display_input: "2+3", text: "5", note: "" }),
		].join("\r\n");
		const said = await calc.importFile("history.jsonl", new TextEncoder().encode(file));
		expect(said).toBe("history.jsonl: 1 new History line(s) of 2");
		expect(calc.entries.map(entry => entry.input)).toEqual(["2+3", "6*7"]);
		expect((await store.load()).entries.map(entry => entry.input)).toEqual(["2+3", "6*7"]);
		const restored = engine.sent.filter(message => message.op === "restore_results").at(-1);
		expect(restored?.args.texts).toEqual(["5", "42"]);
		expect(engine.sent.some(message => message.op === "clear_history")).toBe(true);
	});

	it("Import of anything else adds its definitions as a .pcf, keeps the others, and saves", async () =>
	{
		const store = new MemoryStore();
		const { calc, engine } = await started(store);
		calc.typed("tax=7", 5);
		await calc.press("=");
		const said = await calc.importFile("mine.pcf", pcfBytes("rate=5\r\n"));
		expect(engine.sent.findLast(message => message.op === "pcf_import")?.args.text).toBe("tax=7\r\nrate=5\r\n");
		expect(said).toBe("mine.pcf: 2 variable(s) and function(s) now");
		await new Promise(resolve => setTimeout(resolve, 0));
		expect((await store.load()).definitions).toContain("rate=5");
		expect((await store.load()).definitions).toContain("tax=7");
	});

	it("Import keeps a .pcf's [Graph] equations apart from the definitions, without twins", async () =>
	{
		const store = new MemoryStore();
		const { calc, engine } = await started(store);
		const file = "rate=5\r\n[Graph]\r\ny=x\r\nx^2+y^2=4\r\nsin(x)\r\n";
		const said = await calc.importFile("mine.pcf", pcfBytes(file));
		expect(engine.sent.findLast(message => message.op === "pcf_import")?.args.text).toBe("rate=5\r\n");
		expect(said).toBe("mine.pcf: 1 variable(s) and function(s) now, 2 graph equation(s) kept, 1 line(s) skipped");
		await calc.importFile("again.pcf", pcfBytes("[graph]\r\nY=X\r\nr=1\r\n"));
		expect(calc.graph).toEqual(["y=sin(x)", "y=x/360", "y=x", "x^2+y^2=4", "r=1"]);    // after the first run's two
		await new Promise(resolve => setTimeout(resolve, 0));
		expect(await store.get("graph")).toEqual(["y=sin(x)", "y=x/360", "y=x", "x^2+y^2=4", "r=1"]);
	});

	it("Import into an empty list sends the file alone", async () =>
	{
		const { calc, engine } = await started();
		await calc.importFile("mine.pcf", pcfBytes("rate=5\r\n"));
		expect(engine.sent.findLast(message => message.op === "pcf_import")?.args.text).toBe("rate=5\r\n");
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

describe("names and History lines (owner 2026-11-18)", () =>
{
	it("a definition shows in the Variables list; deleting it saves the definitions again", async () =>
	{
		const store = new MemoryStore();
		const { calc } = await started(store);
		calc.typed("rate=5", 6);
		await calc.press("=");
		calc.typed("f(x)=x^2", 8);
		await calc.press("=");
		expect(calc.definitions.variables).toEqual([{ name: "rate", value: "5" }]);
		expect(calc.definitions.functions.map(fn => fn.name)).toEqual(["f"]);
		await calc.deleteName("rate");
		expect(calc.definitions.variables).toEqual([]);
		expect(store.data.definitions).not.toContain("rate=");
	});

	it("a deleted memory is defined again by M+, not added to", async () =>
	{
		const store = new MemoryStore();
		store.data.definitions = "memory=3\n";
		const { calc } = await started(store);
		await calc.deleteName("memory");
		await press(calc, "2", "M+");
		expect(calc.entries.at(-1)?.input).toBe("memory=(2)");
	});

	it("a name from the list goes in at the caret; a function as name(", async () =>
	{
		const { calc } = await started();
		calc.typed("2*", 2);
		await calc.insertName("rate", false);
		expect(calc.text).toBe("2*rate");
		await calc.insertName("f", true);
		expect(calc.text).toBe("2*ratef(");
	});

	it("a History result or line goes in at the caret, a negative result in brackets", async () =>
	{
		const { calc } = await started();
		await press(calc, "5", "=");
		calc.insertText("-5", true);
		expect(calc.text).toBe("(-5)");
		await calc.press("AC");
		calc.insertText("-5", false);
		expect(calc.text).toBe("-5");    // no answer in front: not an operator typed after a result
		calc.typed("2*", 2);
		calc.insertText("1+2", false);
		expect(calc.text).toBe("2*1+2");
	});

	it("deleting a result line tells the engine its $n (1 = newest)", async () =>
	{
		const store = new MemoryStore();
		const { calc, engine } = await started(store);
		for (const line of ["1", "2", "3"])
		{
			calc.typed(line, 1);
			await calc.press("=");
		}
		await calc.deleteEntry(0);
		expect(engine.sent.find(message => message.op === "delete_result")?.args).toEqual({ n: 3 });
		expect(calc.entries.map(entry => entry.input)).toEqual(["2", "3"]);
		await new Promise(resolve => setTimeout(resolve, 0));
		expect(store.data.entries.map(entry => entry.input)).toEqual(["2", "3"]);
	});

	it("deleting a definition line leaves $n alone", async () =>
	{
		const { calc, engine } = await started();
		calc.typed("rate=5", 6);
		await calc.press("=");
		await calc.deleteEntry(0);
		expect(engine.sent.some(message => message.op === "delete_result")).toBe(false);
		expect(calc.entries).toEqual([]);
	});

	it("decimals step from all to 2, then by one within 0 to 30", () =>
	{
		expect(stepDecimals(null, 1)).toBe(2);
		expect(stepDecimals(2, -1)).toBe(1);
		expect(stepDecimals(0, -1)).toBe(0);
		expect(stepDecimals(30, 1)).toBe(30);
	});
});

describe("the Graph's lines (G2, owner 2026-11-19)", () =>
{
	it("an equation is checked, added, renamed and removed without a History line", async () =>
	{
		const { calc, store } = await started();
		expect(await calc.putEquation("x^2", null)).toMatch(/needs '='/);
		expect(await calc.putEquation("f(x)=x", null)).toMatch(/function/);
		expect(await calc.putEquation("Y=SIN(X)", null)).toMatch(/already/);
		expect(await calc.putEquation("x^2+y^2=9", null)).toBeNull();
		expect(await calc.putEquation("x^2+y^2=4", "x^2+y^2=9")).toBeNull();
		expect(calc.graph).toEqual(["y=sin(x)", "y=x/360", "x^2+y^2=4"]);
		calc.removeEquation("y=x/360");
		expect(await store.get("graph")).toEqual(["y=sin(x)", "x^2+y^2=4"]);
		expect(calc.entries).toEqual([]);
	});

	it("a function needs one parameter and a free name; a new name drops the old", async () =>
	{
		const { calc, engine } = await started();
		expect(await calc.putFunction("f(x, y)=x", null)).toMatch(/one parameter/);
		expect(await calc.putFunction("f(x)=x^2", null)).toBeNull();
		expect(await calc.putFunction("f(t)=t", null)).toMatch(/already defined/);
		expect(await calc.putFunction("g(x)=x^3", "f")).toBeNull();
		expect(calc.definitions.functions.map(fn => fn.name)).toEqual(["g"]);
		expect(engine.definitions).not.toContain("f(x)");
		expect(calc.entries).toEqual([]);
	});
});
