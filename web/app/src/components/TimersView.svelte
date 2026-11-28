<!-- TimersView.svelte - ⋮ > Timers: stopwatch, timers and alarms (owner 2026-11-28).
     The desktop's Stopwatch & Timers window: the running numbers big, with buttons, beside what
     Input does with :sw, :timer and :alarm. The state is the Calculator's (timers.ts); a button
     calls the same code as a typed line, and a new timer or alarm goes through the engine as its
     :timer / :alarm line, so it takes the same calculations (0:05*3, now+0:15). -->
<script lang="ts">
	import { onMount } from "svelte";
	import type { Calculator } from "../lib/calc";
	import * as clocks from "../lib/timers";
	import type { Countdown } from "../lib/timers";

	let { calc, sounding, onquiet, onclose }: {
		calc: Calculator;
		sounding: boolean;
		onquiet: () => void;
		onclose: () => void;
	} = $props();

	type Tab = "stopwatch" | "timer" | "alarm";
	let tab: Tab = $state("stopwatch");
	let now = $state(Date.now());
	let entry = $state("");
	let name = $state("");
	let version = $state(0);    // bumped after a change, so the lists are read again
	const s = () => { void version; void now; return calc.timers; };

	onMount(() =>
	{
		const ticker = setInterval(() => { now = Date.now(); }, 100);
		const unsubscribe = calc.subscribe(() => { version++; });
		return () => { clearInterval(ticker); unsubscribe(); };
	});

	async function watch(does: string)
	{
		await calc.stopwatch(does);
		version++;
	}

	async function startOne()
	{
		const text = entry.trim();
		if (text.length === 0)
			return;
		const named = name.trim().length > 0 ? ` "${name.trim().replaceAll("\"", "")}"` : "";
		await calc.runTimerLine(`:${tab} ${text}${named}`);
		entry = "";
		name = "";
		version++;
	}

	function cancel(countdown: Countdown)
	{
		calc.cancelCountdown(countdown);
		version++;
	}

	const EXAMPLES: Record<string, string> = { timer: "5m, 90s, 1h30m, 0:05*3", alarm: "14:30, 30.11.2026 7:00, now+0:15" };
</script>

