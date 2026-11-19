<!-- RandomView.svelte - ⋮ > Random (owner 2026-11-20): the desktop's Random window for the web.
     Simple: whole numbers, decimals, coins and dice; Password: passwords with a strength line and an
     optional signature. Draws come from the browser's secure generator (random.ts). A tap on a result
     offers Insert (into Input) and Copy; a password is copied at once and cleared from the clipboard
     30 s later. Options are kept; results and the signature never are, and nothing goes to History. -->
<script lang="ts">
	import type { Calculator } from "../lib/calc";
	import { copySecret, copyText } from "../lib/clipboard";
	import {
		coins, command, decimals, DEFAULT_HEAD, DEFAULT_TAIL, dice, integers, MAX_COUNT, poolSize, randomPart, secure, signed,
		strengthBits, SYMBOL_GROUPS, toggleSymbols, type PasswordOptions, type RandomPrefs,
	} from "../lib/random";
	import { tapOn } from "../lib/repeat";
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
	const TABS: [string, string][] = [["simple", "Simple"], ["password", "Password"]];

	let saveTimer: ReturnType<typeof setTimeout> | undefined;
	$effect(() =>
	{
		const snapshot = $state.snapshot(p) as RandomPrefs;
		clearTimeout(saveTimer);
		saveTimer = setTimeout(() => calc.saveRandom(snapshot), 300);
	});

	let menuFor: string | null = $state(null);    // the result whose menu is open
	$effect(() =>
	{
		inner = menuFor !== null;
	});

	export function closeInner()
	{
		menuFor = null;
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

	function drawNumbers()
	{
		const n = count(p.count);
		if (p.kind === "whole")
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
		{#if p.tab === "password"}
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
					<button class="primary" use:tap={drawNumbers}>Generate</button>
					<button use:tap={sortNumbers} disabled={numbers.length < 2 || p.kind === "coin"}>Sort</button>
					<button use:tap={() => void copyAll(numbers, false)} disabled={numbers.length === 0}>Copy all</button>
					<button use:tap={() => oninsert(command(p.kind, p.from, p.to, Math.floor(p.places), p.headsPercent, Math.floor(p.sides)))}
						title="A rand() line that draws one value in Input">Insert command</button>
				</div>
				{#if numbersNote}<p class="note" class:error={numbersError}>{numbersNote}</p>{/if}
			</div>
			<div class="results">
				{#each numbers as text, index (index)}
					<button class="result" use:tap={() => { if (isNumber(text)) menuFor = text; else void copyText(text); }}>{text}</button>
				{/each}
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
</style>
