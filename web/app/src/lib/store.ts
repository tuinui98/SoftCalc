// store.ts - What the app keeps between visits: settings, definitions and History (IndexedDB)
//
// The desktop keeps settings.json, functions.pcf and history.jsonl in its
// data folder (MainForm.Persistence.cs); the web app keeps the same three
// things in one IndexedDB database on the device. Nothing leaves the
// device. MemoryStore does the same in memory, for tests and for browsers
// where IndexedDB is not available (private windows may refuse it).

import type { Entry } from "./calc";

export interface Saved
{
	settings: Record<string, unknown>;
	definitions: string;      // the engine's pcf_export text
	entries: Entry[];
}

export interface Store
{
	load(): Promise<Saved>;
	// Anything else kept by name: the ECB rates ("rates") and coin prices ("prices")
	get(key: string): Promise<unknown>;
	put(key: string, value: unknown): Promise<void>;
	saveSettings(settings: Record<string, unknown>): Promise<void>;
	saveDefinitions(text: string): Promise<void>;
	saveEntries(entries: Entry[]): Promise<void>;
}

export const HISTORY_LIMIT = 1000;    // older lines are dropped when saving

export class MemoryStore implements Store
{
	data: Saved = { settings: {}, definitions: "", entries: [] };
	other: Record<string, unknown> = {};

	async get(key: string): Promise<unknown>
	{
		return structuredClone(this.other[key]);
	}

	async put(key: string, value: unknown)
	{
		this.other[key] = structuredClone(value);
	}

	async load(): Promise<Saved>
	{
		return structuredClone(this.data);
	}

	async saveSettings(settings: Record<string, unknown>)
	{
		this.data.settings = structuredClone(settings);
	}

	async saveDefinitions(text: string)
	{
		this.data.definitions = text;
	}

	async saveEntries(entries: Entry[])
	{
		this.data.entries = structuredClone(entries.slice(-HISTORY_LIMIT));
	}
}

const DB_NAME = "softcalc";
const TABLE = "kv";

export class IndexedStore implements Store
{
	private db: Promise<IDBDatabase>;

	constructor()
	{
		this.db = new Promise((resolve, reject) =>
		{
			const request = indexedDB.open(DB_NAME, 1);
			request.onupgradeneeded = () => request.result.createObjectStore(TABLE);
			request.onsuccess = () => resolve(request.result);
			request.onerror = () => reject(request.error);
		});
	}

	private async run<T>(mode: IDBTransactionMode, work: (table: IDBObjectStore) => IDBRequest<T>): Promise<T>
	{
		const db = await this.db;
		return new Promise((resolve, reject) =>
		{
			const request = work(db.transaction(TABLE, mode).objectStore(TABLE));
			request.onsuccess = () => resolve(request.result);
			request.onerror = () => reject(request.error);
		});
	}

	private read<T>(key: string, fallback: T): Promise<T>
	{
		return this.run("readonly", table => table.get(key)).then(value => (value ?? fallback) as T);
	}

	get(key: string): Promise<unknown>
	{
		return this.read<unknown>(key, undefined);
	}

	put(key: string, value: unknown): Promise<void>
	{
		return this.run("readwrite", table => table.put(value, key)).then(() => undefined);
	}

	async load(): Promise<Saved>
	{
		return {
			settings: await this.read("settings", {}),
			definitions: await this.read("definitions", ""),
			entries: await this.read("entries", []),
		};
	}

	saveSettings(settings: Record<string, unknown>)
	{
		return this.put("settings", settings);
	}

	saveDefinitions(text: string)
	{
		return this.put("definitions", text);
	}

	saveEntries(entries: Entry[])
	{
		return this.put("entries", entries.slice(-HISTORY_LIMIT));
	}
}

// IndexedDB when the browser allows it, else memory (nothing kept)
export async function openStore(): Promise<{ store: Store; kept: boolean }>
{
	try
	{
		const store = new IndexedStore();
		await store.load();
		return { store, kept: true };
	}
	catch
	{
		return { store: new MemoryStore(), kept: false };
	}
}
