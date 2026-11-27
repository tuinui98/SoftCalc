// wrap.test.ts - Keypad keys around the selection, the desktop's WrapRulesTests table (owner 2026-11-27)

import { describe, expect, it } from "vitest";
import { wrapSelection } from "../src/lib/wrap";

const WRAPS: [string, string, string][] = [
	["2+3", "ins:(", "(2+3)"], ["2+3", "ins:)", "(2+3)"], ["2+3", "ins:[", "[2+3]"], ["2+3", "ins:]", "[2+3]"],
	["(2+3)", "ins:(", "((2+3))"],
	["2+3", "fn:sin", "sin(2+3)"], ["(2+3)", "fn:sqrt", "sqrt(2+3)"], ["x", "fn:log10", "log10(x)"],
	["2+3", "ins:^2", "(2+3)^2"], ["x", "ins:^2", "x^2"], ["2.5", "ins:^3", "2.5^3"], ["sqrt(2)", "ins:^2", "sqrt(2)^2"],
	["(1)+(2)", "ins:^2", "((1)+(2))^2"], ["[1, 2]", "ins:^2", "[1, 2]^2"], ["-3", "ins:^2", "(-3)^2"],
	["2+3", "ins:^(-1)", "(2+3)^(-1)"], ["2+3", "ins:!", "(2+3)!"], ["2+3", "ins:^(1/3)", "(2+3)^(1/3)"],
	["2+3", "ins:^", "(2+3)^"], ["2+3", "ins:^(1/", "(2+3)^(1/"],
	["2+3", "ins:10^", "10^(2+3)"], ["x", "ins:10^", "10^x"], ["2+3", "ins:-", "-(2+3)"], ["5", "ins:-", "-5"],
];

describe("keys around the selection", () =>
{
	it.each(WRAPS)("%s with %s gives %s, the caret after it", (selected, id, want) =>
	{
		const line = `1*${selected}+4`;
		expect(wrapSelection(line, 2, 2 + selected.length, id)).toEqual({ text: `1*${want}+4`, caret: 2 + want.length });
	});

	it.each(["ins:7", "ins:pi", "ins:,", "ins: mod ", "ins:->", "fn:mod", "fn:+", "x"])("%s does not wrap", id =>
	{
		expect(wrapSelection("2+3", 0, 3, id)).toBeNull();
	});

	it("no selection, no wrap", () =>
	{
		expect(wrapSelection("2+3", 1, 1, "ins:(")).toBeNull();
	});
});
