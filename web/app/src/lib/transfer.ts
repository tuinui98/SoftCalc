// transfer.ts - Export and Import in the desktop's own file forms (owner 2026-11-19)
//
// History is history.jsonl as the desktop writes it (HistoryStore.cs): one
// JSON object per line, an entry {id, time, kind, input, display_input,
// text, note}, a {cleared_at} mark or a {deleted, time} mark. Definitions
// are a .pcf file as the desktop's Save writes it (PcfFile.cs): UTF-16 LE
// without a BOM, CRLF lines. Either file goes between the web app, the
// Android app and the desktop unchanged.

import type { Entry } from "./calc";

// Export names carry the local date and time, so files from several exports stay apart
// (owner 2026-11-19); Import tells the two kinds apart by what is in them, not by name
export function exportNames(time = new Date()): { history: string; definitions: string }
{
	const two = (n: number) => String(n).padStart(2, "0");
	const at = `${time.getFullYear()}-${two(time.getMonth() + 1)}-${two(time.getDate())}-${two(time.getHours())}${two(time.getMinutes())}`;
	return { history: `SoftCalc-history-${at}.jsonl`, definitions: `SoftCalc-definitions-${at}.pcf` };
}

//==================================================================
//  Entry ids and times, as the desktop's
//==================================================================

// "<start, UTC, to the ms>-<number>-<sequence>", as HistoryStore.MakeId; the desktop's middle part
// is its process id, here a random number, so two devices started in the same ms still differ
const session = `${stamp(new Date())}-${Math.floor(Math.random() * 900000) + 100000}`;
let sequence = 0;

function stamp(time: Date): string
{
	return time.toISOString().replace(/[-:TZ.]/g, "");
}

export function newId(): string
{
	sequence++;
	return `${session}-${sequence}`;
}

// Lines kept before ids came in get one, and a time just before now in their order
export function withIds(entries: Entry[], now = Date.now()): Entry[]
{
	return entries.map((entry, index) => entry.id && entry.time ? entry
		: { ...entry, id: entry.id ?? newId(), time: entry.time ?? new Date(now - (entries.length - index)).toISOString() });
}

//==================================================================
//  History file
//==================================================================

export function historyText(entries: Entry[]): string
{
	return entries.filter(entry => entry.kind !== "notice").map(entry => JSON.stringify({
		id: entry.id, time: entry.time, kind: entry.kind, input: entry.input,
		display_input: entry.display, text: entry.text, note: entry.note ?? "",
	}) + "\n").join("");
}

// The entries a history.jsonl shows, as the desktop reads it (HistoryStore.Visible): after the
// newest clear, without deleted ids; a damaged line is skipped. null: not a History file.
export function parseHistory(text: string): Entry[] | null
{
	const entries: Entry[] = [];
	const deleted = new Set<string>();
	let cleared = -Infinity;
	let objects = 0;
	for (const line of text.split(/\r?\n/))
	{
		if (line.trim().length === 0)
		{
			continue;
		}
		let item: Record<string, unknown>;
		try
		{
			item = JSON.parse(line);
		}
		catch
		{
			continue;
		}
		if (typeof item !== "object" || item === null)
		{
			continue;
		}
		objects++;
		if (typeof item.cleared_at === "string")
		{
			cleared = Math.max(cleared, Date.parse(item.cleared_at));
		}
		else if (typeof item.deleted === "string")
		{
			deleted.add(item.deleted);
		}
		else if (typeof item.id === "string" && item.id.length > 0 && typeof item.time === "string"
			&& typeof item.kind === "string" && typeof item.input === "string" && typeof item.text === "string")
		{
			const note = typeof item.note === "string" && item.note.length > 0 ? item.note : undefined;
			const display = typeof item.display_input === "string" ? item.display_input : item.input;
			entries.push({ id: item.id, time: item.time, kind: item.kind, input: item.input, display, text: item.text, note });
		}
	}
	if (objects === 0)
	{
		return null;
	}
	return entries.filter(entry => Date.parse(entry.time!) > cleared && !deleted.has(entry.id!));
}

// Both lists in one, as the desktop merges windows: one line per id (the file's wins), by time
// then id, the newest `cap`
export function mergeHistory(mine: Entry[], theirs: Entry[], cap: number): Entry[]
{
	const byId = new Map<string, Entry>();
	for (const entry of [...mine, ...theirs])
	{
		byId.set(entry.id!, entry);
	}
	const merged = [...byId.values()].sort((a, b) =>
		(Date.parse(a.time!) - Date.parse(b.time!)) || (a.id! < b.id! ? -1 : a.id! > b.id! ? 1 : 0));
	return merged.slice(-cap);
}

//==================================================================
//  .pcf file
//==================================================================

// UTF-16 LE, no BOM, CRLF (the desktop's PcfFile.Encode)
export function pcfBytes(text: string): Uint8Array
{
	const crlf = text.replace(/\r?\n/g, "\r\n");
	const bytes = new Uint8Array(crlf.length * 2);
	for (let i = 0; i < crlf.length; i++)
	{
		const unit = crlf.charCodeAt(i);
		bytes[i * 2] = unit & 0xff;
		bytes[i * 2 + 1] = unit >> 8;
	}
	return bytes;
}

// Any file's text: UTF-8 or UTF-16 LE/BE, with or without a BOM (the desktop's PcfFile.Decode)
export function fileText(bytes: Uint8Array): string
{
	if (bytes.length >= 3 && bytes[0] === 0xef && bytes[1] === 0xbb && bytes[2] === 0xbf)
	{
		return new TextDecoder("utf-8").decode(bytes.subarray(3));
	}
	if (bytes.length >= 2 && bytes[0] === 0xff && bytes[1] === 0xfe)
	{
		return new TextDecoder("utf-16le").decode(bytes.subarray(2));
	}
	if (bytes.length >= 2 && bytes[0] === 0xfe && bytes[1] === 0xff)
	{
		return new TextDecoder("utf-16be").decode(bytes.subarray(2));
	}
	return new TextDecoder(looksUtf16Le(bytes) ? "utf-16le" : "utf-8").decode(bytes);
}

// ASCII text in UTF-16 LE has a zero in most odd bytes; UTF-8 text has none
function looksUtf16Le(bytes: Uint8Array): boolean
{
	if (bytes.length < 2 || bytes.length % 2 !== 0)
	{
		return false;
	}
	let zeros = 0;
	for (let i = 1; i < bytes.length; i += 2)
	{
		if (bytes[i] === 0)
		{
			zeros++;
		}
	}
	return zeros * 2 > bytes.length / 2;
}
