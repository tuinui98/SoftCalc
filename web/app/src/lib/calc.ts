// calc.ts - The calculator behind the web app: Input, History, keys, recall walk, saving
//
// A plain class the Svelte screen draws from (subscribe), with the engine
// and the store passed in, so tests can use fakes. Rules follow the desktop
// MainForm / InputRules / KeypadRules / InputRecall (owner 2026-11-16):
// C removes the last number, % only appends %, an operator on an empty
// Input after a result starts with `answer`, Enter on an empty Input
// repeats the last line, tapping a History line recalls it and Up /
// entry walk on from there, tapping it again only takes the highlight off.
// Start, as MainForm.Persistence.cs: settings, then definitions
// (pcf_import), then the saved History with its results back as $n
// (restore_results). After a definition the definitions are saved again
// (pcf_export); History and settings are saved as they change. Infix
// only: RPN is not in the web app (owner 2026-11-18).

import type { Answer } from "./engine";
import { PriceSource } from "./prices";
import { RatesSource } from "./rates";
import { inputsFrom, type CompoundInputs, type Schedule } from "./compound";
import { cardsFrom, engineDecks, type CardsState, type Deck } from "./cards";
import { pictureName, READY_PICTURE, readyPictureUrl } from "./covers";
import { decksZip, importDecks, readDecks, report, usedPictures, type Pictures } from "./decks";
import { prefsFrom, type RandomPrefs } from "./random";
import { FIRST_EQUATIONS, functionHead, same, stateFrom, type GraphState } from "./graph";
import { HISTORY_LIMIT, type Store } from "./store";
import { helpQuery, isHelpLine } from "./helpfind";
import { isZip } from "./zip";
import { addGraph, fileText, mergeHistory, newId, parseHistory, splitGraph, withIds } from "./transfer";

export interface EngineLike
{
	readonly ready: Promise<string>;
	readonly marks?: { name: string; at: number }[];
	onstep?: ((name: string) => void) | null;   // called as each start step ends
	send(op: string, args?: Record<string, unknown>): Promise<Answer>;
}

// The status line while the engine loads, by the step just done
const LOADING: Record<string, string> = { worker: "Loading Python...", python: "Loading the engine...",
	unpack: "Loading the engine...", import: "Restoring History..." };

const WORKING_AFTER = 1000;    // ms before "Working..." shows

// "Finished in 0.23 s" after every line (owner 2026-11-24: time each line); 2 places under 10 s
export function tookText(seconds: number): string
{
	return `Finished in ${seconds.toFixed(seconds < 10 ? 2 : 1)} s`;
}

export interface Entry
{
	kind: string;          // result, definition, error, notice
	input: string;
	display: string;       // the History expression line (numbers substituted)
	text: string;
	note?: string;         // where a rate came from: "ECB 2026-11-17", "CoinGecko 2026-11-18 08:00", ", offline"
	id?: string;           // as the desktop's history.jsonl (transfer.ts), so files merge without twins
	time?: string;         // ISO, UTC
}

export interface Settings
{
	precision: number;
	angle: string;
	base: string;
	[key: string]: unknown;
}

export type Catalog = {
	functions: Record<string, string[]>;
	conversions: Record<string, string[]>;
	crypto?: Record<string, string[]>;    // Conversions > Cryptocurrency: coin -> its units (owner 2026-11-24)
	crypto_targets?: string[];    // the currencies a coin unit converts to
	subgroups?: Record<string, Record<string, string[]>>;    // a group's submenus, after its own items (owner 2026-11-25)
};

export const SUBMENU = " ›";    // marks a submenu in a pick list

// A function group's pick list: its own functions, then its submenus marked with SUBMENU
export function functionItems(catalog: Catalog, group: string): string[]
{
	return [...(catalog.functions[group] ?? []), ...Object.keys(catalog.subgroups?.[group] ?? {}).map(name => name + SUBMENU)];
}

// The functions of a picked submenu ("Build ›"), or null when the pick is a function
export function submenuItems(catalog: Catalog, group: string, item: string): string[] | null
{
	return item.endsWith(SUBMENU) ? catalog.subgroups?.[group]?.[item.slice(0, -SUBMENU.length)] ?? null : null;
}

export const CRYPTO_GROUP = "Cryptocurrency";

// What a coin unit converts to: its coin's other units, the other coins, the common currencies
export function cryptoTargets(catalog: Catalog, coin: string, source: string): string[]
{
	const coins = catalog.crypto ?? {};
	const others = Object.entries(coins).filter(([name, units]) => name !== coin && units.length > 0).map(([, units]) => units[0]);
	return [...(coins[coin] ?? []).filter(unit => unit !== source), ...others, ...(catalog.crypto_targets ?? [])];
}

// The user's own names (engine `state`), for the Variables sheet and Functions > Your functions
export interface Definitions
{
	variables: { name: string; value: string }[];
	functions: { name: string; source: string; params: string[] }[];
}

