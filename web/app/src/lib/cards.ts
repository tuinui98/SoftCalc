// cards.ts - ⋮ > Random's Cards tab: decks and draws (owner 2026-11-20, R3)
//
// The desktop's ready-made decks (data/decks/*.txt, shared; ReadyDecks.cs)
// and decks of the user's own, typed as "name | weight" lines or read from
// CSV (name,weight,image,color; Cards.cs, so files go both ways; a CSV's
// images are not read here). Export / Import Decks (decks.ts) carries the
// pictures, kept but not shown yet. A draw picks by weight among the ticked cards; No repeat
// works like a pile: a drawn card stays out until Shuffle, also across
// visits. The desktop's draw tabs, conditions and boards are not here.

import type { RandomSource } from "./random";

const files = import.meta.glob("../../../../data/decks/*.txt", { query: "?raw", import: "default", eager: true }) as Record<string, string>;

export interface Card
{
	name: string;
	thai?: string;     // ready-made: the Thai name; name (English) stays the card's key
	weight: number;
	color?: string;    // "#RRGGBB" (the ready-made colours and pack)
	off?: boolean;     // starts unticked (the jokers)
	image?: string;    // a picture's name (decks.ts); kept, not shown yet
}

export interface Deck
{
	id: string;
	name: string;
	thai?: string;
	cards: Card[];
	alphabetical?: boolean;
}

export const READY = "ready:";
export const MAX_CARDS = 5000;
export const MAX_NAME = 200;
export const MAX_WEIGHT = 1000;

export const shownCard = (card: Card, language: string) => language === "th" && card.thai ? card.thai : card.name;
export const shownDeck = (deck: Deck, language: string) => language === "th" && deck.thai ? deck.thai : deck.name;

// The cards in the order they are listed for this language
export function listed(deck: Deck, language: string): Card[]
{
	if (!deck.alphabetical)
	{
		return deck.cards;
	}
	const order = new Intl.Collator(language === "th" ? "th-TH" : "en-US", { sensitivity: "base" });
	return [...deck.cards].sort((a, b) => order.compare(shownCard(a, language), shownCard(b, language)));
}

//==================================================================
//  Ready-made decks (ReadyDecks.cs)
//==================================================================
function rows(name: string): string[][]
{
	const key = Object.keys(files).find(path => path.endsWith(`/${name}.txt`));
	return (key ? files[key] : "").replace(/\r\n/g, "\n").split("\n")
		.filter(line => line.length > 0 && !line.startsWith("#")).map(line => line.split("|"));
}

const SUITS = ["♠", "♥", "♦", "♣"];
const RANKS = ["A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K"];
const SHADES = [["#202020", "#4A4A4A"], ["#C62828", "#E05353"], ["#1E5AA8", "#4A86D0"], ["#2E7D32", "#55A35A"]];
const LISTS: [string, string, string, string, boolean][] = [
	["months", "months", "Months", "เดือน", false],
	["days", "days", "Days of the week", "วันในสัปดาห์", false],
	["provinces", "provinces", "Provinces of Thailand", "จังหวัดในประเทศไทย", true],
	["colors", "colors", "Colours", "สี", false],
	["zodiac", "zodiac", "Zodiac signs", "ราศี", false],
	["chinese-zodiac", "animal-years", "Animal years", "ปีนักษัตร", false],
];
const CONTINENTS: [string, string][] = [
	["Africa", "แอฟริกา"], ["Asia", "เอเชีย"], ["Europe", "ยุโรป"],
	["North America", "อเมริกาเหนือ"], ["South America", "อเมริกาใต้"], ["Oceania", "โอเชียเนีย"],
];
const slug = (text: string) => text.toLowerCase().replace(/ /g, "-");

