<!-- TimersView.svelte - ⋮ > Timers: stopwatch, timers and alarms (owner 2026-11-28).
     The desktop's Stopwatch & Timers window: the running numbers big, with buttons, beside what
     Input does with :sw, :timer and :alarm. The state is the Calculator's (timers.ts); a button
     calls the same code as a typed line, and a new timer or alarm goes through the engine as its
     :timer / :alarm line, so it takes the same calculations (0:05*3, now+0:15). A tap on a timer
     or an alarm opens its countdown page (owner 2026-11-28, the desktop's CountdownPage): three
     numbers chosen with a tap, Set / Started / Ends, Change time, ticks in the last 10 s. -->
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
	// a copy each time: the lists change in place, and the same object would not show a new lap (owner 2026-11-28)
	const s = () =>
	{
		void version; void now;
		const t = calc.timers;
		return { ...t, laps: [...t.laps], pauses: [...t.pauses], countdowns: [...t.countdowns] };
	};

	onMount(() =>
	{
		const ticker = setInterval(() => { now = Date.now(); tickSound(); }, 100);
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

	//==================================================================
	//  The countdown page of one timer or alarm
	//==================================================================
	let opened: Countdown | null = $state.raw(null);    // raw: the very countdown in calc.timers, not a copy
	let dismissed = $state(false);
	let menuFor: number | null = $state(null);
	let editing = $state(false);
	let newValue = $state("");
	let fromStart = $state(false);
	let editError = $state("");
	let lastWhole = Number.NaN;
	let audio: AudioContext | null = null;

	function openPage(countdown: Countdown)
	{
		opened = countdown;
		dismissed = false;
		editing = false;
		menuFor = null;
		lastWhole = Number.NaN;
		try { audio ??= new AudioContext(); } catch { /* no ticks here */ }
	}

	function closePage()
	{
		opened = null;
		menuFor = null;
	}

	// a tick a second in the last 10 s, unless Quiet countdown (owner 2026-11-28)
	function tickSound()
	{
		if (!opened)
			return;
		const left = (opened.due - Date.now()) / 1000;
		const whole = Math.ceil(left);
		if (whole !== lastWhole && !Number.isNaN(lastWhole) && left > 0 && left <= 10 && !calc.timers.quietCountdown && audio)
		{
			try
			{
				const tone = audio.createOscillator(), gain = audio.createGain();
				tone.frequency.value = 1200;
				gain.gain.setValueAtTime(0.15, audio.currentTime);
				gain.gain.exponentialRampToValueAtTime(0.001, audio.currentTime + 0.05);
				tone.connect(gain).connect(audio.destination);
				tone.start();
				tone.stop(audio.currentTime + 0.05);
			}
			catch { /* ignore */ }
		}
		lastWhole = whole;
	}

	function slotsOf(kind: string): clocks.Slot[]
	{
		return kind === "alarm" ? calc.timers.alarmSlots : calc.timers.timerSlots;
	}

	function choose(which: number, show: string | null, format: string | null)
	{
		const slot = slotsOf(opened!.kind)[which];
		if (show)
			slot[0] = show;
		if (format)
			slot[1] = format;
		menuFor = null;
		calc.saveTimers();
		version++;
	}

	function caption(slot: clocks.Slot): string
	{
		return slot[0] === "done" || slot[0] === "remain" ? clocks.SHOW_NAMES[slot[0]] : `${clocks.SHOW_NAMES[slot[0]]} · ${slot[1]}`;
	}

	function startEdit()
	{
		editing = !editing;
		newValue = opened?.label ?? "";
		editError = "";
		fromStart = false;
	}

	async function applyEdit()
	{
		if (!opened || newValue.trim().length === 0)
			return;
		const error = await calc.changeTime(opened, newValue.trim(), fromStart);
		if (error)
		{
			editError = error;
			return;
		}
		editing = false;
		dismissed = false;
		version++;
	}
</script>

<div class="timers">
	<div class="head">
		<button class="bar" onclick={onclose} aria-label="Back">◂</button>
		<h2 class="name">Timers</h2>
		<div class="tabs" class:hidden={opened !== null}>
			{#each [["stopwatch", "Stopwatch"], ["timer", "Timer"], ["alarm", "Alarm"]] as [id, label]}
				<button class:on={tab === id} onclick={() => { tab = id as Tab; }}>{label}</button>
			{/each}
		</div>
	</div>

	<div class="body">
		{#if opened}
			{@const c = opened}
			{@const slots = (void version, slotsOf(c.kind))}
			{@const left = (c.due - now) / 1000}
			<div class="cd" class:last10={left > 0 && left <= 10} class:done={left <= 0 && !dismissed}>
				<div class="cd-head">
					<span class="cd-name"><b>{c.kind === "alarm" ? "Alarm" : "Timer"}:</b> {c.name ?? c.label}</span>
					<span class="cd-now">{clocks.stamp(now)}</span>
				</div>
				<div class="dash">
					{#each [0, 1, 2] as which}
						<button class="slot s{which}" onclick={() => { menuFor = menuFor === which ? null : which; }}>
							<span class="num">{clocks.slot(c, slots[which][0], slots[which][1], now)}</span>
							<span class="what">{caption(slots[which])}</span>
						</button>
					{/each}
				</div>
				{#if menuFor !== null}
					{@const current = slots[menuFor]}
					<div class="menu" role="menu">
						<div class="menu-col">
							<h4>Show</h4>
							{#each clocks.SHOWS as show}
								<button class:on={current[0] === show} onclick={() => choose(menuFor!, show, null)}>
									{clocks.SHOW_NAMES[show]}{show === "left" ? " (counts down)" : show === "since" ? " (counts up)" : ""}</button>
							{/each}
						</div>
						<div class="menu-col">
							<h4>Format</h4>
							{#each clocks.FORMATS as format}
								<button class="fmt" class:on={current[1] === format && current[0] !== "done" && current[0] !== "remain"}
									onclick={() => choose(menuFor!, current[0] === "done" || current[0] === "remain" ? "left" : null, format)}>{format}</button>
							{/each}
						</div>
					</div>
				{/if}
				{#if left <= 0 && !dismissed}
					<div class="donenote">Done at {clocks.stamp(c.due).slice(11)}
						<button onclick={() => { dismissed = true; closePage(); }}>Dismiss</button></div>
				{/if}
				<dl class="info">
					<dt>Set</dt><dd>{clocks.length((c.due - c.set) / 1000)}{c.kind === "alarm" ? "  (waited from when it was set)" : ""}</dd>
					<dt>Started</dt><dd>{clocks.stamp(c.set)}</dd>
					<dt>Ends</dt><dd>{clocks.stamp(c.due)}</dd>
				</dl>
				{#if editing}
					<form class="edit" onsubmit={event => { event.preventDefault(); void applyEdit(); }}>
						<div class="row">
							<input bind:value={newValue} autocomplete="off" autocapitalize="off" spellcheck="false" placeholder={EXAMPLES[c.kind]} />
							<button type="submit" class="main">Set</button>
							<button type="button" onclick={() => { editing = false; }}>Cancel</button>
						</div>
						{#if c.kind === "timer"}
							<div class="row">
								<label><input type="radio" bind:group={fromStart} value={false} /> from now</label>
								<label><input type="radio" bind:group={fromStart} value={true} /> from its start</label>
							</div>
						{/if}
						<small class:err={editError !== ""}>{editError || EXAMPLES[c.kind]}</small>
					</form>
				{/if}
				<div class="cd-bar">
					<button onclick={closePage}>◂ Back</button>
					<button onclick={startEdit}>Change time…</button>
					<button class="danger" disabled={left <= 0} onclick={() => { cancel(c); closePage(); }}>Cancel {c.kind}</button>
					<label class="quiet"><input type="checkbox" checked={calc.timers.quietCountdown}
						onchange={event => { calc.timers.quietCountdown = event.currentTarget.checked; calc.saveTimers(); }} /> Quiet countdown</label>
				</div>
			</div>
		{:else if tab === "stopwatch"}
			{@const sw = s()}
			<!-- this run's laps and pauses, or the last run's when none is going (owner 2026-11-28) -->
			{@const run = clocks.started(sw) || !sw.lastRun ? { laps: sw.laps, pauses: sw.pauses } : sw.lastRun}
			<div class="clock">{clocks.clock(clocks.elapsed(sw, now), 1)}</div>
			<div class="buttons">
				<button class="main" onclick={() => watch(clocks.running(sw) ? "stop" : "start")}>{clocks.running(sw) ? "Stop" : "Start"}</button>
				<button disabled={!clocks.started(sw)} onclick={() => watch(clocks.running(sw) ? "pause" : "go")}>
					{clocks.running(sw) ? "Pause" : "Go on"}</button>
				<button disabled={!clocks.running(sw)} onclick={() => watch("lap")}>Lap</button>
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
						<tr class="item" onclick={() => openPage(countdown)}>
							<td>{countdown.name ?? countdown.label}</td><td>{clocks.left(countdown, now)}</td>
							<td>{clocks.at(countdown.due, tab === "alarm")}</td>
							<td><button class="cancel" onclick={event => { event.stopPropagation(); cancel(countdown); }} aria-label="Cancel">✕</button></td>
						</tr>
					{/each}
				</tbody>
			</table>
			<p class="note">Tap one for its countdown. In Input: {tab === "alarm" ? ":alarm 14:30 \"name\", :alarm off" : ":timer 5m \"name\", :timer off"}</p>
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
	.tabs.hidden { visibility: hidden; }
	tr.item { cursor: pointer; }

	/* the countdown page: numbers sized by the page's width, so a narrow window never cuts them (owner 2026-11-28) */
	.cd { --amber: #b45309; display: flex; flex-direction: column; gap: 8px; container-type: inline-size; }
	:global(.dark) .cd { --amber: #fbbf24; }
	.cd-head { display: flex; justify-content: space-between; align-items: baseline; gap: 8px; flex-wrap: wrap; }
	.cd-name { font-size: 16px; font-weight: 600; min-width: 0; overflow-wrap: anywhere; }
	.cd-name b { color: var(--faint); font-weight: 500; }
	.cd-now { color: var(--faint); font-size: 13px; font-variant-numeric: tabular-nums; }
	.dash { border: 1px solid var(--line); border-radius: 10px; display: grid; grid-template-columns: 1fr 1fr; overflow: hidden; }
	.slot
	{
		border: none; background: none; color: var(--ink); font: inherit; cursor: pointer; padding: 10px 6px; min-width: 0;
		display: flex; flex-direction: column; align-items: center; gap: 2px; touch-action: manipulation;
	}
	.slot.s0 { grid-column: 1 / -1; border-bottom: 1px solid var(--line); padding: 16px 6px 10px; }
	.slot.s1 { border-right: 1px solid var(--line); }
	.num { font-variant-numeric: tabular-nums; white-space: nowrap; max-width: 100%; overflow: hidden; text-overflow: clip; }
	.s0 .num { font-size: min(64px, 10.5cqw); font-weight: 500; }
	.s1 .num, .s2 .num { font-size: min(26px, 5.2cqw); }
	.what { font-size: 11px; color: var(--faint); text-transform: uppercase; letter-spacing: 0.05em; }
	.cd.last10 .s0 .num { color: var(--amber); }
	.cd.done .s0 .num { color: var(--error); font-weight: 700; }
	.cd.done .dash { border-color: var(--error); }
	.menu { display: flex; flex-wrap: wrap; gap: 4px 16px; border: 1px solid var(--line); border-radius: 10px; padding: 6px; }
	.menu-col { display: flex; flex-direction: column; min-width: 0; }
	.menu h4 { margin: 4px 6px; font-size: 11px; color: var(--faint); font-weight: 500; text-transform: uppercase; }
	.menu button { border: none; background: none; color: var(--ink); font: inherit; font-size: 14px; text-align: left; padding: 6px; border-radius: 6px; cursor: pointer; }
	.menu button.on { color: var(--select); font-weight: 600; }
	.menu .fmt { font-variant-numeric: tabular-nums; }
	.donenote { color: var(--error); text-align: center; font-size: 14px; }
	.donenote button { margin-left: 8px; border: 1px solid var(--error); color: var(--error); background: none; border-radius: 14px; padding: 2px 12px; font: inherit; }
	.info { display: grid; grid-template-columns: auto 1fr; gap: 4px 14px; margin: 0; font-size: 14px; }
	.info dt { color: var(--faint); }
	.info dd { margin: 0; font-variant-numeric: tabular-nums; min-width: 0; overflow-wrap: anywhere; }
	.edit { display: flex; flex-direction: column; gap: 6px; border: 1px dashed var(--line); border-radius: 10px; padding: 8px; }
	.edit .row { display: flex; gap: 6px; flex-wrap: wrap; align-items: center; }
	.edit input:not([type])
	{
		flex: 1; min-width: 0; height: 36px; padding: 0 10px; border: 1px solid var(--line); border-radius: 8px;
		background: none; color: var(--ink); font: inherit; font-size: 15px;
	}
	.edit button
	{
		height: 36px; padding: 0 14px; border: 1px solid var(--line); border-radius: 18px; background: none; color: var(--ink); font: inherit;
	}
	.edit small { color: var(--faint); }
	.edit small.err { color: var(--error); }
	.cd-bar { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
	.cd-bar button
	{
		height: 36px; padding: 0 14px; border: 1px solid var(--line); border-radius: 18px; background: none; color: var(--ink);
		font: inherit; font-size: 15px; cursor: pointer; touch-action: manipulation;
	}
	.cd-bar .danger { color: var(--error); }
	.cd-bar .quiet { display: inline-flex; gap: 6px; align-items: center; font-size: 14px; }
</style>
