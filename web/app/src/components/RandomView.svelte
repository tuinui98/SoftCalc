<!-- RandomView.svelte - ⋮ > Random (owner 2026-11-20): the desktop's Random window for the web.
     Simple: whole numbers, decimals, coins and dice; Advanced: values that follow a shape, drawn over
     its picture; Password: passwords with a strength line and an optional signature. Cards: draws from
     decks (CardsTab.svelte). Draws come from the browser's secure generator, or with Seed from the engine, repeatably
     and as typing rands(seed) and the Insert command line would give (Password always System). A tap on a result
     offers Insert (into Input) and Copy; a password is copied at once and cleared from the clipboard
     30 s later. Options are kept; results and the signature never are, and nothing goes to History. -->
<script lang="ts">
	import type { Calculator } from "../lib/calc";
	import { copySecret, copyText } from "../lib/clipboard";
	import {
		coins, command, decimals, DEFAULT_HEAD, DEFAULT_TAIL, dice, integers, MAX_COUNT, poolSize, randomPart, secure, signed,
		strengthBits, SYMBOL_GROUPS, toggleSymbols, newSeed, type PasswordOptions, type RandomPrefs,
	} from "../lib/random";
	import { tapOn } from "../lib/repeat";
	import {
		bars, check, curve, hint, line, MAX_COUNT as SHAPE_MAX, SHAPES, shapeCommand, summary, usesStrength, values,
		type Range,
	} from "../lib/shapes";
	import CardsTab from "./CardsTab.svelte";
	import Sheet from "./Sheet.svelte";

	let { calc, onclose, oninsert, inner = $bindable(false) }: {
		calc: Calculator;
		onclose: () => void;
		oninsert: (text: string) => void;
		inner?: boolean;    // a menu is open: the phone's Back closes it first (closeInner)
	} = $props();

	// svelte-ignore state_referenced_locally
	let p: RandomPrefs = $state({ ...calc.random });
	const tap = (button: HTMLElement, act: () => void) => ({ destroy: tapOn(button, () => act()) });
	const TABS: [string, string][] = [["simple", "Simple"], ["advanced", "Advanced"], ["password", "Password"], ["cards", "Cards"]];
	const SHOWN = 300;    // results listed; Copy all takes every one

	// Seed (R2): repeatable draws from the engine; never saved, as the desktop's
	let seedOn = $state(false);
	let seed = $state("");
	const seedText = $derived(seed.trim());
	const seeded = $derived(seedOn && p.tab !== "password" && seedText !== "");

	let saveTimer: ReturnType<typeof setTimeout> | undefined;
	$effect(() =>
	{
		const snapshot = $state.snapshot(p) as RandomPrefs;
		clearTimeout(saveTimer);
		saveTimer = setTimeout(() => calc.saveRandom(snapshot), 300);
	});

	let menuFor: string | null = $state(null);    // the result whose menu is open
	let cardsInner = $state(false);    // a menu or the deck editor open on the Cards tab
	let cardsTab: CardsTab | undefined = $state();
	$effect(() =>
	{
		inner = menuFor !== null || cardsInner;
	});

	export function closeInner()
	{
		menuFor = null;
		cardsTab?.closeInner();
	}

	//==================================================================
	//  Simple
	//==================================================================
	let numbers: string[] = $state([]);
	let numbersNote = $state("");
	let numbersError = $state(false);
	const KINDS: [string, string][] = [["whole", "Whole"], ["decimal", "Decimal"], ["coin", "Coin"], ["dice", "Dice"]];
	const count = (value: number) => Math.min(MAX_COUNT, Math.max(1, Math.floor(value) || 1));

	function show(lines: string[] | null, error: string | null, note: string)
	{
		numbersError = error !== null;
		numbers = lines ?? [];
		numbersNote = error ?? note;
	}

	async function drawNumbers()
	{
		const n = count(p.count);
		if (seeded)
		{
			await drawNumbersSeeded(n);
		}
		else if (p.kind === "whole")
		{
			const result = integers(secure, p.from, p.to, n, p.noRepeats);
			show(result.values, result.error, "");
		}
		else if (p.kind === "decimal")
		{
			const result = decimals(secure, p.from, p.to, Math.floor(p.places), n);
			show(result.values, result.error, "");
		}
		else if (p.kind === "coin")
		{
			const head = p.headWord.trim() || DEFAULT_HEAD, tail = p.tailWord.trim() || DEFAULT_TAIL;
			const flips = coins(secure, p.headsPercent, n);
			const heads = flips.filter(Boolean).length;
			show(flips.map(f => f ? head : tail), null, `${head} ${heads}, ${tail} ${flips.length - heads}`);
		}
		else
		{
			const sides = Math.max(2, Math.floor(p.sides) || 6);
			const rolls = dice(secure, sides, n);
			show(rolls.map(String), null, `Sum ${rolls.reduce((a, b) => a + b, 0)}`);
		}
	}

	// Seed: Insert command's line drawn n times by the engine (desktop DrawNumbersSeeded)
	async function drawNumbersSeeded(n: number)
	{
		const unique = p.kind === "whole" && p.noRepeats;
		const answer = await calc.seedDraws(seedText, simpleCommand(), n, unique);
		if (!answer.texts)
		{
			show(null, answer.error, "");
			return;
		}
		const texts = answer.texts;
		if (p.kind === "coin")
		{
			const head = p.headWord.trim() || DEFAULT_HEAD, tail = p.tailWord.trim() || DEFAULT_TAIL;
			const heads = texts.filter(t => t === "1").length;
			show(texts.map(t => t === "1" ? head : tail), null, `${head} ${heads}, ${tail} ${texts.length - heads} (seed ${seedText})`);
		}
		else if (p.kind === "dice")
		{
			show(texts, null, `Sum ${texts.reduce((sum, t) => sum + Number(t), 0)} (seed ${seedText})`);
		}
		else
		{
			show(texts, null, `Seed ${seedText}: in Input, rands(${seedText}) then the command gives the same`);
		}
	}

	const simpleCommand = () => command(p.kind, p.from, p.to, Math.floor(p.places), p.headsPercent, Math.floor(p.sides));

	//==================================================================
	//  Advanced: a shape, its picture, and after Generate a histogram
	//==================================================================
	let shaped: string[] = $state([]);
	let shapeNote = $state("");
	let shapeError = $state(false);
	let picture: HTMLCanvasElement | undefined = $state();

	const shapeCurve = $derived(curve(p.shape, p.shapeStrength, p.shapeA, p.shapeB));
	const range = $derived<Range>({ from: p.shapeFrom, to: p.shapeTo, peak: p.shapePeak, whole: p.shapeWhole, places: Math.floor(p.shapePlaces) });

	async function drawShape()
	{
		const n = Math.floor(p.shapeCount);
		const problem = check(shapeCurve, range, n);
		if (problem)
		{
			shaped = [];
			shapeError = true;
			shapeNote = problem;
			return;
		}
		let texts: string[];
		if (seeded)
		{
			const answer = await calc.seedDraws(seedText, shapeCommand(shapeCurve, range), n, false);
			if (!answer.texts)
			{
				shaped = [];
				shapeError = true;
				shapeNote = answer.error ?? "";
				return;
			}
			texts = answer.texts;
		}
		else
		{
			texts = values(secure, shapeCurve, range, n);
		}
		shaped = texts;
		shapeError = false;
		shapeNote = summary(texts, range.places) + (seeded ? ` (seed ${seedText})` : "");
	}

	$effect(() =>
	{
		void shapeCurve;
		void range;
		shaped = [];    // a changed shape or range: the old histogram no longer fits
		shapeNote = "";
	});

	// The shape's line over the results' bars
	$effect(() =>
	{
		const canvas = picture;
		if (!canvas)
		{
			return;
		}
		const width = canvas.clientWidth, height = canvas.clientHeight;
		const ratio = window.devicePixelRatio || 1;
		canvas.width = Math.round(width * ratio);
		canvas.height = Math.round(height * ratio);
		const context = canvas.getContext("2d")!;
		context.setTransform(ratio, 0, 0, ratio, 0, 0);
		context.clearRect(0, 0, width, height);
		const css = getComputedStyle(document.documentElement);
		const peak = peakAtOf();
		const at = Number.isFinite(peak) ? Math.min(1, Math.max(0, peak)) : 0.5;
		const ys = line(shapeCurve, at);
		const histogram = bars(shaped, range);
		const top = Math.max(...ys.filter(Number.isFinite), ...histogram, 0.001) * 1.1;
		const pad = 6;
		const y = (value: number) => height - pad - value / top * (height - 2 * pad);
		context.fillStyle = css.getPropertyValue("--line").trim();
		histogram.forEach((value, index) =>
		{
			const left = pad + index / histogram.length * (width - 2 * pad);
			context.fillRect(left + 1, y(value), (width - 2 * pad) / histogram.length - 2, height - pad - y(value));
		});
		context.strokeStyle = css.getPropertyValue("--select").trim();
		context.lineWidth = 2;
		context.beginPath();
		ys.forEach((value, index) =>
		{
			const x = pad + index / (ys.length - 1) * (width - 2 * pad);
			const yy = y(Math.min(value, top));
			if (index === 0)
			{
				context.moveTo(x, yy);
			}
			else
			{
				context.lineTo(x, yy);
			}
		});
		context.stroke();
	});

	function peakAtOf(): number
	{
		const from = range.whole ? Math.trunc(Math.min(range.from, range.to)) : Math.min(range.from, range.to);
		const to = range.whole ? Math.trunc(Math.max(range.from, range.to)) : Math.max(range.from, range.to);
		const peak = range.whole ? Math.trunc(range.peak) : range.peak;
		return range.whole ? (peak - from + 0.5) / (to - from + 1) : (peak - from) / (to - from);
	}

	// Sort: the list in order, smallest first (numbers only)
	function sortNumbers()
	{
		if (numbers.length > 1 && numbers.every(text => text !== "" && !Number.isNaN(Number(text))))
		{
			numbers = [...numbers].sort((a, b) => Number(a) - Number(b));
		}
	}

	const isNumber = (text: string) => text !== "" && !Number.isNaN(Number(text));

	function resultMenu(choice: string | null)
	{
		const text = menuFor;
		menuFor = null;
		if (text === null || choice === null)
		{
			return;
		}
		if (choice === "Insert into Input")
		{
			oninsert(text);
		}
		else if (choice === "Copy")
		{
			void copyText(text).then(done => { if (done) numbersNote = "Copied " + text; });
		}
	}

	async function copyAll(lines: string[], secret: boolean)
	{
		const text = lines.join("\n");
		if (await (secret ? copySecret(text) : copyText(text)))
		{
			if (secret)
			{
				passwordNote = `Copied ${lines.length} (cleared in 30 s)`;
			}
			else
			{
				numbersNote = `Copied ${lines.length}`;
			}
		}
	}

	//==================================================================
	//  Password
	//==================================================================
	let passwords: string[] = $state([]);
	let passwordNote = $state("");
	let passwordError = $state(false);
	let signature = $state("");    // never saved

	const options = $derived<PasswordOptions>({
		length: Math.min(128, Math.max(4, Math.floor(p.passwordLength) || 16)), upper: p.upper, lower: p.lower, digits: p.digits,
		symbols: p.symbols, symbolSet: p.symbolSet, noLookAlikes: p.noLookAlikes,
	});

	const strength = $derived.by(() =>
	{
		const part = randomPart(options, signature);
		const pool = poolSize(part);
		const signed = signature.length > 0 ? ` + signature ${[...signature].length}` : "";
		return pool === 0 ? "No characters chosen." : `${pool} characters, ${part.length} random${signed}: about ${Math.round(strengthBits(part))} bits`;
	});

	function drawPasswords()
	{
		const made: string[] = [];
		const n = Math.min(50, Math.max(1, Math.floor(p.passwordCount) || 1));
		for (let i = 0; i < n; i++)
		{
			const result = signed(secure, options, signature, p.signaturePlace);
			if (result.password === null)
			{
				passwords = [];
				passwordError = true;
				passwordNote = result.error ?? "";
				return;
			}
			made.push(result.password);
		}
		passwords = made;
		passwordError = false;
		passwordNote = "Tap one to copy it";
	}

	async function copyPassword(text: string)
	{
		if (await copySecret(text))
		{
			passwordNote = "Copied (cleared from the clipboard in 30 s)";
		}
	}