function build(): Deck[]
{
	const pack: Card[] = SUITS.flatMap((suit, s) => RANKS.map((rank, r) => ({ name: rank + suit, weight: 1, color: SHADES[s][r % 2] })));
	pack.push({ name: "Joker (black)", thai: "โจ๊กเกอร์ดำ", weight: 1, off: true, color: SHADES[0][0] });
	pack.push({ name: "Joker (red)", thai: "โจ๊กเกอร์แดง", weight: 1, off: true, color: SHADES[1][0] });
	const decks: Deck[] = [{ id: READY + "playing-cards", name: "Playing cards", thai: "ไพ่ 1 สำรับ", cards: pack }];
	for (const [file, id, english, thai, alphabetical] of LISTS)
	{
		decks.push({
			id: READY + id, name: english, thai, alphabetical,
			cards: rows(file).map(r => ({ name: r[0], thai: r[1], weight: 1, ...(r[2] ? { color: r[2] } : {}) })),
		});
	}
	const countries = rows("countries");
	decks.push({ id: READY + "countries", name: "Countries: all", thai: "ประเทศ: ทั้งหมด", alphabetical: true,
		cards: countries.map(r => ({ name: r[1], thai: r[2], weight: 1 })) });
	for (const [english, thai] of CONTINENTS)
	{
		decks.push({ id: READY + "countries-" + slug(english), name: "Countries: " + english, thai: "ประเทศ: " + thai, alphabetical: true,
			cards: countries.filter(r => r[0] === english).map(r => ({ name: r[1], thai: r[2], weight: 1 })) });
	}
	// Districts: all of them, a name two provinces share followed by its province, and a deck per province
	const thaiProvince = new Map(rows("provinces").map(r => [r[0], r[1]]));
	const districts = rows("districts");
	const counts = new Map<string, number>();
	districts.forEach(r => counts.set(r[1], (counts.get(r[1]) ?? 0) + 1));
	decks.push({ id: READY + "districts", name: "Districts: all", thai: "อำเภอ: ทั้งหมด", alphabetical: true,
		cards: districts.map(r => (counts.get(r[1]) ?? 0) > 1
			? { name: `${r[1]} (${r[0]})`, thai: `${r[2]} (${thaiProvince.get(r[0])})`, weight: 1 }
			: { name: r[1], thai: r[2], weight: 1 }) });
	for (const province of [...new Set(districts.map(r => r[0]))].sort())
	{
		decks.push({ id: READY + "districts-" + slug(province), name: "Districts: " + province, thai: "อำเภอ: " + thaiProvince.get(province),
			alphabetical: true, cards: districts.filter(r => r[0] === province).map(r => ({ name: r[1], thai: r[2], weight: 1 })) });
	}
	return decks;
}

let ready: Deck[] | null = null;

export function readyDecks(): Deck[]
{
	ready ??= build();
	return ready;
}

//==================================================================
//  Text forms (Cards.cs CardText)
//==================================================================
function weightOf(text: string): { value: number; error: string | null }
{
	const t = text.trim();
	if (t.length === 0)
	{
		return { value: 1, error: null };
	}
	const value = /^\d+$/.test(t) ? Number(t) : NaN;
	return value >= 1 && value <= MAX_WEIGHT ? { value, error: null }
		: { value: 0, error: `the weight '${t}' must be a whole number from 1 to ${MAX_WEIGHT}.` };
}

function make(name: string, weight: number): { card: Card | null; error: string | null }
{
	if (name.length === 0)
	{
		return { card: null, error: "a card needs a name." };
	}
	if ([...name].length > MAX_NAME)
	{
		return { card: null, error: `a name may have at most ${MAX_NAME} characters.` };
	}
	return { card: { name, weight }, error: null };
}

// One card per line, a weight after the last bar: "Fried rice | 2"
export function parseLines(text: string): { cards: Card[] | null; error: string | null }
{
	const cards: Card[] = [];
	const lines = text.split("\n");
	for (let i = 0; i < lines.length; i++)
	{
		const line = lines[i].trim();
		if (line.length === 0)
		{
			continue;
		}
		const bar = line.lastIndexOf("|");
		let name = line, weight = 1;
		if (bar >= 0)
		{
			name = line.slice(0, bar).trim();
			const parsed = weightOf(line.slice(bar + 1));
			if (parsed.error)
			{
				return { cards: null, error: `Line ${i + 1}: ${parsed.error}` };
			}
			weight = parsed.value;
		}
		const made = make(name, weight);
		if (!made.card)
		{
			return { cards: null, error: `Line ${i + 1}: ${made.error}` };
		}
		cards.push(made.card);
	}
	return cards.length > MAX_CARDS ? { cards: null, error: `At most ${MAX_CARDS} cards in a deck.` } : { cards, error: null };
}

export function formatLines(cards: Card[]): string
{
	return cards.map(card => card.weight === 1 ? card.name : `${card.name} | ${card.weight}`).join("\n");
}

// RFC 4180: fields split by commas, quoted fields may hold commas, quotes ("") and line breaks
function records(text: string): string[][]
{
	const out: string[][] = [];
	let fields: string[] = [], field = "", quoted = false;
	for (let i = 0; i < text.length; i++)
	{
		const c = text[i];
		if (quoted)
		{
			if (c === "\"" && text[i + 1] === "\"")
			{
				field += "\"";
				i++;
			}
			else if (c === "\"")
			{
				quoted = false;
			}
			else
			{
				field += c;
			}
		}
		else if (c === "\"")
		{
			quoted = true;
		}
		else if (c === ",")
		{
			fields.push(field);
			field = "";
		}
		else if (c === "\n" || c === "\r")
		{
			if (c === "\r" && text[i + 1] === "\n")
			{
				i++;
			}
			fields.push(field);
			field = "";
			out.push(fields);
			fields = [];
		}
		else
		{
			field += c;
		}
	}
	if (field.length > 0 || fields.length > 0)
	{
		fields.push(field);
		out.push(fields);
	}
	return out;
}

const isColour = (text: string) => /^#[0-9a-fA-F]{6}$/.test(text);