const OPERATORS = "+-*/^!%";

export class Calculator
{
	readonly engine: EngineLike;
	private readonly store: Store;
	readonly started: Promise<void>;    // resolves once the saved state is back (or failed)
	text = "";
	caret = 0;
	entries: Entry[] = [];
	highlight = -1;            // History index shown by the walk, -1 when not walking
	settings: Settings = { precision: 64, angle: "deg", base: "dec" };
	second = false;
	status = "Loading Python...";
	loading = "page";          // the last start step done: page, worker, python, unpack, import, then done or failed
	startTimes = "";           // how long each start step took (Settings), "" until started
	ready = false;
	catalog: Catalog = { functions: {}, conversions: {} };
	onhelp?: (query: string) => void;    // ?word in Input (owner 2026-11-26): the app opens Help
	definitions: Definitions = { variables: [], functions: [] };
	graph: string[] = [];    // the Graph's equations; a .pcf's [Graph] section (transfer.ts)
	graphView: GraphState = stateFrom(null);    // the Graph's range, lines shown and colours
	compound: CompoundInputs = inputsFrom(null);    // ⋮ > Compound's inputs, kept between visits
	random: RandomPrefs = prefsFrom(null);    // ⋮ > Random's options (never results or a signature)
	cards: CardsState = cardsFrom(null);    // Random > Cards: own decks, unticked cards, piles
	pictures: Pictures = {};    // the own decks' pictures by name (decks.ts), kept for Export Decks
	hint = "";                 // the line under Input (engine `hint`), "" for none
	showHints = true;
	private hintAsked = 0;     // newest hint request; older replies are dropped
	private hintFor = "";      // text and caret of the last request, so a redraw does not ask again
	private lastWasResult = false;
	private waitingEngine = false;   // a submit waits for the engine to load
	private hasMemory = false;
	private draft = "";
	private listeners = new Set<() => void>();

	private readonly rates: RatesSource;
	private readonly prices: PriceSource;

	// net: the rate and price sources (tests pass ones with a fake download)
	constructor(engine: EngineLike, store: Store, net: { rates?: RatesSource; prices?: PriceSource } = {})
	{
		this.engine = engine;
		this.store = store;
		this.rates = net.rates ?? new RatesSource(store);
		this.prices = net.prices ?? new PriceSource(store);
		engine.onstep = name =>
		{
			this.loading = name;
			this.status = LOADING[name] ?? this.status;
			this.changed();
		};
		this.started = this.start().then(version =>
		{
			this.ready = true;
			this.loading = "done";
			const done = performance.now();
			this.status = `Engine ${version} ready in ${(done / 1000).toFixed(1)} s`;
			this.startTimes = startSteps([...(this.engine.marks ?? []), { name: "History", at: done }]);
			this.changed();
		}, error =>
		{
			this.loading = "failed";
			this.status = "Engine failed to load: " + (error instanceof Error ? error.message : String(error));
			this.changed();
		});
	}

	//==================================================================
	//  Start: the saved state back into the engine (MainForm.Persistence)
	//==================================================================
	private async start(): Promise<string>
	{
		// History is on the screen while the engine is still loading
		const saved = await this.store.load();
		this.entries = withIds(saved.entries);
		if (this.entries.some((entry, index) => entry !== saved.entries[index]))
		{
			this.save(this.store.saveEntries(this.entries));    // lines kept before ids came in
		}
		const graph = await this.store.get("graph");
		// A first run draws the desktop's first two equations (graph.ts FIRST_EQUATIONS)
		this.graph = Array.isArray(graph) ? graph.filter(line => typeof line === "string") : [...FIRST_EQUATIONS];
		this.graphView = stateFrom(await this.store.get("graphView"));
		this.compound = inputsFrom(await this.store.get("compound"));
		this.random = prefsFrom(await this.store.get("random"));
		this.cards = cardsFrom(await this.store.get("cards"));
		const pictures = await this.store.get("pictures");
		if (typeof pictures === "object" && pictures !== null)
		{
			this.pictures = Object.fromEntries(Object.entries(pictures).filter(([, bytes]) => bytes instanceof Uint8Array));
		}
		this.changed();
		const version = await this.engine.ready;
		const wanted = { angle: "deg", ...pick(saved.settings, ["precision", "angle", "base", "rpn", "decimals", "pad_zeros", "word_size"]) };
		this.settings = settingsOf(await this.engine.send("settings", wanted));
		// Kept rates and prices go in first; they are fetched only when a line needs newer ones
		await this.engine.send("set_rates", { ...await this.rates.loadKept() });
		await this.engine.send("set_prices", { ...await this.prices.loadKept() });
		if (saved.definitions.trim().length > 0)
		{
			const imported = await this.engine.send("pcf_import", { text: saved.definitions });
			const skipped = (imported.skipped ?? []) as unknown[];
			if (skipped.length > 0)
			{
				this.add({ kind: "notice", input: "", display: "(some saved definitions were skipped)",
					text: `${skipped.length} line(s)` });
			}
		}
		const results = saved.entries.filter(entry => entry.kind === "result").map(entry => entry.text);
		if (results.length > 0)
		{
			await this.engine.send("restore_results", { texts: results });
		}
		await this.sendDecks();
		await this.refreshDefinitions();
		this.catalog = (await this.engine.send("catalog")) as unknown as Catalog;
		return version;
	}

