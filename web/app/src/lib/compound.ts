// compound.ts - ⋮ > Compound: the desktop's Compound Interest window for the web (owner 2026-11-20)
//
// The engine's `schedule` does the sums (protocol.md); here are the inputs
// kept between visits, which field an engine error belongs to, the growth
// graph's scale and the text forms: CSV as the desktop's Export CSV
// (CompoundTable.cs: RFC 4180 quoting, CRLF, UTF-8 without BOM), tab text
// for the clipboard and a one-line summary.

import { niceStep } from "./graph";

// owner 2026-11-28: weekly too, quarterly kept
export const COMPOUNDING = ["yearly", "quarterly", "monthly", "weekly", "daily", "continuous"] as const;
export const CONTRIBUTION_EVERY = ["yearly", "quarterly", "monthly", "weekly", "daily"] as const;
// target: goal mode only (owner 2026-11-28)
export const FIELDS = ["target", "principal", "annual_rate", "years", "contribution"] as const;
export const MOST_DECIMALS = 15;
// owner 2026-11-29: formatting every row took seconds, so the first rows come with the answer and the
// others a block at a time when the table shows them (all of them for CSV)
export const FIRST_ROWS = 200;
export const BLOCK = 200;
// owner 2026-11-28: the rate per day, week, month or year; the length in any of them (annual_rate and years keep
// their names: the protocol's fields)
export const RATE_UNITS = ["yearly", "monthly", "weekly", "daily"] as const;
export const LENGTH_UNITS = ["years", "months", "weeks", "days"] as const;
export type Field = typeof FIELDS[number];

export interface CompoundInputs
{
	principal: string;
	annual_rate: string;
	years: string;
	rate_unit: string;
	length_unit: string;
	contribution: string;
	compounding: string;
	contribution_freq: string;
	at_start: boolean;
	// goal mode (owner 2026-11-28): four of the five give the fifth, the one set longest ago (goal_order[0])
	mode: "normal" | "goal";
	target: string;
	goal_order: Field[];
	table_decimals: number | null;    // the table's own places, null for all
}

export interface ScheduleRow
{
	period: number;
	start: string;
	interest: string;
	contribution: string;
	end: string;
	part?: string;    // a last row shorter than a period (goal mode): "0.2725"
}

export interface Schedule
{
	future_value: string;
	total_contributions: string;
	total_interest: string;
	rows: ScheduleRow[];
	periods_per_year: number;
	periods_per_unit?: number;    // rows per length unit, the chart's x
	length_unit?: string;
	partial?: number;    // the last row's share of a period, 0 when whole
	row_count?: number;    // all the rows; `rows` holds the first row_limit (owner 2026-11-29)
	schedule_id?: number;    // for schedule_rows
	// the worked-out field in the page's places, every digit, all five in the places (owner 2026-11-29)
	solved?: { field: Field; value: string; length_ymd: string; exact?: string; values?: Partial<Record<Field, string>> };
	series: { principal: number; contributions: number[]; interest: number[] };
}

// The desktop's first values (SettingsStore.cs CompoundPrefs)
export function firstInputs(): CompoundInputs
{
	return {
		principal: "10000", annual_rate: "5", years: "10", rate_unit: "yearly", length_unit: "years", contribution: "0",
		compounding: "monthly", contribution_freq: "monthly", at_start: false,
		mode: "normal", target: "1000000", goal_order: [...FIELDS], table_decimals: 2,
	};
}

// A field was set: it goes last (the most important); the first is worked out
export function setGoalField(inputs: CompoundInputs, field: Field)
{
	inputs.goal_order = [...inputs.goal_order.filter(name => name !== field), field];
}