// CSV rows: name, weight, image (not kept here), colour; a first row starting with "name" is a header
export function parseCsv(text: string): { cards: Card[] | null; error: string | null }
{
	const cards: Card[] = [];
	const rowsRead = records(text);
	for (let i = 0; i < rowsRead.length; i++)
	{
		const fields = rowsRead[i];
		if (fields.every(f => f.trim().length === 0) || (i === 0 && fields[0].trim().toLowerCase() === "name"))
		{
			continue;
		}
		let weight = 1;
		if (fields.length > 1 && fields[1].trim().length > 0)
		{
			const parsed = weightOf(fields[1]);
			if (parsed.error)
			{
				return { cards: null, error: `Row ${i + 1}: ${parsed.error}` };
			}
			weight = parsed.value;
		}
		const made = make(fields[0].trim(), weight);
		if (!made.card)
		{
			return { cards: null, error: `Row ${i + 1}: ${made.error}` };
		}
		const colour = (fields[3] ?? "").trim();
		if (colour.length > 0)
		{
			if (!isColour(colour))
			{
				return { cards: null, error: `Row ${i + 1}: the colour '${colour}' must be #RRGGBB.` };
			}
			made.card.color = colour.toUpperCase();
		}
		cards.push(made.card);
	}
	return cards.length > MAX_CARDS ? { cards: null, error: `At most ${MAX_CARDS} cards in a deck.` } : { cards, error: null };
}

const quote = (field: string) => /[,"\r\n]/.test(field) ? `"${field.replace(/"/g, "\"\"")}"` : field;

// As the desktop writes it: UTF-8 with a BOM, so Excel reads Thai
export function toCsv(cards: Card[], language: string): string
{
	return "﻿name,weight,image,color\r\n"
		+ cards.map(card => `${quote(shownCard(card, language))},${card.weight},,${card.color ?? ""}\r\n`).join("");
}

// A file's bytes: UTF-8 (with or without a BOM), UTF-16 with a BOM, else the Thai code page Excel writes
export function readText(bytes: Uint8Array): string
{
	if (bytes.length >= 2 && ((bytes[0] === 0xFF && bytes[1] === 0xFE) || (bytes[0] === 0xFE && bytes[1] === 0xFF)))
	{
		return new TextDecoder(bytes[0] === 0xFF ? "utf-16le" : "utf-16be").decode(bytes.subarray(2));
	}
	try
	{
		return new TextDecoder("utf-8", { fatal: true }).decode(bytes).replace(/^﻿/, "");
	}
	catch
	{
		return new TextDecoder("windows-874").decode(bytes);
	}
}

//==================================================================
//  Draws: by weight among the ticked cards; a pile with No repeat
//==================================================================
// count cards; pile: the names drawn so far with No repeat, added to (null: every card each time)
export function drawCards(source: RandomSource, cards: Card[], count: number, pile: Set<string> | null)
	: { drawn: Card[]; error: string | null }
{
	const drawn: Card[] = [];
	for (let i = 0; i < count; i++)
	{
		const open = pile ? cards.filter(card => !pile.has(card.name)) : cards;
		const total = open.reduce((sum, card) => sum + card.weight, 0);
		if (total === 0)
		{
			return { drawn, error: drawn.length === 0 ? "No cards left: Shuffle puts them back." : `Only ${drawn.length} left; Shuffle puts them back.` };
		}
		let at = Number(source.below(BigInt(total)));
		const card = open.find(item => (at -= item.weight) < 0)!;
		drawn.push(card);
		pile?.add(card.name);
	}
	return { drawn, error: null };
}

//==================================================================
//  What is kept: own decks, unticked cards, piles, the last choices
//==================================================================
export interface CardsState
{
	decks: Deck[];    // the user's own
	deck: string;    // the deck chosen
	language: string;    // en, th: ready-made names
	count: number;
	noRepeat: boolean;
	off: Record<string, string[]>;    // deck id -> card names unticked
	piles: Record<string, string[]>;    // deck id -> card names drawn (No repeat)
}

export function firstCards(): CardsState
{
	return { decks: [], deck: READY + "playing-cards", language: "en", count: 1, noRepeat: false, off: {}, piles: {} };
}

export function cardsFrom(saved: unknown): CardsState
{
	const state = firstCards();
	if (typeof saved !== "object" || saved === null)
	{
		return state;
	}
	const s = saved as Record<string, unknown>;
	if (Array.isArray(s.decks))
	{
		state.decks = s.decks.filter((d): d is Deck => typeof d === "object" && d !== null && typeof (d as Deck).id === "string"
			&& typeof (d as Deck).name === "string" && Array.isArray((d as Deck).cards));
	}
	if (typeof s.deck === "string")
	{
		state.deck = s.deck;
	}
	state.language = s.language === "th" ? "th" : "en";
	if (typeof s.count === "number")
	{
		state.count = s.count;
	}
	state.noRepeat = s.noRepeat === true;
	for (const key of ["off", "piles"] as const)
	{
		if (typeof s[key] === "object" && s[key] !== null)
		{
			state[key] = s[key] as Record<string, string[]>;
		}
	}
	return state;
}

// A new own deck's id
export const newDeckId = () => "mine:" + Date.now().toString(36) + Math.floor(Math.random() * 1e6).toString(36);