	// Variables show in the current precision and base, so this follows settings too
	private async refreshDefinitions()
	{
		const state = await this.engine.send("state");
		this.definitions = {
			variables: ((state.variables ?? []) as Definitions["variables"]).map(({ name, value }) => ({ name, value })),
			functions: ((state.functions ?? []) as Definitions["functions"]).map(({ name, source, params }) => ({ name, source, params: Array.isArray(params) ? params : [] })),
		};
		this.hasMemory = this.definitions.variables.some(variable => variable.name.toLowerCase() === "memory");
	}

	private async saveDefinitions()
	{
		const exported = await this.engine.send("pcf_export");
		if (exported.ok)
		{
			this.save(this.store.saveDefinitions(String(exported.text ?? "")));
		}
	}

	// A failed save is shown, not thrown: the calculation itself went fine
	private save(work: Promise<void>)
	{
		work.catch(error =>
		{
			this.status = "Not saved: " + (error instanceof Error ? error.message : String(error));
			this.changed();
		});
	}

	subscribe(listener: () => void): () => void
	{
		this.listeners.add(listener);
		return () => this.listeners.delete(listener);
	}

	private changed()
	{
		this.autoAnswer = this.autoAnswer && dropAutoAnswer(this.text) !== null;    // gone once edited away
		this.notify();
		void this.updateHint();
	}

	// `answer` put in front by the auto-insert rule and still there (owner 2026-11-26, InputRules.cs)
	private autoAnswer = false;

	// Esc or the keypad's C: the auto-inserted `answer` goes, the operator stays; false when there is none
	dropAutoAnswer(): boolean
	{
		const rest = this.autoAnswer ? dropAutoAnswer(this.text) : null;
		if (rest === null)
		{
			return false;
		}
		this.autoAnswer = false;
		this.setText(rest, Math.max(0, this.caret - AUTO_ANSWER.length));
		this.highlight = -1;
		void this.updateHint(true);
		return true;
	}

	private notify()
	{
		for (const listener of this.listeners)
		{
			listener();
		}
	}

	//==================================================================
	//  Hint line: the engine's rules (hints.py), shared with the desktop
	//==================================================================
	// force: ask again for the same text (a definition, the base or Show Input Hints changed)
	async updateHint(force = false)
	{
		const key = `${this.caret}:${this.text}`;
		if (!this.ready || !this.showHints || (!force && key === this.hintFor))
		{
			if (!this.showHints && this.hint !== "")
			{
				this.hint = "";
				this.notify();
			}
			return;
		}
		this.hintFor = key;
		const asked = ++this.hintAsked;
		let next = "";
		if (this.caret > 0)    // nothing left of the caret: no hint, no need to ask
		{
			const answer = await this.engine.send("hint", { text: this.text, caret: this.caret });
			next = answer.ok && typeof answer.text === "string" ? answer.text : "";
		}
		if (this.autoAnswer)
		{
			next = AUTO_ANSWER_HINT + (next ? "  |  " + next : "");
		}
		if (asked === this.hintAsked && next !== this.hint)
		{
			this.hint = next;
			this.notify();
		}
	}

	setShowHints(on: boolean)
	{
		this.showHints = on;
		void this.updateHint(true);
	}

	//==================================================================
	//  Input editing
	//==================================================================
	// Typed with the system keyboard: the walk ends, Input is what was typed
	typed(text: string, caret: number)
	{
		this.text = text;
		this.caret = caret;
		this.highlight = -1;
		this.changed();
	}

	// ⌫ beside Input: one character before the caret (owner 2026-11-18, for touch screens)
	backspace()
	{
		if (this.caret > 0)
		{
			this.setText(this.text.slice(0, this.caret - 1) + this.text.slice(this.caret), this.caret - 1);
			this.highlight = -1;
			this.changed();
		}
	}

	// A tap or an arrow key in Input moved the caret: the hint follows it
	moveCaret(caret: number)
	{
		this.caret = Math.max(0, Math.min(caret, this.text.length));
		void this.updateHint();
	}

	private setText(text: string, caret = text.length)
	{
		this.text = text;
		this.caret = caret;
	}

	private insert(piece: string)
	{
		this.setText(this.text.slice(0, this.caret) + piece + this.text.slice(this.caret), this.caret + piece.length);
		this.highlight = -1;
	}

