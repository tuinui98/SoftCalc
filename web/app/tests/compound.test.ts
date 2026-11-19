// compound.test.ts - ⋮ > Compound's inputs, errors, graph scale and text forms (desktop CompoundTable tests)

import { describe, expect, it } from "vitest";
import { chartScale, firstInputs, inputsFrom, routeError, rowAt, short, summary, toCsv, toTabs, type Schedule } from "../src/lib/compound";

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
			"10000 at 5% for 10 years, monthly, +100/month at the start: future value 1102.5 (contributions 1000, interest 102.5)");
		expect(summary(firstInputs(), SCHEDULE)).toBe("10000 at 5% for 10 years, monthly: future value 1102.5 (contributions 1000, interest 102.5)");
	});
});
