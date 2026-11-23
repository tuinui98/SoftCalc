// table.test.ts - A matrix result as a History table (matrix round 2 D), as MatrixTableTests.cs

import { describe, expect, it } from "vitest";
import { parseTable, shortCell, shownCells, tableTsv } from "../src/lib/table";

describe("matrix table", () =>
{
	it("reads a matrix result into rows of cells", () =>
	{
		const table = parseTable("[[1, -2.5], [(3-4i), 4]]")!;
		expect(table.size).toBe("2x2");
		expect(table.cells[1]).toEqual(["(3-4i)", "4"]);
		expect(tableTsv(table)).toBe("1\t-2.5\r\n(3-4i)\t4");
	});

	it("leaves anything but a matrix alone", () =>
	{
		for (const text of ["[1, 2, 3]", "5", "ERROR:Sizes do not fit", "[[1, 2], [3]]"])
			expect(parseTable(text)).toBeNull();
	});

	it("shortens cells to 6 digits, as the desktop does", () =>
	{
		expect(shortCell("2.1380899352993950774764278470380")).toBe("2.13809");
		expect(shortCell("-1.3292049616611459904487618767859e68")).toBe("-1.3292e68");
		expect(shortCell("6.9852941176470588235294117647059e-4")).toBe("0.000698529");
		expect(shortCell("(-1.92-15.221077491426157245651276015896i)")).toBe("(-1.92-15.2211i)");
		expect(shortCell("0.33333333333333333333333333333333i")).toBe("0.333333i");
		expect(shortCell("100000000000000000001")).toBe("1e20");
		expect(shortCell("123456")).toBe("123456");
		expect(shortCell("0x1F")).toBe("0x1F");
	});

	it("shows at most 10 x 10", () =>
	{
		const row = Array.from({ length: 12 }, () => "1").join(", ");
		const table = parseTable("[" + Array.from({ length: 12 }, () => `[${row}]`).join(", ") + "]")!;
		const shown = shownCells(table, true);
		expect([table.size, shown.length, shown[0].length]).toEqual(["12x12", 10, 10]);
	});
});
