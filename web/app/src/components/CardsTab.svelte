<!-- CardsTab.svelte - Random > Cards (owner 2026-11-20, R3): draw cards from a deck.
     The desktop's ready-made decks in English or Thai, and decks of your own: New, Edit (one card a
     line, "name | weight"), Copy a ready-made one to your decks, Delete, Import and Export CSV (the
     desktop's file), and all your decks with their pictures in one zip (Import decks, Export all decks;
     also ⋮ > Export / Import). Untick cards to leave them out; No repeat keeps drawn cards out until Shuffle,
     also next time. Cards are drawn by weight from the secure generator (cards.ts). -->
<script lang="ts">
	import type { Calculator } from "../lib/calc";
	import {
		cardCommand, drawCards, formatLines, listed, newDeckId, parseCsv, parseLines, READY, readText, readyDecks, shownCard, shownDeck, toCsv,
		type CardsState, type Deck,
	} from "../lib/cards";
	import { copyText } from "../lib/clipboard";
	import { CSV_TYPES, pickFiles, saveFiles, ZIP_TYPES } from "../lib/files";
	import { exportNames } from "../lib/transfer";
	import { secure } from "../lib/random";
	import { tapOn } from "../lib/repeat";
	import Sheet from "./Sheet.svelte";

	let { calc, oninsert, inner = $bindable(false) }: {
		calc: Calculator;
		oninsert: (text: string) => void;
		inner?: boolean;
	} = $props();

	// svelte-ignore state_referenced_locally
	let s: CardsState = $state(structuredClone(calc.cards));
	const tap = (button: HTMLElement, act: () => void) => ({ destroy: tapOn(button, () => act()) });
	const ready = readyDecks();

	$effect(() =>
	{
		calc.saveCards($state.snapshot(s) as CardsState);
	});

	const deck = $derived<Deck>(s.decks.find(d => d.id === s.deck) ?? ready.find(d => d.id === s.deck) ?? ready[0]);
	const mine = $derived(!deck.id.startsWith(READY));
	const cards = $derived(listed(deck, s.language));
	// A ready-made card that starts unticked (the jokers) is off until ticked
	const isOff = (name: string) => (s.off[deck.id] ?? cards.filter(c => c.off).map(c => c.name)).includes(name);
	const ticked = $derived(cards.filter(card => !isOff(card.name)));
	const pile = $derived(new Set(s.piles[deck.id] ?? []));
	const left = $derived(ticked.filter(card => !pile.has(card.name)).length);

	let drawn: string[] = $state([]);
	let note = $state("");
	let error = $state(false);
	let showCards = $state(false);
	let menuFor: string | null = $state(null);
	let deleting = $state(false);
	let editor: { id: string | null; name: string; lines: string; error: string } | null = $state(null);

	$effect(() =>
	{
		inner = menuFor !== null || deleting || editor !== null;
	});

	export function closeInner()
	{
		menuFor = null;
		deleting = false;
		editor = null;
	}

	function say(text: string, isError = false)
	{
		note = text;
		error = isError;
	}

	function choose(id: string)
	{
		s.deck = id;
		drawn = [];
		say("");
	}

	function setOff(name: string, off: boolean)
	{
		const now = new Set(s.off[deck.id] ?? cards.filter(c => c.off).map(c => c.name));
		if (off)
		{
			now.add(name);
		}
		else
		{
			now.delete(name);
		}
		s.off = { ...s.off, [deck.id]: [...now] };
	}

	function draw()
	{
		const n = Math.min(1000, Math.max(1, Math.floor(s.count) || 1));
		const kept = s.noRepeat ? new Set(pile) : null;
		const result = drawCards(secure, ticked, n, kept);
		if (kept)
		{
			s.piles = { ...s.piles, [deck.id]: [...kept] };
		}
		drawn = result.drawn.map(card => shownCard(card, s.language));
		if (result.error)
		{
			say(result.error, true);
		}
		else
		{
			say(s.noRepeat ? `${ticked.length - kept!.size} left in the pile` : "");
		}
	}

	function shuffle()
	{
		const { [deck.id]: _, ...rest } = s.piles;
		s.piles = rest;
		say("Shuffled: every card is back");
	}

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
		else
		{
			void copyText(text).then(done => { if (done) say("Copied " + text); });
		}
	}

	// card() for this deck, Count and No repeat, to run in Input (owner 2026-11-24); it leaves out
	// only the cards saved unticked, not the ticks here
	function insertCommand()
	{
		oninsert(cardCommand(shownDeck(deck, s.language), Math.min(1000, Math.max(1, Math.floor(s.count) || 1)), s.noRepeat));
	}

	//==================================================================
	//  Own decks
	//==================================================================
	function newDeck()
	{
		editor = { id: null, name: "", lines: "", error: "" };
	}

	function editDeck()
	{
		editor = { id: deck.id, name: deck.name, lines: formatLines(deck.cards), error: "" };
	}

	// A ready-made deck as one of your own, with the names in the language shown
	function copyDeck()
	{
		const copy: Deck = {
			id: newDeckId(), name: shownDeck(deck, s.language),
			cards: cards.map(card => ({ name: shownCard(card, s.language), weight: card.weight, ...(card.color ? { color: card.color } : {}) })),
		};
		s.decks = [...s.decks, copy];
		choose(copy.id);
		say(`Copied to your decks as "${copy.name}"`);
	}

	function saveEditor()
	{
		const e = editor;
		if (!e)
		{
			return;
		}
		const name = e.name.trim();
		const parsed = parseLines(e.lines);
		if (name.length === 0)
		{
			e.error = "The deck needs a name.";
			return;
		}
		if (!parsed.cards || parsed.cards.length === 0)
		{
			e.error = parsed.error ?? "Type at least one card.";
			return;
		}
		if (s.decks.some(d => d.id !== e.id && d.name.toLowerCase() === name.toLowerCase()))
		{
			e.error = "You have a deck of that name.";
			return;
		}
		const old = s.decks.find(d => d.id === e.id);
		// colours, pictures and unticked starts stay with the cards that keep their name
		const cardsNow = parsed.cards.map(card =>
		{
			const was = old?.cards.find(c => c.name === card.name);
			return { ...card, ...(was?.color ? { color: was.color } : {}), ...(was?.image ? { image: was.image } : {}), ...(was?.off ? { off: true } : {}) };
		});
		if (e.id === null)
		{
			const made: Deck = { id: newDeckId(), name, cards: cardsNow };
			s.decks = [...s.decks, made];
			choose(made.id);
		}
		else
		{
			s.decks = s.decks.map(d => d.id === e.id ? { ...d, name, cards: cardsNow } : d);
		}
		editor = null;
	}

	function deleteDeck(choice: string | null)
	{
		deleting = false;
		if (choice === null || !mine)
		{
			return;
		}
		const id = deck.id;
		s.decks = s.decks.filter(d => d.id !== id);
		const { [id]: _a, ...off } = s.off;
		const { [id]: _b, ...piles } = s.piles;
		s.off = off;
		s.piles = piles;
		choose(READY + "playing-cards");
	}

	async function importCsv()
	{
		const files = await pickFiles(CSV_TYPES);
		for (const file of files)
		{
			const parsed = parseCsv(readText(new Uint8Array(await file.arrayBuffer())));
			if (!parsed.cards || parsed.cards.length === 0)
			{
				say(`${file.name}: ${parsed.error ?? "no cards"}`, true);
				return;
			}
			let name = file.name.replace(/\.[^.]*$/, "");
			for (let n = 2; s.decks.some(d => d.name.toLowerCase() === name.toLowerCase()); n++)
			{
				name = `${file.name.replace(/\.[^.]*$/, "")} ${n}`;
			}
			const made: Deck = { id: newDeckId(), name, cards: parsed.cards };
			s.decks = [...s.decks, made];
			choose(made.id);
			say(`Added "${name}": ${parsed.cards.length} cards`);
		}
	}

	async function exportCsv()
	{
		const name = shownDeck(deck, s.language).replace(/[\\/:*?"<>|]/g, "-") + ".csv";
		try
		{
			const where = await saveFiles([{ name, type: "text/csv", bytes: new TextEncoder().encode(toCsv(cards, s.language)) }]);
			say(`Saved ${name} to ${where}`);
		}
		catch (problem)
		{
			say("Not saved: " + (problem instanceof Error ? problem.message : String(problem)), true);
		}
	}

	//==================================================================
	//  All my decks in one zip (owner 2026-11-23; decks.ts)
	//==================================================================
	async function exportAll()
	{
		calc.saveCards($state.snapshot(s) as CardsState);
		const bytes = calc.decksFile();
		if (!bytes)
		{
			say("No decks of your own to export: New deck or Copy to my decks makes one.");
			return;
		}
		const name = exportNames().decks;
		try
		{
			const where = await saveFiles([{ name, type: "application/zip", bytes }]);
			say(`Saved ${s.decks.length} deck(s) to ${where}: ${name}`);
		}
		catch (problem)
		{
			say("Not saved: " + (problem instanceof Error ? problem.message : String(problem)), true);
		}
	}

	async function importAll()
	{
		const files = await pickFiles(ZIP_TYPES);
		const said: string[] = [];
		let failed = false;
		for (const file of files)
		{
			calc.saveCards($state.snapshot(s) as CardsState);
			const done = await calc.importDecks(file.name, new Uint8Array(await file.arrayBuffer()));
			s = structuredClone(calc.cards);
			said.push(done.said);
			failed ||= done.error;
		}
		if (said.length > 0)
		{
			say(said.join(" "), failed);
		}
	}
</script>

<div class="form">
	<div class="grid">
		<label class="field"><span>Deck</span>
			<select value={deck.id} onchange={event => choose((event.currentTarget as HTMLSelectElement).value)}>
				{#if s.decks.length > 0}
					<optgroup label="My decks">{#each s.decks as d (d.id)}<option value={d.id}>{d.name}</option>{/each}</optgroup>
				{/if}
				<optgroup label="Ready-made">{#each ready as d (d.id)}<option value={d.id}>{shownDeck(d, s.language)}</option>{/each}</optgroup>
			</select></label>
		<label class="field"><span>Count</span><input type="number" min="1" max="1000" bind:value={s.count} /></label>
	</div>
	<div class="row">
		{#if !mine}
			<div class="kinds">
				<button class:on={s.language === "en"} use:tap={() => { s.language = "en"; }}>English</button>
				<button class:on={s.language === "th"} use:tap={() => { s.language = "th"; }}>ไทย</button>
			</div>
		{/if}
		<label class="check"><input type="checkbox" bind:checked={s.noRepeat} /> No repeat (a pile)</label>
	</div>
	<button class="link" use:tap={() => { showCards = !showCards; }}>
		{showCards ? "▾" : "▸"} {cards.length} cards{ticked.length < cards.length ? `, ${cards.length - ticked.length} unticked` : ""}{s.noRepeat ? `, ${left} left` : ""}
	</button>
	{#if showCards}
		<div class="cards">
			{#each cards as card (card.name)}
				<label class="card">
					<input type="checkbox" checked={!isOff(card.name)} onchange={event => setOff(card.name, !(event.currentTarget as HTMLInputElement).checked)} />
					{#if card.color}<span class="swatch" style:background={card.color}></span>{/if}
					<span class:out={s.noRepeat && pile.has(card.name)}>{shownCard(card, s.language)}</span>
					{#if card.weight !== 1}<span class="weight">×{card.weight}</span>{/if}
				</label>
			{/each}
		</div>
	{/if}
	<div class="buttons">
		<button class="primary" use:tap={draw}>Draw</button>
		{#if s.noRepeat}<button use:tap={shuffle}>Shuffle</button>{/if}
		<button use:tap={() => void copyText(drawn.join("\n")).then(done => { if (done) say(`Copied ${drawn.length}`); })} disabled={drawn.length === 0}>Copy all</button>
		<button use:tap={insertCommand}>Insert command</button>
	</div>
	<div class="buttons small">
		<button use:tap={newDeck}>New deck</button>
		{#if mine}
			<button use:tap={editDeck}>Edit</button>
			<button use:tap={() => { deleting = true; }}>Delete</button>
		{:else}
			<button use:tap={copyDeck}>Copy to my decks</button>
		{/if}
		<button use:tap={() => void importCsv()}>Import CSV</button>
		<button use:tap={() => void exportCsv()}>Export CSV</button>
		<button use:tap={() => void importAll()}>Import decks</button>
		<button use:tap={() => void exportAll()}>Export all decks</button>
	</div>
	{#if note}<p class="note" class:error>{note}</p>{/if}
</div>
<div class="results">
	{#each drawn as text, index (index)}
		<button class="result" use:tap={() => { menuFor = text; }}>{text}</button>
	{/each}
</div>
{#if menuFor !== null}
	<Sheet title={menuFor} items={["Insert into Input", "Copy"]} onpick={resultMenu} />
{/if}
{#if deleting}
	<Sheet title={`Delete the deck "${deck.name}"?`} items={["Delete"]} onpick={deleteDeck} />
{/if}
{#if editor}
	<div class="backdrop" role="presentation"></div>
	<div class="editor" role="dialog" aria-label="Deck">
		<div class="title">{editor.id === null ? "New deck" : "Edit deck"}</div>
		<input bind:value={editor.name} placeholder="Name" autocomplete="off" />
		<textarea bind:value={editor.lines} rows="8" placeholder={"One card a line; a weight after a bar:\nFried rice | 2\nNoodles"}></textarea>
		<div class="hint">One card a line. A weight after a bar makes it more likely: Fried rice | 2</div>
		{#if editor.error}<div class="bad">{editor.error}</div>{/if}
		<div class="buttons end">
			<button use:tap={() => { editor = null; }}>Cancel</button>
			<button class="primary" use:tap={saveEditor}>OK</button>
		</div>
	</div>
{/if}

<style>
	.form { padding: 10px 12px; display: flex; flex-direction: column; gap: 10px; }
	.grid { display: grid; grid-template-columns: 2fr 1fr; gap: 8px 12px; }
	.field { display: flex; flex-direction: column; gap: 3px; font-size: 13px; color: var(--faint); min-width: 0; }
	.field input, .field select, .editor input, .editor textarea
	{
		font: inherit; font-size: 16px; padding: 7px 9px; border: 1px solid var(--line); border-radius: 8px;
		background: var(--page); color: var(--ink); min-width: 0;
	}
	.row { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 16px; }
	.kinds { display: flex; gap: 6px; }
	.kinds button, .buttons button
	{
		height: 34px; padding: 0 14px; border: 1px solid var(--line); border-radius: 17px; background: none; flex: none;
		color: var(--ink); font: inherit; font-size: 15px; cursor: pointer; touch-action: manipulation;
	}
	.kinds button.on, .buttons .primary { background: var(--select); border-color: var(--select); color: var(--select-ink); }
	.buttons { display: flex; flex-wrap: wrap; gap: 8px; }
	.buttons.small button { height: 30px; font-size: 14px; padding: 0 12px; }
	.buttons.end { justify-content: flex-end; }
	.buttons button:disabled { opacity: 0.4; }
	.check { display: flex; align-items: center; gap: 6px; font-size: 15px; }
	input[type="checkbox"] { width: 20px; height: 20px; flex: none; }
	.link { align-self: flex-start; border: none; background: none; color: var(--select); font: inherit; font-size: 15px; padding: 2px 0; cursor: pointer; }
	.cards { max-height: 40vh; overflow-y: auto; border: 1px solid var(--line); border-radius: 8px; padding: 4px 8px; }
	.card { display: flex; align-items: center; gap: 8px; padding: 5px 0; font-size: 15px; }
	.swatch { width: 14px; height: 14px; border-radius: 3px; flex: none; }
	.weight { color: var(--faint); font-size: 13px; }
	.out { text-decoration: line-through; color: var(--faint); }
	.note { margin: 0; color: var(--faint); font-size: 14px; overflow-wrap: anywhere; }
	.note.error, .bad { color: var(--error); font-size: 14px; }
	.results { display: flex; flex-direction: column; padding-bottom: max(16px, env(safe-area-inset-bottom)); }
	.result
	{
		text-align: left; padding: 11px 14px; border: none; border-top: 1px solid var(--line); background: none; color: inherit;
		font: inherit; font-size: 17px; cursor: pointer; touch-action: manipulation; overflow-wrap: anywhere;
	}
	.result:active { background: var(--line); }
	.backdrop { position: fixed; inset: 0; background: rgb(0 0 0 / 0.35); z-index: 33; touch-action: none; }
	.editor
	{
		position: fixed; left: 0; right: 0; top: 0; z-index: 34; margin: 0 auto; max-width: 560px; background: var(--page);
		box-shadow: 0 4px 20px rgb(0 0 0 / 0.3); padding: 14px 16px; padding-top: max(14px, env(safe-area-inset-top));
		border-radius: 0 0 16px 16px; display: flex; flex-direction: column; gap: 10px;
	}
	.editor textarea { resize: vertical; font-size: 15px; }
	.title { font-weight: 600; color: var(--faint); }
	.hint { color: var(--faint); font-size: 13px; }
</style>