	// An operator on an empty Input right after a result starts with `answer` (InputRules.AutoAnswer)
	private insertTyped(piece: string)
	{
		if (this.text.length === 0 && this.lastWasResult && piece.length > 0 && OPERATORS.includes(piece[0]))
		{
			this.setText(AUTO_ANSWER + piece);
			this.autoAnswer = true;
			this.highlight = -1;
			void this.updateHint(true);    // says Esc or C takes it away
			return;
		}
		this.insert(piece);
	}

	private async menuInsert(args: Record<string, unknown>)
	{
		const answer = await this.engine.send("menu_insert", { ...args, text: this.text, caret: this.caret });
		if (answer.ok)
		{
			this.setText(answer.text, answer.caret);
			this.highlight = -1;
		}
	}

	// A function types its name and a bracket at the caret (owner 2026-11-19: wrapping the
	// whole line was awkward on a phone); an operator goes through the engine's menu rule
	async insertFunction(name: string)
	{
		if (name === "mod" || (name.length === 1 && OPERATORS.includes(name)))
		{
			await this.menuInsert({ kind: "operator", symbol: name === "mod" ? " mod " : name });
		}
		else
		{
			this.insert(name + "(");
		}
		this.changed();
	}

	async insertConversion(source: string, target: string)
	{
		await this.menuInsert({ kind: "conversion", source, target });
		this.changed();
	}

	//==================================================================
	//  Submit and History
	//==================================================================
	async submit(line = this.text)
	{
		// ?word opens Help at it (owner 2026-11-26); the engine never gets the line
		if (isHelpLine(line))
		{
			this.setText("");
			this.onhelp?.(helpQuery(line));
			this.notify();
			return;
		}
		const toRun = line.length > 0 ? line : this.entries.at(-1)?.input;
		if (!toRun || this.waitingEngine)
		{
			return;
		}
		// = while the engine is still loading: the line runs once it is ready
		if (!this.ready)
		{
			this.waitingEngine = true;
			await this.started;
			this.waitingEngine = false;
			if (!this.ready)
			{
				return;
			}
		}
		const answer = await this.evaluate(toRun);
		this.highlight = -1;
		this.draft = "";
		if (!answer.ok)
		{
			this.add({ kind: "error", input: toRun, display: toRun, text: answer.error ?? "" });
		}
		else if (answer.kind !== "empty")
		{
			const note = typeof answer.note === "string" && answer.note.length > 0 ? answer.note : undefined;
			this.add({ kind: answer.kind, input: answer.input, display: answer.display_input, text: answer.text, note });
			// a `:` line changed the settings (owner 2026-11-26): kept as a Settings change keeps them
			if (answer.settings && typeof answer.settings === "object")
			{
				this.settings = answer.settings as Settings;
				this.save(this.store.saveSettings(this.settings));
			}
			if (answer.kind === "definition")
			{
				await this.saveDefinitions();
				await this.refreshDefinitions();
			}
		}
		this.save(this.store.saveEntries(this.entries.filter(entry => entry.kind !== "notice")));
		this.lastWasResult = answer.ok && answer.kind === "result";
		if (answer.ok && answer.kind !== "error")
		{
			this.setText("");
		}
		this.changed();
	}

	// eval, and when the line needs currency rates or coin prices the engine does not have yet:
	// fetch them, hand them over and evaluate again (desktop M8 / crypto_plan C1). A line may need
	// both (BTC to an ECB currency), so up to two rounds; then the engine answers with what it has.
	private async evaluate(line: string): Promise<Answer>
	{
		const started = Date.now();
		let answer = await this.timed(this.engine.send("eval", { input: line }), started);
		let failed = "";
		for (let round = 0; round < 2 && answer.ok && (answer.kind === "rates_needed" || answer.kind === "prices_needed"); round++)
		{
			const rates = answer.kind === "rates_needed";
			this.say(rates ? "Getting ECB rates..." : "Getting coin prices...");
			const given = rates ? await this.rates.fetch() : await this.prices.fetch();
			await this.engine.send(rates ? "set_rates" : "set_prices", { ...given });
			// The reason stays in the status line, so a failure on a phone can be told apart
			failed = given.stale ? `${rates ? "ECB rates" : "Coin prices"} not fetched: ${(rates ? this.rates : this.prices).lastError}` : failed;
			answer = await this.engine.send("eval", { input: line });
		}
		const took = (Date.now() - started) / 1000;
		this.status = failed || tookText(took);
		return answer;
	}

	// A line that takes over a second (a 50 x 50 inv on a phone, owner 2026-11-24) shows
	// "Working... 1.4 s" in the status line, counting, so the app does not look stuck
	private async timed<T>(work: Promise<T>, started: number): Promise<T>
	{
		let ticker: ReturnType<typeof setInterval> | undefined;
		const show = () => this.say(`Working... ${((Date.now() - started) / 1000).toFixed(1)} s`);
		const wait = setTimeout(() =>
		{
			show();
			ticker = setInterval(show, 200);
		}, WORKING_AFTER);
		try
		{
			return await work;
		}
		finally
		{
			clearTimeout(wait);
			clearInterval(ticker);
		}
	}

