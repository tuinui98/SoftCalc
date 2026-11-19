<!-- GraphView.svelte - ⋮ > Graph: functions of one parameter and equations in x and y (owner 2026-11-19).
     One finger traces the line nearest it; two fingers move and zoom (both axes, or one when they
     lie along it). A mouse traces by hovering, drags to move and wheels to zoom. Fit, 1:1, Reset,
     Grid and XY/Polar as the desktop's Graph window. The lines come from the engine (plotter.ts);
     the geometry is graph.ts. The phone's Back closes the Graph. Pan is a trial button: one finger
     moves the graph instead of tracing (owner 2026-11-19). -->
<script lang="ts">
	import { onMount } from "svelte";
	import type { Calculator } from "../lib/calc";
	import { copyText } from "../lib/clipboard";
	import {
		fitRange, forgetLine, formatTick, freeName, functionHead, gridLines, lineColour, niceStep, PICK_COLOURS, pinch, pinchKind,
		renameLine, RESET_POLAR, RESET_XY, runs, same, sameTarget, square, toScreen, traceTarget, viewOf, zoom,
		type Area, type GraphState, type PinchKind, type Point, type Rect, type TraceTarget,
	} from "../lib/graph";
	import { Plotter, traceText } from "../lib/plotter";
	import { closeOnTouch, tapOn, tapOrHold } from "../lib/repeat";
	import Sheet from "./Sheet.svelte";

	let { calc, functions, equations, names, onclose, oninsert, inner = $bindable(false) }: {
		calc: Calculator;
		functions: { name: string; source: string }[];    // the user's functions of one parameter
		equations: string[];
		names: string[];    // every user variable and function, for a free new name
		onclose: () => void;
		oninsert: (text: string, isResult: boolean) => void;    // a result goes in brackets when negative
		inner?: boolean;    // a menu or the editor is open: the phone's Back closes it first (closeInner)
	} = $props();

	// svelte-ignore state_referenced_locally
	let st: GraphState = $state(structuredClone(calc.graphView));
	// svelte-ignore state_referenced_locally
	const plotter = new Plotter(calc.engine);
	let tick = $state(0);    // bumped when the plotter has news
	plotter.onchange = () => { tick++; draw(); };

	let wrap: HTMLElement;
	let canvas: HTMLCanvasElement;
	let size = { width: 0, height: 0 };
	let ready = $state(false);

	const graphable = $derived([...functions.map(fn => fn.name), ...equations]);
	const labelOf = (name: string) => functions.find(fn => same(fn.name, name))?.source ?? name;
	// Lines no longer there are forgotten, as the desktop's (GraphPanel.UpdateFunctions)
	const shown = $derived(st.shown.filter(name => graphable.some(item => same(item, name))));
	const tap = (button: HTMLElement, act: () => void) => ({ destroy: tapOn(button, () => act()) });
	const outside = (box: HTMLElement, act: () => void) => ({ destroy: closeOnTouch(box, () => act()) });
	const press = (button: HTMLElement, name: string) =>
	{
		let current = name;
		const off = tapOrHold(button, () => select(current), () => { menuFor = current; });
		return { update(next: string) { current = next; }, destroy: off };
	};

	//==================================================================
	//  Drawing area and colours
	//==================================================================
	const FONT = "12px system-ui, sans-serif";
	const TICK_SPACING_X = 70;    // CSS pixels between x labels at most
	const TICK_SPACING_Y = 36;

	function colours()
	{
		const css = getComputedStyle(document.documentElement);
		const get = (name: string) => css.getPropertyValue(name).trim();
		return { page: get("--page"), ink: get("--ink"), faint: get("--faint"), line: get("--line"), select: get("--select") };
	}

	// XY leaves room for the axis numbers; polar keeps a square area so circles stay round
	function areaOf(width: number, height: number, world: Rect): Area
	{
		if (st.mode === "polar")
		{
			const side = Math.max(1, Math.min(width, height) - 12);
			return { left: (width - side) / 2, top: (height - side) / 2, width: side, height: side };
		}
		const context = canvas?.getContext("2d");
		let labelWidth = 30;
		if (context)
		{
			context.font = FONT;
			const step = niceStep(world.maxY - world.minY, Math.max(1, Math.floor(height / TICK_SPACING_Y)));
			labelWidth = Math.max(16, ...gridLines(world.minY, world.maxY, step).map(y => context.measureText(formatTick(y, step)).width));
		}
		const left = Math.ceil(labelWidth) + 10;
		return { left, top: 8, width: Math.max(1, width - left - 10), height: Math.max(1, height - 8 - 24) };
	}

	function world(): Rect
	{
		return viewOf(st);
	}

	function area(): Area
	{
		return areaOf(size.width, size.height, world());
	}

	//==================================================================
	//  Drawing
	//==================================================================
	function draw()
	{
		const context = canvas?.getContext("2d");
		if (!context || size.width === 0)
		{
			return;
		}
		const ratio = window.devicePixelRatio || 1;
		context.setTransform(ratio, 0, 0, ratio, 0, 0);
		const c = colours();
		const w = world();
		const a = areaOf(size.width, size.height, w);
		context.fillStyle = c.page;
		context.fillRect(0, 0, size.width, size.height);
		context.lineWidth = 1;
		if (st.grid)
		{
			if (st.mode === "xy")
			{
				drawXyGrid(context, w, a, c.line);
			}
			else
			{
				drawPolarGrid(context, w, a, c.line);
			}
		}
		drawAxes(context, w, a, c.faint);
		drawTicks(context, w, a, c.faint);
		context.save();
		context.beginPath();
		context.rect(a.left, a.top, a.width, a.height);
		context.clip();
		context.lineJoin = "round";
		for (const line of plotter.series)
		{
			const thick = same(line.name, st.selected) && plotter.series.length > 1;
			context.strokeStyle = lineColour(st, graphable, line.name) ?? c.select;
			context.lineWidth = thick ? 2.6 : 1.8;
			context.beginPath();
			for (const run of runs(line.points, w))
			{
				run.forEach(([x, y], index) =>
				{
					const p = clampPoint(toScreen(x, y, w, a), a);
					if (index === 0)
					{
						context.moveTo(p.x, p.y);
					}
					else
					{
						context.lineTo(p.x, p.y);
					}
				});
			}
			for (const [x1, y1, x2, y2] of line.segments)
			{
				const p = toScreen(x1, y1, w, a), q = toScreen(x2, y2, w, a);
				context.moveTo(p.x, p.y);
				context.lineTo(q.x, q.y);
			}
			context.stroke();
		}
		drawTrace(context, w, a, c);
		context.restore();
		if (plotter.message)
		{
			context.fillStyle = c.faint;
			context.font = "15px system-ui, sans-serif";
			context.textAlign = "center";
			context.fillText(plotter.message, size.width / 2, size.height / 2, size.width - 20);
			context.textAlign = "start";
		}
	}

	// Far-off points would overflow the canvas
	function clampPoint(p: Point, a: Area): Point
	{
		const limit = Math.max(a.width, a.height) * 20;
		return {
			x: Math.min(a.left + a.width + limit, Math.max(a.left - limit, p.x)),
			y: Math.min(a.top + a.height + limit, Math.max(a.top - limit, p.y)),
		};
	}

	function drawXyGrid(context: CanvasRenderingContext2D, w: Rect, a: Area, colour: string)
	{
		context.strokeStyle = colour;
		context.beginPath();
		const xStep = niceStep(w.maxX - w.minX, Math.max(1, Math.floor(a.width / TICK_SPACING_X)));
		for (const x of gridLines(w.minX, w.maxX, xStep))
		{
			const sx = Math.round(toScreen(x, 0, w, a).x) + 0.5;
			context.moveTo(sx, a.top);
			context.lineTo(sx, a.top + a.height);
		}
		const yStep = niceStep(w.maxY - w.minY, Math.max(1, Math.floor(a.height / TICK_SPACING_Y)));
		for (const y of gridLines(w.minY, w.maxY, yStep))
		{
			const sy = Math.round(toScreen(0, y, w, a).y) + 0.5;
			context.moveTo(a.left, sy);
			context.lineTo(a.left + a.width, sy);
		}
		context.stroke();
	}

	function drawPolarGrid(context: CanvasRenderingContext2D, w: Rect, a: Area, colour: string)
	{
		context.strokeStyle = colour;
		context.beginPath();
		const centre = toScreen(0, 0, w, a);
		const scale = a.width / (w.maxX - w.minX);
		const maxR = w.maxX;
		const step = niceStep(maxR, Math.max(1, Math.floor(a.width / 2 / TICK_SPACING_X)));
		for (const r of gridLines(0, maxR, step).filter(r => r > 0))
		{
			context.moveTo(centre.x + r * scale, centre.y);
			context.arc(centre.x, centre.y, r * scale, 0, Math.PI * 2);
		}
		for (let degrees = 0; degrees < 360; degrees += 15)
		{
			const rad = degrees * Math.PI / 180;
			context.moveTo(centre.x, centre.y);
			context.lineTo(centre.x + maxR * scale * Math.cos(rad), centre.y - maxR * scale * Math.sin(rad));
		}
		context.stroke();
	}

	function drawAxes(context: CanvasRenderingContext2D, w: Rect, a: Area, colour: string)
	{
		context.strokeStyle = colour;
		context.beginPath();
		if (w.minX <= 0 && w.maxX >= 0)
		{
			const sx = Math.round(toScreen(0, 0, w, a).x) + 0.5;
			context.moveTo(sx, a.top);
			context.lineTo(sx, a.top + a.height);
		}
		if (w.minY <= 0 && w.maxY >= 0)
		{
			const sy = Math.round(toScreen(0, 0, w, a).y) + 0.5;
			context.moveTo(a.left, sy);
			context.lineTo(a.left + a.width, sy);
		}
		context.rect(Math.round(a.left) + 0.5, Math.round(a.top) + 0.5, Math.round(a.width), Math.round(a.height));
		context.stroke();
	}

	function drawTicks(context: CanvasRenderingContext2D, w: Rect, a: Area, colour: string)
	{
		context.fillStyle = colour;
		context.font = FONT;
		if (st.mode === "polar")
		{
			const step = niceStep(w.maxX, Math.max(1, Math.floor(a.width / 2 / TICK_SPACING_X)));
			for (const r of gridLines(0, w.maxX, step).filter(r => r > 0))
			{
				const p = toScreen(r, 0, w, a);
				context.fillText(formatTick(r, step), p.x + 2, p.y + 13);
			}
			return;
		}
		const xStep = niceStep(w.maxX - w.minX, Math.max(1, Math.floor(a.width / TICK_SPACING_X)));
		context.textAlign = "center";
		for (const x of gridLines(w.minX, w.maxX, xStep))
		{
			context.fillText(formatTick(x, xStep), toScreen(x, 0, w, a).x, a.top + a.height + 16);
		}
		const yStep = niceStep(w.maxY - w.minY, Math.max(1, Math.floor(a.height / TICK_SPACING_Y)));
		context.textAlign = "right";
		context.textBaseline = "middle";
		for (const y of gridLines(w.minY, w.maxY, yStep))
		{
			context.fillText(formatTick(y, yStep), a.left - 5, toScreen(0, y, w, a).y);
		}
		context.textAlign = "start";
		context.textBaseline = "alphabetic";
	}

	// The traced point: a dot on the line and its value beside it, above the finger
	function drawTrace(context: CanvasRenderingContext2D, w: Rect, a: Area, c: ReturnType<typeof colours>)
	{
		const traced = plotter.traced;
		if (!traced || !plotter.series.some(line => same(line.name, traced.line)))
		{
			return;
		}
		let point: Point;
		let label: string;
		if (traced.equation && traced.x !== undefined && traced.y !== undefined)
		{
			point = toScreen(traced.x, traced.y, w, a);
			label = `(${short(traced.x)}, ${short(traced.y)})`;
		}
		else if (traced.value !== null)
		{
			const v = traced.value;
			point = st.mode === "polar" ? toScreen(v * Math.cos(traced.t), v * Math.sin(traced.t), w, a) : toScreen(traced.t, v, w, a);
			label = short(v);
		}
		else
		{
			return;
		}
		context.fillStyle = lineColour(st, graphable, traced.line) ?? c.select;
		context.beginPath();
		context.arc(point.x, point.y, 5, 0, Math.PI * 2);
		context.fill();
		context.font = "bold 13px system-ui, sans-serif";
		const width = context.measureText(label).width;
		const x = Math.min(a.left + a.width - width - 4, Math.max(a.left + 4, point.x - width / 2));
		const y = point.y - 44 < a.top + 14 ? point.y + 28 : point.y - 44;    // above the finger, else below
		context.fillStyle = c.page;
		context.fillRect(x - 3, y - 13, width + 6, 18);
		context.fillStyle = c.ink;
		context.fillText(label, x, y);
	}

	// G6 in C#: six significant digits
	function short(value: number): string
	{
		return Number(value.toPrecision(6)).toString();
	}

	//==================================================================
	//  Engine requests and keeping the state
	//==================================================================
	function refresh()
	{
		draw();
		if (!ready)
		{
			return;
		}
		const a = area();
		plotter.refresh({ names: shown, mode: st.mode, world: world(), area: a, thetas: [st.polar.minT, st.polar.maxT] });
	}

	let saveTimer: ReturnType<typeof setTimeout> | undefined;
	function keep()
	{
		clearTimeout(saveTimer);
		saveTimer = setTimeout(() => calc.saveGraphView($state.snapshot(st) as GraphState), 300);
	}

	function setWorld(next: Rect)
	{
		if (st.mode === "polar")
		{
			st.polar.maxR = Math.max(1e-9, Math.min(1e12, (next.maxX - next.minX) / 2));
		}
		else
		{
			st.xy = next;
		}
		plotter.clearTrace();
		refresh();
	}

	function changed()
	{
		refresh();
		keep();
	}

	//==================================================================
	//  Fingers and the mouse
	//==================================================================
	const pointers = new Map<number, Point>();
	let pinchFrom: [Point, Point] | null = null;
	let pinchWorld: Rect | null = null;
	let pinchMode: PinchKind = "both";
	let tracing = false;
	let dragFrom: Point | null = null;    // mouse drag: moving the graph
	let dragWorld: Rect | null = null;
	let lastTap = { at: 0, x: 0, y: 0 };
	let currentTrace: TraceTarget | null = null;
	let panOne = $state(false);    // temporary trial (owner 2026-11-19): one finger moves instead of tracing

	function local(event: PointerEvent | WheelEvent): Point
	{
		const box = canvas.getBoundingClientRect();
		return { x: event.clientX - box.left, y: event.clientY - box.top };
	}

	function traceAt(at: Point)
	{
		const target = traceTarget(plotter.series, at, world(), area(), st.mode, st.selected, plotter.traced?.line ?? null);
		if (target === null)
		{
			currentTrace = null;
			plotter.clearTrace();
			return;
		}
		if (!sameTarget(target, currentTrace))
		{
			currentTrace = target;
			plotter.trace(target);
		}
	}

	function pair(): [Point, Point]
	{
		const [a, b] = [...pointers.values()];
		return [{ ...a }, { ...b }];
	}

	function onDown(event: PointerEvent)
	{
		canvas.setPointerCapture(event.pointerId);
		const at = local(event);
		pointers.set(event.pointerId, at);
		if (pointers.size === 2)
		{
			tracing = false;
			dragFrom = null;
			dragWorld = null;
			pinchFrom = pair();
			pinchWorld = { ...world() };
			pinchMode = st.mode === "polar" ? "both" : pinchKind(pinchFrom[0], pinchFrom[1], st.square);
			return;
		}
		if (pointers.size !== 1)
		{
			return;
		}
		const now = performance.now();
		if (now - lastTap.at < 300 && Math.hypot(at.x - lastTap.x, at.y - lastTap.y) < 30)
		{
			zoomAt(at, 0.5);    // a double tap zooms in there
			lastTap = { at: 0, x: 0, y: 0 };
			return;
		}
		lastTap = { at: now, x: at.x, y: at.y };
		if (event.pointerType === "mouse" || panOne)
		{
			dragFrom = at;
			dragWorld = { ...world() };
			return;
		}
		tracing = true;
		traceAt(at);
	}

	function onMove(event: PointerEvent)
	{
		const at = local(event);
		if (event.pointerType === "mouse" && pointers.size === 0)
		{
			traceAt(at);    // a mouse traces by hovering
			return;
		}
		if (!pointers.has(event.pointerId))
		{
			return;
		}
		pointers.set(event.pointerId, at);
		if (pointers.size === 2 && pinchFrom && pinchWorld)
		{
			if (pinchMode !== "both" && st.square)
			{
				st.square = false;    // a one-axis zoom ends 1:1, as the desktop's
			}
			setWorld(pinch(pinchWorld, area(), pinchFrom, pair(), pinchMode));
		}
		else if (dragFrom && dragWorld)
		{
			const offset = (p: Point): [Point, Point] => [p, { x: p.x + 100, y: p.y }];
			setWorld(pinch(dragWorld, area(), offset(dragFrom), offset(at), "both"));
		}
		else if (tracing)
		{
			traceAt(at);
		}
	}

	function onUp(event: PointerEvent)
	{
		pointers.delete(event.pointerId);
		if (pinchFrom && pointers.size < 2)
		{
			pinchFrom = null;
			pinchWorld = null;
			keep();
			if (panOne && pointers.size === 1)
			{
				dragFrom = { ...[...pointers.values()][0] };    // the finger left keeps moving the graph
				dragWorld = { ...world() };
			}
		}
		if (dragFrom && pointers.size === 0)
		{
			dragFrom = null;
			dragWorld = null;
			keep();
		}
		if (pointers.size === 0)
		{
			tracing = false;    // the traced value stays to read, copy or insert
		}
	}

	function onWheel(event: WheelEvent)
	{
		event.preventDefault();
		zoomAt(local(event), Math.pow(1.0015, event.deltaY));
	}

	function zoomAt(at: Point, factor: number)
	{
		const w = world(), a = area();
		const centre = st.mode === "polar" ? { x: 0, y: 0 } : { x: w.minX + (at.x - a.left) / a.width * (w.maxX - w.minX), y: w.maxY - (at.y - a.top) / a.height * (w.maxY - w.minY) };
		const x = zoom(w.minX, w.maxX, centre.x, factor), y = zoom(w.minY, w.maxY, centre.y, factor);
		if (x && y)
		{
			setWorld({ minX: x[0], maxX: x[1], minY: y[0], maxY: y[1] });
			keep();
		}
	}

	//==================================================================
	//  Toolbar and list
	//==================================================================
	function fit()
	{
		if (st.mode === "polar")
		{
			return;
		}
		const values = plotter.series.flatMap(line => [
			...line.points.filter((p): p is [number, number] => p !== null).map(p => p[1]),
			...line.segments.flatMap(s => [s[1], s[3]]),
		]);
		const range = fitRange(values);
		if (range)
		{
			st.square = false;
			setWorld({ ...st.xy, minY: range[0], maxY: range[1] });
			keep();
		}
	}

	function toggleSquare()
	{
		st.square = !st.square;
		if (st.square && st.mode === "xy")
		{
			const a = area();
			setWorld(square(st.xy, a.width, a.height));
		}
		changed();
	}

	function reset()
	{
		st.square = false;
		if (st.mode === "xy")
		{
			st.xy = { ...RESET_XY };
		}
		else
		{
			st.polar = { ...RESET_POLAR };
		}
		plotter.clearTrace();
		changed();
	}

	function setMode(mode: "xy" | "polar")
	{
		st.mode = mode;
		plotter.clearTrace();
		changed();
	}

	function toggleShown(name: string)
	{
		const on = st.shown.some(item => same(item, name));
		st.shown = on ? st.shown.filter(item => !same(item, name)) : [...st.shown, name];
		if (!on)
		{
			st.selected = name;
		}
		plotter.clearTrace();
		changed();
	}

	function select(name: string)
	{
		st.selected = name;
		if (!st.shown.some(item => same(item, name)))
		{
			st.shown = [...st.shown, name];
		}
		changed();
	}

	const status = $derived.by(() =>
	{
		void tick;
		if (plotter.traced)
		{
			return traceText(plotter.traced, formatTick);
		}
		if (panOne)
		{
			return st.mode === "polar" ? "Two fingers: zoom" : "One finger: move; two fingers: move and zoom";
		}
		return st.mode === "polar" ? "One finger: trace; two fingers: zoom" : "One finger: trace; two fingers: move and zoom";
	});

	// The traced value to copy or insert: a function's value, an equation's solved coordinate
	const tracedValue = $derived.by(() =>
	{
		void tick;
		return plotter.traced?.text ?? null;
	});

	async function copyValue()
	{
		if (tracedValue && await copyText(tracedValue))
		{
			calc.say("Copied " + tracedValue);
		}
	}

	//==================================================================
	//  Size
	//==================================================================
	onMount(() =>
	{
		const observer = new ResizeObserver(resized);
		observer.observe(wrap);
		void calc.started.then(() => { ready = true; refresh(); });
		return () =>
		{
			observer.disconnect();
			clearTimeout(saveTimer);
			calc.saveGraphView($state.snapshot(st) as GraphState);
		};
	});

	// The phone's keyboard, up while the editor is open, shrinks the page for a moment: the plot
	// keeps its size, so the range does not change under it (owner 2026-11-19); once the editor
	// is closed the size is read again.
	function resized()
	{
		if (editor !== null)
		{
			return;
		}
		const box = wrap.getBoundingClientRect();
		const next = { width: Math.floor(box.width), height: Math.floor(box.height) };
		if (next.width === size.width && next.height === size.height)
		{
			return;
		}
		const ratio = window.devicePixelRatio || 1;
		const before = size.width;
		size = next;
		canvas.width = Math.round(size.width * ratio);
		canvas.height = Math.round(size.height * ratio);
		canvas.style.width = size.width + "px";
		canvas.style.height = size.height + "px";
		if (st.square && st.mode === "xy" && before > 0)
		{
			const a = area();
			st.xy = square(st.xy, a.width, a.height);
		}
		refresh();
	}

	$effect(() =>
	{
		if (editor === null)
		{
			setTimeout(() => { if (wrap) resized(); }, 300);    // the keyboard is going down meanwhile
		}
	});

	//==================================================================
	//  Add, Edit, Delete and Colour (G2, owner 2026-11-19): + Function and
	//  + Equation above the list; a press held on a line opens its menu
	//==================================================================
	interface Editor
	{
		kind: "function" | "equation";
		old: string | null;    // the line edited; null when adding
		text: string;
		error: string;
		busy: boolean;
	}
	let editor: Editor | null = $state(null);
	let menuFor: string | null = $state(null);
	let colourFor: string | null = $state(null);
	let deleteFor: string | null = $state(null);
	$effect(() =>
	{
		inner = editor !== null || menuFor !== null || colourFor !== null || deleteFor !== null;
	});

	export function closeInner()
	{
		editor = null;
		menuFor = null;
		colourFor = null;
		deleteFor = null;
	}

	const isEquation = (name: string) => name.includes("=");    // a function's line is its name

	function addFunction()
	{
		editor = { kind: "function", old: null, text: freeName(names) + "(x)=", error: "", busy: false };
	}

	function addEquation()
	{
		editor = { kind: "equation", old: null, text: "x^2+y^2=9", error: "", busy: false };
	}

	async function saveEditor()
	{
		const e = editor;
		if (!e || e.busy)
		{
			return;
		}
		e.busy = true;
		const problem = e.kind === "equation" ? await calc.putEquation(e.text, e.old) : await calc.putFunction(e.text, e.old);
		e.busy = false;
		if (editor !== e)
		{
			return;    // closed meanwhile
		}
		if (problem)
		{
			e.error = problem;
			return;
		}
		const name = e.kind === "equation" ? e.text.trim() : functionHead(e.text)!.name;
		if (e.old !== null)
		{
			renameLine(st, e.old, name);
		}
		if (!st.shown.some(item => same(item, name)))
		{
			st.shown = [...st.shown, name];
		}
		st.selected = name;
		editor = null;
		plotter.clearTrace();
		changed();
	}

	function lineMenu(choice: string | null)
	{
		const name = menuFor;
		menuFor = null;
		if (name === null || choice === null)
		{
			return;
		}
		if (choice === "Insert")
		{
			// a function as name(, as the function keys; an equation as it is
			oninsert(isEquation(name) ? name : name + "(", false);
		}
		else if (choice === "Edit")
		{
			editor = { kind: isEquation(name) ? "equation" : "function", old: name, text: labelOf(name), error: "", busy: false };
		}
		else if (choice === "Colour")
		{
			colourFor = name;
		}
		else if (choice === "Delete")
		{
			deleteFor = name;
		}
	}

	function setColour(colour: string | null)
	{
		const name = colourFor;
		colourFor = null;
		if (name === null)
		{
			return;
		}
		st.colours = Object.fromEntries(Object.entries(st.colours).filter(([key]) => !same(key, name)));
		if (colour !== null)
		{
			st.colours[name] = colour;
		}
		changed();
	}

	async function remove(choice: string | null)
	{
		const name = deleteFor;
		deleteFor = null;
		if (name === null || choice === null)
		{
			return;
		}
		forgetLine(st, name);
		if (isEquation(name))
		{
			calc.removeEquation(name);
		}
		else
		{
			await calc.deleteName(name);
		}
		plotter.clearTrace();
		changed();
	}

	// The caret at the end of the line, once the dialog is on screen
	const focusEnd = (box: HTMLInputElement) =>
	{
		setTimeout(() => { box.focus(); box.setSelectionRange(box.value.length, box.value.length); }, 50);
	};

	// Lines added or deleted while the Graph is open
	$effect(() =>
	{
		void shown.join("\n");
		refresh();
	});
