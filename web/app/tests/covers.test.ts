// covers.test.ts - Deck covers and pictures on the web (owner 2026-11-24), as DeckCoversTests.cs

import { describe, expect, it } from "vitest";
import { backFor, deckCover, initial, mosaic, pictureName, readyPictureUrl } from "../src/lib/covers";
import { cardsFrom, firstCards, readyDecks, type Deck } from "../src/lib/cards";
import { decksZip, importDecks, readDecks, usedPictures } from "../src/lib/decks";

const files = new Set(["aaaaaaaaaaaaaaaa.png", "bbbbbbbbbbbbbbbb.png"]);
const has = (name: string) => files.has(name);

describe("deck covers", () =>
{
	it("takes the deck's own picture, then the first card's, then colours, then the first letter", () =>
	{
		const food: Deck = { id: "d1", name: "Food", cover: "bbbbbbbbbbbbbbbb.png", cards: [{ name: "Rice", weight: 1, image: "aaaaaaaaaaaaaaaa.png" }] };
		expect(deckCover(food, "Food", has)).toEqual({ kind: "picture", image: "bbbbbbbbbbbbbbbb.png", chosen: true });
		expect(deckCover({ ...food, cover: "cccccccccccccccc.png" }, "Food", has))
			.toEqual({ kind: "picture", image: "aaaaaaaaaaaaaaaa.png", chosen: false });
		const gym: Deck = { id: "d2", name: "Gym", cards: ["#ff0000", "#00FF00", "#FF0000", "#0000ff", "#111111", "#222222"]
			.map((color, i) => ({ name: `c${i}`, weight: 1, color })) };
		expect(deckCover(gym, "Gym", has)).toEqual({ kind: "colours", colours: ["#FF0000", "#00FF00", "#0000FF", "#111111"] });
		expect(mosaic(["#AAAAAA", "#BBBBBB"])).toEqual(["#AAAAAA", "#BBBBBB", "#AAAAAA", "#BBBBBB"]);
		expect(deckCover({ id: "abc123", name: "chores", cards: [{ name: "Dishes", weight: 1 }] }, "chores", has))
			.toEqual({ kind: "letter", letter: "C", back: "#8E3B46" });
	});

	it("gives a deck the same colour as the desktop does", () =>
	{
		// DeckCovers.BackFor: FNV-1a over the id
		expect([backFor("abc123"), backFor("ready:months"), backFor("food01")]).toEqual(["#8E3B46", "#8E3B46", "#5B4A8A"]);
		expect([initial("ไทย"), initial("กิน"), initial("  ")]).toEqual(["ไ", "กิ", "?"]);
		expect([initial("Districts: Bangkok"), initial("อำเภอ: กรุงเทพมหานคร"), initial("Note: ")]).toEqual(["B", "ก", "N"]);
	});

	it("shows the playing cards and the flags from the build", () =>
	{
		const ready = readyDecks();
		const pack = ready.find(d => d.id === "ready:playing-cards")!;
		expect(pack.cards.every(card => card.image && readyPictureUrl(card.image))).toBe(true);
		expect(pack.cards[0].image).toBe("ready:playing/As.png");
		const countries = ready.find(d => d.id === "ready:countries")!;
		expect(countries.cards).toHaveLength(196);
		expect(countries.cards.every(card => card.image && readyPictureUrl(card.image))).toBe(true);
		expect(countries.cards.find(card => card.name === "Thailand")!.image).toBe("ready:flags/th.png");
		expect(readyPictureUrl("ready:flags/xx.png")).toBeNull();
	});

	it("names a picture as the desktop does: the first 16 hex of its SHA-256", async () =>
	{
		// SHA-256("abc") = ba7816bf8f01cfea...
		expect(await pictureName(new TextEncoder().encode("abc"))).toBe("ba7816bf8f01cfea.png");
	});

	it("keeps a deck's own cover through the zip, and no ready-made picture in an own deck", async () =>
	{
		const png = new Uint8Array([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52, 0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0]);
		const deck: Deck = { id: "d1", name: "Food", cover: "bbbbbbbbbbbbbbbb.png", cards: [{ name: "Rice", weight: 1 }] };
		expect(usedPictures([deck]).has("bbbbbbbbbbbbbbbb.png")).toBe(true);
		const { read } = await readDecks(decksZip([deck], { "bbbbbbbbbbbbbbbb.png": png }));
		const into = importDecks({ ...firstCards(), decks: [] }, {}, read!);
		expect(into.state.decks[0].cover).toBe("bbbbbbbbbbbbbbbb.png");
		expect(into.pictures["bbbbbbbbbbbbbbbb.png"]).toBeDefined();

		const saved = cardsFrom({ decks: [{ id: "d9", name: "Mine", cover: "ready:playing/As.png",
			cards: [{ name: "A♠", weight: 1, image: "ready:playing/As.png" }] }] });
		expect(saved.decks[0].cover).toBeUndefined();
		expect(saved.decks[0].cards[0].image).toBeUndefined();
	});
});
