<!-- CompoundView.svelte - ⋮ > Compound: savings with compound interest (owner 2026-11-20, C1).
     The desktop's Compound Interest window: Principal, Annual rate %, Years and Contribution take any
     expression; the engine's `schedule` works it out 300 ms after the last change (a field it
     cannot read shows why under it, and the last good result stays). The totals go to Input with a
     tap. Chart: paid in below, interest above, so the top edge is the balance; a finger or the
     mouse on it reads a period. Table: every period, drawn only where it is on screen, so daily
     periods over decades stay quick. A tablet on its side shows the inputs beside the chart. -->
<script lang="ts">
	import { onMount } from "svelte";
	import type { Calculator } from "../lib/calc";
	import {
		chartScale, COMPOUNDING, CONTRIBUTION_EVERY, routeError, rowAt, short, type CompoundInputs, type Field, type Schedule,
	} from "../lib/compound";
	import { tapOn } from "../lib/repeat";

	let { calc, onclose, oninsert }: {
		calc: Calculator;
		onclose: () => void;
		oninsert: (text: string) => void;
	} = $props();

	// svelte-ignore state_referenced_locally
	let inputs: CompoundInputs = $state({ ...calc.compound });
	let schedule: Schedule | null = $state(null);
	let errors: Partial<Record<Field, string>> = $state({});
	let general = $state("");
	let showTable = $state(false);
	let picked: number | null = $state(null);    // the row read on the chart
	const tap = (button: HTMLElement, act: () => void) => ({ destroy: tapOn(button, () => act()) });

	const LABELS: [Field, string][] = [
		["principal", "Principal"], ["annual_rate", "Annual rate %"], ["years", "Years"], ["contribution", "Contribution"],
	];

	//==================================================================
	//  Working it out: 300 ms after the last change, newest answer only
	//==================================================================
	let timer: ReturnType<typeof setTimeout> | undefined;
	let request = 0;

	function changed()
	{
		clearTimeout(timer);
		timer = setTimeout(() => void work(), 300);
	}

	async function work()
	{
		const mine = ++request;
		const answer = await calc.schedule($state.snapshot(inputs) as CompoundInputs);
		if (mine !== request)
		{
			return;
		}
		errors = {};
		general = "";
		if (answer.schedule)
		{
			schedule = answer.schedule;
			picked = null;
		}
		else
		{
			const { field, message } = routeError(answer.error ?? "");
			if (field)
			{
				errors = { [field]: message };
			}
			else
			{
				general = message;
			}
		}
	}

	//==================================================================
	//  Chart (CompoundChart.cs)
	//==================================================================
	const PAID = "#6e96cd";
	const INTEREST = "#78be78";
	const MARGIN = { left: 52, right: 12, top: 26, bottom: 24 };
	let chartBox: HTMLElement | undefined = $state();
	let canvas: HTMLCanvasElement | undefined = $state();
	let size = { width: 0, height: 0 };

	function colours()
	{
		const css = getComputedStyle(document.documentElement);
		const get = (name: string) => css.getPropertyValue(name).trim();
		return { page: get("--page"), ink: get("--ink"), faint: get("--faint"), line: get("--line"), select: get("--select") };
	}

	function draw()
	{
		const context = canvas?.getContext("2d");
		if (!canvas || !context || size.width === 0)
		{
			return;
		}
		const ratio = window.devicePixelRatio || 1;
		context.setTransform(ratio, 0, 0, ratio, 0, 0);
		const c = colours();
		context.fillStyle = c.page;
		context.fillRect(0, 0, size.width, size.height);
		const plot = { left: MARGIN.left, top: MARGIN.top, right: size.width - MARGIN.right, bottom: size.height - MARGIN.bottom };
		const s = schedule;
		if (!s || plot.right - plot.left < 40 || plot.bottom - plot.top < 40 || s.series.contributions.length === 0)
		{
			return;
		}
		const scale = chartScale(s, plot.right - plot.left, plot.bottom - plot.top);
		if (!(scale.top > 0) || !Number.isFinite(scale.top))
		{
			return;
		}
		const at = (year: number, money: number) => ({
			x: plot.left + year / scale.years * (plot.right - plot.left),
			y: plot.bottom - money / scale.yMax * (plot.bottom - plot.top),
		});
		// grid and labels
		context.font = "12px system-ui, sans-serif";
		context.strokeStyle = c.line;
		context.lineWidth = 1;
		context.fillStyle = c.faint;
		context.textBaseline = "middle";
		context.textAlign = "right";
		context.beginPath();
		for (let y = 0; y <= scale.yMax + scale.yStep / 2; y += scale.yStep)
		{
			const p = at(0, y);
			context.moveTo(plot.left, Math.round(p.y) + 0.5);
			context.lineTo(plot.right, Math.round(p.y) + 0.5);
			context.fillText(short(y), plot.left - 6, p.y);
		}
		context.textAlign = "center";
		context.textBaseline = "top";
		for (let x = 0; x <= scale.years + scale.xStep / 1000; x += scale.xStep)
		{
			const p = at(x, 0);
			context.moveTo(Math.round(p.x) + 0.5, plot.top);
			context.lineTo(Math.round(p.x) + 0.5, plot.bottom);
			context.fillText(String(Math.round(x * 100) / 100), p.x, plot.bottom + 5);
		}
		context.stroke();
		context.textAlign = "right";
		context.textBaseline = "middle";
		context.fillText("x: years", plot.right, 14);
		// stacked areas, at most one point per pixel column
		const { count, perYear } = scale;
		const step = Math.max(1, Math.floor(count / Math.max(1, plot.right - plot.left)));
		const indices: number[] = [];
		for (let i = 0; i < count; i += step)
		{
			indices.push(i);
		}
		if ((count - 1) % step !== 0)
		{
			indices.push(count - 1);
		}
		const paid = s.series.contributions, interest = s.series.interest;
		const area = (points: { x: number; y: number }[], colour: string) =>
		{
			context.fillStyle = colour;
			context.beginPath();
			points.forEach((p, index) => index === 0 ? context.moveTo(p.x, p.y) : context.lineTo(p.x, p.y));
			context.closePath();
			context.fill();
		};
		const lower = [at(0, s.series.principal), ...indices.map(i => at((i + 1) / perYear, paid[i]))];
		const upper = [at(0, s.series.principal), ...indices.map(i => at((i + 1) / perYear, paid[i] + interest[i]))];
		area([...upper, ...[...lower].reverse()], INTEREST);
		area([at(0, 0), ...lower, at(scale.years, 0)], PAID);
		context.strokeStyle = c.faint;
		context.beginPath();
		context.moveTo(plot.left, plot.top);
		context.lineTo(plot.left, plot.bottom);
		context.lineTo(plot.right, plot.bottom);
		context.stroke();
		// legend
		context.textAlign = "left";
		context.textBaseline = "middle";
		let lx = plot.left + 6;
		for (const [colour, text] of [[PAID, "Paid in"], [INTEREST, "Interest"]])
		{
			context.fillStyle = colour;
			context.fillRect(lx, 8, 12, 12);
			context.fillStyle = c.ink;
			context.fillText(text, lx + 16, 14);
			lx += 16 + context.measureText(text).width + 14;
		}
		// the period read
		if (picked !== null && picked < count)
		{
			const p = at((picked + 1) / perYear, paid[picked] + interest[picked]);
			context.strokeStyle = c.ink;
			context.setLineDash([4, 3]);
			context.beginPath();
			context.moveTo(p.x, plot.top);
			context.lineTo(p.x, plot.bottom);
			context.stroke();
			context.setLineDash([]);
			context.fillStyle = c.ink;
			context.beginPath();
			context.arc(p.x, p.y, 4, 0, Math.PI * 2);
			context.fill();
		}
	}

	function readAt(event: PointerEvent)
	{
		if (!schedule || !canvas)
		{
			return;
		}
		const box = canvas.getBoundingClientRect();
		const x = event.clientX - box.left;
		const width = size.width - MARGIN.left - MARGIN.right;
		const years = schedule.series.contributions.length / Math.max(1, schedule.periods_per_year);
		const year = Math.min(years, Math.max(0, (x - MARGIN.left) / width * years));
		picked = rowAt(schedule, year);
	}

	let reading = false;
	const onDown = (event: PointerEvent) => { reading = true; canvas?.setPointerCapture(event.pointerId); readAt(event); };
	const onMove = (event: PointerEvent) => { if (reading || event.pointerType === "mouse") readAt(event); };
	const onUp = () => { reading = false; };

	const pickedText = $derived.by(() =>
	{
		const row = picked !== null ? schedule?.rows[picked] : undefined;
		if (!row || !schedule)
		{
			return "";
		}
		const year = Math.round(row.period / Math.max(1, schedule.periods_per_year) * 100) / 100;
		return `Period ${row.period} (year ${year}): balance ${row.end}, interest ${row.interest}`;
	});

	$effect(() =>
	{
		void schedule;
		void picked;
		void showTable;
		draw();
	});

	//==================================================================
	//  Table: only the rows on screen are made
	//==================================================================
	const ROW = 34;
	let tableBox: HTMLElement | undefined = $state();
	let scrollTop = $state(0);
	let tableHeight = $state(400);
	const first = $derived(Math.max(0, Math.floor(scrollTop / ROW) - 5));
	const rows = $derived((schedule as Schedule | null)?.rows ?? []);
	const rowCount = $derived(rows.length);
	const shownRows = $derived(rows.slice(first, first + Math.ceil(tableHeight / ROW) + 10));

	//==================================================================
	//  Size
	//==================================================================
	function resized()
	{
		if (chartBox && canvas)
		{
			const box = chartBox.getBoundingClientRect();
			const ratio = window.devicePixelRatio || 1;
			size = { width: Math.floor(box.width), height: Math.floor(box.height) };
			canvas.width = Math.round(size.width * ratio);
			canvas.height = Math.round(size.height * ratio);
			canvas.style.width = size.width + "px";
			canvas.style.height = size.height + "px";
			draw();
		}
		if (tableBox)
		{
			tableHeight = tableBox.clientHeight;
		}
	}

	onMount(() =>
	{
		const observer = new ResizeObserver(resized);
		const watch = () =>
		{
			observer.disconnect();
			if (chartBox)
			{
				observer.observe(chartBox);
			}
			if (tableBox)
			{
				observer.observe(tableBox);
			}
		};
		watch();
		void work();
		const stop = $effect.root(() =>
		{
			$effect(() =>
			{
				void chartBox;
				void tableBox;
				watch();
				resized();
			});
		});
		return () =>
		{
			stop();
			observer.disconnect();
			clearTimeout(timer);
		};
	});
