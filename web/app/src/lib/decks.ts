// decks.ts - Export / Import Decks: the user's own decks and their pictures in one zip (owner 2026-11-23)
//
// The desktop's file (DeckTransfer.cs): decks.json and pictures/<name>.png,
// each picture once however many cards use it, and a deck's own cover
// picture (owner 2026-11-24), so a deck goes through a phone and back with
// its pictures. Import adds, never replaces: a deck of
// the same name and the same cards is skipped, one of the same name with
// other cards comes in as "name (2)". Only decks.json and
// pictures/<16 hex>.png are read from a zip, with limits (zip.ts).

import { MAX_CARDS, MAX_NAME, MAX_WEIGHT, type Card, type CardsState, type Deck, newDeckId } from "./cards";
import { readZip, writeZip, ZipError, type ZipEntry } from "./zip";

export type Pictures = Record<string, Uint8Array>;

export const FORMAT = "softcalc-decks";
export const VERSION = 1;
export const MAX_DECKS = 500;
export const MAX_ZIP = 64 * 1024 * 1024;
export const MAX_JSON = 32 * 1024 * 1024;
export const MAX_PICTURE = 1024 * 1024;
const MAX_ENTRIES = 20000;
const MAX_TOTAL = 256 * 1024 * 1024;
const MAX_SIDE = 4096;
const DECKS = "decks.json";
const FOLDER = "pictures/";
const IMAGE = /^[0-9a-f]{16}\.png$/;

interface RawDeck
{
	name: unknown;
	cards: unknown;
	cover?: unknown;
}

export interface DecksRead
{
	decks: RawDeck[];
	pictures: Pictures;
}

export interface DecksImported
{
	added: number;
	renamed: number;
	same: number;
	overLimit: number;
	picturesLost: number;
}

export function report(done: DecksImported): string
{
	const parts = [`${done.added} deck(s) added`];
	if (done.renamed > 0)
	{
		parts.push(`${done.renamed} of them renamed, as a deck of that name has other cards`);
	}
	if (done.same > 0)
	{
		parts.push(`${done.same} skipped, already in my decks`);
	}
	if (done.overLimit > 0)
	{
		parts.push(`${done.overLimit} left out: at most ${MAX_DECKS} decks`);
	}
	if (done.picturesLost > 0)
	{
		parts.push(`${done.picturesLost} picture(s) could not be read`);
	}
	return parts.join("; ") + ".";
}

// The picture names the decks use
export function usedPictures(decks: Deck[]): Set<string>
{
	return new Set(decks.flatMap(deck => [...deck.cards.map(card => card.image ?? ""), deck.cover ?? ""]).filter(name => name.length > 0));
}

//==================================================================
//  Export
//==================================================================
// The decks and the pictures they use; a card whose picture is not here goes without one
export function decksZip(decks: Deck[], pictures: Pictures, time = new Date()): Uint8Array
{
	const used = new Map<string, Uint8Array>();
	const keep = (name: string) =>
	{
		used.set(name, pictures[name]);
		return name;
	};
	const model = {
		format: FORMAT, version: VERSION,
		decks: decks.map(deck => ({
			name: deck.name,
			...(deck.cover && pictures[deck.cover] ? { cover: keep(deck.cover) } : {}),
			cards: deck.cards.map(card =>
			{
				const image = card.image && pictures[card.image] ? card.image : undefined;
				if (image)
				{
					used.set(image, pictures[image]);
				}
				return { name: card.name, weight: card.weight, ...(image ? { image } : {}), ...(card.color ? { color: card.color } : {}),
					...(card.off ? { off: true } : {}) };
			}),
		})),
	};
	const entries: ZipEntry[] = [{ name: DECKS, bytes: new TextEncoder().encode(JSON.stringify(model, null, 2)) }];
	for (const [name, bytes] of used)
	{
		entries.push({ name: FOLDER + name, bytes });
	}
	return writeZip(entries, time);
}

//==================================================================
//  Read (with limits)
//==================================================================
// A PNG no larger than MAX_SIDE a side (its header)
function isPng(bytes: Uint8Array): boolean
{
	const sign = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
	if (bytes.length < 24 || sign.some((b, i) => bytes[i] !== b))
	{
		return false;
	}
	const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
	const width = view.getUint32(16), height = view.getUint32(20);
	return width >= 1 && height >= 1 && width <= MAX_SIDE && height <= MAX_SIDE;
}

