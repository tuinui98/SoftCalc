// compound.test.ts - ⋮ > Compound's inputs, errors, graph scale and text forms (desktop CompoundTable tests)

import { describe, expect, it } from "vitest";
import { chartScale, firstInputs, inputsFrom, placesSample, request, resultText, routeError, rowAt, setGoalField, short, summary, toCsv, toTabs, xOf, type Schedule } from "../src/lib/compound";

const SCHEDULE: Schedule = {
	future_value: "1102.5", total_contributions: "1000", total_interest: "102.5",
	rows: [
		{ period: 1, start: "1000", interest: "50", contribution: "0", end: "1050" },
		{ period: 2, start: "1050", interest: "52.5", contribution: "0", end: "1,102.5" },
	],
	periods_per_year: 1,
	series: { principal: 1000, contributions: [1000, 1000], interest: [50, 102.5] },
};

describe("compound", () =>
{
	it("keeps usable saved inputs and the desktop's first values for the rest", () =>
	{
		const back = inputsFrom({ principal: "5000", compounding: "hourly", contribution_freq: "daily", at_start: true, years: 3 });
		expect(back).toEqual({ ...firstInputs(), principal: "5000", contribution_freq: "daily", at_start: true });
		expect(inputsFrom(null)).toEqual(firstInputs());
	});

	it("an engine error goes to its field; another stays whole", () =>
	{
		expect(routeError("years: must be positive")).toEqual({ field: "years", message: "must be positive" });
		expect(routeError("too many periods")).toEqual({ field: null, message: "too many periods" });
		expect(routeError("x: y")).toEqual({ field: null, message: "x: y" });
	});

	it("the CSV is the desktop's: quoted where needed, CRLF; tabs for the clipboard", () =>
	{
		expect(toCsv(SCHEDULE.rows)).toBe(
			"Period,Start balance,Interest,Contribution,End balance\r\n1,1000,50,0,1050\r\n2,1050,52.5,0,\"1,102.5\"\r\n");
		expect(toTabs(SCHEDULE.rows).split("\r\n")[2]).toBe("2\t1050\t52.5\t0\t1,102.5");
	});

	it("the graph's scale and the period under a finger", () =>
	{
		const scale = chartScale(SCHEDULE, 300, 200);
		expect(scale.years).toBe(2);
		expect(scale.yMax).toBeGreaterThanOrEqual(1102.5);
		expect(rowAt(SCHEDULE, 0.2)).toBe(0);
		expect(rowAt(SCHEDULE, 1.5)).toBe(1);
		expect(rowAt(SCHEDULE, 9)).toBe(1);
		expect(short(1500)).toBe("1.5k");
		expect(short(2000000)).toBe("2M");
	});

	it("the summary names the inputs and the totals", () =>
	{
		const inputs = { ...firstInputs(), contribution: "100", at_start: true };
		expect(summary(inputs, SCHEDULE)).toBe(
			"10000 at 5% a year for 10 years, monthly, +100/month at the start: future value 1102.5 (contributions 1000, interest 102.5)");
		expect(summary(firstInputs(), SCHEDULE)).toBe("10000 at 5% a year for 10 years, monthly: future value 1102.5 (contributions 1000, interest 102.5)");
	});

	it("goal mode (owner 2026-11-28): the field set longest ago is worked out; what goes to the engine", () =>
	{
		const inputs = firstInputs();
		expect(inputs.goal_order[0]).toBe("target");
		setGoalField(inputs, "target");
		setGoalField(inputs, "principal");
		expect(inputs.goal_order).toEqual(["annual_rate", "years", "contribution", "target", "principal"]);
		const sent = request({ ...inputs, mode: "goal", table_decimals: null });
		expect(sent.solve).toBe("annual_rate");
		expect(sent.table_decimals).toBe("all");
		expect(sent).not.toHaveProperty("goal_order");
		expect(sent.order).toEqual(["annual_rate", "years", "contribution", "target", "principal"]);
		expect(request(inputs).table_decimals).toBe(2);
		const back = inputsFrom({ mode: "goal", goal_order: ["years", "years", "nope"], table_decimals: 99 });
		expect(back.mode).toBe("goal");
		expect(back.goal_order).toEqual(["target", "principal", "annual_rate", "contribution", "years"]);
		expect(back.table_decimals).toBe(2);
		expect(inputsFrom({ table_decimals: null }).table_decimals).toBeNull();
		expect(routeError("target: must be positive")).toEqual({ field: "target", message: "must be positive" });
	});

	it("the places as a sample (owner 2026-11-29)", () =>
	{
		expect([null, 0, 2, 5, 6].map(placesSample)).toEqual(["All", "0", "0.00", "0.00000", "6"]);
	});

	it("Copy result takes the Result box's lines in their order (owner 2026-11-29)", () =>
	{
		expect(resultText([{ label: "Principal (result)", value: "50.00" }, { label: "Target", value: "100.00" }]))
			.toBe("Principal (result): 50.00\r\nTarget: 100.00\r\n");
	});

	it("a part last row: its share in the table, its place on the x axis", () =>
	{
		const part: Schedule = { ...SCHEDULE, partial: 0.25,
			rows: [SCHEDULE.rows[0], { ...SCHEDULE.rows[1], part: "0.25" }] };
		expect(toCsv(part.rows).split("\r\n")[2].startsWith("2 (0.25),")).toBe(true);
		expect(xOf(part, 0)).toBe(1);
		expect(xOf(part, 1)).toBe(1.25);
		expect(chartScale(part, 300, 200).years).toBe(1.25);
	});

	it("rate and length units (owner 2026-11-28): kept when known, the chart in the length's unit", () =>
	{
		expect(inputsFrom({ rate_unit: "daily", length_unit: "months" })).toEqual({ ...firstInputs(), rate_unit: "daily", length_unit: "months" });
		expect(inputsFrom({ rate_unit: "hourly", length_unit: "decades" })).toEqual(firstInputs());
		// 24 monthly rows over a length in months: one row a month, x runs to 24
		const months: Schedule = { ...SCHEDULE, periods_per_year: 12, periods_per_unit: 1, length_unit: "months",
			rows: Array.from({ length: 24 }, (_, i) => ({ ...SCHEDULE.rows[0], period: i + 1 })),
			series: { principal: 1000, contributions: Array(24).fill(1000), interest: Array(24).fill(1) } };
		expect(chartScale(months, 300, 200).years).toBe(24);
		expect(rowAt(months, 6.5)).toBe(6);
		// an older engine: no periods_per_unit, years as before
		expect(chartScale({ ...months, periods_per_unit: undefined }, 300, 200).years).toBe(2);
		const inputs = { ...firstInputs(), annual_rate: "0.1", rate_unit: "daily", years: "90", length_unit: "days" };
		expect(summary(inputs, SCHEDULE).startsWith("10000 at 0.1% a day for 90 days, monthly:")).toBe(true);
	});
});