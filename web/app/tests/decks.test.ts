// decks.test.ts - Export / Import Decks: one zip with decks.json and the pictures (desktop DeckTransferTests)

import { deflateRawSync } from "node:zlib";
import { describe, expect, it } from "vitest";
import { firstCards, type CardsState, type Deck } from "../src/lib/cards";
import { decksZip, importDecks, MAX_DECKS, MAX_PICTURE, readDecks, report, type DecksRead, type Pictures } from "../src/lib/decks";
import { crc32, readZip, writeZip } from "../src/lib/zip";

// A PNG header: the signature and an IHDR of width x height (enough for the checks)
function png(width = 40, height = 40, fill = 1): Uint8Array
{
	const bytes = new Uint8Array(64).fill(fill);
	bytes.set([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52]);
	const view = new DataView(bytes.buffer);
	view.setUint32(16, width);
	view.setUint32(20, height);
	return bytes;
}

const RED = "aaaaaaaaaaaaaaaa.png";

function withDecks(): { state: CardsState; pictures: Pictures }
{
	const decks: Deck[] = [
		{ id: "mine:1", name: "Food", cards: [
			{ name: "Rice", weight: 3, image: RED },
			{ name: "Noodles", weight: 1, image: RED, color: "#112233" },
			{ name: "Soup", weight: 1, off: true },
		] },
		{ id: "mine:2", name: "Days", cards: [{ name: "Mon", weight: 1 }, { name: "Tue", weight: 1 }] },
	];
	return { state: { ...firstCards(), decks }, pictures: { [RED]: png() } };
}

async function readBack(zip: Uint8Array): Promise<DecksRead>
{
	const { read, error } = await readDecks(zip);
	expect(error).toBeNull();
	return read!;
}

// A zip the way the desktop's ZipArchive writes it: entries deflated
function deflatedZip(entries: [string, Uint8Array][]): Uint8Array
{
	const parts: Uint8Array[] = [], central: Uint8Array[] = [];
	let offset = 0;
	for (const [name, bytes] of entries)
	{
		const packed = new Uint8Array(deflateRawSync(bytes));
		const nameBytes = new TextEncoder().encode(name);
		const local = new Uint8Array(30 + nameBytes.length);
		const l = new DataView(local.buffer);
		l.setUint32(0, 0x04034B50, true);
		l.setUint16(8, 8, true);
		l.setUint32(14, crc32(bytes), true);
		l.setUint32(18, packed.length, true);
		l.setUint32(22, bytes.length, true);
		l.setUint16(26, nameBytes.length, true);
		local.set(nameBytes, 30);
		const head = new Uint8Array(46 + nameBytes.length);
		const c = new DataView(head.buffer);
		c.setUint32(0, 0x02014B50, true);
		c.setUint16(10, 8, true);
		c.setUint32(16, crc32(bytes), true);
		c.setUint32(20, packed.length, true);
		c.setUint32(24, bytes.length, true);
		c.setUint16(28, nameBytes.length, true);
		c.setUint32(42, offset, true);
		head.set(nameBytes, 46);
		parts.push(local, packed);
		central.push(head);
		offset += local.length + packed.length;
	}
	const size = central.reduce((sum, p) => sum + p.length, 0);
	const end = new Uint8Array(22);
	const e = new DataView(end.buffer);
	e.setUint32(0, 0x06054B50, true);
	e.setUint16(8, entries.length, true);
	e.setUint16(10, entries.length, true);
	e.setUint32(12, size, true);
	e.setUint32(16, offset, true);
	const all = [...parts, ...central, end];
	const out = new Uint8Array(all.reduce((sum, p) => sum + p.length, 0));
	let at = 0;
	for (const p of all)
	{
		out.set(p, at);
		at += p.length;
	}
	return out;
}

const json = (value: unknown) => new TextEncoder().encode(JSON.stringify(value));

