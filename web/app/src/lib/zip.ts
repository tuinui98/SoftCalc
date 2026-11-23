// zip.ts - A small zip writer and reader for Export / Import Decks (owner 2026-11-23)
//
// Writes its entries stored, not packed: the pictures are PNGs, packed
// already, and decks.json is small. Reads stored and deflated entries (the
// desktop's ZipArchive deflates decks.json) through the browser's own
// DecompressionStream, so no library is needed. A zip from elsewhere is
// read with limits: how many entries, each entry's size counted as it
// unpacks (not what the zip claims), and a total. No zip64, no encryption.

export interface ZipEntry
{
	name: string;
	bytes: Uint8Array;
}

export interface ZipLimits
{
	maxEntries: number;
	maxTotal: number;    // unpacked bytes of the entries read
	// The largest size wanted for an entry, or null to leave it unread
	wanted: (name: string) => number | null;
}

export class ZipError extends Error {}

const TABLE = (() =>
{
	const table = new Uint32Array(256);
	for (let n = 0; n < 256; n++)
	{
		let c = n;
		for (let k = 0; k < 8; k++)
		{
			c = c & 1 ? 0xEDB88320 ^ (c >>> 1) : c >>> 1;
		}
		table[n] = c >>> 0;
	}
	return table;
})();

export function crc32(bytes: Uint8Array): number
{
	let c = 0xFFFFFFFF;
	for (let i = 0; i < bytes.length; i++)
	{
		c = TABLE[(c ^ bytes[i]) & 0xFF] ^ (c >>> 8);
	}
	return (c ^ 0xFFFFFFFF) >>> 0;
}

export const isZip = (bytes: Uint8Array) => bytes.length >= 4 && bytes[0] === 0x50 && bytes[1] === 0x4B && bytes[2] === 0x03 && bytes[3] === 0x04;

//==================================================================
//  Write
//==================================================================
export function writeZip(entries: ZipEntry[], time = new Date()): Uint8Array
{
	const dosTime = (time.getHours() << 11) | (time.getMinutes() << 5) | Math.floor(time.getSeconds() / 2);
	const dosDate = ((time.getFullYear() - 1980) << 9) | ((time.getMonth() + 1) << 5) | time.getDate();
	const encoder = new TextEncoder();
	const locals: Uint8Array[] = [];
	const centrals: Uint8Array[] = [];
	let offset = 0;
	for (const entry of entries)
	{
		const name = encoder.encode(entry.name);
		const crc = crc32(entry.bytes);
		const local = new Uint8Array(30 + name.length);
		const l = new DataView(local.buffer);
		l.setUint32(0, 0x04034B50, true);
		l.setUint16(4, 20, true);
		l.setUint16(6, 0x0800, true);    // names in UTF-8
		l.setUint16(8, 0, true);    // stored
		l.setUint16(10, dosTime, true);
		l.setUint16(12, dosDate, true);
		l.setUint32(14, crc, true);
		l.setUint32(18, entry.bytes.length, true);
		l.setUint32(22, entry.bytes.length, true);
		l.setUint16(26, name.length, true);
		local.set(name, 30);
		const central = new Uint8Array(46 + name.length);
		const c = new DataView(central.buffer);
		c.setUint32(0, 0x02014B50, true);
		c.setUint16(4, 20, true);
		c.setUint16(6, 20, true);
		c.setUint16(8, 0x0800, true);
		c.setUint16(10, 0, true);
		c.setUint16(12, dosTime, true);
		c.setUint16(14, dosDate, true);
		c.setUint32(16, crc, true);
		c.setUint32(20, entry.bytes.length, true);
		c.setUint32(24, entry.bytes.length, true);
		c.setUint16(28, name.length, true);
		c.setUint32(42, offset, true);
		central.set(name, 46);
		locals.push(local, entry.bytes);
		centrals.push(central);
		offset += local.length + entry.bytes.length;
	}
	const directorySize = centrals.reduce((sum, part) => sum + part.length, 0);
	const end = new Uint8Array(22);
	const e = new DataView(end.buffer);
	e.setUint32(0, 0x06054B50, true);
	e.setUint16(8, entries.length, true);
	e.setUint16(10, entries.length, true);
	e.setUint32(12, directorySize, true);
	e.setUint32(16, offset, true);
	const out = new Uint8Array(offset + directorySize + end.length);
	let at = 0;
	for (const part of [...locals, ...centrals, end])
	{
		out.set(part, at);
		at += part.length;
	}
	return out;
}

