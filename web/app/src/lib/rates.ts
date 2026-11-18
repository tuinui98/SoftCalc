// rates.ts - ECB reference rates for currency lines (`100 [THB->USD]`), as the desktop RatesSource
//
// Fetched only when the engine answers `rates_needed`, then kept on the
// device. The desktop reads eurofxref-daily.xml, but a web page may not
// (the ECB sends no CORS header there); the ECB's own Data API gives the
// same reference rates and allows it (owner 2026-11-18). The ECB
// publishes once per TARGET working day at about 16:00 Frankfurt time
// (weekends, 1 Jan, Good Friday, Easter Monday, 1 May, 25 and 26 Dec are
// closed); a fetch is skipped while the kept rates are the latest, and a
// failed or early fetch is tried again after an hour (EcbRates.cs).

// The last 10 days (the longest ECB gap, Easter, is 4 days); the newest day is used. Not
// lastNObservations=1: the API keeps answers per page origin for 30 s only, and for a new origin
// that query took 60 to 90 s, this one 3 to 12 s (2026-11-18, owner's phone: "unavailable").
export function ecbUrl(nowMs: number): string
{
	const from = new Date(nowMs - 10 * 86400000).toISOString().slice(0, 10);
	return `https://data-api.ecb.europa.eu/service/data/EXR/D..EUR.SP00.A?startPeriod=${from}&format=csvdata&detail=dataonly`;
}
// The desktop waits an hour after a failed fetch; here the Data API is the one way in and it is
// sometimes slow (0.6 s mostly, 15 s and over 40 s seen on 2026-11-18), so: with no rates at all,
// the next currency line tries again; with older rates kept, after 5 minutes
const RETRY_MS = 5 * 60 * 1000;
const RETRY_NONE_MS = 10 * 1000;    // just past this line, so it answers "unavailable" instead of asking again
const TIMEOUT_MS = 30000;
const ECB_ZONE = "Europe/Berlin";    // CET / CEST, as the desktop's Central European time
const PUBLISH_HOUR = 16;

export interface Rates
{
	date: string;                      // yyyy-mm-dd of the publication
	perEur: Record<string, string>;    // USD -> "1.0989": units per 1 EUR
}

// What `set_rates` takes (protocol.md)
export interface RatesPayload
{
	date: string | null;
	rates: Record<string, string>;
	stale: boolean;
	next_check: number;    // Unix seconds
}

//==================================================================
//  The Data API's CSV: one row per currency, the newest day only
//==================================================================
export function parseEcbCsv(csv: string): Rates | null
{
	const lines = csv.trim().split(/\r?\n/);
	if (lines.length < 2)
	{
		return null;
	}
	const head = splitCsv(lines[0]);
	const [currency, day, value] = ["CURRENCY", "TIME_PERIOD", "OBS_VALUE"].map(name => head.indexOf(name));
	if (currency < 0 || day < 0 || value < 0)
	{
		return null;
	}
	const rows = lines.slice(1).map(splitCsv)
		.filter(row => /^[A-Z]{3}$/.test(row[currency] ?? "") && /^\d{4}-\d{2}-\d{2}$/.test(row[day] ?? "") && Number(row[value]) > 0);
	if (rows.length === 0)
	{
		return null;
	}
	// Old currencies (CYP, EEK, ...) still answer with their last day: only the newest day counts
	const date = rows.map(row => row[day]).sort().at(-1)!;
	const perEur: Record<string, string> = {};
	for (const row of rows.filter(row => row[day] === date).sort((a, b) => a[currency].localeCompare(b[currency])))
	{
		perEur[row[currency]] = row[value];
	}
	return { date, perEur };
}

// One CSV line; quoted fields may hold commas ("US dollar/Euro, ...")
function splitCsv(line: string): string[]
{
	const fields: string[] = [];
	let field = "";
	let quoted = false;
	for (let i = 0; i < line.length; i++)
	{
		const char = line[i];
		if (quoted)
		{
			if (char === "\"" && line[i + 1] === "\"")
			{
				field += "\"";
				i++;
			}
			else if (char === "\"")
			{
				quoted = false;
			}
			else
			{
				field += char;
			}
		}
		else if (char === "\"")
		{
			quoted = true;
		}
		else if (char === ",")
		{
			fields.push(field);
			field = "";
		}
		else
		{
			field += char;
		}
	}
	fields.push(field);
	return fields;
}

//==================================================================
//  The ECB calendar (EcbCalendar in EcbRates.cs)
//==================================================================
const isoDay = (date: Date) => date.toISOString().slice(0, 10);
const addDays = (day: string, days: number) => isoDay(new Date(Date.parse(day + "T00:00:00Z") + days * 86400000));

// Anonymous Gregorian algorithm
export function easterSunday(year: number): string
{
	const a = year % 19, b = Math.floor(year / 100), c = year % 100;
	const d = Math.floor(b / 4), e = b % 4, f = Math.floor((b + 8) / 25), g = Math.floor((b - f + 1) / 3);
	const h = (19 * a + b - d - g + 15) % 30;
	const i = Math.floor(c / 4), k = c % 4;
	const l = (32 + 2 * e + 2 * i - h - k) % 7;
	const m = Math.floor((a + 11 * h + 22 * l) / 451);
	const month = Math.floor((h + l - 7 * m + 114) / 31);
	const dayOfMonth = (h + l - 7 * m + 114) % 31 + 1;
	return `${year}-${String(month).padStart(2, "0")}-${String(dayOfMonth).padStart(2, "0")}`;
}

