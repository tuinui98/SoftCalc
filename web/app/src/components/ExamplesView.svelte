<!-- ExamplesView.svelte - Graph > Examples: classic curves with a small graph each (G3, owner 2026-11-19).
     The desktop's list (data/graph-examples.json), one kind at a time. A tap on a curve opens it in
     the Graph's editor with its constants as numbers, the form and what they change shown under it.
     The pictures are drawn by the engine (an equation grid of 40 x 30 cells; a function as the
     equation y = body, which drops the jumps at poles), always in radians, and kept for next time. -->
<script module lang="ts">
	const drawn = new Map<string, [number, number, number, number][]>();    // by title
</script>

<script lang="ts">
	import { onMount } from "svelte";
	import type { Calculator } from "../lib/calc";
	import { EQUATIONS, FUNCTIONS, formLine, isEquation, withDefaults, type Example } from "../lib/examples";
	import { tapOn } from "../lib/repeat";

	let { calc, onpick, onclose }: { calc: Calculator; onpick: (example: Example) => void; onclose: () => void } = $props();

	let equations = $state(false);
	const shown = $derived(equations ? EQUATIONS : FUNCTIONS);
	const tap = (button: HTMLElement, act: () => void) => ({ destroy: tapOn(button, () => act()) });
	// svelte-ignore state_referenced_locally
	const angle = calc.settings.angle;
	const angleName = angle === "deg" ? "Degrees" : angle === "grad" ? "Grads" : "Radians";

	const W = 120, H = 90;

	function paint(canvas: HTMLCanvasElement, example: Example)
	{
		const ratio = window.devicePixelRatio || 1;
		canvas.width = W * ratio;
		canvas.height = H * ratio;
		const context = canvas.getContext("2d");
		if (!context)
		{
			return;
		}
		const css = getComputedStyle(document.documentElement);
		const v = example.view;
		const px = (x: number) => (x - v.minX) / (v.maxX - v.minX) * W;
		const py = (y: number) => (v.maxY - y) / (v.maxY - v.minY) * H;
		context.setTransform(ratio, 0, 0, ratio, 0, 0);
		context.clearRect(0, 0, W, H);
		context.strokeStyle = css.getPropertyValue("--faint").trim();
		context.lineWidth = 1;
		context.beginPath();
		if (v.minX < 0 && v.maxX > 0)
		{
			context.moveTo(px(0), 0);
			context.lineTo(px(0), H);
		}
		if (v.minY < 0 && v.maxY > 0)
		{
			context.moveTo(0, py(0));
			context.lineTo(W, py(0));
		}
		context.stroke();
		context.strokeStyle = css.getPropertyValue("--select").trim();
		context.lineWidth = 1.6;
		context.beginPath();
		for (const [x1, y1, x2, y2] of drawn.get(example.title) ?? [])
		{
			context.moveTo(px(x1), py(y1));
			context.lineTo(px(x2), py(y2));
		}
		context.stroke();
	}

	// Every tile on screen, painted again when its picture comes
	const tiles = new Map<HTMLCanvasElement, Example>();
	const picture = (canvas: HTMLCanvasElement, example: Example) =>
	{
		tiles.set(canvas, example);
		paint(canvas, example);
		return {
			update(next: Example) { tiles.set(canvas, next); paint(canvas, next); },
			destroy() { tiles.delete(canvas); },
		};
	};

	onMount(() =>
	{
		let gone = false;
		void (async () =>
		{
			for (const example of [...FUNCTIONS, ...EQUATIONS])
			{
				if (gone)
				{
					return;
				}
				if (drawn.has(example.title))
				{
					continue;
				}
				const text = withDefaults(example.form, example.defaults);
				const v = example.view;
				const answer = await calc.engine.send("plot_equation", {
					equation: isEquation(example) ? text : "y=" + text,
					x_min: v.minX, x_max: v.maxX, y_min: v.minY, y_max: v.maxY, cols: 40, rows: 30, angle: "rad",
				});
				if (answer.ok)
				{
					drawn.set(example.title, (answer.segments ?? []) as [number, number, number, number][]);
					for (const [canvas, shown] of tiles)
					{
						if (shown === example)
						{
							paint(canvas, shown);
						}
					}
				}
			}
		})();
		return () => { gone = true; };
	});
</script>

<div class="examples" role="dialog" aria-label="Examples">
	<div class="head">
		<span class="name">Examples</span>
		<button class="bar" use:tap={onclose}>Cancel</button>
	</div>
	<div class="kinds">
		<button class:on={!equations} use:tap={() => { equations = false; }}>Functions y = f(x)</button>
		<button class:on={equations} use:tap={() => { equations = true; }}>Equations in x and y</button>
	</div>
	<p class="about">
		Tap a curve: it opens in the editor with its constants as numbers; change them, then OK adds it.
		{#if angle !== "rad"}Pictures use radians; SoftCalc is in {angleName}, so sin, cos and tan draw differently.{/if}
	</p>
	<div class="tiles">
		{#each shown as example (example.title)}
			<button class="tile" use:tap={() => onpick(example)} title={formLine(example) + "\n" + example.note}>
				<canvas use:picture={example} style:width="{W}px" style:height="{H}px"></canvas>
				<span>{example.title}</span>
			</button>
		{/each}
	</div>
</div>

<style>
	.examples
	{
		position: fixed; inset: 0; z-index: 32; display: flex; flex-direction: column; background: var(--page); color: var(--ink);
	}
	.head
	{
		display: flex; align-items: center; justify-content: space-between; padding: 4px 6px;
		padding-top: max(4px, env(safe-area-inset-top)); border-bottom: 1px solid var(--line);
	}
	.name { margin: 0 10px; font-size: 20px; font-weight: 500; }
	.bar
	{
		height: 40px; border: none; border-radius: 20px; background: none; color: var(--select);
		font: inherit; font-size: 16px; padding: 0 12px; cursor: pointer; touch-action: manipulation;
	}
	.kinds { display: flex; gap: 6px; padding: 8px; overflow-x: auto; }
	.kinds button
	{
		height: 34px; padding: 0 14px; border: 1px solid var(--line); border-radius: 17px; background: none; flex: none;
		color: var(--ink); font: inherit; font-size: 15px; cursor: pointer; touch-action: manipulation;
	}
	.kinds button.on { background: var(--select); border-color: var(--select); color: var(--select-ink); }
	.about { margin: 0; padding: 0 12px 8px; color: var(--faint); font-size: 14px; }
	.tiles
	{
		flex: 1; overflow-y: auto; overscroll-behavior: contain; display: grid; gap: 10px; padding: 4px 10px;
		padding-bottom: max(16px, env(safe-area-inset-bottom)); grid-template-columns: repeat(auto-fill, minmax(132px, 1fr));
		align-content: start;
	}
	.tile
	{
		display: flex; flex-direction: column; align-items: center; gap: 4px; padding: 6px 2px; border: none; border-radius: 10px;
		background: none; color: inherit; font: inherit; font-size: 14px; cursor: pointer; touch-action: manipulation;
	}
	.tile:active { background: var(--line); }
	.tile canvas { border: 1px solid var(--line); border-radius: 4px; }
</style>