describe("decks zip", () =>
{
	it("writes and reads a zip", async () =>
	{
		const zip = writeZip([{ name: "a.txt", bytes: new TextEncoder().encode("hello") }, { name: "ไทย/b", bytes: new Uint8Array(0) }]);
		const found = await readZip(zip, { maxEntries: 10, maxTotal: 100, wanted: () => 10 });
		expect(new TextDecoder().decode(found.get("a.txt")!)).toBe("hello");
		expect(found.get("ไทย/b")).toEqual(new Uint8Array(0));
		// larger than wanted: null; past the total: refused
		expect((await readZip(zip, { maxEntries: 10, maxTotal: 100, wanted: () => 3 })).get("a.txt")).toBeNull();
		await expect(readZip(zip, { maxEntries: 1, maxTotal: 100, wanted: () => 10 })).rejects.toThrow("more than 1");
		await expect(readZip(zip, { maxEntries: 10, maxTotal: 3, wanted: () => 10 })).rejects.toThrow("too much");
	});

	it("takes decks and pictures out and back", async () =>
	{
		const from = withDecks();
		const zip = decksZip(from.state.decks, from.pictures);
		const entries = await readZip(zip, { maxEntries: 10, maxTotal: 1e6, wanted: () => 1e6 });
		// the shared picture once
		expect([...entries.keys()]).toEqual(["decks.json", "pictures/" + RED]);
		const text = new TextDecoder().decode(entries.get("decks.json")!);
		expect(text).toContain("\"format\": \"softcalc-decks\"");
		expect(text).not.toContain("\"id\"");
		expect(text).not.toContain("false");

		const done = importDecks(firstCards(), {}, await readBack(zip));
		expect(done.done).toEqual({ added: 2, renamed: 0, same: 0, overLimit: 0, picturesLost: 0 });
		const food = done.state.decks[0];
		expect(food.name).toBe("Food");
		expect(food.id).not.toBe("mine:1");
		expect(food.cards).toEqual(from.state.decks[0].cards);
		expect(Object.keys(done.pictures)).toEqual([RED]);
	});

	it("skips the same deck and renames another", async () =>
	{
		const from = withDecks();
		let to = importDecks(firstCards(), {}, await readBack(decksZip(from.state.decks, from.pictures)));
		to = importDecks(to.state, to.pictures, await readBack(decksZip(from.state.decks, from.pictures)));
		expect(to.done).toEqual({ added: 0, renamed: 0, same: 2, overLimit: 0, picturesLost: 0 });

		from.state.decks[1].cards.push({ name: "Wed", weight: 1 });
		to = importDecks(to.state, to.pictures, await readBack(decksZip(from.state.decks, from.pictures)));
		expect(to.done).toEqual({ added: 1, renamed: 1, same: 1, overLimit: 0, picturesLost: 0 });
		expect(to.state.decks.map(d => d.name)).toEqual(["Food", "Days", "Days (2)"]);
		expect(report(to.done)).toBe("1 deck(s) added; 1 of them renamed, as a deck of that name has other cards; 1 skipped, already in my decks.");
		// the renamed one again: skipped, not "Days (3)"
		to = importDecks(to.state, to.pictures, await readBack(decksZip(from.state.decks, from.pictures)));
		expect(to.done.same).toBe(2);
	});

	it("stops at the deck limit", async () =>
	{
		const state = firstCards();
		state.decks = Array.from({ length: MAX_DECKS - 1 }, (_, i) => ({ id: "d" + i, name: "D" + i, cards: [{ name: "x", weight: 1 }] }));
		const from = withDecks();
		const to = importDecks(state, {}, await readBack(decksZip(from.state.decks, from.pictures)));
		expect(to.done).toEqual({ added: 1, renamed: 0, same: 0, overLimit: 1, picturesLost: 0 });
	});

	it("reads the desktop's deflated zip", async () =>
	{
		const model = { format: "softcalc-decks", version: 1, decks: [{ name: "Desk", cards: [{ name: "a", weight: 2, image: RED }] }] };
		const read = await readBack(deflatedZip([["decks.json", json(model)], ["pictures/" + RED, png()]]));
		expect(read.pictures[RED]).toEqual(png());
		expect(importDecks(firstCards(), {}, read).state.decks[0].cards).toEqual([{ name: "a", weight: 2, image: RED }]);
	});

	it("refuses what is not a decks file", async () =>
	{
		expect((await readDecks(new TextEncoder().encode("not a zip"))).error).toBe("the zip file: not a zip file, or it is damaged.");
		expect((await readDecks(writeZip([{ name: "other.json", bytes: json({}) }]))).error).toBe("this is not a SoftCalc decks file.");
		expect((await readDecks(writeZip([{ name: "decks.json", bytes: json({ format: "x" }) }]))).error).toBe("this is not a SoftCalc decks file.");
		expect((await readDecks(writeZip([{ name: "decks.json", bytes: new TextEncoder().encode("{broken") }]))).error)
			.toBe("decks.json in the file could not be read.");
		expect((await readDecks(writeZip([{ name: "decks.json", bytes: json({ format: "softcalc-decks", version: 2 }) }]))).error)
			.toBe("the file was made by a newer SoftCalc.");
		const damaged = writeZip([{ name: "decks.json", bytes: json({ format: "softcalc-decks", version: 1, decks: [] }) }]);
		damaged[40] ^= 0xFF;
		expect((await readDecks(damaged)).error).toBe("the zip file: damaged.");
	});

	it("takes only well-named small PNGs and checks the cards", async () =>
	{
		const BIG = "bbbbbbbbbbbbbbbb.png", WIDE = "cccccccccccccccc.png";
		const model = { format: "softcalc-decks", version: 1, decks: [{ name: " Odd ", cards: [
			{ name: "a", image: RED }, { name: "b", image: "../evil.png" }, { name: "c", image: BIG, weight: 99999, color: "red" },
			{ name: "d", image: WIDE }, { name: "A" }, { name: " " }, "junk", { name: 5 },
		] }, { name: "Empty", cards: [] }] };
		const read = await readBack(writeZip([
			{ name: "decks.json", bytes: json(model) }, { name: "pictures/" + RED, bytes: png() }, { name: "pictures/../evil.png", bytes: png() },
			{ name: "pictures/" + BIG, bytes: new Uint8Array(MAX_PICTURE + 1) }, { name: "pictures/" + WIDE, bytes: png(5000, 10) },
		]));
		expect(Object.keys(read.pictures)).toEqual([RED]);
		const to = importDecks(firstCards(), {}, read);
		expect(to.done).toEqual({ added: 1, renamed: 0, same: 0, overLimit: 0, picturesLost: 2 });
		const deck = to.state.decks[0];
		expect(deck.name).toBe("Odd");
		expect(deck.cards).toEqual([{ name: "a", weight: 1, image: RED }, { name: "b", weight: 1 }, { name: "c", weight: 1000 }, { name: "d", weight: 1 }]);
	});

	it("drops pictures no deck uses", async () =>
	{
		const from = withDecks();
		const read = await readBack(decksZip(from.state.decks, from.pictures));
		const old = "dddddddddddddddd.png";
		const to = importDecks(firstCards(), { [old]: png() }, read);
		expect(Object.keys(to.pictures)).toEqual([RED]);
	});
});
