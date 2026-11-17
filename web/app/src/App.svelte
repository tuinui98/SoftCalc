<!-- App.svelte - SoftCalc web app, main screen (Svelte, owner's choice 2026-11-18).
     Keys: Basic and Sci are two pages; a swipe left or right on the keys flips between them and the
     keys follow the finger, a swipe up opens the menu, "Sci ▸" / "◂ Basic" flip too. The system's
     own gestures and Back are left alone. Menus and Settings are bottom sheets. Wide screens show
     Sci beside Basic; a phone on its side puts History left and the keys right. -->
<script lang="ts">
	import { onMount, tick } from "svelte";
	import Keypad from "./components/Keypad.svelte";
	import Sheet from "./components/Sheet.svelte";
	import { Calculator } from "./lib/calc";
	import { Engine } from "./lib/engine";
	import { BASIC, SCIENTIFIC, type Key } from "./lib/keys";
	import { applyTheme, loadTheme, saveTheme, WIDE } from "./lib/page";
	import { closeOnTouch, holdToRepeat } from "./lib/repeat";
	import type { Store } from "./lib/store";
	import { swipeOn } from "./lib/swipe";

	let { store, kept }: { store: Store; kept: boolean } = $props();
	// The store is given once at start and never changes
	// svelte-ignore state_referenced_locally
	const calc = new Calculator(new Engine(new URL("./worker.js", document.baseURI).href), store);
	const snap = () => ({
		text: calc.text, entries: [...calc.entries], highlight: calc.highlight, settings: { ...calc.settings },
		second: calc.second, armed: calc.storeArmed, status: calc.status,
	});
	let view = $state(snap());
	let input: HTMLInputElement;
	let historyBox: HTMLElement;
	let pagesBox: HTMLElement;
	let backBox: HTMLElement;
	let systemKeys = $state(false);
	let settingsOpen = $state(false);
	let theme = $state(loadTheme());
	let wide = $state(matchMedia(WIDE).matches);
	let sheet: { title: string; items: string[]; done: (item: string | null) => void } | null = $state(null);

	//==================================================================
	//  Pages: the shown one moves by dx; the other one sits next to it,
	//  on the side the finger comes from (dir), and follows
	//==================================================================
	let sci = $state(false);
	let dx = $state(0);
	let dir = $state(1);           // +1: the other page is on the left; -1: on the right
	let sliding = $state(false);   // CSS transition on (a finished swipe, or a tap on Sci / Basic)
	let pageWidth = $state(1);    // state: the other page's place depends on it (owner 2026-11-18: glitch on the first swipe)

	const shown = $derived(`transform: translateX(${dx}px)`);
	const other = $derived(`transform: translateX(${dx + (dir > 0 ? -pageWidth : pageWidth)}px)`);
	const otherVisible = $derived(dx !== 0 || sliding);

	function dragMove(offset: number)
	{
		sliding = false;
		pageWidth = pagesBox.clientWidth || 1;
		dx = offset;
		if (offset !== 0)
		{
			dir = offset > 0 ? 1 : -1;
		}
	}

	// From where the pages are now, flip (or settle back) with the CSS transition
	async function settle(flip: boolean)
	{
		if (flip)
		{
			// The incoming page becomes the shown one, at the same place on the screen
			sci = !sci;
			dx = dx - dir * pageWidth;
			dir = -dir;
		}
		await tick();
		startSlide();
	}

	// The start place is drawn first (reading the size makes the browser apply it), then the
	// transition takes the pages to dx 0. No requestAnimationFrame: it may not run at all.
	function startSlide()
	{
		void pagesBox.offsetWidth;
		sliding = true;
		dx = 0;
		const slide = ++slideCount;
		setTimeout(() => { if (slide === slideCount) sliding = false; }, 400);    // in case no transitionend comes
	}
	let slideCount = 0;

	function dragEnd(offset: number, velocity: number)
	{
		const far = Math.abs(offset) > pageWidth * 0.3;
		const flick = Math.abs(offset) > 40 && Math.abs(velocity) > 0.6 && Math.sign(velocity) === Math.sign(offset);
		settle(offset !== 0 && (far || flick));
	}

	// A tap on "Sci ▸" (Sci comes from the left) or "◂ Basic" (Basic comes from the right)
	async function flipTo(toSci: boolean)
	{
		if (sci === toSci)
		{
			return;
		}
		pageWidth = pagesBox.clientWidth || 1;
		sliding = false;
		// The incoming page starts beside the old one (which is still in place), then both slide
		sci = toSci;
		dx = toSci ? -pageWidth : pageWidth;
		dir = toSci ? -1 : 1;
		await tick();
		startSlide();
	}

	// Only the page's own slide counts: a key's press effect also ends with a transitionend that
	// bubbles up, and stopping the slide on it made the pages jump on a phone (owner 2026-11-18)
	function slid(event: TransitionEvent)
	{
		if (event.target === event.currentTarget && event.propertyName === "transform")
		{
			sliding = false;
		}
	}

	//==================================================================
	//  Sheets
	//==================================================================
	function pick(title: string, items: string[]): Promise<string | null>
	{
		return new Promise(resolve =>
		{
			sheet = { title, items, done: item => { sheet = null; resolve(item); } };
		});
	}

	async function openFunctions()
	{
		const group = await pick("Functions", Object.keys(calc.catalog.functions));
		const name = group && await pick(group, calc.catalog.functions[group]);
		if (name) await calc.insertFunction(name);
	}

	async function openConversions()
	{
		const groups = Object.keys(calc.catalog.conversions).filter(group => group !== "Currency");
		const group = await pick("Conversions", groups);
		const units = group ? calc.catalog.conversions[group] : [];
		const source = group && await pick(`${group}: from`, units);
		const target = source && await pick(`${source} to`, units.filter(unit => unit !== source));
		if (source && target) await calc.insertConversion(source, target);
	}

	async function openMore()
	{
		const choice = await pick("SoftCalc", ["Functions", "Conversions", "Settings", "Clear History"]);
		if (choice === "Functions") await openFunctions();
		else if (choice === "Conversions") await openConversions();
		else if (choice === "Settings") settingsOpen = true;
		else if (choice === "Clear History") calc.clearHistory();
	}

	//==================================================================
	//  Calculator
	//==================================================================
	onMount(() =>
	{
		applyTheme(theme);
		const unsubscribe = calc.subscribe(async () =>
		{
			const before = view.entries.length;
			view = snap();
			await tick();
			input.value = calc.text;
			input.setSelectionRange(calc.caret, calc.caret);
			if (view.entries.length !== before)
			{
				historyBox.scrollTo({ top: historyBox.scrollHeight });
			}
		});
		const media = matchMedia(WIDE);
		const onWide = () => { wide = media.matches; dx = 0; sliding = false; };
		media.addEventListener("change", onWide);
		const unswipe = swipeOn(pagesBox, {
			move: offset => { if (!wide) dragMove(offset); },
			end: (offset, velocity) => { if (!wide) dragEnd(offset, velocity); },
			up: () => { openMore(); },
		});
		const unhold = holdToRepeat(backBox, () => calc.backspace());
		const sizer = new ResizeObserver(() => { pageWidth = pagesBox.clientWidth || 1; });
		sizer.observe(pagesBox);
		return () =>
		{
			unsubscribe(); unswipe(); unhold(); sizer.disconnect();
			media.removeEventListener("change", onWide);
		};
	});

	const status = $derived(view.armed ? "STO: press x or y" : view.second ? "2nd"
		: kept ? view.status : view.status + " (this browser does not keep History)");

	async function press(key: Key)
	{
		const menu = await calc.press(calc.resolve(key));
		if (menu === "func") await openFunctions();
		else if (menu === "units") await openConversions();
	}

	function onInput(event: Event)
	{
		const box = event.target as HTMLInputElement;
		calc.typed(box.value, box.selectionStart ?? box.value.length);
	}

	function onKey(event: KeyboardEvent)
	{
		if (event.key === "Enter") { event.preventDefault(); calc.submit(); }
		else if (event.key === "ArrowUp") { event.preventDefault(); calc.walk(true); }
		else if (event.key === "ArrowDown") { event.preventDefault(); calc.walk(false); }
		else if (event.key === "Escape") { calc.cancelWalk(); }
	}

	async function toggleKeyboard()
	{
		systemKeys = !systemKeys;
		await tick();
		input.blur();
		input.focus();
	}

	const outside = (box: HTMLElement) => ({ destroy: closeOnTouch(box, () => { settingsOpen = false; }) });

	function setTheme(value: string)
	{
		theme = value;
		saveTheme(value);
		applyTheme(value);
	}
</script>

{#snippet basicPage()}
	<button class="sci-tab" onclick={() => flipTo(true)}>Sci ▸</button>
	<Keypad keys={BASIC} second={view.second} armed={view.armed} base={view.settings.base}
		angle={view.settings.angle} onpress={press} />
{/snippet}

{#snippet sciPage()}
	<button class="sci-tab" onclick={() => flipTo(false)}>◂ Basic</button>
	<Keypad keys={SCIENTIFIC} sci second={view.second} armed={view.armed} base={view.settings.base}
		angle={view.settings.angle} onpress={press} />
{/snippet}

<div class="app">
	<header>
		<h1>SoftCalc</h1>
		<button class="bar-button" class:on={systemKeys} onclick={toggleKeyboard} aria-label="System keyboard">⌨</button>
		<button class="bar-button" onclick={openMore} aria-label="More">⋮</button>
	</header>

	<div class="calc">
		<div bind:this={historyBox} class="history">
			{#if view.entries.length === 0}<div class="empty-history">Type with the keys; tap a line to use it again.
				Swipe the keys left or right for Sci, up for the menu.</div>{/if}
			{#each view.entries as entry, index}
				<div class="entry {entry.kind}" class:selected={index === view.highlight} role="button" tabindex="-1"
					onclick={() => calc.tapEntry(index)} onkeydown={() => {}}>
					<div class="top"><span>{entry.input}</span>
						{#if entry.display !== entry.input}<span class="sub">{entry.display}</span>{/if}</div>
					<div class="result">{entry.text}</div>
				</div>
			{/each}
		</div>
		<div class="hint">{status}</div>
		<div class="input-row">
			<input bind:this={input} inputmode={systemKeys ? "text" : "none"} autocomplete="off" autocapitalize="off"
				spellcheck="false" placeholder="0" oninput={onInput} onkeydown={onKey}
				onclick={event => calc.moveCaret(event.currentTarget.selectionStart ?? 0)} />
			<button bind:this={backBox} class="backspace" aria-label="Delete one character">⌫</button>
		</div>
		<div bind:this={pagesBox} class="keys pages" class:wide>
			{#if wide}
				<Keypad keys={SCIENTIFIC} sci second={view.second} armed={view.armed} base={view.settings.base}
					angle={view.settings.angle} onpress={press} />
				<Keypad keys={BASIC} second={view.second} armed={view.armed} base={view.settings.base}
					angle={view.settings.angle} onpress={press} />
			{:else}
				<!-- Each page keeps its keys; only the places change, so no keys are rebuilt mid-swipe -->
				<div class="page" class:sliding class:hidden={sci && !otherVisible} style={sci ? other : shown}
					aria-hidden={sci} ontransitionend={slid}>
					{@render basicPage()}
				</div>
				<div class="page" class:sliding class:hidden={!sci && !otherVisible} style={sci ? shown : other}
					aria-hidden={!sci} ontransitionend={slid}>
					{@render sciPage()}
				</div>
			{/if}
		</div>
	</div>

	{#if sheet}<Sheet title={sheet.title} items={sheet.items} onpick={item => sheet?.done(item)} />{/if}

	{#if settingsOpen}
		<div class="backdrop" role="presentation" use:outside></div>
		<div class="settings" role="dialog" aria-label="Settings">
			<div class="settings-head"><span>Settings</span><button class="bar-button" onclick={() => settingsOpen = false}>Done</button></div>
			<div class="row"><span>Precision</span><div class="seg">
				{#each [32, 64, 128, 512] as digits}
					<button class:on={view.settings.precision === digits} onclick={() => calc.setSetting("precision", digits)}>{digits}</button>
				{/each}</div></div>
			<div class="row"><span>Angle</span><div class="seg">
				{#each ["deg", "rad", "grad"] as angle}
					<button class:on={view.settings.angle === angle} onclick={() => calc.setSetting("angle", angle)}>{angle}</button>
				{/each}</div></div>
			<div class="row"><span>Output base</span><div class="seg">
				{#each ["dec", "hex", "oct", "bin"] as base}
					<button class:on={view.settings.base === base} onclick={() => calc.setSetting("base", base)}>{base}</button>
				{/each}</div></div>
			<div class="row"><span>Theme</span><div class="seg">
				{#each ["system", "light", "dark"] as value}
					<button class:on={theme === value} onclick={() => setTheme(value)}>{value}</button>
				{/each}</div></div>
		</div>
	{/if}
</div>

<style>
	:global(html), :global(body) { margin: 0; height: 100%; font-family: system-ui, Roboto, sans-serif; background: var(--page); }
	:global(#app) { height: 100%; }
	.app { display: flex; flex-direction: column; height: 100%; overflow: hidden; color: var(--ink); background: var(--page); }
	header
	{
		display: flex; align-items: center; gap: 4px; padding: 4px 6px; padding-top: max(4px, env(safe-area-inset-top));
		border-bottom: 1px solid var(--line);
	}
	h1 { flex: 1; margin: 0 8px; font-size: 20px; font-weight: 500; }
	.bar-button
	{
		min-width: 44px; height: 40px; border: none; border-radius: 20px; background: none; color: var(--ink);
		font: inherit; font-size: 18px; padding: 0 12px; cursor: pointer;
	}
	.bar-button:active, .bar-button.on { background: var(--line); }
	.calc { flex: 1; min-height: 0; height: auto; }

	/* Keys: two pages side by side; only the shown one is in place */
	.pages { padding-top: 0; overflow: hidden; touch-action: none; }
	.pages.wide { padding-top: 0; }
	.page
	{
		position: absolute; inset: 0; display: flex; padding-top: 26px; background: var(--key-bg);
		will-change: transform;
	}
	.page.sliding { transition: transform 220ms ease-out; }
	.page.hidden { visibility: hidden; }
	.page > :global(.keypad) { flex: 1; }

	.backdrop { position: fixed; inset: 0; background: rgb(0 0 0 / 0.35); z-index: 30; touch-action: none; }
	.settings
	{
		position: fixed; left: 0; right: 0; bottom: 0; z-index: 31; background: var(--page); border-radius: 16px 16px 0 0;
		padding: 4px 0 calc(16px + env(safe-area-inset-bottom)); max-width: 640px; margin: 0 auto;
		box-shadow: 0 -4px 20px rgb(0 0 0 / 0.25);
	}
	.settings-head { display: flex; align-items: center; justify-content: space-between; padding: 6px 6px 6px 16px; font-weight: 600; }
	.row { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 10px 16px; flex-wrap: wrap; }
	.seg { display: flex; border: 1px solid var(--line); border-radius: 10px; overflow: hidden; }
	.seg button { border: none; background: none; color: var(--ink); font: inherit; padding: 8px 12px; cursor: pointer; }
	.seg button.on { background: var(--select); color: var(--select-ink); }
</style>
