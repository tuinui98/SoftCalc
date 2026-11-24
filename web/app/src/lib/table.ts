// table.ts - A matrix result shown as a table in History (design/matrix_plan.md round 2 D)
//
// Owner 2026-11-24: a matrix result (`[[1, 2], [3, 4]]`) shows as a table, its
// cells shortened; at most 10 x 10 shown, the size beside it; a list stays one
// line. Built from the result text, like the desktop's MatrixTable.cs, so old
// History lines show the same way; taps and copies still use the full text.

export const MAX_SHOWN = 10;
export const SHORT_DIGITS = 6;

export interface MatrixTable
{
	cells: string[][];
	rows: number;
	cols: number;
	size: string;    // "2x3"
}

// A table for `[[a, b], [c, d]]`; null for anything else (a number, a list, an error)
export function parseTable(text: string): MatrixTable | null
{
	text = text.trim();
	if (!text.startsWith("[[") || !text.endsWith("]]"))
		return null;
	const cells = text.slice(2, -2).split("], [").map(row => row.split(", "));
	if (cells.some(row => row.some(cell => cell === "" || cell.includes("[") || cell.includes("]"))))
		return null;
	if (cells.some(row => row.length !== cells[0].length))
		return null;
	return { cells, rows: cells.length, cols: cells[0].length, size: `${cells.length}x${cells[0].length}` };
}

// Every cell in full: tab between cells, a line per row (a spreadsheet takes it)
export function tableTsv(table: MatrixTable): string
{
	return table.cells.map(row => row.join("\t")).join("\r\n");
}

const REAL = /^-?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?$/;
const COMPLEX = /^(-?[\d.]+(?:e[+-]?\d+)?)([+-])([\d.]+(?:e[+-]?\d+)?)i$/;

// A cell as the table shows it: decimals past 6 significant digits are rounded
// (complex parts each); other forms (hex, binary) stay as they are
export function shortCell(cell: string): string
{
	if (cell.length > 2 && cell.startsWith("(") && cell.endsWith(")"))
	{
		const parts = COMPLEX.exec(cell.slice(1, -1));
		return parts ? `(${shortCell(parts[1])}${parts[2]}${shortCell(parts[3])}i)` : cell;
	}
	if (cell.endsWith("i") && REAL.test(cell.slice(0, -1)))
		return shortCell(cell.slice(0, -1)) + "i";
	if (!REAL.test(cell) || significant(cell) <= SHORT_DIGITS)
		return cell;
	const value = Number(cell);
	if (!Number.isFinite(value))
		return cell;
	// Like .NET "G6": fixed while the exponent is from -5 to 5, else d.ddddde±x, trailing zeros dropped
	const exponent = value === 0 ? 0 : Math.floor(Math.log10(Math.abs(Number(value.toPrecision(SHORT_DIGITS)))));
	if (exponent < -5 || exponent >= SHORT_DIGITS)
	{
		const [mantissa, power] = value.toExponential(SHORT_DIGITS - 1).split("e");
		return trimZeros(mantissa) + "e" + Number(power);
	}
	return trimZeros(value.toPrecision(SHORT_DIGITS));
}

function trimZeros(text: string): string
{
	return text.includes(".") ? text.replace(/0+$/, "").replace(/\.$/, "") : text;
}

function significant(cell: string): number
{
	return cell.split(/[eE]/)[0].replace("-", "").replace(".", "").replace(/^0+/, "").length;
}

// The part shown: at most 10 x 10 cells, shortened unless a number of decimal places is set
export function shownCells(table: MatrixTable, short: boolean): string[][]
{
	return table.cells.slice(0, MAX_SHOWN).map(row => row.slice(0, MAX_SHOWN).map(cell => short ? shortCell(cell) : cell));
}