	private recallLines(): number[]
	{
		return this.entries.map((entry, index) => entry.kind === "notice" ? -1 : index).filter(index => index >= 0);
	}

	private show(index: number)
	{
		this.highlight = index;
		this.setText(index >= 0 ? this.entries[index].input : this.draft);
	}

	// Tap on a History line: recall it; a tap on the highlighted line keeps it and drops the highlight
	tapEntry(index: number)
	{
		if (this.entries[index]?.kind === "notice")
		{
			return;
		}
		if (this.highlight === index)
		{
			this.highlight = -1;
		}
		else
		{
			if (this.highlight < 0)
			{
				this.draft = this.text;
			}
			this.show(index);
		}
		this.changed();
	}

	// Up (entry key): one line older; from no highlight, the newest
	walk(up: boolean)
	{
		const lines = this.recallLines();
		if (lines.length === 0)
		{
			return;
		}
		const at = lines.indexOf(this.highlight);
		if (up)
		{
			if (at < 0)
			{
				this.draft = this.text;
				this.show(lines.at(-1)!);
			}
			else if (at > 0)
			{
				this.show(lines[at - 1]);
			}
		}
		else if (at >= 0)
		{
			this.show(at + 1 < lines.length ? lines[at + 1] : -1);
		}
		this.changed();
	}

	// Esc: the highlight goes and the draft comes back
	cancelWalk()
	{
		if (this.highlight >= 0)
		{
			this.show(-1);
			this.changed();
		}
	}

	// The $n of a result line: 1 for the newest result
	private resultNumber(index: number): number
	{
		return this.entries.slice(index).filter(entry => entry.kind === "result").length;
	}

	// One History line goes; a result line also leaves $n, so later ones renumber (desktop DeleteHistoryEntryAsync)
	async deleteEntry(index: number)
	{
		const entry = this.entries[index];
		if (!entry || !this.ready)
		{
			return;
		}
		if (entry.kind === "result")
		{
			const answer = await this.engine.send("delete_result", { n: this.resultNumber(index) });
			if (!answer.ok)
			{
				this.status = "Not deleted: " + (answer.error ?? "");
				this.changed();
				return;
			}
		}
		this.entries.splice(index, 1);
		if (this.highlight >= 0)
		{
			this.show(-1);    // the walk's numbering is gone with the line
		}
		this.save(this.store.saveEntries(this.entries.filter(item => item.kind !== "notice")));
		this.changed();
		void this.updateHint(true);
	}

	// A user variable or function goes (the Variables sheet)
	async deleteName(name: string)
	{
		const answer = await this.engine.send("delete", { name });
		if (answer.ok)
		{
			await this.saveDefinitions();
		}
		await this.refreshDefinitions();
		this.changed();
		void this.updateHint(true);
	}

	// A History line or result goes in at the caret (owner 2026-11-18); a negative result in
	// brackets, so 2* and -5 make 2*(-5). No `answer` in front: this is not typing an operator.
	insertText(piece: string, isResult: boolean)
	{
		this.insert(isResult && piece.startsWith("-") ? `(${piece})` : piece);
		this.changed();
	}

	// A variable name goes in at the caret; a function as name( (insertFunction)
	async insertName(name: string, isFunction: boolean)
	{
		if (isFunction)
		{
			await this.insertFunction(name);
			return;
		}
		this.insertTyped(name);
		this.changed();
	}

	// A short word in the status line ("Copied")
	say(text: string)
	{
		this.status = text;
		this.notify();
	}

	// A new History line, with its id and time
	private add(entry: Entry)
	{
		this.entries.push({ ...entry, id: newId(), time: new Date().toISOString() });
	}

	//==================================================================
	//  Export and Import (transfer.ts; owner 2026-11-19)
	//==================================================================
	// Random's Seed (protocol `draws` with apart): the line drawn count times after rands(seed), in an
	// engine of its own, so Input's seed and $n stay as they were
	async seedDraws(seed: string, line: string, count: number, unique: boolean): Promise<{ texts: string[] | null; error: string | null }>
	{
		await this.started;
		const answer = await this.engine.send("draws", { seed, line, count, unique, apart: true });
		return answer.ok ? { texts: answer.texts as string[], error: null } : { texts: null, error: answer.error ?? "Not drawn" };
	}

	// card() in Input draws from these (owner 2026-11-24); sent again only when the decks changed
	private decksSent = "";

	private async sendDecks()
	{
		const key = JSON.stringify(this.cards.decks);
		if (key === this.decksSent)
		{
			return;
		}
		this.decksSent = key;
		await this.engine.send("decks", engineDecks(this.cards.decks));
	}

