// hintlines.ts - One or two lines for the hint under Input (owner 2026-11-26)
//
// The desktop's rule (ui/SoftCalc/Logic/HintLines.cs): a hint shows on one
// line when that line holds at least SHOWN_SHARE of it; otherwise it takes
// two lines. A tap still shows all of it.

export const SHOWN_SHARE = 0.75;

// textWidth: the whole hint on one line; room: the width the hint line has
export function hintLines(textWidth: number, room: number): 1 | 2
{
	return textWidth <= 0 || room >= textWidth * SHOWN_SHARE ? 1 : 2;
}

let canvas: HTMLCanvasElement | null = null;

// The hint's width on one line in the element's own font
export function textWidth(element: HTMLElement, text: string): number
{
	canvas ??= document.createElement("canvas");
	const context = canvas.getContext("2d");
	if (!context)
	{
		return 0;
	}
	const style = getComputedStyle(element);
	context.font = `${style.fontStyle} ${style.fontWeight} ${style.fontSize} ${style.fontFamily}`;
	return context.measureText(text).width;
}
