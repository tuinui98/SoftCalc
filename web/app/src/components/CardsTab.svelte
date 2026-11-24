<!-- CardsTab.svelte - Random > Cards (owner 2026-11-20, R3): draw cards from a deck.
     The desktop's ready-made decks in English or Thai, and decks of your own: New, Edit (one card a
     line, "name | weight"), Copy a ready-made one to your decks, Delete, Import and Export CSV (the
     desktop's file), and all your decks with their pictures in one zip (Import decks, Export all decks;
     also ⋮ > Export / Import). Untick cards to leave them out; No repeat keeps drawn cards out until Shuffle,
     also next time. Cards are drawn by weight from the secure generator (cards.ts).
     Owner 2026-11-24 (deck covers): decks are chosen from their covers (covers.ts), a deck's cards show as
     pictures, and a tap on one shows it full screen: swipe for the next, Use as cover, Leave out. -->
<script lang="ts">
	import type { Calculator } from "../lib/calc";
	import {
		cardCommand, drawCards, formatLines, listed, newDeckId, parseCsv, parseLines, READY, readText, readyDecks, shownCard, shownDeck, toCsv,
		type CardsState, type Deck,
	} from "../lib/cards";
	import { copyText } from "../lib/clipboard";
	import { deckCover, mosaic, type Cover } from "../lib/covers";
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
	let picking = $state(false);    // the decks as covers, to choose one
	let viewing: number | null = $state(null);    // the card shown full screen

	$effect(() =>
	{
		inner = menuFor !== null || deleting || editor !== null || picking || viewing !== null;
	});

	export function closeInner()
	{
		menuFor = null;
		deleting = false;
		editor = null;
		picking = false;
		viewing = null;
	}

	//==================================================================
	//  Covers and pictures (owner 2026-11-24)
	//==================================================================
	const coverOf = (d: Deck): Cover => deckCover(d, shownDeck(d, s.language), name => calc.pictureUrl(name) !== null);
	const pictured = $derived(cards.some(card => calc.pictureUrl(card.image) !== null));

	function pick(id: string)
	{
		choose(id);
		picking = false;
	}

	function useAsCover(image: string | undefined)
	{
		if (!mine || !image)
		{
			return;
		}
		const id = deck.id;
		s.decks = s.decks.map(d => d.id === id ? { ...d, cover: image } : d);
		say("Cover set");
	}

	// Swipe left or right in the full-screen view for the next card
	let swipeFrom: number | null = null;
	function step(by: number)
	{
		if (viewing !== null && cards.length > 0)
		{
			viewing = (viewing + by + cards.length) % cards.length;
		}
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

	// A ready-made deck as one of your own, with the names in the language shown and its pictures
	async function copyDeck()
	{
		const copy: Deck = await calc.ownPictures({
			id: newDeckId(), name: shownDeck(deck, s.language),
			cards: cards.map(card => ({ name: shownCard(card, s.language), weight: card.weight, ...(card.color ? { color: card.color } : {}),
				...(card.image ? { image: card.image } : {}) })),
		});
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
		<div class="field"><span>Deck</span>
			<button class="deck-pick" use:tap={() => { picking = true; }} aria-label="Choose a deck">
				<span class="mini">{@render coverArt(coverOf(deck))}</span>
				<span class="deck-name">{shownDeck(deck, s.language)}</span>
				<span aria-hidden="true">▾</span>
			</button></div>
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
	{#if showCards && pictured}
		<div class="tiles">
			{#each cards as card, index (card.name)}
				<div class="tile" class:off={isOff(card.name)}>
					<button class="art" use:tap={() => { viewing = index; }} aria-label={`Show ${shownCard(card, s.language)}`}>
						{@render cardArt(card)}
						{#if card.weight !== 1}<span class="badge">×{card.weight}</span>{/if}
					</button>
					<label class="tile-name">
						<input type="checkbox" checked={!isOff(card.name)} onchange={event => setOff(card.name, !(event.currentTarget as HTMLInputElement).checked)} />
						<span class:out={s.noRepeat && pile.has(card.name)}>{shownCard(card, s.language)}</span>
					</label>
				</div>
			{/each}
		</div>
	{:else if showCards}
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
			<button use:tap={() => void copyDeck()}>Copy to my decks</button>
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
{#if picking}
	<div class="overlay" role="dialog" aria-label="Choose a deck">
		<div class="overlay-head">
			<span>Choose a deck</span>
			<button class="close" use:tap={() => { picking = false; }} aria-label="Close">✕</button>
		</div>
		<div class="overlay-body">
			{#if s.decks.length > 0}
				<div class="section">My decks</div>
				<div class="covers">
					{#each s.decks as d (d.id)}
						<button class="cover-tile" class:chosen={d.id === deck.id} use:tap={() => pick(d.id)}>
							<span class="cover">{@render coverArt(coverOf(d))}</span>
							<span class="cover-name">{d.name}</span>
							<span class="cover-count">{d.cards.length} cards</span>
						</button>
					{/each}
				</div>
			{/if}
			<div class="section">Ready-made</div>
			<div class="covers">
				{#each ready as d (d.id)}
					<button class="cover-tile" class:chosen={d.id === deck.id} use:tap={() => pick(d.id)}>
						<span class="cover">{@render coverArt(coverOf(d))}</span>
						<span class="cover-name">{shownDeck(d, s.language)}</span>
						<span class="cover-count">{d.cards.length} cards</span>
					</button>
				{/each}
			</div>
		</div>
	</div>
{/if}
{#if viewing !== null && cards[viewing]}
	{@const card = cards[viewing]}
	<div class="viewer" role="dialog" tabindex="-1" aria-label={shownCard(card, s.language)}
		onpointerdown={event => { swipeFrom = event.clientX; }}
		onpointerup={event => { if (swipeFrom !== null && Math.abs(event.clientX - swipeFrom) > 50) step(event.clientX < swipeFrom ? 1 : -1); swipeFrom = null; }}>
		<div class="overlay-head">
			<span>{shownDeck(deck, s.language)} · {viewing + 1} of {cards.length}</span>
			<button class="close" use:tap={() => { viewing = null; }} aria-label="Close">✕</button>
		</div>
		<div class="stage">
			<button class="turn" use:tap={() => step(-1)} aria-label="Previous card">‹</button>
			<div class="big">{@render cardArt(card)}</div>
			<button class="turn" use:tap={() => step(1)} aria-label="Next card">›</button>
		</div>
		<div class="big-name">{shownCard(card, s.language)}</div>
		<div class="big-note">{card.weight !== 1 ? `weight ${card.weight} · ` : ""}{isOff(card.name) ? "left out" : "in the draw"} · swipe for the next card</div>
		<div class="buttons end viewer-buttons">
			{#if mine && card.image && calc.pictureUrl(card.image)}<button use:tap={() => useAsCover(card.image)}>{deck.cover === card.image ? "The cover" : "Use as cover"}</button>{/if}
			<button use:tap={() => setOff(card.name, !isOff(card.name))}>{isOff(card.name) ? "Put back" : "Leave out"}</button>
		</div>
	</div>
{/if}

{#snippet coverArt(cover: Cover)}
	{#if cover.kind === "picture"}
		<img class="fill" src={calc.pictureUrl(cover.image)} alt="" />
	{:else if cover.kind === "colours"}
		<span class="mosaic">{#each mosaic(cover.colours) as colour, i (i)}<span style:background={colour}></span>{/each}</span>
	{:else}
		<span class="letter" style:background={cover.back}>{cover.letter}</span>
	{/if}
{/snippet}

{#snippet cardArt(card: Deck["cards"][number])}
	{#if calc.pictureUrl(card.image)}
		<img class="fit" src={calc.pictureUrl(card.image)} alt="" />
	{:else if card.color}
		<span class="swatch-big" style:background={card.color}></span>
	{:else}
		<span class="letter small">{shownCard(card, s.language).slice(0, 2)}</span>
	{/if}
{/snippet}
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
	.field input, .editor input, .editor textarea
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

	/* Covers and pictures (owner 2026-11-24): pictures on white, so a transparent one reads on the dark theme */
	.deck-pick
	{
		display: flex; align-items: center; gap: 10px; min-height: 44px; padding: 4px 10px 4px 4px; border: 1px solid var(--line);
		border-radius: 8px; background: var(--page); color: var(--ink); font: inherit; font-size: 16px; text-align: left; cursor: pointer;
	}
	.deck-name { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
	.mini { width: 48px; height: 36px; flex: none; border-radius: 5px; overflow: hidden; display: flex; }
	.fill { width: 100%; height: 100%; object-fit: cover; background: #fff; display: block; }
	.fit { width: 100%; height: 100%; object-fit: contain; background: #fff; display: block; }
	.mosaic { width: 100%; height: 100%; display: grid; grid-template-columns: 1fr 1fr; gap: 2px; background: #fff; }
	.letter { width: 100%; height: 100%; display: flex; align-items: center; justify-content: center; color: #fff; font-size: 26px; font-weight: 600; }
	.letter.small { background: var(--line); color: var(--ink); font-size: 18px; }
	.mini .letter { font-size: 18px; }
	.swatch-big { width: 60%; height: 60%; border-radius: 6px; margin: auto; }
	.tiles { display: grid; grid-template-columns: repeat(auto-fill, minmax(96px, 1fr)); gap: 8px; max-height: 50vh; overflow-y: auto; }
	.tile { display: flex; flex-direction: column; gap: 3px; min-width: 0; }
	.tile.off .art { opacity: 0.4; }
	.art
	{
		position: relative; aspect-ratio: 3 / 4; border: 1px solid var(--line); border-radius: 8px; overflow: hidden; padding: 0;
		background: #fff; display: flex; cursor: pointer; touch-action: manipulation;
	}
	.badge { position: absolute; top: 4px; right: 4px; font-size: 12px; padding: 0 6px; border-radius: 8px; background: #2f3a4a; color: #fff; }
	.tile-name { display: flex; align-items: center; gap: 4px; font-size: 13px; min-width: 0; }
	.tile-name span { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
	.tile-name input[type="checkbox"] { width: 18px; height: 18px; }
	.overlay, .viewer
	{
		position: fixed; inset: 0; z-index: 34; background: var(--page); display: flex; flex-direction: column;
		padding-top: env(safe-area-inset-top); padding-bottom: env(safe-area-inset-bottom);
	}
	.viewer { background: #050506; color: #e8eaed; touch-action: pan-y; }
	.overlay-head { display: flex; align-items: center; gap: 8px; padding: 6px 8px 6px 14px; font-size: 16px; color: var(--faint); }
	.viewer .overlay-head { color: #9aa0a6; }
	.overlay-head span { flex: 1; }
	.close { width: 44px; height: 44px; border: none; background: none; color: inherit; font-size: 20px; cursor: pointer; }
	.overlay-body { flex: 1; overflow-y: auto; padding: 0 12px 16px; }
	.section { font-size: 13px; color: var(--faint); margin: 10px 0 6px; text-transform: uppercase; }
	.covers { display: grid; grid-template-columns: repeat(auto-fill, minmax(104px, 1fr)); gap: 10px; }
	.cover-tile
	{
		display: flex; flex-direction: column; gap: 3px; padding: 5px; border: 1px solid var(--line); border-radius: 10px; background: none;
		color: var(--ink); font: inherit; text-align: left; cursor: pointer; touch-action: manipulation; min-width: 0;
	}
	.cover-tile.chosen { border: 2px solid var(--select); }
	.cover { aspect-ratio: 4 / 3; border-radius: 6px; overflow: hidden; display: flex; }
	.cover-name { font-size: 13px; font-weight: 500; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
	.cover-count { font-size: 12px; color: var(--faint); }
	.stage { flex: 1; min-height: 0; display: flex; align-items: center; gap: 4px; padding: 0 4px; }
	.big { flex: 1; height: 100%; max-height: 70vh; min-width: 0; display: flex; align-items: center; justify-content: center; }
	.big .fit { width: auto; height: auto; max-width: 100%; max-height: 100%; border-radius: 12px; }
	.big .swatch-big { width: 60%; height: 60%; }
	.turn { width: 40px; height: 64px; border: none; background: none; color: #e8eaed; font-size: 32px; cursor: pointer; flex: none; }
	.big-name { text-align: center; font-size: 22px; font-weight: 500; padding-top: 10px; }
	.big-note { text-align: center; font-size: 13px; color: #9aa0a6; padding: 4px 12px; }
	.viewer-buttons { padding: 10px 12px 14px; }
	.viewer .buttons button { color: #e8eaed; border-color: #3a3d44; }
</style>