	saveCards(state: CardsState)
	{
		this.cards = state;
		if (this.decksSent !== "")    // after start's first send
		{
			void this.sendDecks();
		}
		this.save(this.store.put("cards", JSON.parse(JSON.stringify(state))));
		// a deleted deck's or edited card's pictures go
		const names = Object.keys(this.pictures);
		if (names.length > 0)
		{
			const used = usedPictures(state.decks);
			if (names.some(name => !used.has(name)))
			{
				this.pictures = Object.fromEntries(Object.entries(this.pictures).filter(([name]) => used.has(name)));
				this.save(this.store.put("pictures", this.pictures));
			}
		}
	}

	//==================================================================
	//  Pictures (owner 2026-11-24, deck covers): an own deck's from this.pictures,
	//  a ready-made deck's from the build (covers.ts)
	//==================================================================
	private pictureUrls = new Map<string, string>();

	// A URL to show the picture, or null when there is none
	pictureUrl(name: string | undefined): string | null
	{
		if (!name)
		{
			return null;
		}
		if (name.startsWith(READY_PICTURE))
		{
			return readyPictureUrl(name);
		}
		const bytes = this.pictures[name];
		if (!bytes)
		{
			return null;
		}
		let url = this.pictureUrls.get(name);
		if (!url)
		{
			url = URL.createObjectURL(new Blob([bytes as BlobPart], { type: "image/png" }));
			this.pictureUrls.set(name, url);
		}
		return url;
	}

	// A ready-made deck copied to the own decks: its built pictures kept as its own (named as the desktop does)
	async ownPictures(deck: Deck): Promise<Deck>
	{
		const named = new Map<string, string | undefined>();
		const cards = [];
		for (const card of deck.cards)
		{
			if (!card.image?.startsWith(READY_PICTURE))
			{
				cards.push(card);
				continue;
			}
			if (!named.has(card.image))
			{
				let name: string | undefined;
				const url = readyPictureUrl(card.image);
				try
				{
					const bytes = url ? new Uint8Array(await (await fetch(url)).arrayBuffer()) : null;
					if (bytes && bytes.length > 0)
					{
						name = await pictureName(bytes);
						this.pictures = { ...this.pictures, [name]: bytes };
					}
				}
				catch
				{
					name = undefined;    // offline before the picture was ever loaded: the card goes without
				}
				named.set(card.image, name);
			}
			const name = named.get(card.image);
			cards.push(name ? { ...card, image: name } : { ...card, image: undefined });
		}
		this.save(this.store.put("pictures", this.pictures));
		return { ...deck, cards, cover: undefined };
	}

	// Export Decks (owner 2026-11-23): all own decks and their pictures in one zip, null when there are none
	decksFile(time = new Date()): Uint8Array | null
	{
		return this.cards.decks.length === 0 ? null : decksZip(this.cards.decks, this.pictures, time);
	}

	// Import Decks: adds a zip's decks to the own decks; what to say, and whether it went wrong
	async importDecks(name: string, bytes: Uint8Array): Promise<{ said: string; error: boolean }>
	{
		const { read, error } = await readDecks(bytes);
		if (!read)
		{
			return { said: `${name}: ${error}`, error: true };
		}
		const result = importDecks(this.cards, this.pictures, read);
		this.pictures = result.pictures;
		this.save(this.store.put("pictures", this.pictures));
		this.saveCards(result.state);
		return { said: `${name}: ${report(result.done)}`, error: false };
	}

	saveRandom(prefs: RandomPrefs)
	{
		this.random = { ...prefs };
		this.save(this.store.put("random", { ...prefs }));
	}

	// ⋮ > Compound (compound.ts): the engine's schedule, or the error to show; the inputs are kept
	async schedule(inputs: CompoundInputs): Promise<{ schedule: Schedule | null; error: string | null }>
	{
		this.compound = { ...inputs };
		this.save(this.store.put("compound", { ...inputs }));
		await this.started;
		const answer = await this.engine.send("schedule", { ...inputs });
		return answer.ok ? { schedule: answer as unknown as Schedule, error: null } : { schedule: null, error: answer.error ?? "Not worked out" };
	}

	// The Graph's state, kept when the Graph changes it (graph.ts GraphState)
	saveGraphView(state: GraphState)
	{
		this.graphView = state;
		this.save(this.store.put("graphView", JSON.parse(JSON.stringify(state))));
	}

	//==================================================================
	//  The Graph's lines (G2, owner 2026-11-19): equations are kept here,
	//  functions are the engine's; neither adds a History line (desktop [DEF])
	//==================================================================
	// An equation to add or to put in place of `old`; the problem to show, or null when done
	async putEquation(text: string, old: string | null): Promise<string | null>
	{
		const typed = text.trim();
		if (!typed.includes("="))
		{
			return "An equation needs '=' (for a function use + Function)";
		}
		if (functionHead(typed))
		{
			return "This is a function: use + Function";
		}
		if (this.graph.some(line => same(line, typed) && !same(line, old)))
		{
			return "This equation is already in the list";
		}
		const answer = await this.engine.send("plot_equation", { equation: typed, x_min: -1, x_max: 1, y_min: -1, y_max: 1, cols: 1, rows: 1 });
		if (!answer.ok)
		{
			return answer.error ?? "Not an equation";
		}
		const at = old === null ? -1 : this.graph.findIndex(line => same(line, old));
		this.setGraph(at < 0 ? [...this.graph, typed] : this.graph.map((line, index) => index === at ? typed : line));
		return null;
	}