</script>

<div class="compound" role="dialog" aria-label="Compound interest">
	<div class="head">
		<span class="name">Compound</span>
		<button class="bar" use:tap={onclose}>Done</button>
	</div>
	<div class="body">
		<div class="side">
			<div class="fields">
				{#each LABELS as [field, label] (field)}
					<label class="field">
						<span>{label}</span>
						<input bind:value={inputs[field]} oninput={changed} autocapitalize="off" autocomplete="off" spellcheck="false" />
						{#if errors[field]}<span class="error">{errors[field]}</span>{/if}
					</label>
				{/each}
				<label class="field">
					<span>Compounding</span>
					<select bind:value={inputs.compounding} onchange={changed}>
						{#each COMPOUNDING as choice (choice)}<option value={choice}>{choice}</option>{/each}
					</select>
				</label>
				<label class="field">
					<span>Contribution every</span>
					<select bind:value={inputs.contribution_freq} onchange={changed}>
						{#each CONTRIBUTION_EVERY as choice (choice)}<option value={choice}>{choice}</option>{/each}
					</select>
				</label>
				<label class="check">
					<input type="checkbox" bind:checked={inputs.at_start} onchange={changed} />
					<span>Contribution at the start of each period</span>
				</label>
			</div>
			{#if general}<p class="error general">{general}</p>{/if}
			{#if schedule}
				<div class="totals">
					{#each [["Future value", schedule.future_value], ["Total contributions", schedule.total_contributions],
						["Total interest", schedule.total_interest]] as [label, value] (label)}
						<button class="total" use:tap={() => oninsert(value)} title="Tap: put it in Input">
							<span>{label}</span><b>{value}</b>
						</button>
					{/each}
				</div>
			{/if}
		</div>
		<div class="view">
			<div class="switch">
				<button class:on={!showTable} use:tap={() => { showTable = false; }}>Chart</button>
				<button class:on={showTable} use:tap={() => { showTable = true; }}>Table</button>
			</div>
			{#if !showTable}
				<div class="chart" bind:this={chartBox}>
					<canvas bind:this={canvas} onpointerdown={onDown} onpointermove={onMove} onpointerup={onUp} onpointercancel={onUp}></canvas>
				</div>
				<p class="read">{pickedText || "Drag a finger along the chart to read a period"}</p>
			{:else}
				<div class="table" bind:this={tableBox} onscroll={event => { scrollTop = (event.currentTarget as HTMLElement).scrollTop; }}>
					<div class="row header"><span>Period</span><span>Start</span><span>Interest</span><span>Added</span><span>End</span></div>
					<div style:height="{rowCount * ROW}px" style:position="relative">
						{#each shownRows as row, index (row.period)}
							<div class="row" style:top="{(first + index) * ROW}px">
								<span>{row.period}</span><span>{row.start}</span><span>{row.interest}</span><span>{row.contribution}</span><span>{row.end}</span>
							</div>
						{/each}
					</div>
				</div>
			{/if}
		</div>
	</div>
</div>

<style>
	.compound
	{
		position: fixed; inset: 0; z-index: 40; display: flex; flex-direction: column;
		background: var(--page); color: var(--ink); height: 100dvh;
	}
	.head
	{
		display: flex; align-items: center; justify-content: space-between; padding: 4px 6px;
		padding-top: max(4px, env(safe-area-inset-top)); border-bottom: 1px solid var(--line);
	}
	.name { margin: 0 10px; font-size: 20px; font-weight: 500; }
	.bar
	{
		min-width: 44px; height: 40px; border: none; border-radius: 20px; background: none; color: var(--select);
		font: inherit; font-size: 16px; padding: 0 12px; cursor: pointer; touch-action: manipulation;
	}
	.body { flex: 1; min-height: 0; overflow-y: auto; display: flex; flex-direction: column; }
	.side { padding: 8px 12px; }
	.fields { display: grid; grid-template-columns: 1fr 1fr; gap: 8px 12px; }
	.field { display: flex; flex-direction: column; gap: 3px; font-size: 13px; color: var(--faint); min-width: 0; }
	.field input, .field select
	{
		font: inherit; font-size: 16px; padding: 7px 9px; border: 1px solid var(--line); border-radius: 8px;
		background: var(--page); color: var(--ink); min-width: 0;
	}
	.check { grid-column: 1 / -1; display: flex; align-items: center; gap: 8px; font-size: 14px; }
	.check input { width: 20px; height: 20px; }
	.error { color: var(--error); font-size: 13px; }
	.general { margin: 6px 0 0; }
	.totals { display: flex; flex-direction: column; margin-top: 10px; border-top: 1px solid var(--line); }
	.total
	{
		display: flex; flex-wrap: wrap; justify-content: space-between; gap: 2px 10px; padding: 9px 2px; border: none; border-bottom: 1px solid var(--line);
		background: none; color: inherit; font: inherit; font-size: 15px; cursor: pointer; touch-action: manipulation; text-align: left;
	}
	.total:active { background: var(--line); }
	.total span { flex: none; }
	.total b { font-weight: 600; overflow-wrap: anywhere; text-align: right; margin-left: auto; }
	.view { display: flex; flex-direction: column; min-height: 320px; flex: 1; }
	.switch { display: flex; gap: 6px; padding: 6px 12px; }
	.switch button
	{
		height: 34px; padding: 0 16px; border: 1px solid var(--line); border-radius: 17px; background: none;
		color: var(--ink); font: inherit; font-size: 15px; cursor: pointer; touch-action: manipulation;
	}
	.switch button.on { background: var(--select); border-color: var(--select); color: var(--select-ink); }
	.chart { flex: 1; min-height: 240px; position: relative; }
	canvas { position: absolute; inset: 0; touch-action: none; display: block; }
	.read { margin: 0; padding: 6px 12px max(10px, env(safe-area-inset-bottom)); color: var(--faint); font-size: 14px; min-height: 20px; }
	.table { flex: 1; min-height: 240px; overflow-y: auto; overscroll-behavior: contain; position: relative; font-size: 13px; }
	.row
	{
		position: absolute; left: 0; right: 0; height: 34px; display: grid; grid-template-columns: 0.6fr 1fr 1fr 1fr 1fr;
		align-items: center; padding: 0 8px; gap: 6px; border-bottom: 1px solid var(--line); box-sizing: border-box;
		font-variant-numeric: tabular-nums;
	}
	.row span { text-align: right; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
	.row span:first-child { text-align: center; }
	.row.header { position: sticky; top: 0; z-index: 1; background: var(--page); color: var(--faint); font-weight: 600; }

	/* a tablet on its side: the inputs on the left, the chart or table on the right */
	@media (orientation: landscape) and (min-width: 700px)
	{
		.body { flex-direction: row; overflow: hidden; }
		.side { width: 340px; flex: none; overflow-y: auto; border-right: 1px solid var(--line); }
		.view { min-height: 0; }
	}
</style>
