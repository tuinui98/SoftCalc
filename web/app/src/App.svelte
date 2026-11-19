<!-- App.svelte - SoftCalc web app, main screen (Svelte, owner's choice 2026-11-18).
     Keys: Basic and Sci are two pages; a swipe left or right on the keys flips between them and the
     keys follow the finger, a swipe up opens the menu, "Sci ▸" / "◂ Basic" flip too. The system's
     own gestures and Back are left alone. Menus and Settings are bottom sheets. Wide screens show
     Sci beside Basic; a phone on its side puts History left and the keys right. -->
<script lang="ts">
	import { onMount, tick } from "svelte";
	import Keypad from "./components/Keypad.svelte";
	import CompoundView from "./components/CompoundView.svelte";
	import GraphView from "./components/GraphView.svelte";
	import HelpView from "./components/HelpView.svelte";
	import NamesSheet from "./components/NamesSheet.svelte";
	import Sheet from "./components/Sheet.svelte";
	import { Calculator, stepDecimals } from "./lib/calc";
	import { copyText } from "./lib/clipboard";
	import type { Engine } from "./lib/engine";
	import { BASIC, SCIENTIFIC, type Key } from "./lib/keys";
	import { applyTheme, loadHints, loadTextSize, loadTheme, saveHints, saveTextSize, saveTheme, TEXT_SIZES, WIDE } from "./lib/page";
	import { closeOnTouch, holdToRepeat } from "./lib/repeat";
	import type { Store } from "./lib/store";
	import { swipeOn } from "./lib/swipe";
	import { keyTaps } from "./lib/keytap";
	import { inApp, pickFiles, saveFiles, type OutFile } from "./lib/files";
	import { EXIT_MS, exitGuard } from "./lib/exitguard";
	import { exportNames, historyText, pcfBytes, withGraph } from "./lib/transfer";

	let { engine, store, kept }: { engine: Engine; store: Store; kept: boolean } = $props();
	// The engine and the store are given once at start and never change
	// svelte-ignore state_referenced_locally
	const calc = new Calculator(engine, store);
	calc.showHints = loadHints();
	const snap = () => ({
		text: calc.text, hint: calc.hint, entries: [...calc.entries], highlight: calc.highlight, settings: { ...calc.settings },
		second: calc.second, status: calc.status, loading: calc.loading, startTimes: calc.startTimes, definitions: calc.definitions, graph: [...calc.graph],
	});
	let view = $state(snap());
	let input: HTMLInputElement;
	let historyBox: HTMLElement;
	let pagesBox: HTMLElement;
	let basicPageBox: HTMLElement | undefined = $state();
	let sciPageBox: HTMLElement | undefined = $state();
	let backBox: HTMLElement;
	let systemKeys = $state(false);
	let settingsOpen = $state(false);
	let theme = $state(loadTheme());
	let showHints = $state(calc.showHints);
	let textSize = $state(loadTextSize());
	let namesOpen = $state(false);
	let helpOpen = $state(false);
	let graphOpen = $state(false);
	let compoundOpen = $state(false);
	let graphInner = $state(false);    // a menu or the editor open on the Graph
	let graphView: GraphView | undefined = $state();
	let helpTopic: string | null = $state(null);
	let hintOpen = $state(false);    // a long hint is cut to one line; a tap shows all of it (owner 2026-11-18)
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

	const YOURS = "Your functions";

	async function openFunctions()
	{
		const mine = calc.definitions.functions.map(fn => fn.name);
		const group = await pick("Functions", [...(mine.length > 0 ? [YOURS] : []), ...Object.keys(calc.catalog.functions)]);
		const name = group && await pick(group, group === YOURS ? mine : calc.catalog.functions[group]);
		if (name) await calc.insertFunction(name);
		focusInput();
	}

	// Currency: the common ones first, then the coins (prices from CoinGecko), then the other ECB codes
	function currencyCodes(): string[]
	{
		const common = calc.catalog.conversions.Currency ?? [];
		const all = ((calc.catalog as unknown as { currencies?: string[] }).currencies ?? []).filter(code => !common.includes(code));
		return [...common, "BTC", "ETH", "TRX", "USDT", ...all];
	}

	async function openConversions()
	{
		const groups = Object.keys(calc.catalog.conversions);
		const group = await pick("Conversions", groups);
		const units = group === "Currency" ? currencyCodes() : group ? calc.catalog.conversions[group] : [];
		const source = group && await pick(`${group}: from`, units);
		const target = source && await pick(`${source} to`, units.filter(unit => unit !== source));
		if (source && target) await calc.insertConversion(source, target);
		focusInput();
	}

	async function openMore()
	{
		const choice = await pick("SoftCalc", ["Functions", "Conversions", "Variables", "Graph", "Compound", "Settings", "Export", "Import", "Help", "Clear History"]);
		if (choice === "Functions") await openFunctions();
		else if (choice === "Conversions") await openConversions();
		else if (choice === "Variables") namesOpen = true;
		else if (choice === "Settings") settingsOpen = true;
		else if (choice === "Graph") graphOpen = true;
		else if (choice === "Compound") compoundOpen = true;
		else if (choice === "Help") { helpTopic = null; helpOpen = true; }
		else if (choice === "Export") await exportFiles();
		else if (choice === "Import") await importFiles();
		else if (choice === "Clear History") calc.clearHistory();
	}

	//==================================================================
	//  Export and Import: the desktop's history.jsonl and .pcf (owner 2026-11-19)
	//==================================================================
	const HISTORY_ITEM = "History";
	const NAMES_ITEM = "Variables and functions";

	async function exportFiles()
	{
		const what = await pick("Export", [HISTORY_ITEM, NAMES_ITEM, "Both"]);
		if (!what)
		{
			return;
		}
		try
		{
			const files: OutFile[] = [];
			const names = exportNames();
			if (what !== NAMES_ITEM)
			{
				// The .pcf's octet-stream downloads in Chrome on Android, its ndjson did not (owner 2026-11-19)
				files.push({ name: names.history, type: "application/octet-stream",
					bytes: new TextEncoder().encode(historyText(calc.historyLines())) });
			}
			if (what !== HISTORY_ITEM)
			{
				files.push({ name: names.definitions, type: "application/octet-stream", bytes: pcfBytes(withGraph(await calc.definitionsText(), calc.graph)) });
			}
			const where = await saveFiles(files);
			calc.say(files.length === 1 ? `Saved to ${where}: ${files[0].name}` : `Saved ${files.length} files to ${where}`);
		}
		catch (error)
		{
			calc.say("Not exported: " + (error instanceof Error ? error.message : String(error)));
		}
		focusInput();
	}

	async function importFiles()
	{
		const files = await pickFiles();
		for (const file of files)
		{
			try
			{
				await calc.importFile(file.name, new Uint8Array(await file.arrayBuffer()));
			}
			catch (error)
			{
				calc.say(`${file.name}: not imported: ` + (error instanceof Error ? error.message : String(error)));
			}
		}
		focusInput();
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
			if (calc.hint !== view.hint)
			{
				hintOpen = false;
			}
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
		const unhold = holdToRepeat(backBox, () => { focusInput(); calc.backspace(); });
		// The key is the one on the page being shown, where it will be when the slide ends
		const untap = keyTaps(pagesBox, () => (wide ? pagesBox : sci ? sciPageBox : basicPageBox) ?? pagesBox,
			id => { const key = [...BASIC, ...SCIENTIFIC].find(item => item.id === id); if (key) void press(key); });
		const sizer = new ResizeObserver(() => { pageWidth = pagesBox.clientWidth || 1; });
		sizer.observe(pagesBox);
		return () =>
		{
			unsubscribe(); unswipe(); unhold(); untap(); sizer.disconnect();
			media.removeEventListener("change", onWide);
		};
	});

	// The bar on top of Input while the engine loads (owner 2026-11-19): after each step, how far it
	// goes and in how many ms, about a phone's time for the next step. It slows as it nears the mark,
	// and a quicker step only makes it jump on to the next one.
	const LOAD_BAR: Record<string, [number, number]> = {
		page: [0.15, 600], worker: [0.65, 2500], python: [0.75, 300], unpack: [0.92, 800], import: [1, 400], done: [1, 150],
	};
	let barMoving = $state(false);    // first drawn empty, so its first move shows
	onMount(() => { requestAnimationFrame(() => requestAnimationFrame(() => { barMoving = true; })); });
	const bar = $derived(barMoving ? LOAD_BAR[view.loading] ?? [1, 150] : [0, 0]);

	const status = $derived(view.second ? "2nd"
		: kept ? view.status : view.status + " (this browser does not keep History)");

	// Every key, ⌫, a History tap and a menu pick bring the focus back to Input, so the caret
	// shows and the next key needs no tap on Input first (owner 2026-11-18). Only when it is not
	// already there: with the system keyboard on, refocusing would bring back a keyboard the
	// user just put away.
	function focusInput()
	{
		if (document.activeElement !== input)
		{
			input.focus({ preventScroll: true });
		}
	}

	async function press(key: Key)
	{
		focusInput();
		const menu = await calc.press(calc.resolve(key));
		if (menu === "func") await openFunctions();
		else if (menu === "units") await openConversions();
		focusInput();
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

	//==================================================================
	//  History line menu: a long press (or a right click) on a line
	//==================================================================
	async function openEntryMenu(index: number)
	{
		const entry = calc.entries[index];
		if (!entry || entry.kind === "notice")
		{
			return;
		}
		// Only a calculated line has a result worth using again (not "Variable Added" or an error)
		const result = entry.kind === "result";
		const choice = await pick(entry.input, [...(result ? ["Insert result"] : []), "Insert line",
			...(result ? ["Copy result"] : []), "Copy line", "Delete line"]);
		if (choice === "Insert result" || choice === "Insert line")
		{
			calc.insertText(choice === "Insert result" ? entry.text : entry.input, choice === "Insert result");
		}
		else if (choice === "Copy result" || choice === "Copy line")
		{
			const done = await copyText(choice === "Copy result" ? entry.text : entry.input);
			calc.say(done ? "Copied" : "Could not copy");
		}
		else if (choice === "Delete line")
		{
			await calc.deleteEntry(index);
		}
		focusInput();
	}

	// Fires after 500 ms without the finger moving; the tap that follows the lift is dropped,
	// so the line is not also recalled. The browser's own long-press menu is kept away.
	function longPress(line: HTMLElement, index: number)
	{
		let timer: ReturnType<typeof setTimeout> | undefined;
		let start = { x: 0, y: 0 };
		let fired = false;
		const stop = () => { clearTimeout(timer); timer = undefined; };
		const fire = () =>
		{
			stop();
			fired = true;
			setTimeout(() => { fired = false; }, 600);
			openEntryMenu(index);
		};
		const down = (event: PointerEvent) =>
		{
			start = { x: event.clientX, y: event.clientY };
			stop();
			timer = setTimeout(fire, 500);
		};
		const move = (event: PointerEvent) =>
		{
			if (Math.hypot(event.clientX - start.x, event.clientY - start.y) > 10)
			{
				stop();    // a scroll, not a press
			}
		};
		const menu = (event: Event) =>
		{
			event.preventDefault();
			if (!fired)
			{
				fire();
			}
		};
		const click = (event: MouseEvent) =>
		{
			if (fired)
			{
				event.stopImmediatePropagation();
				event.preventDefault();
			}
		};
		line.addEventListener("pointerdown", down);
		line.addEventListener("pointermove", move);
		for (const name of ["pointerup", "pointercancel", "pointerleave"])
		{
			line.addEventListener(name, stop);
		}
		line.addEventListener("contextmenu", menu);
		line.addEventListener("click", click, true);
		return {
			update(next: number) { index = next; },
			destroy() { stop(); },
		};
	}

	//==================================================================
	//  System Back closes the top layer first (menu, Variables, Settings),
	//  then leaves the app. Help, Variables and Settings go back to the ⋮
	//  menu that opened them; in Help a topic goes to the topics list first.
	//  Each level has one history entry. Entries are pushed only after a
	//  tap opened that level: Chrome on Android skips an entry pushed
	//  without one (from popstate), and Back then leaves the app (owner
	//  2026-11-18). Back only takes a level off; closing in the app takes
	//  its entries off too. The check waits a moment, so a menu that opens
	//  the next one (Functions, then a group) keeps its entry.
	//==================================================================
	function layerDepth(): number
	{
		// Help, Variables, Graph and Settings stand on the ⋮ menu that opened them
		if (helpOpen)
		{
			return helpTopic ? 3 : 2;
		}
		if (graphOpen)
		{
			return graphInner ? 3 : 2;
		}
		if (settingsOpen || namesOpen || compoundOpen)
		{
			return 2;
		}
		return sheet !== null ? 1 : 0;
	}
	const depth = $derived(layerDepth());
	let pushed = 0;
	let skipPop = false;

	$effect(() =>
	{
		void depth;
		setTimeout(() =>
		{
			const wanted = layerDepth();
			while (pushed < wanted)
			{
				history.pushState({ softcalcLayer: pushed + 1 }, "");
				pushed++;
			}
			if (pushed > wanted)
			{
				skipPop = true;
				history.go(wanted - pushed);
				pushed = wanted;
			}
		}, 0);
	});

	// The browser: on the main screen one more history entry, below the levels', takes the
	// first Back, which shows the note (exitguard.ts); the next Back leaves the page. It is
	// pushed when a finger lifts or a key goes down, so Chrome counts it as the user's.
	let guarded = false;
	let exitNote = $state(false);
	let exitTimer: ReturnType<typeof setTimeout> | undefined;
	exitGuard.onshow = () =>
	{
		exitNote = true;
		clearTimeout(exitTimer);
		exitTimer = setTimeout(() => { exitNote = false; }, EXIT_MS);
	};

	function guardExit()
	{
		exitGuard.reset();
		if (!inApp() && !guarded && pushed === 0 && layerDepth() === 0)
		{
			history.pushState({ softcalcGuard: true }, "");
			guarded = true;
		}
	}

	function onBack()
	{
		if (skipPop)
		{
			skipPop = false;
			return;
		}
		if (guarded && pushed === 0 && layerDepth() === 0)
		{
			guarded = false;
			exitGuard.press();
			return;
		}
		pushed = Math.max(0, pushed - 1);
		if (sheet) sheet.done(null);
		else if (helpOpen && helpTopic) helpTopic = null;
		else if (graphOpen && graphInner) graphView?.closeInner();
		else if (helpOpen || namesOpen || settingsOpen || graphOpen || compoundOpen)
		{
			helpOpen = false;
			namesOpen = false;
			settingsOpen = false;
			graphOpen = false;
			compoundOpen = false;
			void openMore();
		}
	}

	function setTextSize(size: number)
	{
		textSize = size;
		saveTextSize(size);
	}

	const decimalsShown = $derived(view.settings.decimals === null || view.settings.decimals === undefined
		? "all" : String(view.settings.decimals));

	function setHints(on: boolean)
	{
		showHints = on;
		saveHints(on);
		calc.setShowHints(on);
	}

	function setTheme(value: string)
	{
		theme = value;
		saveTheme(value);
		applyTheme(value);
	}
</script>

{#snippet basicPage()}
	<button class="sci-tab" onpointerdown={event => event.preventDefault()}
		onclick={() => { focusInput(); flipTo(true); }}>Sci ▸</button>
	<Keypad keys={BASIC} second={view.second} base={view.settings.base}
		angle={view.settings.angle} onpress={press} />
{/snippet}

{#snippet sciPage()}
	<button class="sci-tab" onpointerdown={event => event.preventDefault()}
		onclick={() => { focusInput(); flipTo(false); }}>◂ Basic</button>
	<Keypad keys={SCIENTIFIC} sci second={view.second} base={view.settings.base}
		angle={view.settings.angle} onpress={press} />
{/snippet}

<svelte:window onpopstate={onBack} onpointerup={guardExit} onkeydown={guardExit} />

{#if exitNote}
	<div class="exit-note" role="status">Press Back again to exit</div>
{/if}
<div class="app" style="--text-scale: {textSize / 100}">
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
					use:longPress={index}
					onclick={() => { calc.tapEntry(index); focusInput(); }} onkeydown={() => {}}>
					<div class="top"><span>{entry.input}</span>
						{#if entry.display !== entry.input}<span class="sub">{entry.display}</span>{/if}</div>
					<div class="result">{entry.text}</div>
					{#if entry.note}<div class="note">{entry.note}</div>{/if}
				</div>
			{/each}
		</div>
		<div class="hint">{status}</div>
		<div class="input-row">
			{#if view.loading !== "failed"}
				<div class="load-bar" class:gone={view.loading === "done"} style:--to={bar[0]} style:--ms="{bar[1]}ms"></div>
			{/if}
			<input bind:this={input} inputmode={systemKeys ? "text" : "none"} autocomplete="off" autocapitalize="off"
				spellcheck="false" placeholder="0" oninput={onInput} onkeydown={onKey}
				onclick={event => calc.moveCaret(event.currentTarget.selectionStart ?? 0)}
				onkeyup={event => calc.moveCaret(event.currentTarget.selectionStart ?? 0)} />
			<button bind:this={backBox} class="backspace" aria-label="Delete one character">⌫</button>
		</div>
		{#if showHints}
			<div class="input-hint" class:open={hintOpen} role="button" tabindex="-1"
				onclick={() => { hintOpen = !hintOpen; focusInput(); }} onkeydown={() => {}}>{view.hint}</div>
		{/if}
		<div bind:this={pagesBox} class="keys pages" class:wide>
			{#if wide}
				<Keypad keys={SCIENTIFIC} sci second={view.second} base={view.settings.base}
					angle={view.settings.angle} onpress={press} />
				<Keypad keys={BASIC} second={view.second} base={view.settings.base}
					angle={view.settings.angle} onpress={press} />
			{:else}
				<!-- Each page keeps its keys; only the places change, so no keys are rebuilt mid-swipe -->
				<div class="page" bind:this={basicPageBox} class:sliding class:hidden={sci && !otherVisible} style={sci ? other : shown}
					aria-hidden={sci} ontransitionend={slid}>
					{@render basicPage()}
				</div>
				<div class="page" bind:this={sciPageBox} class:sliding class:hidden={!sci && !otherVisible} style={sci ? shown : other}
					aria-hidden={!sci} ontransitionend={slid}>
					{@render sciPage()}
				</div>
			{/if}
		</div>
	</div>

	{#if sheet}<Sheet title={sheet.title} items={sheet.items} onpick={item => sheet?.done(item)} />{/if}

	{#if helpOpen}
		<HelpView bind:topic={helpTopic} onclose={() => { helpOpen = false; focusInput(); }}
			onexample={line => { helpOpen = false; calc.typed(line, line.length); focusInput(); }} />
	{/if}

	{#if compoundOpen}
		<CompoundView {calc} onclose={() => { compoundOpen = false; focusInput(); }}
			oninsert={text => { compoundOpen = false; calc.insertText(text, true); focusInput(); }} />
	{/if}
	{#if graphOpen}
		<GraphView bind:this={graphView} bind:inner={graphInner} {calc} functions={view.definitions.functions.filter(fn => fn.params.length === 1)}
			equations={view.graph} names={[...view.definitions.functions, ...view.definitions.variables].map(item => item.name)}
			onclose={() => { graphOpen = false; focusInput(); }}
			oninsert={(text, isResult) => { graphOpen = false; calc.insertText(text, isResult); focusInput(); }} />
	{/if}

	{#if namesOpen}
		<NamesSheet definitions={view.definitions} onclose={() => { namesOpen = false; focusInput(); }}
			oninsert={(name, isFunction) => { namesOpen = false; calc.insertName(name, isFunction); focusInput(); }}
			ondelete={name => calc.deleteName(name)} />
	{/if}

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
			<div class="row"><span>Decimals</span><div class="seg">
				<button class:on={decimalsShown === "all"} onclick={() => calc.setSetting("decimals", null)}>all</button>
				<button aria-label="Fewer decimals" onclick={() => calc.setSetting("decimals", stepDecimals(view.settings.decimals as number | null, -1))}>−</button>
				<span class="count">{decimalsShown === "all" ? "*" : decimalsShown}</span>
				<button aria-label="More decimals" onclick={() => calc.setSetting("decimals", stepDecimals(view.settings.decimals as number | null, +1))}>+</button>
				</div></div>
			<div class="row"><span>Pad zeros</span><div class="seg">
				{#each [true, false] as on}
					<button class:on={(view.settings.pad_zeros === true) === on} onclick={() => calc.setSetting("pad_zeros", on)}>{on ? "on" : "off"}</button>
				{/each}</div></div>
			<div class="row"><span>Word size</span><div class="seg">
				{#each [8, 16, 32, 64] as bits}
					<button class:on={view.settings.word_size === bits} onclick={() => calc.setSetting("word_size", bits)}>{bits}</button>
				{/each}</div></div>
			<div class="row"><span>Text size</span><div class="seg">
				{#each TEXT_SIZES as size}
					<button class:on={textSize === size} onclick={() => setTextSize(size)}>{size}%</button>
				{/each}</div></div>
			<div class="row"><span>Input hints</span><div class="seg">
				{#each [true, false] as on}
					<button class:on={showHints === on} onclick={() => setHints(on)}>{on ? "show" : "hide"}</button>
				{/each}</div></div>
			<div class="row"><span>Theme</span><div class="seg">
				{#each ["system", "light", "dark"] as value}
					<button class:on={theme === value} onclick={() => setTheme(value)}>{value}</button>
				{/each}</div></div>
			{#if view.startTimes}<div class="row start-times"><span>Start</span><span>{view.startTimes}</span></div>{/if}
		</div>
	{/if}
</div>

<style>
	:global(html), :global(body) { margin: 0; height: 100%; overflow: hidden; overscroll-behavior: none; font-family: system-ui, Roboto, sans-serif; background: var(--page); }
	:global(#app) { height: 100%; }
	/* Fixed to the screen's edges: the box Chrome on a phone shows now, with or without its address
	   bar; 100% or 100dvh with a page that could scroll pushed the bottom key row off the screen
	   (owner 2026-11-18, twice) */
	.app { position: fixed; inset: 0; display: flex; flex-direction: column; overflow: hidden; color: var(--ink); background: var(--page); }
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
	.input-row { position: relative; }
	.load-bar
	{
		/* faint: the yellow was too loud (owner 2026-11-19) */
		position: absolute; left: 0; right: 0; top: -1px; height: 2px; background: var(--faint); opacity: 0.45;
		transform-origin: left; transform: scaleX(var(--to)); transition: transform var(--ms) ease-out, opacity 400ms 300ms;
		pointer-events: none;
	}
	.load-bar.gone { opacity: 0; }

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
		max-height: 85vh; overflow-y: auto; overscroll-behavior: contain;
		box-shadow: 0 -4px 20px rgb(0 0 0 / 0.25);
	}
	.settings-head { display: flex; align-items: center; justify-content: space-between; padding: 6px 6px 6px 16px; font-weight: 600; }
	.row { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 10px 16px; flex-wrap: wrap; }
	.start-times > span:last-child { font-size: 13px; opacity: 0.75; }
	.seg { display: flex; border: 1px solid var(--line); border-radius: 10px; overflow: hidden; }
	.seg button { border: none; background: none; color: var(--ink); font: inherit; padding: 8px 12px; cursor: pointer; }
	.seg button.on { background: var(--select); color: var(--select-ink); }
	.seg .count { min-width: 2.4em; padding: 8px 4px; text-align: center; font-variant-numeric: tabular-nums; }
	.exit-note
	{
		position: fixed; left: 50%; bottom: 96px; transform: translateX(-50%); z-index: 60; pointer-events: none;
		padding: 10px 18px; border-radius: 20px; background: var(--ink); color: var(--page); font-size: 15px; white-space: nowrap;
	}
</style>