// What goes to the engine's schedule: the inputs, the field to work out, the table's places
export function request(inputs: CompoundInputs): Record<string, unknown>
{
	const { goal_order, table_decimals, ...rest } = inputs;
	return { ...rest, table_decimals: table_decimals ?? "all", solve: goal_order[0], row_limit: FIRST_ROWS };
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
	if ((RATE_UNITS as readonly string[]).includes(s.rate_unit as string))
	{
		inputs.rate_unit = s.rate_unit as string;
	}
	if ((LENGTH_UNITS as readonly string[]).includes(s.length_unit as string))
	{
		inputs.length_unit = s.length_unit as string;
	}
	inputs.at_start = s.at_start === true;
	inputs.mode = s.mode === "goal" ? "goal" : "normal";
	if (s.table_decimals === null || (Number.isInteger(s.table_decimals) && (s.table_decimals as number) >= 0
		&& (s.table_decimals as number) <= MOST_DECIMALS))
	{
		inputs.table_decimals = s.table_decimals as number | null;
	}
	// the five once each in their order; unknown or missing ones first (worked out first)
	const order = Array.isArray(s.goal_order) ? [...new Set(s.goal_order.filter(name => (FIELDS as readonly unknown[]).includes(name)))] as Field[] : [];
	inputs.goal_order = [...FIELDS.filter(name => !order.includes(name)), ...order];
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
// Rows per x unit: the length's unit when the engine says it, else years
export function perUnit(schedule: Schedule): number
{
	return schedule.periods_per_unit && schedule.periods_per_unit > 0 ? schedule.periods_per_unit : Math.max(1, schedule.periods_per_year);
}

// Where row i ends on the x axis; a part last row ends part of a period on
export function xOf(schedule: Schedule, i: number): number
{
	const part = schedule.partial && schedule.partial > 0 && i === schedule.series.contributions.length - 1;
	return (i + (part ? schedule.partial! : 1)) / perUnit(schedule);
}

export function chartScale(schedule: Schedule, width: number, height: number)
{
	const count = schedule.series.contributions.length;
	const perYear = perUnit(schedule);    // per x unit (the name stays from when it was years)
	const years = xOf(schedule, count - 1);
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

// The row a point on the graph's x axis (in the length's unit) falls in: the period ending at or after it
export function rowAt(schedule: Schedule, year: number): number
{
	const count = schedule.rows.length;
	const index = Math.ceil(year * perUnit(schedule)) - 1;
	return Math.min(count - 1, Math.max(0, index));
}

//==================================================================
//  Text forms
//==================================================================
export const HEADER = ["Period", "Start balance", "Interest", "Contribution", "End balance"];

const cells = (row: ScheduleRow) => [row.part ? `${row.period} (${row.part})` : String(row.period), row.start, row.interest, row.contribution, row.end];

function quote(cell: string): string
{
	return /[,"\r\n]/.test(cell) ? `"${cell.replace(/"/g, "\"\"")}"` : cell;
}

export function toCsv(rows: ScheduleRow[]): string
{
	return [HEADER, ...rows.map(cells)].map(line => line.map(quote).join(",") + "\r\n").join("");
}

// owner 2026-11-29: the places as a sample, 0.00 for 2; past 5, the number; All for every digit (CompoundTable.PlacesSample)
export function placesSample(places: number | null): string
{
	return places === null ? "All" : places === 0 ? "0" : places <= 5 ? "0." + "0".repeat(places) : String(places);
}

// The Result box as text (owner 2026-11-29; CompoundTable.ResultText): "Principal (result): 50000.00", CRLF
export function resultText(lines: { label: string; value: string }[]): string
{
	return lines.map(line => `${line.label}: ${line.value}\r\n`).join("");
}

export function toTabs(rows: ScheduleRow[]): string
{
	return [HEADER, ...rows.map(cells)].map(line => line.join("\t") + "\r\n").join("");
}

const EVERY: Record<string, string> = { yearly: "year", quarterly: "quarter", monthly: "month", weekly: "week", daily: "day" };

// One line to paste in a chat: 10000 at 5% a year for 10 years, monthly, +100/month: future value ...
export function summary(inputs: CompoundInputs, schedule: Schedule): string
{
	const per = ({ yearly: "a year", monthly: "a month", weekly: "a week", daily: "a day" } as Record<string, string>)[inputs.rate_unit] ?? "a year";
	if (inputs.mode === "goal")
	{
		// goal: what was worked out first, then the rest as the engine read them
		const contribution = inputs.contribution.trim() !== "" && Number(inputs.contribution) !== 0
			? `, +${inputs.contribution}/${EVERY[inputs.contribution_freq] ?? inputs.contribution_freq}${inputs.at_start ? " at the start" : ""}` : "";
		return `Goal ${inputs.target}: ${inputs.principal} at ${inputs.annual_rate}% ${per} for ${inputs.years} ${inputs.length_unit}${contribution}`
			+ ` (${schedule.solved?.field ?? inputs.goal_order[0]} worked out; contributions ${schedule.total_contributions}, interest ${schedule.total_interest})`;
	}
	const parts = [`${inputs.principal} at ${inputs.annual_rate}% ${per} for ${inputs.years} ${inputs.length_unit}, ${inputs.compounding}`];
	if (inputs.contribution.trim() !== "" && Number(inputs.contribution) !== 0)
	{
		parts.push(`+${inputs.contribution}/${EVERY[inputs.contribution_freq] ?? inputs.contribution_freq}${inputs.at_start ? " at the start" : ""}`);
	}
	return `${parts.join(", ")}: future value ${schedule.future_value} (contributions ${schedule.total_contributions}, interest ${schedule.total_interest})`;
}
