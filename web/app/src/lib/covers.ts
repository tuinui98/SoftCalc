// covers.ts - Deck covers and card pictures on the web (owner 2026-11-24; desktop DeckCovers.cs, CardPictures.cs)
//
// A cover is, in this order: the picture chosen for the deck (Deck.cover);
// the first card's picture; up to four of its cards' colours as a 2 x 2
// mosaic; else the first letter of its name on a colour of its own (the
// same deck id gets the same colour as on the desktop). Pictures are the
// own decks' (kept with the decks, decks.ts) or the ready-made decks'
// (data/cards/*/png, shared with the desktop, "ready:<set>/<file>.png").

import type { Deck } from "./cards";

export const MOSAIC = 4;
export const READY_PICTURE = "ready:";

// Letter covers: dark enough for white text (DeckCovers.cs Backs)
const BACKS = ["#2F3A4A", "#4A5D78", "#356A9A", "#2E7D6B", "#7A4E2D", "#8E3B46", "#5B4A8A", "#3E6B3A"];

export type Cover =
	| { kind: "picture"; image: string; chosen: boolean }
	| { kind: "colours"; colours: string[] }
	| { kind: "letter"; letter: string; back: string };

export function deckCover(deck: Deck, shownName: string, exists: (name: string) => boolean): Cover
{
	if (deck.cover && exists(deck.cover))
	{
		return { kind: "picture", image: deck.cover, chosen: true };
	}
	const pictured = deck.cards.find(card => card.image && exists(card.image));
	if (pictured)
	{
		return { kind: "picture", image: pictured.image!, chosen: false };
	}
	const colours = [...new Set(deck.cards.filter(card => card.color).map(card => card.color!.toUpperCase()))].slice(0, MOSAIC);
	if (colours.length > 0)
	{
		return { kind: "colours", colours };
	}
	return { kind: "letter", letter: initial(shownName), back: backFor(deck.id) };
}

// The first letter as a reader sees it (a Thai letter with its marks); of the part after ": " when
// there is one, so "Districts: Bangkok" is B and the district decks are told apart
export function initial(name: string): string
{
	const colon = name.lastIndexOf(": ");
	const trimmed = (colon >= 0 && name.slice(colon + 2).trim().length > 0 ? name.slice(colon + 2) : name).trim();
	if (trimmed.length === 0)
	{
		return "?";
	}
	const first = new Intl.Segmenter(undefined, { granularity: "grapheme" }).segment(trimmed)[Symbol.iterator]().next().value!.segment;
	return first.toUpperCase();
}

// FNV-1a over the id's UTF-16 units, as the desktop, so a deck keeps its colour on both
export function backFor(id: string): string
{
	let hash = 2166136261;
	for (let i = 0; i < id.length; i++)
	{
		hash = Math.imul(hash ^ id.charCodeAt(i), 16777619) >>> 0;
	}
	return BACKS[hash % BACKS.length];
}

// The four cells of a mosaic, colours repeating when there are fewer
export function mosaic(colours: string[]): string[]
{
	return Array.from({ length: MOSAIC }, (_, i) => colours[i % colours.length]);
}

//==================================================================
//  Ready-made pictures: built files, so they are cached by name
//==================================================================
const built = import.meta.glob("../../../../data/cards/*/png/*.png", { query: "?url", import: "default", eager: true }) as Record<string, string>;
const readyUrls = new Map(Object.entries(built).map(([path, url]) =>
{
	const [, set, file] = /\/data\/cards\/([^/]+)\/png\/([^/]+)$/.exec(path)!;
	return [`${READY_PICTURE}${set}/${file}`, url];
}));

export function readyPictureUrl(name: string): string | null
{
	return readyUrls.get(name) ?? null;
}

// A picture's name as the desktop makes it (CardStore.ImageNameOf): the first 16 hex of its SHA-256
export async function pictureName(png: Uint8Array): Promise<string>
{
	const hash = new Uint8Array(await crypto.subtle.digest("SHA-256", png));
	return [...hash.slice(0, 8)].map(b => b.toString(16).padStart(2, "0")).join("") + ".png";
}