	removeEquation(text: string)
	{
		this.setGraph(this.graph.filter(line => !same(line, text)));
	}

	private setGraph(lines: string[])
	{
		this.graph = lines;
		this.save(this.store.put("graph", [...lines]));
		this.changed();
	}

	// A function of one parameter, new or in place of `old` (a new name drops the old one)
	async putFunction(text: string, old: string | null): Promise<string | null>
	{
		const typed = text.trim();
		const head = functionHead(typed);
		if (!head)
		{
			return "A function of one parameter, such as f(x)=x^2";
		}
		const renamed = old === null || !same(head.name, old);
		const taken = [...this.definitions.functions, ...this.definitions.variables].some(item => same(item.name, head.name));
		if (renamed && taken)
		{
			return `${head.name} is already defined`;
		}
		const answer = await this.engine.send("eval", { input: typed });
		if (!answer.ok || answer.kind !== "definition")
		{
			const text = String(answer.ok ? answer.text ?? "" : answer.error ?? "");
			return text.startsWith("ERROR:") ? text.slice(6) : text || "Not a function";
		}
		if (old !== null && renamed)
		{
			await this.engine.send("delete", { name: old });
		}
		await this.saveDefinitions();
		await this.refreshDefinitions();
		this.changed();
		void this.updateHint(true);
		return null;
	}

	historyLines(): Entry[]
	{
		return this.entries.filter(entry => entry.kind !== "notice");
	}

	// The engine's .pcf text of the user's variables and functions
	async definitionsText(): Promise<string>
	{
		await this.started;
		const exported = await this.engine.send("pcf_export");
		if (!exported.ok)
		{
			throw new Error(exported.error ?? "pcf_export failed");
		}
		return String(exported.text ?? "");
	}

	// A history.jsonl merges into History, a zip is Import Decks; anything else is a .pcf whose definitions are added.
	// Returns what the status line says.
	async importFile(name: string, bytes: Uint8Array): Promise<string>
	{
		if (isZip(bytes))
		{
			this.status = (await this.importDecks(name, bytes)).said;
			this.changed();
			return this.status;
		}
		await this.started;
		if (!this.ready)
		{
			return `${name}: the engine is not running`;
		}
		const text = fileText(bytes);
		const lines = parseHistory(text);
		let said: string;
		if (lines !== null)
		{
			const before = new Set(this.entries.map(entry => entry.id));
			const added = lines.filter(entry => !before.has(entry.id)).length;
			this.entries = mergeHistory(this.historyLines(), lines, HISTORY_LIMIT);
			this.highlight = -1;
			// $n counts the merged History's results, as after a restart
			await this.engine.send("clear_history");
			const results = this.entries.filter(entry => entry.kind === "result").map(entry => entry.text);
			if (results.length > 0)
			{
				await this.engine.send("restore_results", { texts: results });
			}
			this.save(this.store.saveEntries(this.entries));
			said = `${name}: ${added} new History line(s) of ${lines.length}`;
		}
		else
		{
			// pcf_import replaces everything (the desktop's Load); Import adds instead: the current
			// definitions, then the file's, so a name in both takes the file's (owner 2026-11-19)
			const counted = () => this.definitions.variables.length + this.definitions.functions.length;
			const parts = splitGraph(text);
			const graph = addGraph(this.graph, parts.graph);
			if (graph.added > 0)
			{
				this.graph = graph.graph;
				this.save(this.store.put("graph", this.graph));
			}
			const current = await this.definitionsText();
			const kept = current.trim().length > 0 ? current.replace(/\s*$/, "\r\n") : "";
			const imported = await this.engine.send("pcf_import", { text: kept + parts.definitions });
			const keptLines = kept.split(/\r?\n/).length - 1;
			// skipped lines counted in the file only (the engine numbers lines from 1)
			const skipped = ((imported.skipped ?? []) as { line: number }[]).filter(item => item.line > keptLines).length + graph.skipped;
			await this.saveDefinitions();
			await this.refreshDefinitions();
			said = `${name}: ${counted()} variable(s) and function(s) now` + (graph.added > 0 ? `, ${graph.added} graph equation(s) kept` : "") + (skipped > 0 ? `, ${skipped} line(s) skipped` : "");
		}
		this.status = said;
		this.changed();
		void this.updateHint(true);
		return said;
	}

	clearHistory()
	{
		this.entries = [];
		this.highlight = -1;
		this.engine.send("clear_history");
		this.save(this.store.saveEntries([]));
		this.changed();
	}