export async function readDecks(bytes: Uint8Array): Promise<{ read: DecksRead | null; error: string | null }>
{
	const notDecks = { read: null, error: "this is not a SoftCalc decks file." };
	if (bytes.length > MAX_ZIP)
	{
		return { read: null, error: `the file is larger than ${MAX_ZIP / (1024 * 1024)} MB.` };
	}
	let found: Map<string, Uint8Array | null>;
	try
	{
		found = await readZip(bytes, {
			maxEntries: MAX_ENTRIES, maxTotal: MAX_TOTAL,
			wanted: name => name === DECKS ? MAX_JSON : name.startsWith(FOLDER) && IMAGE.test(name.slice(FOLDER.length)) ? MAX_PICTURE : null,
		});
	}
	catch (problem)
	{
		return { read: null, error: problem instanceof ZipError ? `the zip file: ${problem.message}.` : String(problem) };
	}
	const json = found.get(DECKS);
	if (json === undefined)
	{
		return notDecks;
	}
	if (json === null)
	{
		return { read: null, error: "decks.json is too large." };
	}
	let model: { format?: unknown; version?: unknown; decks?: unknown };
	try
	{
		model = JSON.parse(new TextDecoder().decode(json).replace(/^﻿/, ""));
	}
	catch
	{
		return { read: null, error: "decks.json in the file could not be read." };
	}
	if (typeof model !== "object" || model === null || model.format !== FORMAT)
	{
		return notDecks;
	}
	if (typeof model.version === "number" && model.version > VERSION)
	{
		return { read: null, error: "the file was made by a newer SoftCalc." };
	}
	const decks = Array.isArray(model.decks) ? model.decks.filter((d): d is RawDeck => typeof d === "object" && d !== null) : [];
	const pictures: Pictures = {};
	for (const [name, picture] of found)
	{
		if (name.startsWith(FOLDER) && picture !== null && isPng(picture))
		{
			pictures[name.slice(FOLDER.length)] = picture;
		}
	}
	return { read: { decks, pictures }, error: null };
}

//==================================================================
//  Import: add to my decks
//==================================================================
function trimmed(value: unknown, fallback: string): string
{
	const text = (typeof value === "string" ? value : "").trim();
	const chars = [...text];
	return chars.length === 0 ? fallback : chars.length > MAX_NAME ? chars.slice(0, MAX_NAME).join("") : text;
}

// A deck's cards as the desktop keeps them (CardStore.SanitizeDeck); a name that is there already goes
function cardsOf(raw: unknown, pictures: Pictures, lost: Set<string>): Card[]
{
	const cards: Card[] = [];
	const names = new Set<string>();
	for (const item of Array.isArray(raw) ? raw : [])
	{
		if (typeof item !== "object" || item === null || typeof item.name !== "string" || item.name.trim().length === 0)
		{
			continue;
		}
		const name = trimmed(item.name, "Card");
		if (cards.length >= MAX_CARDS || names.has(name.toLowerCase()))
		{
			continue;
		}
		names.add(name.toLowerCase());
		const weight = typeof item.weight === "number" && Number.isFinite(item.weight) ? Math.min(MAX_WEIGHT, Math.max(1, Math.trunc(item.weight))) : 1;
		const card: Card = { name, weight };
		if (typeof item.image === "string" && IMAGE.test(item.image))
		{
			if (pictures[item.image])
			{
				card.image = item.image;
			}
			else
			{
				lost.add(item.image);
			}
		}
		if (typeof item.color === "string" && /^#[0-9a-fA-F]{6}$/.test(item.color))
		{
			card.color = item.color;
		}
		if (item.off === true)
		{
			card.off = true;
		}
		cards.push(card);
	}
	return cards;
}

// "Food" is "Food", or "Food (2)" from an earlier import
function sameName(have: string, name: string): boolean
{
	const a = have.toLowerCase(), b = name.toLowerCase();
	return a === b || (a.startsWith(b + " (") && /^\d+\)$/.test(a.slice(b.length + 2)));
}

const sameCards = (a: Card[], b: Card[]) => a.length === b.length && a.every((card, i) => card.name === b[i].name
	&& card.weight === b[i].weight && card.image === b[i].image && card.color === b[i].color && (card.off ?? false) === (b[i].off ?? false));

// The state and pictures with the decks added; pictures no deck uses are dropped
export function importDecks(state: CardsState, pictures: Pictures, read: DecksRead): { state: CardsState; pictures: Pictures; done: DecksImported }
{
	const decks = [...state.decks];
	const lost = new Set<string>();
	const done: DecksImported = { added: 0, renamed: 0, same: 0, overLimit: 0, picturesLost: 0 };
	for (const raw of read.decks)
	{
		const cards = cardsOf(raw.cards, read.pictures, lost);
		const name = trimmed(raw.name, "Deck");
		if (cards.length === 0)
		{
			continue;
		}
		if (decks.some(d => sameName(d.name, name) && sameCards(d.cards, cards)))
		{
			done.same++;
			continue;
		}
		if (decks.length >= MAX_DECKS)
		{
			done.overLimit++;
			continue;
		}
		let free = name;
		for (let n = 2; decks.some(d => d.name.toLowerCase() === free.toLowerCase()); n++)
		{
			free = `${name} (${n})`;
		}
		if (free !== name)
		{
			done.renamed++;
		}
		const cover = typeof raw.cover === "string" && IMAGE.test(raw.cover) && read.pictures[raw.cover] ? raw.cover : undefined;
		decks.push({ id: newDeckId(), name: free, cards, ...(cover ? { cover } : {}) });
		done.added++;
	}
	done.picturesLost = lost.size;
	const used = usedPictures(decks);
	const kept: Pictures = {};
	for (const name of used)
	{
		const picture = pictures[name] ?? read.pictures[name];
		if (picture)
		{
			kept[name] = picture;
		}
	}
	return { state: { ...state, decks }, pictures: kept, done };
}
