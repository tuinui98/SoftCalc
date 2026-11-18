// rates.test.ts - ECB rates and coin prices for the web app (desktop RatesTests / PriceSourceTests)

import { describe, expect, it } from "vitest";
import { Calculator, type EngineLike } from "../src/lib/calc";
import type { Answer } from "../src/lib/engine";
import { parsePrices, PriceSource, PRICES_URL } from "../src/lib/prices";
import { easterSunday, ecbUrl, isWorkingDay, latestPublication, nextCheckMs, parseEcbCsv, publicationMs,
	RatesSource } from "../src/lib/rates";
import { MemoryStore } from "../src/lib/store";

const CSV = [
	"KEY,FREQ,CURRENCY,CURRENCY_DENOM,EXR_TYPE,EXR_SUFFIX,TIME_PERIOD,OBS_VALUE,OBS_STATUS,TITLE",
	"EXR.D.CYP.EUR.SP00.A,D,CYP,EUR,SP00,A,2007-12-31,0.585274,A,\"Cyprus pound/Euro, ECB\"",
	"EXR.D.USD.EUR.SP00.A,D,USD,EUR,SP00,A,2026-11-17,1.0989,A,\"US dollar/Euro, ECB reference exchange rate\"",
	"EXR.D.THB.EUR.SP00.A,D,THB,EUR,SP00,A,2026-11-17,37.95,A,\"Thai baht/Euro, ECB reference exchange rate\"",
].join("\r\n");

const utc = (text: string) => Date.parse(text);

describe("ECB rates", () =>
{
	it("reads the Data API CSV, newest day only (old currencies left out)", () =>
	{
		expect(parseEcbCsv(CSV)).toEqual({ date: "2026-11-17", perEur: { THB: "37.95", USD: "1.0989" } });
		expect(parseEcbCsv("<html>busy</html>")).toBeNull();
		expect(parseEcbCsv("")).toBeNull();
	});

	it("knows the ECB's closed days", () =>
	{
		expect(easterSunday(2026)).toBe("2026-04-05");
		expect(isWorkingDay("2026-04-03")).toBe(false);    // Good Friday
		expect(isWorkingDay("2026-04-06")).toBe(false);    // Easter Monday
		expect(isWorkingDay("2026-11-21")).toBe(false);    // Saturday
		expect(isWorkingDay("2026-12-25")).toBe(false);
		expect(isWorkingDay("2026-11-17")).toBe(true);
	});

	it("publishes at 16:00 Frankfurt time, winter and summer", () =>
	{
		expect(publicationMs("2026-11-17")).toBe(utc("2026-11-17T15:00:00Z"));
		expect(publicationMs("2026-07-15")).toBe(utc("2026-07-15T14:00:00Z"));
	});

	it("the newest publication: today after 16:00, else the working day before", () =>
	{
		expect(latestPublication(utc("2026-11-17T15:30:00Z"))).toBe("2026-11-17");
		expect(latestPublication(utc("2026-11-17T14:30:00Z"))).toBe("2026-11-16");
		expect(latestPublication(utc("2026-11-21T12:00:00Z"))).toBe("2026-11-20");    // Saturday: Friday's
	});

	it("latest rates are due at the next publication; older ones after the retry", () =>
	{
		const friday = utc("2026-11-20T18:00:00Z");
		expect(nextCheckMs("2026-11-20", friday, 3600000)).toBe(utc("2026-11-23T15:00:00Z"));    // Monday 16:00 CET
		expect(nextCheckMs("2026-11-19", friday, 3600000)).toBe(friday + 3600000);
	});

	it("a fetch keeps the rates; a failed one gives the kept rates, stale", async () =>
	{
		const store = new MemoryStore();
		const now = utc("2026-11-17T16:00:00Z");
		const asked: string[] = [];
		const good = new RatesSource(store, async url => { asked.push(url); return CSV; }, () => now);
		const fetched = await good.fetch();
		expect(asked).toEqual(["https://data-api.ecb.europa.eu/service/data/EXR/D..EUR.SP00.A?startPeriod=2026-11-07&format=csvdata&detail=dataonly"]);
		expect(ecbUrl(now)).toBe(asked[0]);
		expect(fetched).toMatchObject({ date: "2026-11-17", stale: false, rates: { USD: "1.0989" } });
		expect(fetched.next_check).toBe(utc("2026-11-18T15:00:00Z") / 1000);

		const offline = new RatesSource(store, async () => { throw new Error("offline"); }, () => now);
		expect((await offline.loadKept()).rates.USD).toBe("1.0989");
		const failed = await offline.fetch();
		expect(failed).toMatchObject({ date: "2026-11-17", stale: true });
		expect(failed.next_check).toBe((now + 5 * 60000) / 1000);    // older rates kept: again in 5 minutes
		expect(offline.lastError).toBe("offline");
	});

	it("no rates at all and the fetch fails: the next currency line tries again", async () =>
	{
		const now = utc("2026-11-17T16:00:00Z");
		const source = new RatesSource(new MemoryStore(), async () => { throw new DOMException("late", "TimeoutError"); }, () => now);
		const failed = await source.fetch();
		expect(failed).toEqual({ date: null, rates: {}, stale: true, next_check: now / 1000 + 10 });
		expect(source.lastError).toBe("no answer in 30 s");
	});

	it("nothing kept yet: no rates, due at once", async () =>
	{
		const now = utc("2026-11-17T16:00:00Z");
		const source = new RatesSource(new MemoryStore(), async () => CSV, () => now);
		expect(await source.loadKept()).toEqual({ date: null, rates: {}, stale: false, next_check: now / 1000 });
	});
});