export function isWorkingDay(day: string): boolean
{
	const weekday = new Date(day + "T00:00:00Z").getUTCDay();
	if (weekday === 0 || weekday === 6)
	{
		return false;
	}
	if (["01-01", "05-01", "12-25", "12-26"].includes(day.slice(5)))
	{
		return false;
	}
	const easter = easterSunday(Number(day.slice(0, 4)));
	return day !== addDays(easter, -2) && day !== addDays(easter, 1);
}

// Frankfurt wall-clock day and hour at `ms`
function ecbClock(ms: number): { day: string; hour: number }
{
	const parts = Object.fromEntries(new Intl.DateTimeFormat("en-CA", {
		timeZone: ECB_ZONE, year: "numeric", month: "2-digit", day: "2-digit", hour: "2-digit", hourCycle: "h23",
	}).formatToParts(new Date(ms)).map(part => [part.type, part.value]));
	return { day: `${parts.year}-${parts.month}-${parts.day}`, hour: Number(parts.hour) };
}

// When the publication for `day` is expected (16:00 Frankfurt time), in Unix ms
export function publicationMs(day: string): number
{
	const guess = Date.parse(`${day}T${PUBLISH_HOUR}:00:00Z`);
	// Frankfurt is UTC+1 or +2: step back by the offset seen at the guess
	const seen = ecbClock(guess);
	const offsetHours = (seen.hour - PUBLISH_HOUR + 24) % 24;
	return guess - offsetHours * 3600000;
}

// The newest publication that should exist at `nowMs`
export function latestPublication(nowMs: number): string
{
	const { day, hour } = ecbClock(nowMs);
	if (isWorkingDay(day) && hour >= PUBLISH_HOUR)
	{
		return day;
	}
	let back = addDays(day, -1);
	while (!isWorkingDay(back))
	{
		back = addDays(back, -1);
	}
	return back;
}

// When rates of `held` are due again: the next publication when they are the latest, else after `retryMs`
export function nextCheckMs(held: string, nowMs: number, retryMs: number): number
{
	if (held >= latestPublication(nowMs))
	{
		let next = addDays(held, 1);
		while (!isWorkingDay(next))
		{
			next = addDays(next, 1);
		}
		return publicationMs(next);
	}
	return nowMs + retryMs;
}

//==================================================================
//  The source: kept rates at start, a fetch on `rates_needed`
//==================================================================
export interface Kept
{
	get(key: string): Promise<unknown>;
	put(key: string, value: unknown): Promise<void>;
}

export type Fetcher = (url: string) => Promise<string>;

export async function fetchText(url: string): Promise<string>
{
	const answer = await fetch(url, { signal: AbortSignal.timeout(TIMEOUT_MS), cache: "no-store" });
	if (!answer.ok)
	{
		throw new Error(`HTTP ${answer.status}`);
	}
	return answer.text();
}

// Why a fetch failed, in a few words for the status line
export function failure(error: unknown): string
{
	if (error instanceof DOMException && (error.name === "TimeoutError" || error.name === "AbortError"))
	{
		return "no answer in 30 s";
	}
	return error instanceof Error ? error.message : String(error);
}

export class RatesSource
{
	private held: Rates | null = null;
	lastError = "";    // why the last fetch failed, "" when it worked

	constructor(private readonly kept: Kept, private readonly download: Fetcher = fetchText,
		private readonly now: () => number = Date.now)
	{
	}

	// Start: the kept rates, due at once when they are not the latest
	async loadKept(): Promise<RatesPayload>
	{
		const saved = await this.kept.get("rates") as Rates | undefined;
		this.held = saved?.date && saved.perEur ? saved : null;
		const now = this.now();
		return payload(this.held, false, this.held ? nextCheckMs(this.held.date, now, 0) : now);
	}

	// On `rates_needed`: fetch, or fall back to the kept rates (shown `, offline`)
	async fetch(): Promise<RatesPayload>
	{
		let fetched: Rates | null = null;
		try
		{
			fetched = parseEcbCsv(await this.download(ecbUrl(this.now())));
			this.lastError = fetched ? "" : "not the ECB's rates";
		}
		catch (error)
		{
			this.lastError = failure(error);    // offline, refused or too slow: the kept rates, marked stale
		}
		const now = this.now();
		if (!fetched)
		{
			return payload(this.held, true, now + (this.held ? RETRY_MS : RETRY_NONE_MS));
		}
		this.held = fetched;
		await this.kept.put("rates", fetched).catch(() => undefined);
		return payload(fetched, false, nextCheckMs(fetched.date, now, RETRY_MS));
	}
}

function payload(rates: Rates | null, stale: boolean, nextMs: number): RatesPayload
{
	return { date: rates?.date ?? null, rates: rates?.perEur ?? {}, stale, next_check: Math.floor(nextMs / 1000) };
}