	//==================================================================
	//  Settings
	//==================================================================
	async setSetting(name: string, value: unknown)
	{
		const answer = await this.engine.send("settings", { [name]: value });
		if (answer.ok)
		{
			this.settings = settingsOf(answer);
			this.save(this.store.saveSettings(this.settings));
			await this.refreshDefinitions();
		}
		this.notify();
		void this.updateHint(true);    // a literal shows in the new base
	}

	//==================================================================
	//  Keys (ids from keys.ts)
	//==================================================================
	// Returns "func" or "units" when the UI should open that menu
	async press(id: string): Promise<"func" | "units" | null>
	{
		if (id === "2nd")
		{
			this.second = !this.second;
			this.changed();
			return null;
		}
		let menu: "func" | "units" | null = null;
		switch (id)
		{
			case "=":
				await this.submit();
				return null;
			case "AC":
				this.setText("");
				this.highlight = -1;
				break;
			case "C":
				if (!this.dropAutoAnswer())
				{
					this.setText(clearEntry(this.text));
					this.highlight = -1;
				}
				break;
			case "M+":
			case "M-":
				await this.submit(memoryAdd(this.text, this.hasMemory, id === "M-"));
				return null;
			case "MC":
				await this.submit("memory=0");
				return null;
			case "MR":
				this.insert("memory");
				break;
			case "base":
			{
				const order = ["dec", "hex", "oct", "bin"];
				await this.setSetting("base", order[(order.indexOf(this.settings.base) + 1) % order.length]);
				const line = this.text;
				if (line.trim().length > 0)
				{
					await this.submit(line);
					this.setText(line);    // the line stays for the next base (owner 2026-11-16)
				}
				break;
			}
			case "angle":
				await this.setSetting("angle", this.settings.angle === "deg" ? "rad" : "deg");
				break;
			case "func":
			case "units":
				menu = id;
				break;
			default:
				if (id.startsWith("fn:"))
				{
					await this.insertFunction(id.slice(3));
					return null;
				}
				this.insertTyped(id.startsWith("ins:") ? id.slice(4) : id);
				break;
		}
		this.changed();
		return menu;
	}

	// The id a key sends now (2nd applied), and 2nd is used up
	resolve(key: { id: string; second?: string }): string
	{
		if (this.second && key.id !== "2nd")
		{
			this.second = false;
			return key.second ?? key.id;
		}
		return key.id;
	}
}

function pick(source: Record<string, unknown>, names: string[]): Record<string, unknown>
{
	return Object.fromEntries(names.filter(name => source[name] !== undefined).map(name => [name, source[name]]));
}

function settingsOf(answer: Answer): Settings
{
	const { id, ok, error, ...settings } = answer;
	return settings as Settings;
}

// Settings > Decimals: − / + step from the setting, all digits starts at 2 (desktop DecimalSteps)
export const DECIMALS_START = 2;
export const DECIMALS_MAX = 30;

export function stepDecimals(current: number | null | undefined, delta: number): number
{
	if (current === null || current === undefined)
	{
		return DECIMALS_START;
	}
	return Math.max(0, Math.min(current + delta, DECIMALS_MAX));
}

// C: the last number or name (or the last other character) goes, with spaces before it (KeypadRules.ClearEntry)
export const AUTO_ANSWER = "answer";
export const AUTO_ANSWER_HINT = "answer was put first: Esc or C takes it away";

// "answer-5" -> "-5": the text without the auto-inserted answer, or null (InputRules.DropAutoAnswer)
export function dropAutoAnswer(text: string): string | null
{
	return text.length > AUTO_ANSWER.length && text.startsWith(AUTO_ANSWER) && "+-*/^".includes(text[AUTO_ANSWER.length])
		? text.slice(AUTO_ANSWER.length)
		: null;
}

export function clearEntry(text: string): string
{
	const trimmed = text.trimEnd();
	let start = trimmed.length;
	while (start > 0 && /[\p{L}\p{N}._]/u.test(trimmed[start - 1]))
	{
		start--;
	}
	if (start === trimmed.length && start > 0)
	{
		start--;    // an operator or a bracket
	}
	return trimmed.slice(0, start).trimEnd();
}

// M+ / M-: an empty Input stands for `answer`; a missing memory counts as 0 (InputRules.MemoryAdd)
export function memoryAdd(input: string, hasMemory: boolean, subtract: boolean): string
{
	const trimmed = input.trim();
	const operand = trimmed.length === 0 ? "answer" : `(${trimmed})`;
	const sign = subtract ? "-" : "+";
	if (hasMemory)
	{
		return `memory=memory${sign}${operand}`;
	}
	return subtract ? `memory=-${operand}` : `memory=${operand}`;
}

// "page 0.4 · worker 0.2 · python 3.1 ... s": each step's own time, from
// the page's start
export function startSteps(marks: { name: string; at: number }[]): string
{
	let before = 0;
	const parts = marks.map(mark =>
	{
		const took = (mark.at - before) / 1000;
		before = mark.at;
		return `${mark.name} ${took.toFixed(1)}`;
	});
	return parts.join(" · ") + " s";
}
