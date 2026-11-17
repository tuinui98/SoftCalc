<!-- App.svelte - Mockup B: Svelte with hand-made parts. Sci in a drawer over the keys only (swipe from
     the left edge of the keys, the Sci button or "Sci ▸"), menus and Settings as bottom sheets, Android
     back closes them. Wide screens show Sci beside Basic; a phone on its side puts History left. -->
<script lang="ts">
	import { onMount, tick } from "svelte";
	import Keypad from "./Keypad.svelte";
	import Sheet from "./Sheet.svelte";
	import { Calculator } from "../shared/calc";
	import { BASIC, SCIENTIFIC, type Key } from "../shared/keys";
	import { applyTheme, loadTheme, saveTheme, trackKeys, WIDE } from "../shared/page";
	import { holdToRepeat } from "../shared/repeat";

	const calc = new Calculator(new URL("./worker.js", document.baseURI).href);
	const snap = () => ({
		text: calc.text, entries: [...calc.entries], highlight: calc.highlight, settings: { ...calc.settings },
		second: calc.second, armed: calc.storeArmed, status: calc.status,
	});
	let view = $state(snap());
	let input: HTMLInputElement;
	let historyBox: HTMLElement;
	let keysBox: HTMLElement;
	let backBox: HTMLElement;
	let systemKeys = $state(false);
	let settingsOpen = $state(false);
	let theme = $state(loadTheme());
	let wide = $state(matchMedia(WIDE).matches);
	let sheet: { title: string; items: string[]; done: (item: string | null) => void } | null = $state(null);

	//==================================================================
	//  Drawer: 0 closed .. 1 open; dragging follows the finger
	//==================================================================
	let drawer = $state(0);
	let dragging = $state(false);
	let drawerBox: HTMLElement;
	let dragFrom = 0, dragStart = 0, dragWidth = 1;
	let ignoreBack = 0;    // our own history.back() after a pick, not the user's Back

	function dragBegin(event: PointerEvent)
	{
		dragging = true;
		dragFrom = event.clientX;
		dragStart = drawer;
		dragWidth = drawerBox.getBoundingClientRect().width || 1;
		(event.currentTarget as HTMLElement).setPointerCapture(event.pointerId);
	}

	function dragMove(event: PointerEvent)
	{
		if (dragging)
		{
			drawer = Math.max(0, Math.min(1, dragStart + (event.clientX - dragFrom) / dragWidth));
		}
	}

	function dragEnd()
	{
		if (!dragging)
		{
			return;
		}
		dragging = false;
		setDrawer(drawer > (dragStart > 0.5 ? 0.7 : 0.3));
	}

	function setDrawer(open: boolean)
	{
		const was = drawer > 0;
		drawer = open ? 1 : 0;
		if (open && !was)
		{
			history.pushState({ layer: "drawer" }, "");
		}
	}

	// Android back (and the browser's Back) closes the top layer first
	function onBack()
	{
		if (ignoreBack > 0)
		{
			ignoreBack--;
			return;
		}
		if (sheet)
		{
			sheet.done(null);
		}
		else if (settingsOpen)
		{
			settingsOpen = false;
		}
		else if (drawer > 0)
		{
			drawer = 0;
		}
	}

	function pick(title: string, items: string[]): Promise<string | null>
	{
		history.pushState({ layer: "sheet" }, "");
		return new Promise(resolve =>
		{
			sheet = { title, items, done: item => { sheet = null; resolve(item); } };
		});
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
		const onWide = () => { wide = media.matches; if (wide) drawer = 0; };
		media.addEventListener("change", onWide);
		addEventListener("popstate", onBack);
		const untrack = trackKeys(keysBox);
		const unhold = holdToRepeat(backBox, () => calc.backspace());
		return () =>
		{
			unsubscribe(); untrack(); unhold();
			media.removeEventListener("change", onWide);
			removeEventListener("popstate", onBack);
		};
	});

	const status = $derived(view.armed ? "STO: press x or y" : view.second ? "2nd" : view.status);

	async function press(key: Key)
	{
		const menu = await calc.press(calc.resolve(key));
		if (menu === "func") await openFunctions();
		else if (menu === "units") await openConversions();
	}

	// A sheet closed by a pick leaves its history entry; drop it so Back stays in step
	async function chosen(item: string | null)
	{
		if (item !== null && history.state?.layer === "sheet")
		{
			ignoreBack++;
			history.back();
			await new Promise(resolve => setTimeout(resolve, 30));
		}
		return item;
	}

	async function openFunctions()
	{
		const group = await chosen(await pick("Functions", Object.keys(calc.catalog.functions)));
		const name = group && await chosen(await pick(group, calc.catalog.functions[group]));
		if (name) await calc.insertFunction(name);
	}

	async function openConversions()
	{
		const groups = Object.keys(calc.catalog.conversions).filter(group => group !== "Currency");
		const group = await chosen(await pick("Conversions", groups));
		const units = group ? calc.catalog.conversions[group] : [];
		const source = group && await chosen(await pick(`${group}: from`, units));
		const target = source && await chosen(await pick(`${source} to`, units.filter(unit => unit !== source)));
		if (source && target) await calc.insertConversion(source, target);
	}

	async function openMore()
	{
		const choice = await chosen(await pick("SoftCalc", ["Functions", "Conversions", "Settings", "Clear History", "Ionic mockup"]));
		if (choice === "Functions") await openFunctions();
		else if (choice === "Conversions") await openConversions();
		else if (choice === "Settings") { settingsOpen = true; history.pushState({ layer: "settings" }, ""); }
		else if (choice === "Clear History") calc.clearHistory();
		else if (choice === "Ionic mockup") location.href = "ionic.html";
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

	function setTheme(value: string)
	{
		theme = value;
		saveTheme(value);
		applyTheme(value);
	}
</script>

<div class="app">
	<header>
		{#if !wide}<button class="bar-button" onclick={() => setDrawer(true)}>Sci</button>{/if}
		<h1>SoftCalc</h1>
		<button class="bar-button" class:on={systemKeys} onclick={toggleKeyboard} aria-label="System keyboard">⌨</button>
		<button class="bar-button" onclick={openMore} aria-label="More">⋮</button>
	</header>

	<div class="calc">
		<div bind:this={historyBox} class="history">
			{#if view.entries.length === 0}<div class="empty-history">Type with the keys; tap a line to use it again.</div>{/if}
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
		<div bind:this={keysBox} class="keys">
			{#if wide}
				<Keypad keys={SCIENTIFIC} sci second={view.second} armed={view.armed} base={view.settings.base}
					angle={view.settings.angle} onpress={press} />
			{:else}
				<button class="sci-tab" onclick={() => setDrawer(true)} aria-label="Scientific keys">Sci ▸</button>
				<div class="edge" role="presentation" onpointerdown={dragBegin} onpointermove={dragMove}
					onpointerup={dragEnd} onpointercancel={dragEnd}></div>
			{/if}
			<Keypad keys={BASIC} second={view.second} armed={view.armed} base={view.settings.base}
				angle={view.settings.angle} onpress={press} />
		</div>
	</div>

	{#if !wide}
		<!-- The drawer moves inside the keys box only, so History and Input stay in view (owner 2026-11-18) -->
		<div class="drawer-clip">
			<div bind:this={drawerBox} class="drawer" class:dragging class:open={drawer > 0}
				style="transform: translateX({(drawer - 1) * 100}%)"
				onpointerdown={event => { if ((event.target as HTMLElement).closest(".key, .sci-tab") == null) dragBegin(event); }}
				onpointermove={dragMove} onpointerup={dragEnd} onpointercancel={dragEnd}>
				<button class="sci-tab" onclick={() => history.back()}>◂ Basic</button>
				<Keypad keys={SCIENTIFIC} sci second={view.second} armed={view.armed} base={view.settings.base}
					angle={view.settings.angle} onpress={press} />
			</div>
		</div>
	{/if}

	{#if sheet}<Sheet title={sheet.title} items={sheet.items} onpick={item => item === null ? history.back() : sheet?.done(item)} />{/if}

	{#if settingsOpen}
		<div class="backdrop" role="presentation" onclick={() => history.back()}></div>
		<div class="settings" role="dialog" aria-label="Settings">
			<div class="settings-head"><span>Settings</span><button class="bar-button" onclick={() => history.back()}>Done</button></div>
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
	.edge { position: absolute; left: 0; top: 26px; bottom: 0; width: 18px; z-index: 2; touch-action: none; }
	.drawer-clip
	{
		position: fixed; z-index: 21; overflow: hidden; pointer-events: none;
		top: var(--keys-top); left: var(--keys-left); width: var(--keys-width); height: var(--keys-h);
	}
	.drawer
	{
		position: absolute; inset: 0; display: flex; padding-top: 26px; background: var(--key-bg);
		transition: transform 200ms ease-out; touch-action: pan-y;
	}
	.drawer.open, .drawer.dragging { pointer-events: auto; box-shadow: 4px 0 16px rgb(0 0 0 / 0.3); }
	.drawer.dragging { transition: none; }
	.drawer > :global(.keypad) { flex: 1; }
	.settings-head { display: flex; align-items: center; justify-content: space-between; padding: 6px 6px 6px 16px; font-weight: 600; }
	.backdrop { position: fixed; inset: 0; background: rgb(0 0 0 / 0.35); z-index: 30; }
	.settings
	{
		position: fixed; left: 0; right: 0; bottom: 0; z-index: 31; background: var(--page); border-radius: 16px 16px 0 0;
		padding: 4px 0 calc(16px + env(safe-area-inset-bottom)); max-width: 640px; margin: 0 auto;
		box-shadow: 0 -4px 20px rgb(0 0 0 / 0.25);
	}
	.row { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 10px 16px; flex-wrap: wrap; }
	.seg { display: flex; border: 1px solid var(--line); border-radius: 10px; overflow: hidden; }
	.seg button { border: none; background: none; color: var(--ink); font: inherit; padding: 8px 12px; cursor: pointer; }
	.seg button.on { background: var(--select); color: var(--select-ink); }
</style>