<div class="timers">
	<div class="head">
		<button class="bar" onclick={onclose} aria-label="Back">◂</button>
		<h2 class="name">Timers</h2>
		<div class="tabs">
			{#each [["stopwatch", "Stopwatch"], ["timer", "Timer"], ["alarm", "Alarm"]] as [id, label]}
				<button class:on={tab === id} onclick={() => { tab = id as Tab; }}>{label}</button>
			{/each}
		</div>
	</div>

	<div class="body">
		{#if tab === "stopwatch"}
			{@const sw = s()}
			<!-- this run's laps and pauses, or the last run's when none is going (owner 2026-11-28) -->
			{@const run = clocks.started(sw) || !sw.lastRun ? { laps: sw.laps, pauses: sw.pauses } : sw.lastRun}
			<div class="clock">{clocks.clock(clocks.elapsed(sw, now), 1)}</div>
			<div class="buttons">
				<button class="main" onclick={() => watch(clocks.running(sw) ? "stop" : "start")}>{clocks.running(sw) ? "Stop" : "Start"}</button>
				<button disabled={!clocks.started(sw)} onclick={() => watch(clocks.running(sw) ? "pause" : "go")}>
					{clocks.running(sw) ? "Pause" : "Go on"}</button>
				<button disabled={!clocks.started(sw)} onclick={() => watch("lap")}>Lap</button>
				<button disabled={!clocks.started(sw)} onclick={() => watch("reset")}>Reset</button>
			</div>
			<table class="list">
				<thead><tr><th>Lap</th><th>Time</th><th>Total</th></tr></thead>
				<tbody>
					{#each [...run.laps.map(l => ({ total: l.total, cells: [String(l.number), clocks.clock(l.seconds, 2), clocks.clock(l.total, 2)] })),
						...run.pauses.map(p => ({ total: p, cells: ["⏸", "paused", clocks.clock(p, 1)] }))].sort((a, b) => b.total - a.total) as row}
						<tr>{#each row.cells as cell}<td>{cell}</td>{/each}</tr>
					{/each}
				</tbody>
			</table>
			<p class="note">Stop and Lap go to History as values. In Input: :sw, :sw pause, :lap; F8 on a keyboard.</p>
		{:else}
			{@const list = s().countdowns.filter(c => c.kind === tab)}
			<div class="add">
				<input bind:value={entry} placeholder={EXAMPLES[tab]} autocomplete="off" autocapitalize="off" spellcheck="false"
					onkeydown={event => { if (event.key === "Enter") void startOne(); }} />
				<input class="short" bind:value={name} placeholder="name" autocomplete="off" />
				<button class="main" onclick={startOne}>Start</button>
			</div>
			<table class="list">
				<thead><tr><th>Name</th><th>Left</th><th>{tab === "alarm" ? "At" : "Done at"}</th><th></th></tr></thead>
				<tbody>
					{#each list as countdown (countdown)}
						<tr>
							<td>{countdown.name ?? countdown.label}</td><td>{clocks.left(countdown, now)}</td>
							<td>{clocks.at(countdown.due, tab === "alarm")}</td>
							<td><button class="cancel" onclick={() => cancel(countdown)} aria-label="Cancel">✕</button></td>
						</tr>
					{/each}
				</tbody>
			</table>
			<p class="note">In Input: {tab === "alarm" ? ":alarm 14:30 \"name\", :alarm off" : ":timer 5m \"name\", :timer off"}</p>
		{/if}
	</div>

	<div class="foot">
		<span>Sound when done</span>
		<div class="seg">
			{#each clocks.SOUNDS as sound}
				<button class:on={s().sound === sound} onclick={() => { calc.setSound(sound); version++; }}>{sound}</button>
			{/each}
		</div>
		<button disabled={!sounding} onclick={onquiet}>Quiet</button>
	</div>
</div>

<style>
	.timers
	{
		position: fixed; inset: 0; z-index: 40; display: flex; flex-direction: column;
		background: var(--page); color: var(--ink); height: 100dvh;
	}
	.head
	{
		display: flex; align-items: center; gap: 6px; padding: 4px 6px;
		padding-top: max(4px, env(safe-area-inset-top)); border-bottom: 1px solid var(--line);
	}
	.name { margin: 0 6px; font-size: 20px; font-weight: 500; flex: none; }
	.tabs { flex: 1; min-width: 0; display: flex; gap: 6px; overflow-x: auto; }
	.tabs button, .seg button, .buttons button, .add button, .foot > button
	{
		height: 36px; padding: 0 14px; border: 1px solid var(--line); border-radius: 18px; background: none; flex: none;
		color: var(--ink); font: inherit; font-size: 15px; cursor: pointer; touch-action: manipulation;
	}
	.tabs button.on, .seg button.on { background: var(--select); color: var(--page); border-color: var(--select); }
	button:disabled { opacity: 0.4; }
	button.main { background: var(--select); color: var(--page); border-color: var(--select); }
	.bar
	{
		min-width: 44px; height: 40px; border: none; border-radius: 20px; background: none; color: var(--select);
		font: inherit; font-size: 18px; cursor: pointer; touch-action: manipulation;
	}
	.body { flex: 1; min-height: 0; overflow-y: auto; padding: 8px 14px; }
	.clock { font-size: clamp(40px, 14vw, 72px); text-align: center; font-variant-numeric: tabular-nums; margin: 12px 0; }
	.buttons { display: flex; gap: 8px; justify-content: center; flex-wrap: wrap; margin-bottom: 12px; }
	.add { display: flex; gap: 6px; margin-bottom: 10px; }
	.add input
	{
		flex: 1; min-width: 0; height: 36px; padding: 0 10px; border: 1px solid var(--line); border-radius: 8px;
		background: none; color: var(--ink); font: inherit; font-size: 15px;
	}
	.add input.short { flex: 0 0 90px; }
	.list { width: 100%; border-collapse: collapse; font-variant-numeric: tabular-nums; }
	.list th { text-align: left; color: var(--faint); font-weight: normal; font-size: 13px; padding: 4px 6px; border-bottom: 1px solid var(--line); }
	.list td { padding: 6px; border-bottom: 1px solid var(--line); }
	.cancel { border: none; background: none; color: var(--faint); font-size: 16px; cursor: pointer; }
	.note { margin: 10px 0 0; color: var(--faint); font-size: 13px; }
	.foot
	{
		display: flex; align-items: center; gap: 8px; flex-wrap: wrap; padding: 8px 14px;
		padding-bottom: max(8px, env(safe-area-inset-bottom)); border-top: 1px solid var(--line); font-size: 14px;
	}
	.seg { display: flex; gap: 4px; }
</style>
