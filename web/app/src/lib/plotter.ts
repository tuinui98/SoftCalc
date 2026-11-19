// plotter.ts - The Graph's engine requests: points and segments of the shown lines, and trace values
//
// As the desktop's PlotView: one round of requests at a time, and what
// changes during a round is folded into one more round with the newest
// range, so the lines follow the fingers without queueing up in the
// engine. Lines arrive one by one; a line still in view is kept until its
// new points come. Trace asks one point at a time and only the newest
// finger position waits (protocol.md: plot, plot_equation, value_at,
// equation_at).

import type { EngineLike } from "./calc";
import { same, type Area, type Mode, type Rect, type Sample, type Series, type TraceTarget } from "./graph";

const POLAR_SAMPLES = 720;
const EQUATION_CELL = 10;    // CSS pixels per coarse equation cell, as the desktop

export interface Request
{
	names: string[];    // lines to draw, in order
	mode: Mode;
	world: Rect;
	area: Area;
	thetas: [number, number];
}

// Function: t is x (polar: theta), text and value the result. Equation: both coordinates.
export interface Traced
{
	line: string;
	equation: boolean;
	t: number;
	step: number;
	text: string | null;
	value: number | null;
	xText?: string;
	yText?: string;
	x?: number;
	y?: number;
}

export class Plotter
{
	series: Series[] = [];
	message: string | null = null;
	traced: Traced | null = null;
	onchange: () => void = () => {};

	private readonly engine: EngineLike;
	private wanted: Request | null = null;
	private busy = false;
	private seriesMode: Mode = "xy";
	private traceWanted: TraceTarget | null = null;
	private traceBusy = false;
	private generation = 0;

	constructor(engine: EngineLike)
	{
		this.engine = engine;
	}

	refresh(request: Request)
	{
		this.wanted = request;
		if (!this.busy)
		{
			void this.rounds();
		}
	}

	private async rounds()
	{
		this.busy = true;
		try
		{
			while (this.wanted)
			{
				const request = this.wanted;
				this.wanted = null;
				await this.round(request);
			}
		}
		finally
		{
			this.busy = false;
		}
	}

	private async round(request: Request)
	{
		const { names, mode, world, area } = request;
		if (names.length === 0)
		{
			this.series = [];
			this.message = "No line ticked";
			this.onchange();
			return;
		}
		const found = new Map<string, Series>();
		if (this.seriesMode === mode)
		{
			for (const old of this.series.filter(line => names.some(name => same(name, line.name))))
			{
				found.set(old.name.toLowerCase(), old);
			}
		}
		this.seriesMode = mode;
		let error: string | null = null;
		const show = () =>
		{
			this.series = names.map(name => found.get(name.toLowerCase())).filter((line): line is Series => line !== undefined);
			this.message = this.series.length === 0 ? error : null;
			this.onchange();
		};
		//==================================================================
		//  BLOCK 1 - Functions over the drawn x range (polar: theta)
		//==================================================================
		const [start, end] = mode === "polar" ? request.thetas : [world.minX, world.maxX];
		const samples = mode === "polar" ? POLAR_SAMPLES : Math.min(2000, Math.max(2, Math.round(area.width)));
		for (const name of names.filter(item => !item.includes("=")))
		{
			const answer = await this.engine.send("plot", { function: name, mode, start, end, samples });
			if (answer.ok)
			{
				found.set(name.toLowerCase(), { name, equation: false, points: (answer.points ?? []) as Sample[], start, end, segments: [] });
			}
			else
			{
				found.delete(name.toLowerCase());
				error ??= `${name}: ${answer.error ?? "not drawn"}`;
			}
			show();
		}
		// end BLOCK 1
		//==================================================================
		//  BLOCK 2 - Equations in the drawn rectangle
		//==================================================================
		const cols = Math.min(150, Math.max(4, Math.floor(area.width / EQUATION_CELL)));
		const rows = Math.min(150, Math.max(4, Math.floor(area.height / EQUATION_CELL)));
		for (const name of names.filter(item => item.includes("=")))
		{
			const answer = await this.engine.send("plot_equation", {
				equation: name, x_min: world.minX, x_max: world.maxX, y_min: world.minY, y_max: world.maxY, cols, rows,
			});
			if (answer.ok)
			{
				found.set(name.toLowerCase(), {
					name, equation: true, points: [], start: 0, end: 0,
					segments: (answer.segments ?? []) as [number, number, number, number][],
				});
			}
			else
			{
				found.delete(name.toLowerCase());
				error ??= `${name}: ${answer.error ?? "not drawn"}`;
			}
			show();
		}
		// end BLOCK 2
		show();
	}

	//==================================================================
	//  Trace
	//==================================================================
	trace(target: TraceTarget | null)
	{
		if (target === null)
		{
			this.clearTrace();
			return;
		}
		this.traceWanted = target;
		if (!this.traceBusy)
		{
			void this.traceRounds();
		}
	}

	clearTrace()
	{
		this.traceWanted = null;
		this.generation++;
		if (this.traced)
		{
			this.traced = null;
			this.onchange();
		}
	}

	private async traceRounds()
	{
		this.traceBusy = true;
		try
		{
			while (this.traceWanted)
			{
				const target = this.traceWanted;
				this.traceWanted = null;
				const generation = this.generation;
				const point = await this.ask(target);
				if (generation === this.generation && this.traceWanted === null)
				{
					this.traced = point;
					this.onchange();
				}
			}
		}
		finally
		{
			this.traceBusy = false;
		}
	}

	// null: the engine failed, or an equation has no point near there
	private async ask(target: TraceTarget): Promise<Traced | null>
	{
		if (!target.equation)
		{
			const answer = await this.engine.send("value_at", { function: target.line, x: target.fixed });
			return answer.ok ? {
				line: target.line, equation: false, t: target.t, step: target.step,
				text: answer.text as string | null, value: answer.value as number | null,
			} : null;
		}
		const answer = await this.engine.send("equation_at", {
			equation: target.line, axis: target.axis, value: target.fixed, guess: target.guess, window: target.window,
		});
		if (!answer.ok || typeof answer.x_value !== "number" || typeof answer.y_value !== "number")
		{
			return null;
		}
		return {
			line: target.line, equation: true, t: 0, step: target.step,
			text: String(target.axis === "x" ? answer.y : answer.x), value: null,
			xText: String(answer.x), yText: String(answer.y), x: answer.x_value, y: answer.y_value,
		};
	}
}

// The status line for a trace, as the desktop's Graph window: g(2.35) = 0.9977...
export function traceText(traced: Traced, formatT: (t: number, step: number) => string): string
{
	if (traced.equation)
	{
		return `${traced.line}:  x = ${traced.xText}  y = ${traced.yText}`;
	}
	return `${traced.line}(${formatT(traced.t, traced.step)}) = ${traced.text ?? "undefined"}`;
}
