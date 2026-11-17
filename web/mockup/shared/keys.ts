// keys.ts - Keypad layouts, the same keys as the desktop Keypad.cs (owner 2026-11-16)
//
// Basic: 5 x 5, 0 two columns wide, = two rows tall. Scientific: five
// columns modelled on the TI-89; a key with a second meaning shows it
// small (yellow) above its label, and 2nd applies to the next key only.

export interface Key
{
	id: string;
	label: string;
	second?: string;         // id after 2nd
	secondLabel?: string;
	row: number;
	col: number;
	rows?: number;
	cols?: number;
	kind?: "digit" | "op" | "equals" | "mode";
}

const k = (id: string, label: string, row: number, col: number, extra: Partial<Key> = {}): Key =>
	({ id, label, row, col, ...extra });

export const BASIC: Key[] = [
	k("AC", "AC", 0, 0), k("C", "C", 0, 1), k("%", "%", 0, 2), k("/", "÷", 0, 3, { kind: "op" }), k("*", "×", 0, 4, { kind: "op" }),
	k("MR", "MR", 1, 0), k("7", "7", 1, 1, { kind: "digit" }), k("8", "8", 1, 2, { kind: "digit" }), k("9", "9", 1, 3, { kind: "digit" }),
	k("-", "−", 1, 4, { kind: "op" }),
	k("M+", "M+", 2, 0), k("4", "4", 2, 1, { kind: "digit" }), k("5", "5", 2, 2, { kind: "digit" }), k("6", "6", 2, 3, { kind: "digit" }),
	k("+", "+", 2, 4, { kind: "op" }),
	k("M-", "M−", 3, 0), k("1", "1", 3, 1, { kind: "digit" }), k("2", "2", 3, 2, { kind: "digit" }), k("3", "3", 3, 3, { kind: "digit" }),
	k("=", "=", 3, 4, { rows: 2, kind: "equals" }),
	k("MC", "MC", 4, 0), k("0", "0", 4, 1, { cols: 2, kind: "digit" }), k(".", ".", 4, 3, { kind: "digit" }),
];

// Where a key sits in the 5 x 5 grid (CSS grid lines start at 1)
export function keyPlace(key: Key): string
{
	return `grid-row: ${key.row + 1} / span ${key.rows ?? 1}; grid-column: ${key.col + 1} / span ${key.cols ?? 1}`;
}

export function keyClass(key: Key, second: boolean, armed: boolean): string
{
	const names = ["key"];
	if (key.kind)
	{
		names.push(key.kind);
	}
	if (key.id === "2nd" && second)
	{
		names.push("on");
	}
	if (key.second === "sto" && armed)
	{
		names.push("armed");
	}
	if (second && key.second)
	{
		names.push("second-on");
	}
	return names.join(" ");
}

// base and angle keys show the current setting
export function keyLabel(key: Key, base: string, angle: string): string
{
	return key.id === "base" ? `${base} ▸` : key.id === "angle" ? `${angle} ▸` : key.label;
}

const s = (id: string, label: string, second: string | undefined, secondLabel: string | undefined, row: number, col: number,
	kind?: Key["kind"]): Key => ({ id, label, second, secondLabel, row, col, kind });

export const SCIENTIFIC: Key[] = [
	s("fn:sin", "sin", "fn:asin", "asin", 0, 0), s("fn:cos", "cos", "fn:acos", "acos", 0, 1),
	s("fn:tan", "tan", "fn:atan", "atan", 0, 2), s("fn:ln", "ln", "fn:exp", "eˣ", 0, 3),
	s("fn:log10", "log", "ins:10^", "10ˣ", 0, 4),
	s("ins:^2", "x²", "ins:^3", "x³", 1, 0), s("ins:^", "xʸ", "ins:^(1/", "ʸ√x", 1, 1),
	s("fn:sqrt", "√", "ins:^(1/3)", "∛", 1, 2), s("ins:^(-1)", "1/x", "fn:abs", "|x|", 1, 3),
	s("ins:!", "n!", "fn:rand", "rand", 1, 4),
	s("ins:pi", "π", "fn:sinh", "sinh", 2, 0), s("ins:e", "e", "fn:floor", "floor", 2, 1),
	s("ins:E", "EE", "fn:ceil", "ceil", 2, 2), s("ins:-", "(−)", "ins: mod ", "mod", 2, 3),
	s("ins:,", ",", "fn:round", "round", 2, 4),
	s("x", "x", "ins:=", "=", 3, 0), s("y", "y", "ins:f(", "f(", 3, 1), s("ins:(", "(", "sto", "→x", 3, 2),
	s("ins:)", ")", "entry", "entry", 3, 3), s("ins:ans", "ans", undefined, undefined, 3, 4),
	s("base", "dec ▸", undefined, undefined, 4, 0, "mode"), s("angle", "deg ▸", undefined, undefined, 4, 1, "mode"),
	s("func", "func", undefined, undefined, 4, 2, "mode"), s("units", "units", undefined, undefined, 4, 3, "mode"),
	s("2nd", "2nd", undefined, undefined, 4, 4, "mode"),
];
