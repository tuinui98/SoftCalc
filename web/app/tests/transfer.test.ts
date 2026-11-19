// transfer.test.ts - Export and Import files read and write as the desktop's (HistoryStore.cs, PcfFile.cs)

import { describe, expect, it } from "vitest";
import type { Entry } from "../src/lib/calc";
import { exportNames, fileText, historyText, mergeHistory, newId, parseHistory, pcfBytes, splitGraph, withGraph, withIds } from "../src/lib/transfer";

// Lines as the desktop writes them (System.Text.Json, snake_case, 7-digit fractions)
const DESKTOP = [
	`{"cleared_at":"2026-11-16T11:31:45.1234567Z"}`,
	`{"deleted":"20261118000000000-4242-3","time":"2026-11-18T00:08:00.0000000Z"}`,
	`{"id":"20261115000000000-4242-1","time":"2026-11-15T09:00:00.0000000Z","kind":"result","input":"1+1","display_input":"1+1","text":"2","note":""}`,
	`{"id":"20261118000000000-4242-2","time":"2026-11-18T00:07:34.5000000Z","kind":"result","input":"100 [THB->USD]","display_input":"100 [THB->USD]","text":"2.9","note":"ECB 2026-11-17"}`,
	`{"id":"20261118000000000-4242-3","time":"2026-11-18T00:07:35.0000000Z","kind":"result","input":"3*3","display_input":"3*3","text":"9","note":""}`,
	`not json`,
	`{"id":"20261118000000000-4242-4","time":"2026-11-18T00:07:36.0000000Z","kind":"definition","input":"r=5","display_input":"r=5","text":"Variable Defined","note":""}`,
].join("\r\n");

describe("history.jsonl", () =>
{
	it("reads the desktop's file as the desktop shows it: after the clear, without deleted lines", () =>
	{
		const lines = parseHistory(DESKTOP)!;
		expect(lines.map(entry => entry.input)).toEqual(["100 [THB->USD]", "r=5"]);
		expect(lines[0].note).toBe("ECB 2026-11-17");
		expect(lines[1].note).toBeUndefined();
	});

	it("is not a History file when no line is a JSON object", () =>
	{
		expect(parseHistory("memory = 5\r\nf(x) = x^2\r\n")).toBeNull();
	});

	it("writes the desktop's fields and reads back the same lines; notices stay out", () =>
	{
		const entries = withIds([
			{ kind: "result", input: "6*7", display: "6*7", text: "42" },
			{ kind: "notice", input: "", display: "(some saved definitions were skipped)", text: "1 line(s)" },
			{ kind: "result", input: "1 [BTC->THB]", display: "1 [BTC->THB]", text: "1", note: "CoinGecko" },
		]);
		const text = historyText(entries);
		expect(Object.keys(JSON.parse(text.split("\n")[0]))).toEqual(["id", "time", "kind", "input", "display_input", "text", "note"]);
		expect(parseHistory(text)).toEqual([entries[0], entries[2]]);
	});

	it("merges by id: no twins, in time order, the newest cap", () =>
	{
		const mine = parseHistory(DESKTOP)!;
		const theirs: Entry[] = [
			{ ...mine[0], text: "2.95" },
			{ id: "b", time: "2026-11-17T00:00:00.000Z", kind: "result", input: "5", display: "5", text: "5" },
		];
		const merged = mergeHistory(mine, theirs, 1000);
		expect(merged.map(entry => entry.text)).toEqual(["5", "2.95", "Variable Defined"]);
		expect(mergeHistory(mine, theirs, 2).map(entry => entry.text)).toEqual(["2.95", "Variable Defined"]);
	});

	it("gives old lines ids and times in their order, and new ids never repeat", () =>
	{
		const old = withIds([{ kind: "result", input: "1", display: "1", text: "1" }, { kind: "result", input: "2", display: "2", text: "2" }], 1_000_000);
		expect(old[0].time! < old[1].time!).toBe(true);
		expect(new Set([old[0].id, old[1].id, newId()]).size).toBe(3);
		expect(old[0].id).toMatch(/^\d{17}-\d{6}-\d+$/);
	});
});

describe("export names", () =>
{
	it("carry the local date and time, the same for both files", () =>
	{
		expect(exportNames(new Date(2026, 10, 9, 7, 5, 59))).toEqual(
			{ history: "SoftCalc-history-2026-11-09-0705.jsonl", definitions: "SoftCalc-definitions-2026-11-09-0705.pcf" });
	});
});

describe(".pcf", () =>
{
	it("splits off the [Graph] section and writes it back after the definitions", () =>
	{
		expect(splitGraph("a=1\r\n [graph] \r\ny=x\r\n\r\nx^2+y^2=1")).toEqual({ definitions: "a=1\r\n", graph: ["y=x", "x^2+y^2=1"] });
		expect(splitGraph("a=1\r\n")).toEqual({ definitions: "a=1\r\n", graph: [] });
		expect(withGraph("a=1\r\n\r\n", ["y=x"])).toBe("a=1\r\n[Graph]\r\ny=x\r\n");
		expect(withGraph("", ["y=x"])).toBe("[Graph]\r\ny=x\r\n");
		expect(withGraph("a=1\r\n", [])).toBe("a=1\r\n");
	});

	it("is written UTF-16 LE without a BOM, CRLF, and read back", () =>
	{
		const bytes = pcfBytes("memory=5\nf(x)=x^2 π\n");
		expect([...bytes.subarray(0, 4)]).toEqual([0x6d, 0, 0x65, 0]);
		expect(fileText(bytes)).toBe("memory=5\r\nf(x)=x^2 π\r\n");
	});

	it("reads UTF-8 with or without a BOM, and UTF-16 with a BOM", () =>
	{
		const utf8 = new TextEncoder().encode("x=1 π");
		expect(fileText(utf8)).toBe("x=1 π");
		expect(fileText(new Uint8Array([0xef, 0xbb, 0xbf, ...utf8]))).toBe("x=1 π");
		expect(fileText(new Uint8Array([0xff, 0xfe, ...pcfBytes("y=2")]))).toBe("y=2");
		expect(fileText(new Uint8Array([0xfe, 0xff, 0, 0x7a]))).toBe("z");
	});
});
