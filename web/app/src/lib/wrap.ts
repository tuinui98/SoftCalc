// wrap.ts - A keypad key put around the selection in Input (owner 2026-11-27)
//
// With text selected, a bracket key wraps it ((2+3), [2+3]), a function key
// makes it the argument (sin(2+3)), a power or postfix key brackets it first
// ((2+3)^2, (2+3)!), 10^x and (-) take it as their operand (10^(2+3), -(2+3)).
// A lone number or name, or a selection already in brackets, gets no second
// pair. Without a selection the keys type as before. The desktop's
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
	const put = wrapped(text.slice(start, end), id);
	if (put === null)
	{
		return null;
	}
	return { text: text.slice(0, start) + put + text.slice(end), caret: start + put.length };
}

function wrapped(selected: string, id: string): string | null
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
		return operand(selected) + piece;
	}
	if (BEFORE.includes(piece))
	{
		return piece + operand(selected);
	}
	return null;
}

// The selection as one operand: brackets unless it already is one
function operand(selected: string): string
{
	return simple(selected) ? selected : `(${selected})`;
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
