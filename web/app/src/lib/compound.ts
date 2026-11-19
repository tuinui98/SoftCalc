// compound.ts - ⋮ > Compound: the desktop's Compound Interest window for the web (owner 2026-11-20)
//
// The engine's `schedule` does the sums (protocol.md); here are the inputs
// kept between visits, which field an engine error belongs to, the growth
// graph's scale and the text forms: CSV as the desktop's Export CSV
// (CompoundTable.cs: RFC 4180 quoting, CRLF, UTF-8 without BOM), tab text
// for the clipboard and a one-line summary.

import { niceStep } from "./graph";

export const COMPOUNDING = ["yearly", "quarterly", "monthly", "daily", "continuous"] as const;
export const CONTRIBUTION_EVERY = ["yearly", "quarterly", "monthly", "daily"] as const;
export const FIELDS = ["principal", "annual_rate", "years", "contribution"] as const;
export type Field = typeof FIELDS[number];

export interface CompoundInputs
{
	principal: string;
	annual_rate: string;
	years: string;
	contribution: string;
	compounding: string;
	contribution_freq: string;
	at_start: boolean;
}

export interface ScheduleRow
{
	period: number;
	start: string;
	interest: string;
	contribution: string;
	end: string;
}

export interface Schedule
{
	future_value: string;
	total_contributions: string;
	total_interest: string;
	rows: ScheduleRow[];
	periods_per_year: number;
	series: { principal: number; contributions: number[]; interest: number[] };
}

// The desktop's first values (SettingsStore.cs CompoundPrefs)
export function firstInputs(): CompoundInputs
{
	return {
		principal: "10000", annual_rate: "5", years: "10", contribution: "0",
		compounding: "monthly", contribution_freq: "monthly", at_start: false,
	};
}

// Saved inputs: what is usable, the first values for the rest
export function inputsFrom(saved: unknown): CompoundInputs
{
	const inputs = firstInputs();
	if (typeof saved !== "object" || saved === null)
	{
		return inputs;
	}
	const s = saved as Record<string, unknown>;
	for (const name of FIELDS)
	{
		if (typeof s[name] === "string")
		{
			inputs[name] = s[name] as string;
		}
	}
	if ((COMPOUNDING as readonly string[]).includes(s.compounding as string))
	{
		inputs.compounding = s.compounding as string;
	}
	if ((CONTRIBUTION_EVERY as readonly string[]).includes(s.contribution_freq as string))
	{
		inputs.contribution_freq = s.contribution_freq as string;
	}
	inputs.at_start = s.at_start === true;
	return inputs;
}

// "years: must be positive" -> the field and the rest; another message stays whole (CompoundTable.RouteError)
export function routeError(error: string): { field: Field | null; message: string }
{
	const colon = error.indexOf(":");
	const field = error.slice(0, colon) as Field;
	if (colon > 0 && FIELDS.includes(field))
	{
		return { field, message: error.slice(colon + 1).trim() };
	}
	return { field: null, message: error };
}

//==================================================================
//  The growth graph's scale (CompoundChart.cs)
//==================================================================
export function chartScale(schedule: Schedule, width: number, height: number)
{
	const count = schedule.series.contributions.length;
	const perYear = Math.max(1, schedule.periods_per_year);
	const years = count / perYear;
	let top = schedule.series.principal;
	for (let i = 0; i < count; i++)
	{
		top = Math.max(top, schedule.series.contributions[i] + schedule.series.interest[i]);
	}
	const yStep = niceStep(top, Math.max(2, Math.floor(height / 40)));
	const yMax = Math.ceil(top / yStep) * yStep;
	const xStep = Math.max(1 / perYear, niceStep(years, Math.max(2, Math.floor(width / 70))));
	return { count, perYear, years, top, yStep, yMax, xStep };
}

// Axis labels: 1500 -> 1.5k, 2000000 -> 2M
export function short(value: number): string
{
	const size = Math.abs(value);
	const two = (n: number) => String(Math.round(n * 100) / 100);
	return size >= 1e9 ? two(value / 1e9) + "G" : size >= 1e6 ? two(value / 1e6) + "M" : size >= 1e3 ? two(value / 1e3) + "k" : two(value);
}

// The row a point on the graph's x axis (in years) falls in: the period ending at or after it
export function rowAt(schedule: Schedule, year: number): number
{
	const count = schedule.rows.length;
	const index = Math.ceil(year * Math.max(1, schedule.periods_per_year)) - 1;
	return Math.min(count - 1, Math.max(0, index));
}

//==================================================================
//  Text forms
//==================================================================
export const HEADER = ["Period", "Start balance", "Interest", "Contribution", "End balance"];

const cells = (row: ScheduleRow) => [String(row.period), row.start, row.interest, row.contribution, row.end];

function quote(cell: string): string
{
	return /[,"\r\n]/.test(cell) ? `"${cell.replace(/"/g, "\"\"")}"` : cell;
}

export function toCsv(rows: ScheduleRow[]): string
{
	return [HEADER, ...rows.map(cells)].map(line => line.map(quote).join(",") + "\r\n").join("");
}

export function toTabs(rows: ScheduleRow[]): string
{
	return [HEADER, ...rows.map(cells)].map(line => line.join("\t") + "\r\n").join("");
}

const EVERY: Record<string, string> = { yearly: "year", quarterly: "quarter", monthly: "month", daily: "day" };

// One line to paste in a chat: 10000 at 5% for 10 years, monthly, +100/month: FV 20,535.65
export function summary(inputs: CompoundInputs, schedule: Schedule): string
{
	const parts = [`${inputs.principal} at ${inputs.annual_rate}% for ${inputs.years} years, ${inputs.compounding}`];
	if (inputs.contribution.trim() !== "" && Number(inputs.contribution) !== 0)
	{
		parts.push(`+${inputs.contribution}/${EVERY[inputs.contribution_freq] ?? inputs.contribution_freq}${inputs.at_start ? " at the start" : ""}`);
	}
	return `${parts.join(", ")}: future value ${schedule.future_value} (paid in ${schedule.total_contributions}, interest ${schedule.total_interest})`;
}