describe("coin prices", () =>
{
	const JSON_ANSWER = JSON.stringify({ bitcoin: { thb: 3000000, usd: 90000, last_updated_at: 1 }, tether: { thb: 33 } });

	it("reads CoinGecko's answer into the engine's coin codes", () =>
	{
		expect(parsePrices(JSON_ANSWER, 5)).toEqual({ fetched: 5, perCoin: { btc: { thb: "3000000", usd: "90000" }, usdt: { thb: "33" } } });
		expect(parsePrices("{}", 5)).toBeNull();
		expect(parsePrices("not json", 5)).toBeNull();
	});

	it("asks once in 5 minutes; a failed fetch gives the kept prices, stale", async () =>
	{
		let now = utc("2026-11-18T01:00:00Z");
		let calls = 0;
		const store = new MemoryStore();
		const source = new PriceSource(store, async url => { calls++; expect(url).toBe(PRICES_URL); return JSON_ANSWER; }, () => now);
		expect((await source.fetch()).stale).toBe(false);
		now += 60000;
		await source.fetch();
		expect(calls).toBe(1);

		now += 5 * 60000;
		const offline = new PriceSource(store, async () => { throw new Error("offline"); }, () => now);
		await offline.loadKept();
		const failed = await offline.fetch();
		expect(failed.stale).toBe(true);
		expect(failed.prices.btc.thb).toBe("3000000");
		expect(failed.next_check).toBe((now + 60000) / 1000);
	});
});

describe("a currency line fetches, then calculates (desktop M8)", () =>
{
	// Converts only with rates or prices in hand, like the engine
	class RateEngine implements EngineLike
	{
		readonly ready = Promise.resolve("test");
		rates: Record<string, string> = {};
		prices: Record<string, unknown> = {};
		sent: string[] = [];

		async send(op: string, args: Record<string, unknown> = {}): Promise<Answer>
		{
			this.sent.push(op);
			const ok = (more: Record<string, unknown> = {}) => ({ id: 1, ok: true, ...more }) as Answer;
			if (op === "set_rates") this.rates = args.rates as Record<string, string>;
			if (op === "set_prices") this.prices = args.prices as Record<string, unknown>;
			if (op === "catalog") return ok({ functions: {}, conversions: {} });
			if (op === "settings") return ok({ precision: 32, angle: "deg", base: "dec" });
			if (op === "eval")
			{
				const input = String(args.input);
				if (input.includes("BTC"))
				{
					return Object.keys(this.prices).length === 0 ? ok({ kind: "prices_needed" })
						: ok({ kind: "result", input, display_input: input, text: "3000000", note: "CoinGecko 2026-11-18 08:00" });
				}
				return Object.keys(this.rates).length === 0 ? ok({ kind: "rates_needed" })
					: ok({ kind: "result", input, display_input: input, text: "2.6350", note: "ECB 2026-11-17" });
			}
			return ok();
		}
	}

	it("rates_needed: the rates are fetched and the line runs again, with the date as its note", async () =>
	{
		const engine = new RateEngine();
		const store = new MemoryStore();
		const rates = new RatesSource(store, async () => CSV);
		const calc = new Calculator(engine, store, { rates });
		await calc.started;
		calc.typed("100 [THB->USD]", 14);
		await calc.press("=");
		expect(calc.entries.at(-1)).toMatchObject({ kind: "result", text: "2.6350", note: "ECB 2026-11-17" });
		expect(engine.sent.filter(op => op === "eval")).toHaveLength(2);
	});

	it("a failed fetch says why in the status line", async () =>
	{
		const engine = new RateEngine();
		const store = new MemoryStore();
		const rates = new RatesSource(store, async () => { throw new TypeError("Failed to fetch"); });
		const calc = new Calculator(engine, store, { rates });
		await calc.started;
		calc.typed("100 [THB->USD]", 14);
		await calc.press("=");
		expect(calc.status).toBe("ECB rates not fetched: Failed to fetch");
	});

	it("prices_needed: the prices are fetched", async () =>
	{
		const engine = new RateEngine();
		const store = new MemoryStore();
		const prices = new PriceSource(store, async () => JSON.stringify({ bitcoin: { thb: 3000000 } }));
		const calc = new Calculator(engine, store, { prices });
		await calc.started;
		calc.typed("1 [BTC->THB]", 12);
		await calc.press("=");
		expect(calc.entries.at(-1)).toMatchObject({ kind: "result", text: "3000000" });
	});
});