//==================================================================
//  Read (with limits)
//==================================================================
// The wanted entries by name: their bytes, or null when one is larger than wanted allows
export async function readZip(bytes: Uint8Array, limits: ZipLimits): Promise<Map<string, Uint8Array | null>>
{
	const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
	let end = -1;
	for (let i = bytes.length - 22; i >= Math.max(0, bytes.length - 22 - 0xFFFF); i--)
	{
		if (view.getUint32(i, true) === 0x06054B50)
		{
			end = i;
			break;
		}
	}
	if (end < 0)
	{
		throw new ZipError("not a zip file, or it is damaged");
	}
	const count = view.getUint16(end + 10, true);
	if (count > limits.maxEntries)
	{
		throw new ZipError(`more than ${limits.maxEntries} entries`);
	}
	const decoder = new TextDecoder();
	const found = new Map<string, Uint8Array | null>();
	let at = view.getUint32(end + 16, true);
	let total = 0;
	for (let n = 0; n < count; n++)
	{
		if (at + 46 > bytes.length || view.getUint32(at, true) !== 0x02014B50)
		{
			throw new ZipError("damaged");
		}
		const flags = view.getUint16(at + 8, true);
		const method = view.getUint16(at + 10, true);
		const crc = view.getUint32(at + 16, true);
		const packed = view.getUint32(at + 20, true);
		const size = view.getUint32(at + 24, true);
		const nameLength = view.getUint16(at + 28, true);
		const skip = nameLength + view.getUint16(at + 30, true) + view.getUint16(at + 32, true);
		const local = view.getUint32(at + 42, true);
		const name = decoder.decode(bytes.subarray(at + 46, at + 46 + nameLength));
		at += 46 + skip;
		const cap = limits.wanted(name);
		if (cap === null || found.has(name) || (flags & 1) !== 0 || (method !== 0 && method !== 8))
		{
			continue;
		}
		if (size > cap)
		{
			found.set(name, null);
			continue;
		}
		if (local + 30 > bytes.length || view.getUint32(local, true) !== 0x04034B50)
		{
			throw new ZipError("damaged");
		}
		const start = local + 30 + view.getUint16(local + 26, true) + view.getUint16(local + 28, true);
		if (start + packed > bytes.length)
		{
			throw new ZipError("damaged");
		}
		const data = bytes.subarray(start, start + packed);
		const budget = limits.maxTotal - total;
		const out = method === 0 ? (data.length > Math.min(cap, budget) ? null : data.slice()) : await inflate(data, Math.min(cap, budget));
		if (out === null)
		{
			if (budget < cap)
			{
				throw new ZipError("it unpacks to too much");
			}
			found.set(name, null);
			continue;
		}
		if (crc32(out) !== crc)
		{
			throw new ZipError("damaged");
		}
		total += out.length;
		found.set(name, out);
	}
	return found;
}

// Unpacks a deflated entry, stopping once it grows past cap (null)
async function inflate(data: Uint8Array, cap: number): Promise<Uint8Array | null>
{
	const reader = new Blob([data as BlobPart]).stream().pipeThrough(new DecompressionStream("deflate-raw")).getReader();
	const parts: Uint8Array[] = [];
	let length = 0;
	try
	{
		for (;;)
		{
			const { done, value } = await reader.read();
			if (done)
			{
				break;
			}
			length += value.length;
			if (length > cap)
			{
				await reader.cancel();
				return null;
			}
			parts.push(value);
		}
	}
	catch
	{
		throw new ZipError("damaged");
	}
	const out = new Uint8Array(length);
	let at = 0;
	for (const part of parts)
	{
		out.set(part, at);
		at += part.length;
	}
	return out;
}
