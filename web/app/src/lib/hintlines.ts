// hintlines.ts - One or two lines for the hint under Input (owner 2026-11-26)
//
// The desktop's rule (ui/SoftCalc/Logic/HintLines.cs): the hint line takes
// two lines when it is too narrow for MIN_CHARS characters of hint text,
// and keeps them while the width stays. It changes with the window, never
// with the text, so typing does not make the row jump. A tap still shows
// all of a hint.

export const MIN_CHARS = 80;    // the hints are at most about 82 characters (engine hints.py)
export const SAMPLE = "normcdf(x, [mean, sd]) - normal P(X <= x); normcdf(a, b, [mean, sd]) - P(a to b)";

// sampleWidth: SAMPLE on one line in the hint's font; room: the width the hint line has
export function hintLines(sampleWidth: number, room: number): 1 | 2
{
	return room >= sampleWidth ? 1 : 2;
}

let canvas: HTMLCanvasElement | null = null;

// SAMPLE's width on one line in the element's own font
export function sampleWidth(element: HTMLElement): number
{
	canvas ??= document.createElement("canvas");
	const context = canvas.getContext("2d");
	if (!context)
	{
		return 0;
	}
	const style = getComputedStyle(element);
	context.font = `${style.fontStyle} ${style.fontWeight} ${style.fontSize} ${style.fontFamily}`;
	return context.measureText(SAMPLE).width;
}