</script>

<div class="graph" role="dialog" aria-label="Graph">
	<div class="head">
		<span class="name">Graph</span>
		<div class="tools">
			<button use:tap={fit} disabled={st.mode === "polar"}>Fit</button>
			<button class:on={st.square} use:tap={toggleSquare} disabled={st.mode === "polar"}>1:1</button>
			<button use:tap={reset}>Reset</button>
			<button class:on={st.grid} use:tap={() => { st.grid = !st.grid; changed(); }}>Grid</button>
			<button class:on={st.mode === "polar"} use:tap={() => setMode(st.mode === "xy" ? "polar" : "xy")}>Polar</button>
			<button class:on={panOne} use:tap={() => { panOne = !panOne; plotter.clearTrace(); tick++; }}>Pan</button>
		</div>
		<button class="bar" use:tap={onclose}>Done</button>
	</div>
	<div class="body">
		<div class="plot" bind:this={wrap}>
			<canvas bind:this={canvas} onpointerdown={onDown} onpointermove={onMove} onpointerup={onUp} onpointercancel={onUp}
				onpointerleave={event => { if (event.pointerType === "mouse" && pointers.size === 0) { currentTrace = null; } }}
				onwheel={onWheel}></canvas>
		</div>
		<div class="side">
			<div class="status">
				<span class="text">{status}</span>
				{#if tracedValue}
					<button use:tap={copyValue}>Copy</button>
					<button use:tap={() => oninsert(tracedValue ?? "", true)}>Insert</button>
				{/if}
			</div>
			<div class="lines">
				<div class="adds">
					<button use:tap={addFunction}>+ Function</button>
					<button use:tap={addEquation}>+ Equation</button>
				</div>
				{#each graphable as name (name)}
					{@const on = shown.some(item => same(item, name))}
					<div class="row" class:chosen={same(name, st.selected)}>
						<button class="tick" class:on use:tap={() => toggleShown(name)} aria-label={(on ? "Hide " : "Draw ") + name}>
							<span class="swatch" style:background={on ? (lineColour(st, graphable, name) ?? "var(--select)") : "transparent"}
								style:border-color={lineColour(st, graphable, name) ?? "var(--select)"}></span>
						</button>
						<button class="label" use:press={name}>{labelOf(name)}</button>
					</div>
				{:else}
					<p class="none">No lines yet: + Function or + Equation.</p>
				{/each}
				{#if graphable.length > 0}
					<p class="none">Press and hold a line to insert, edit, colour or delete it.</p>
				{/if}
			</div>
		</div>
	</div>
	{#if menuFor !== null}
		<Sheet title={labelOf(menuFor)} items={["Insert", "Edit", "Colour", "Delete"]} onpick={lineMenu} />
	{/if}
	{#if deleteFor !== null}
		<Sheet title={"Delete " + labelOf(deleteFor) + "?"} items={["Delete"]} onpick={remove} />
	{/if}
	{#if colourFor !== null}
		{@const name = colourFor}
		<div class="backdrop" role="presentation" use:outside={() => { colourFor = null; }}></div>
		<div class="picker" role="dialog" aria-label="Colour">
			<div class="title">{labelOf(name)}</div>
			<div class="swatches">
				{#each PICK_COLOURS as colour (colour)}
					<button class="pick" class:on={lineColour(st, graphable, name) === colour} style:background={colour}
						aria-label={colour} use:tap={() => setColour(colour)}></button>
				{/each}
			</div>
			<button class="wide" use:tap={() => setColour(null)}>Default colour</button>
			<button class="wide cancel" use:tap={() => { colourFor = null; }}>Cancel</button>
		</div>
	{/if}
	{#if editor}
		<div class="backdrop" role="presentation"></div>
		<div class="editor" role="dialog" aria-label="Line">
			<div class="title">{editor.old === null ? "Add" : "Edit"} {editor.kind}</div>
			<input bind:value={editor.text} use:focusEnd autocapitalize="off" autocomplete="off" spellcheck="false"
				onkeydown={event => { if (event.key === "Enter") void saveEditor(); else if (event.key === "Escape") editor = null; }} />
			<div class="hint">{editor.kind === "function" ? "One parameter, such as f(x)=x^2-2"
				: "In x and y, such as x^2+y^2=9 or y^2=x^3-x; other names are variables"}</div>
			{#if editor.error}<div class="error">{editor.error}</div>{/if}
			<div class="buttons">
				<button use:tap={() => { editor = null; }}>Cancel</button>
				<button class="ok" use:tap={() => void saveEditor()}>{editor.busy ? "..." : "OK"}</button>
			</div>
		</div>
	{/if}
</div>

<style>
	.graph
	{
		position: fixed; inset: 0; z-index: 40; display: flex; flex-direction: column;
		background: var(--page); color: var(--ink); height: 100dvh;
	}
	/* upright: the title and Done, the tools on a row below; on its side all on one row */
	.head
	{
		display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; column-gap: 8px;
		padding: 4px 6px 0; padding-top: max(4px, env(safe-area-inset-top)); border-bottom: 1px solid var(--line);
	}
	.name { margin: 0 10px; font-size: 20px; font-weight: 500; flex: none; }
	.bar
	{
		min-width: 44px; height: 40px; border: none; border-radius: 20px; background: none; color: var(--select);
		font: inherit; font-size: 16px; padding: 0 12px; cursor: pointer; touch-action: manipulation;
	}
	.bar:active { background: var(--line); }
	.tools
	{
		order: 3; flex: 1 0 100%; display: flex; gap: 6px; padding: 6px 2px; overflow-x: auto;
		border-top: 1px solid var(--line); margin-top: 4px;
	}
	@media (orientation: landscape)
	{
		.head { flex-wrap: nowrap; padding-bottom: 4px; }
		.tools { order: 0; flex: 1 1 auto; min-width: 0; border-top: none; margin-top: 0; padding: 0; }
	}
	.tools button, .status button
	{
		height: 34px; padding: 0 14px; border: 1px solid var(--line); border-radius: 17px; background: none;
		color: var(--ink); font: inherit; font-size: 15px; cursor: pointer; touch-action: manipulation; flex: none;
	}
	.tools button.on { background: var(--select); border-color: var(--select); color: var(--select-ink); }
	.tools button:disabled { opacity: 0.4; }
	.body { flex: 1; min-height: 0; display: flex; flex-direction: column; }
	.plot { flex: 3; min-height: 0; position: relative; }
	canvas { position: absolute; inset: 0; touch-action: none; display: block; }
	.side { flex: 2; min-height: 0; display: flex; flex-direction: column; border-top: 1px solid var(--line); }
	.status
	{
		display: flex; align-items: center; gap: 6px; padding: 6px 10px; min-height: 46px;
		border-bottom: 1px solid var(--line); font-size: 15px;
	}
	.status .text { flex: 1; min-width: 0; overflow-wrap: anywhere; color: var(--faint); }
	.lines { flex: 1; overflow-y: auto; overscroll-behavior: contain; padding-bottom: env(safe-area-inset-bottom); }
	.row { display: flex; align-items: center; border-bottom: 1px solid var(--line); }
	.row.chosen .label { font-weight: 600; }
	.tick { width: 52px; height: 48px; border: none; background: none; cursor: pointer; flex: none; display: grid; place-items: center; }
	.swatch { width: 20px; height: 20px; border-radius: 5px; border: 2px solid; }
	.label
	{
		flex: 1; min-width: 0; height: 48px; text-align: left; border: none; background: none; color: inherit;
		font: inherit; font-size: 16px; cursor: pointer; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
	}
	.none { padding: 12px 16px; color: var(--faint); }
	.adds { display: flex; gap: 8px; padding: 8px 10px; border-bottom: 1px solid var(--line); }
	.adds button, .buttons button, .wide
	{
		height: 38px; padding: 0 16px; border: 1px solid var(--line); border-radius: 19px; background: none;
		color: var(--select); font: inherit; font-size: 15px; cursor: pointer; touch-action: manipulation;
	}
	.backdrop { position: fixed; inset: 0; background: rgb(0 0 0 / 0.35); z-index: 30; touch-action: none; }
	.editor, .picker
	{
		position: fixed; left: 0; right: 0; z-index: 31; margin: 0 auto; max-width: 520px; background: var(--page);
		box-shadow: 0 4px 20px rgb(0 0 0 / 0.3); padding: 14px 16px; display: flex; flex-direction: column; gap: 10px;
	}
	/* the editor at the top, above the phone's keyboard; the colours at the bottom, near the thumb */
	.editor { top: 0; padding-top: max(14px, env(safe-area-inset-top)); border-radius: 0 0 16px 16px; }
	.picker { bottom: 0; padding-bottom: max(28px, env(safe-area-inset-bottom)); border-radius: 16px 16px 0 0; }
	.title { font-weight: 600; color: var(--faint); }
	.editor input
	{
		font: inherit; font-size: 18px; padding: 10px 12px; border: 1px solid var(--line); border-radius: 8px;
		background: var(--page); color: var(--ink); min-width: 0;
	}
	.hint { color: var(--faint); font-size: 14px; }
	.error { color: var(--error); font-size: 15px; overflow-wrap: anywhere; }
	.buttons { display: flex; justify-content: flex-end; gap: 8px; }
	.buttons .ok { background: var(--select); border-color: var(--select); color: var(--select-ink); min-width: 80px; }
	.swatches { display: grid; grid-template-columns: repeat(6, 1fr); gap: 10px; }
	.pick { aspect-ratio: 1; border-radius: 10px; border: 3px solid transparent; cursor: pointer; touch-action: manipulation; }
	.pick.on { border-color: var(--ink); }
	.wide { width: 100%; }
	.wide.cancel { color: var(--ink); }

	/* a tablet on its side: the graph on the left, the list on the right */
	@media (orientation: landscape) and (min-width: 700px)
	{
		.body { flex-direction: row; }
		.plot { flex: 2; }
		.side { flex: 1; max-width: 360px; border-top: none; border-left: 1px solid var(--line); }
	}
</style>