</script>

<div class="random" role="dialog" aria-label="Random">
	<div class="head">
		<span class="name">Random</span>
		<div class="tabs">
			{#each TABS as [id, label] (id)}
				<button class:on={p.tab === id} use:tap={() => { p.tab = id; }}>{label}</button>
			{/each}
		</div>
		<button class="bar" use:tap={onclose}>Done</button>
	</div>
	<div class="body">
		{#if p.tab === "simple" || p.tab === "advanced"}
			<div class="source">
				<button class:on={!seedOn} use:tap={() => { seedOn = false; }}>System</button>
				<button class:on={seedOn} use:tap={() => { seedOn = true; if (!seed.trim()) seed = newSeed(); }}>Seed</button>
				{#if seedOn}
					<input class="seed mono" bind:value={seed} inputmode="decimal" autocomplete="off" aria-label="Seed" />
					<button use:tap={() => { seed = newSeed(); }}>New</button>
				{/if}
			</div>
		{/if}
		{#if p.tab === "cards"}
			<CardsTab bind:this={cardsTab} bind:inner={cardsInner} {calc} {oninsert} />
		{:else if p.tab === "advanced"}
			<div class="form">
				<div class="grid">
					<label class="field"><span>Shape</span>
						<select bind:value={p.shape}>{#each SHAPES as [id, name] (id)}<option value={id}>{name}</option>{/each}</select></label>
					{#if usesStrength(p.shape)}
						<label class="field"><span>Strength {p.shapeStrength}</span>
							<input type="range" min="1" max="10" step="0.5" bind:value={p.shapeStrength} /></label>
					{/if}
					{#if p.shape === "triangle"}
						<label class="field"><span>Peak at</span><input type="number" bind:value={p.shapePeak} /></label>
					{/if}
					{#if p.shape === "custom"}
						<label class="field"><span>a</span><input type="number" min="0.05" max="100" step="0.1" bind:value={p.shapeA} /></label>
						<label class="field"><span>b</span><input type="number" min="0.05" max="100" step="0.1" bind:value={p.shapeB} /></label>
					{/if}
				</div>
				<canvas class="picture" bind:this={picture}></canvas>
				<p class="note">{hint(p.shape)}</p>
				<div class="kinds">
					<button class:on={p.shapeWhole} use:tap={() => { p.shapeWhole = true; }}>Whole</button>
					<button class:on={!p.shapeWhole} use:tap={() => { p.shapeWhole = false; }}>Decimals</button>
				</div>
				<div class="grid">
					<label class="field"><span>From</span><input type="number" bind:value={p.shapeFrom} /></label>
					<label class="field"><span>To</span><input type="number" bind:value={p.shapeTo} /></label>
					{#if !p.shapeWhole}
						<label class="field"><span>Decimal places</span><input type="number" min="0" max="10" bind:value={p.shapePlaces} /></label>
					{/if}
					<label class="field"><span>Count</span><input type="number" min="1" max={SHAPE_MAX} bind:value={p.shapeCount} /></label>
				</div>
				<div class="buttons">
					<button class="primary" use:tap={() => void drawShape()}>Generate</button>
					<button use:tap={() => void copyAll(shaped, false)} disabled={shaped.length === 0}>Copy all</button>
					<button use:tap={() => oninsert(shapeCommand(shapeCurve, range))} title="The engine call that draws one value in Input">Insert command</button>
				</div>
				{#if shapeNote}<p class="note" class:error={shapeError}>{shapeNote}</p>{/if}
			</div>
			<div class="results">
				{#each shaped.slice(0, SHOWN) as text, index (index)}
					<button class="result" use:tap={() => { menuFor = text; }}>{text}</button>
				{/each}
				{#if shaped.length > SHOWN}<p class="note more">{shaped.length - SHOWN} more: Copy all takes them all</p>{/if}
			</div>
		{:else if p.tab === "password"}
			<div class="form">
				<div class="grid">
					<label class="field"><span>Length</span><input type="number" min="4" max="128" bind:value={p.passwordLength} /></label>
					<label class="field"><span>Count</span><input type="number" min="1" max="50" bind:value={p.passwordCount} /></label>
				</div>
				<div class="checks">
					<label><input type="checkbox" bind:checked={p.upper} /> A-Z</label>
					<label><input type="checkbox" bind:checked={p.lower} /> a-z</label>
					<label><input type="checkbox" bind:checked={p.digits} /> 0-9</label>
					<label><input type="checkbox" bind:checked={p.symbols} /> Symbols</label>
				</div>
				<label class="field"><span>Symbols to use</span>
					<input class="mono" bind:value={p.symbolSet} autocapitalize="off" autocomplete="off" spellcheck="false" disabled={!p.symbols} /></label>
				<div class="checks groups">
					{#each SYMBOL_GROUPS as [name, chars] (name)}
						<label><input type="checkbox" checked={[...chars].every(c => p.symbolSet.includes(c))} disabled={!p.symbols}
							onchange={event => { p.symbolSet = toggleSymbols(p.symbolSet, chars, (event.currentTarget as HTMLInputElement).checked); }} />
							{name} <span class="mono">{chars}</span></label>
					{/each}
				</div>
				<label class="check"><input type="checkbox" bind:checked={p.noLookAlikes} /> Leave out look-alikes (Il1O0o)</label>
				<div class="grid">
					<label class="field"><span>Signature (not saved)</span>
						<input bind:value={signature} autocapitalize="off" autocomplete="off" spellcheck="false" /></label>
					<label class="field"><span>Put it</span>
						<select bind:value={p.signaturePlace}>
							<option value="end">at the end</option><option value="start">at the start</option><option value="anywhere">anywhere</option>
						</select></label>
				</div>
				<p class="note">{strength}</p>
				<div class="buttons">
					<button class="primary" use:tap={drawPasswords}>Generate</button>
					<button use:tap={() => void copyAll(passwords, true)} disabled={passwords.length === 0}>Copy all</button>
				</div>
				{#if passwordNote}<p class="note" class:error={passwordError}>{passwordNote}</p>{/if}
			</div>
			<div class="results">
				{#each passwords as text, index (index)}
					<button class="result mono" use:tap={() => void copyPassword(text)}>{text}</button>
				{/each}
			</div>
		{:else}
			<div class="form">
				<div class="kinds">
					{#each KINDS as [id, label] (id)}
						<button class:on={p.kind === id} use:tap={() => { p.kind = id; }}>{label}</button>
					{/each}
				</div>
				<div class="grid">
					{#if p.kind === "whole" || p.kind === "decimal"}
						<label class="field"><span>From</span><input inputmode="decimal" bind:value={p.from} autocomplete="off" /></label>
						<label class="field"><span>To</span><input inputmode="decimal" bind:value={p.to} autocomplete="off" /></label>
					{/if}
					{#if p.kind === "decimal"}
						<label class="field"><span>Decimal places</span><input type="number" min="0" max="10" bind:value={p.places} /></label>
					{/if}
					{#if p.kind === "coin"}
						<label class="field"><span>Heads %</span><input type="number" min="0" max="100" step="0.01" bind:value={p.headsPercent} /></label>
						<label class="field"><span>Head word</span><input bind:value={p.headWord} placeholder={DEFAULT_HEAD} autocomplete="off" /></label>
						<label class="field"><span>Tail word</span><input bind:value={p.tailWord} placeholder={DEFAULT_TAIL} autocomplete="off" /></label>
					{/if}
					{#if p.kind === "dice"}
						<label class="field"><span>Sides</span><input type="number" min="2" bind:value={p.sides} /></label>
					{/if}
					<label class="field"><span>Count</span><input type="number" min="1" max={MAX_COUNT} bind:value={p.count} /></label>
				</div>
				{#if p.kind === "whole"}
					<label class="check"><input type="checkbox" bind:checked={p.noRepeats} /> No repeats</label>
				{/if}
				<div class="buttons">
					<button class="primary" use:tap={() => void drawNumbers()}>Generate</button>
					<button use:tap={sortNumbers} disabled={numbers.length < 2 || p.kind === "coin"}>Sort</button>
					<button use:tap={() => void copyAll(numbers, false)} disabled={numbers.length === 0}>Copy all</button>
					<button use:tap={() => oninsert(simpleCommand())} title="A rand() line that draws one value in Input">Insert command</button>
				</div>
				{#if numbersNote}<p class="note" class:error={numbersError}>{numbersNote}</p>{/if}
			</div>
			<div class="results">
				{#each numbers.slice(0, SHOWN) as text, index (index)}
					<button class="result" use:tap={() => { if (isNumber(text)) menuFor = text; else void copyText(text); }}>{text}</button>
				{/each}
				{#if numbers.length > SHOWN}<p class="note more">{numbers.length - SHOWN} more: Copy all takes them all</p>{/if}
			</div>
		{/if}
	</div>
	{#if menuFor !== null}
		<Sheet title={menuFor} items={["Insert into Input", "Copy"]} onpick={resultMenu} />
	{/if}
</div>

<style>
	.random
	{
		position: fixed; inset: 0; z-index: 40; display: flex; flex-direction: column;
		background: var(--page); color: var(--ink); height: 100dvh;
	}
	.head
	{
		display: flex; align-items: center; gap: 8px; padding: 4px 6px;
		padding-top: max(4px, env(safe-area-inset-top)); border-bottom: 1px solid var(--line);
	}
	.name { margin: 0 6px 0 10px; font-size: 20px; font-weight: 500; flex: none; }
	.tabs, .kinds { flex: 1; min-width: 0; display: flex; gap: 6px; overflow-x: auto; }
	.tabs button, .kinds button, .buttons button
	{
		height: 34px; padding: 0 14px; border: 1px solid var(--line); border-radius: 17px; background: none; flex: none;
		color: var(--ink); font: inherit; font-size: 15px; cursor: pointer; touch-action: manipulation;
	}
	.tabs button.on, .kinds button.on, .buttons .primary { background: var(--select); border-color: var(--select); color: var(--select-ink); }
	.buttons button:disabled { opacity: 0.4; }
	.bar
	{
		min-width: 44px; height: 40px; border: none; border-radius: 20px; background: none; color: var(--select);
		font: inherit; font-size: 16px; padding: 0 12px; cursor: pointer; touch-action: manipulation; flex: none;
	}
	.body { flex: 1; min-height: 0; overflow-y: auto; display: flex; flex-direction: column; }
	.form { padding: 10px 12px; display: flex; flex-direction: column; gap: 10px; }
	.grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(140px, 1fr)); gap: 8px 12px; }
	.field { display: flex; flex-direction: column; gap: 3px; font-size: 13px; color: var(--faint); min-width: 0; }
	.field input, .field select
	{
		font: inherit; font-size: 16px; padding: 7px 9px; border: 1px solid var(--line); border-radius: 8px;
		background: var(--page); color: var(--ink); min-width: 0;
	}
	.checks { display: flex; flex-wrap: wrap; gap: 6px 16px; font-size: 15px; }
	.checks label, .check { display: flex; align-items: center; gap: 6px; font-size: 15px; }
	.groups { font-size: 14px; color: var(--faint); }
	input[type="checkbox"] { width: 20px; height: 20px; }
	.mono { font-family: ui-monospace, Consolas, monospace; }
	.buttons { display: flex; flex-wrap: wrap; gap: 8px; }
	.note { margin: 0; color: var(--faint); font-size: 14px; overflow-wrap: anywhere; }
	.note.error { color: var(--error); }
	.results { display: flex; flex-direction: column; padding-bottom: max(16px, env(safe-area-inset-bottom)); }
	.result
	{
		text-align: left; padding: 11px 14px; border: none; border-top: 1px solid var(--line); background: none; color: inherit;
		font: inherit; font-size: 17px; cursor: pointer; touch-action: manipulation; overflow-wrap: anywhere;
	}
	.result:active { background: var(--line); }
	.more { padding: 10px 14px; }
	.source { display: flex; align-items: center; gap: 6px; padding: 8px 12px 0; flex-wrap: wrap; }
	.source button
	{
		height: 32px; padding: 0 12px; border: 1px solid var(--line); border-radius: 16px; background: none;
		color: var(--ink); font: inherit; font-size: 14px; cursor: pointer; touch-action: manipulation;
	}
	.source button.on { background: var(--select); border-color: var(--select); color: var(--select-ink); }
	.seed
	{
		flex: 1; min-width: 140px; font-size: 14px; padding: 6px 8px; border: 1px solid var(--line); border-radius: 8px;
		background: var(--page); color: var(--ink);
	}
	.picture { width: 100%; height: 120px; border: 1px solid var(--line); border-radius: 8px; }
</style>
