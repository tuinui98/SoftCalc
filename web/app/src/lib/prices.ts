// prices.ts - Coin prices for lines such as `1 [BTC->THB]`, as the desktop PriceSource
//
// Owner 2026-11-15 (desktop) and 2026-11-18 (web): BTC, ETH, TRX and USDT
// from CoinGecko, fetched only when the engine answers `prices_needed`;
// one request asks for every ECB currency. Prices are good for 5
// minutes and kept on the device; when a fetch fails the kept prices are
// used and shown `, offline`. The request carries only the coins and the
// currencies. CoinGecko allows web pages to read it (CORS `*`).

import { fetchText, type Fetcher, type Kept } from "./rates";

const API = "https://api.coingecko.com/api/v3/simple/price";
const FRESH_MS = 5 * 60 * 1000;
const RETRY_MS = 60 * 1000;

// CoinGecko id -> the code SoftCalc uses
export const COINS: Record<string, string> = { bitcoin: "btc", ethereum: "eth", tron: "trx", tether: "usdt" };

// The ECB's currencies (engine currency.CODES)
const CURRENCIES = ["aud", "brl", "cad", "chf", "cny", "czk", "dkk", "eur", "gbp", "hkd", "huf", "idr", "ils", "inr", "isk",
	"jpy", "krw", "mxn", "myr", "nok", "nzd", "php", "pln", "ron", "sek", "sgd", "thb", "try", "usd", "zar"];

export const PRICES_URL = `${API}?ids=${Object.keys(COINS).join(",")}&vs_currencies=${CURRENCIES.join(",")}&include_last_updated_at=true`;

export interface Prices
{
	fetched: number;                                  // Unix ms
	perCoin: Record<string, Record<string, string>>;  // btc -> thb -> "3000000"
}

// What `set_prices` takes (server.py op_set_prices)
export interface PricesPayload
{
	time: string | null;    // local "yyyy-mm-dd HH:MM", shown in the note
	prices: Record<string, Record<string, string>>;
	stale: boolean;
	next_check: number;     // Unix seconds
}

// CoinGecko's answer: {"bitcoin": {"thb": 2200561, ..., "last_updated_at": ...}, ...}
export function parsePrices(json: string, fetched: number): Prices | null
{
	let root: Record<string, Record<string, unknown>>;
	try
	{
		root = JSON.parse(json);
	}
	catch
	{
		return null;
	}
	const perCoin: Prices["perCoin"] = {};
	for (const [id, code] of Object.entries(COINS))
	{
		const map: Record<string, string> = {};
		for (const [currency, value] of Object.entries(root?.[id] ?? {}))
		{
			if (currency.length === 3 && typeof value === "number" && value > 0)
			{
				map[currency] = String(value);
			}
		}
		if (Object.keys(map).length > 0)
		{
			perCoin[code] = map;
		}
	}
	return Object.keys(perCoin).length > 0 ? { fetched, perCoin } : null;
}

export class PriceSource
{
	private held: Prices | null = null;

	constructor(private readonly kept: Kept, private readonly download: Fetcher = fetchText,
		private readonly now: () => number = Date.now)
	{
	}

	// Start: the kept prices, due at once (a line asks for fresh ones)
	async loadKept(): Promise<PricesPayload>
	{
		const saved = await this.kept.get("prices") as Prices | undefined;
		this.held = saved?.perCoin ? saved : null;
		return payload(this.held, false, this.now());
	}

	// On `prices_needed`: fetch, unless the held ones are still fresh; else the held ones, stale
	async fetch(): Promise<PricesPayload>
	{
		const now = this.now();
		if (this.held && now - this.held.fetched < FRESH_MS)
		{
			return payload(this.held, false, this.held.fetched + FRESH_MS);
		}
		let fetched: Prices | null = null;
		try
		{
			fetched = parsePrices(await this.download(PRICES_URL), now);
		}
		catch
		{
			// offline, refused (CoinGecko limits free requests) or too slow
		}
		if (!fetched)
		{
			return payload(this.held, true, now + RETRY_MS);
		}
		this.held = fetched;
		await this.kept.put("prices", fetched).catch(() => undefined);
		return payload(fetched, false, now + FRESH_MS);
	}
}

function payload(prices: Prices | null, stale: boolean, nextMs: number): PricesPayload
{
	return {
		time: prices ? localTime(prices.fetched) : null,
		prices: prices?.perCoin ?? {},
		stale,
		next_check: Math.floor(nextMs / 1000),
	};
}

function localTime(ms: number): string
{
	const at = new Date(ms);
	const two = (n: number) => String(n).padStart(2, "0");
	return `${at.getFullYear()}-${two(at.getMonth() + 1)}-${two(at.getDate())} ${two(at.getHours())}:${two(at.getMinutes())}`;
}
