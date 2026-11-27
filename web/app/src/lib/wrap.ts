// wrap.ts - A keypad key put around the selection in Input (owner 2026-11-27)
//
// With text selected, a bracket key wraps it ((2+3), [2+3]), a function key
// makes it the argument (sin(2+3)), a power or postfix key brackets it first
// ((2+3)^2, (2+3)!), 10^x and (-) take it as their operand (10^(2+3), -(2+3)).
// A lone number or name, or a selection already in brackets, gets no second
// pair; part of a number or name always does, apart from its neighbours
// (6 of 256 with x² gives 25(6)^2). Without a selection the keys type as before. The desktop's
// WrapRules.cs is the same rule.

const AFTER = ["^2", "^3", "^(-1)", "!", "^(1/3)", "^", "^(1/"];    // keys that follow their operand
const BEFORE = ["10^", "-"];                                          // keys that lead it
const NAME = /^[A-Za-z_][A-Za-z0-9_]*$/;
const ATOM = /^([A-Za-z_][A-Za-z0-9_]*|\d+(\.\d*)?|\.\d+|0x[0-9A-Fa-f]+|0b[01]+|0o[0-7]+)$/;
const CALL = /^[A-Za-z_][A-Za-z0-9_]*(?=\()/;

// The line and caret after the key wraps the selection, or null when it does not
export function wrapSelection(text: string, start: number, end: number, id: string): { text: string; caret: number } | null
{
	if (end <= start || start < 0 || end > text.length)
	{
		return null;
	}
	const left = leftKind(text, start);
	const right = end < text.length && WORD.test(text[end]);
	let put = wrapped(text.slice(start, end), id, left !== "" || right);
	if (put === null)
	{
		return null;
	}
	// Part of a number or name (owner 2026-11-28: 6 of 256 with x² is 25(6)^2): keep it apart from its neighbours
	if (left === "name")
	{
		put = "*" + put;                 // x6 -> x*(6)^2, not a longer name or a call
	}
	else if (left === "number" && !/^[A-Za-z(\[]/.test(put))
	{
		put = `(${put})`;                // 25 then 10^6 or -6 -> 25(10^6), 25(-6)
	}
	if (right)
	{
		put += "*";                      // 5 of 256 -> 2(5)^2*6: nothing multiplies a number after )
	}
	return { text: text.slice(0, start) + put + text.slice(end), caret: start + put.length };
}

const WORD = /[A-Za-z0-9_.]/;

// What the selection is glued to on its left: part of a name, of a number, or nothing
function leftKind(text: string, start: number): "" | "name" | "number"
{
	let i = start;
	while (i > 0 && WORD.test(text[i - 1]))
	{
		i--;
	}
	if (i === start)
	{
		return "";
	}
	return /[A-Za-z_]/.test(text[i]) ? "name" : "number";
}

function wrapped(selected: string, id: string, glued: boolean): string | null
{
	if (id.startsWith("fn:"))
	{
		const name = id.slice(3);
		if (!NAME.test(name) || name.toLowerCase() === "mod")
		{
			return null;    // mod and the operators go in as before
		}
		return name + (enclosed(selected, "(", ")") ? selected : `(${selected})`);
	}
	if (!id.startsWith("ins:"))
	{
		return null;
	}
	const piece = id.slice(4);
	if (piece === "(" || piece === ")")
	{
		return `(${selected})`;
	}
	if (piece === "[" || piece === "]")
	{
		return `[${selected}]`;
	}
	if (AFTER.includes(piece))
	{
		return operand(selected, glued) + piece;
	}
	if (BEFORE.includes(piece))
	{
		return piece + operand(selected, false);    // 10^ or - stands before it already
	}
	return null;
}

// The selection as one operand: brackets unless it already is one and stands alone
function operand(selected: string, glued: boolean): string
{
	return simple(selected) && !glued ? selected : `(${selected})`;
}

// A number, a name, a call, or a whole bracketed or listed part: needs no brackets
export function simple(selected: string): boolean
{
	const s = selected.trim();
	if (ATOM.test(s) || enclosed(s, "(", ")") || enclosed(s, "[", "]"))
	{
		return true;
	}
	const call = CALL.exec(s);
	return call !== null && enclosed(s.slice(call[0].length), "(", ")");
}

// True when the first character opens the pair that the last one closes
function enclosed(s: string, open: string, close: string): boolean
{
	if (s.length < 2 || s[0] !== open || s[s.length - 1] !== close)
	{
		return false;
	}
	let depth = 0;
	for (let i = 0; i < s.length; i++)
	{
		if (s[i] === open)
		{
			depth++;
		}
		else if (s[i] === close)
		{
			depth--;
			if (depth === 0 && i < s.length - 1)
			{
				return false;    // (1)+(2): the first pair closes early
			}
		}
	}
	return depth === 0;
}
