// cards.test.ts - ⋮ > Random's Cards tab: ready-made decks, text forms, draws (desktop CardsTests)

import { describe, expect, it } from "vitest";
import { cardsFrom, drawCards, formatLines, listed, parseCsv, parseLines, readText, readyDecks, toCsv, type Card } from "../src/lib/cards";
import type { RandomSource } from "../src/lib/random";

function fixed(...numbers: number[]): RandomSource
{
	let i = 0;
	return { below: (bound: bigint) => BigInt(numbers[i++ % numbers.length]) % bound };
}

describe("cards", () =>
{
	it("has the desktop's ready-made decks from the shared files", () =>
	{
		const decks = readyDecks();
		const find = (id: string) => decks.find(d => d.id === "ready:" + id)!;
		expect(find("playing-cards").cards).toHaveLength(54);
		expect(find("playing-cards").cards.filter(c => c.off)).toHaveLength(2);
		expect(find("months").cards[0]).toEqual({ name: "January", thai: "มกราคม", weight: 1 });
		expect(find("provinces").cards).toHaveLength(77);
		expect(find("districts").cards).toHaveLength(928);
		expect(find("countries").cards).toHaveLength(196);
		expect(find("colors").cards[0].color).toMatch(/^#/);
		expect(listed(find("days"), "en")[0].name).toBe("Monday");
	});

	it("reads and writes name | weight lines", () =>
	{
		expect(parseLines("Fried rice | 2\n\nNoodles\n")).toEqual({ cards: [{ name: "Fried rice", weight: 2 }, { name: "Noodles", weight: 1 }], error: null });
		expect(parseLines("A | 0").error).toBe("Line 1: the weight '0' must be a whole number from 1 to 1000.");
		expect(formatLines([{ name: "A", weight: 1 }, { name: "B", weight: 3 }])).toBe("A\nB | 3");
	});

	it("CSV goes both ways with the desktop's", () =>
	{
		const cards: Card[] = [{ name: "a, b", weight: 2, color: "#AABBCC" }, { name: "c", weight: 1 }];
		const text = toCsv(cards, "en");
		expect(text).toBe("﻿name,weight,image,color\r\n\"a, b\",2,,#AABBCC\r\nc,1,,\r\n");
		expect(parseCsv(readText(new TextEncoder().encode(text))).cards).toEqual(cards);
		expect(parseCsv("x,1,,red").error).toBe("Row 1: the colour 'red' must be #RRGGBB.");
		expect(readText(new Uint8Array([0xA1, 0xA2]))).toBe("กข");    // the Thai code page
	});

	it("draws by weight; No repeat keeps a pile until Shuffle", () =>
	{
		const cards: Card[] = [{ name: "A", weight: 1 }, { name: "B", weight: 3 }];
		expect(drawCards(fixed(0, 1, 3), cards, 3, null).drawn.map(c => c.name)).toEqual(["A", "B", "B"]);
		const pile = new Set<string>();
		expect(drawCards(fixed(0), cards, 2, pile).drawn.map(c => c.name)).toEqual(["A", "B"]);
		expect(drawCards(fixed(0), cards, 1, pile).error).toBe("No cards left: Shuffle puts them back.");
	});

	it("keeps usable saved state", () =>
	{
		const back = cardsFrom({ decks: [{ id: "mine:1", name: "Lunch", cards: [] }, { bad: 1 }], language: "th", count: 3 });
		expect(back.decks).toHaveLength(1);
		expect([back.language, back.count, back.noRepeat]).toEqual(["th", 3, false]);
	});
});
