// examples.ts - Graph > Examples: the desktop's classic curves (data/graph-examples.json, shared)
//
// Each has a form with constants, a note on what they change and their
// defaults. Use opens the editor with the defaults written in as numbers,
// so no variables are needed (desktop Logic/GraphExamples.cs).

import data from "../../../../data/graph-examples.json";
import type { Rect } from "./graph";

export interface Example
{
	title: string;
	name: string;     // a function's name; "" for an equation
	form: string;
	note: string;
	defaults: [string, string][];
	view: Rect;       // the thumbnail's range
}

function constants(text: string): [string, string][]
{
	return text.split(" ").filter(pair => pair.length > 0).map(pair => pair.split("=") as [string, string]);
}

// Equations get a 4:3 view around the origin, so circles stay round
function square(half: number): Rect
{
	return { minX: -half * 4 / 3, maxX: half * 4 / 3, minY: -half, maxY: half };
}

export const FUNCTIONS: Example[] = data.functions.map(item => ({
	title: item.title, name: item.name, form: item.form, note: item.note, defaults: constants(item.defaults),
	view: { minX: item.view[0], maxX: item.view[1], minY: item.view[2], maxY: item.view[3] },
}));

export const EQUATIONS: Example[] = data.equations.map(item => ({
	title: item.title, name: "", form: item.form, note: item.note, defaults: constants(item.defaults), view: square(item.half),
}));

// The form with each constant (a whole name) replaced by its value; a negative value or a
// fraction goes in brackets, so `a*x` stays `(-1)*x`
export function withDefaults(form: string, defaults: [string, string][]): string
{
	return form.replace(/[A-Za-z_][A-Za-z_0-9]*/g, word =>
	{
		const found = defaults.find(([name]) => name === word);
		if (!found)
		{
			return word;
		}
		const value = found[1];
		return value.startsWith("-") || value.includes("/") ? `(${value})` : value;
	});
}

export const isEquation = (example: Example) => example.name.length === 0;

// The line put in the editor: `name(x)=body` (name made free) or the equation
export function definition(example: Example, taken: string[]): string
{
	const text = withDefaults(example.form, example.defaults);
	return isEquation(example) ? text : `${exampleName(example.name, taken)}(x)=${text}`;
}

// The form as a definition, for the gallery
export function formLine(example: Example): string
{
	return isEquation(example) ? example.form : `${example.name}(x)=${example.form}`;
}

// sin, cos or tan in it: drawn differently outside radians
export function usesAngles(example: Example): boolean
{
	return /\b(sin|cos|tan)\(/.test(example.form);
}

// The example's function name, or name2, name3... when the name is taken
export function exampleName(name: string, taken: string[]): string
{
	const used = new Set(taken.map(item => item.toLowerCase()));
	if (!used.has(name.toLowerCase()))
	{
		return name;
	}
	let n = 2;
	while (used.has((name + n).toLowerCase()))
	{
		n++;
	}
	return name + n;
}
