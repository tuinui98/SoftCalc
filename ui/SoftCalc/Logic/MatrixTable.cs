// MatrixTable.cs - A matrix result shown as a table in History (design/matrix_plan.md round 2 D)
//
// Owner 2026-11-24: a matrix result (`[[1, 2], [3, 4]]`) shows as a table,
// its cells shortened; at most 10 x 10 shown, the size beside it; a list
// stays one line. Built from the result's own text, so old History lines
// show the same way; clicks and copies still use the full text, and Copy
// as Table gives every cell in full, tab-separated (Excel takes it).

using System.Globalization;
using System.Text.RegularExpressions;

namespace SoftCalc.Logic;

internal sealed partial class MatrixTable
{
	public const int MaxShown = 10;
	public const int ShortDigits = 6;

	public IReadOnlyList<IReadOnlyList<string>> Cells { get; }
	public int Rows => Cells.Count;
	public int Cols => Cells[0].Count;
	public string Size => $"{Rows}x{Cols}";

	private MatrixTable(IReadOnlyList<IReadOnlyList<string>> cells)
	{
		Cells = cells;
	}

	// A table for `[[a, b], [c, d]]`; null for anything else (a number, a list, an error)
	public static MatrixTable? Parse(string text)
	{
		text = text.Trim();
		if (!text.StartsWith("[[", StringComparison.Ordinal) || !text.EndsWith("]]", StringComparison.Ordinal))
		{
			return null;
		}
		var rows = new List<IReadOnlyList<string>>();
		foreach (string row in text[2..^2].Split("], [", StringSplitOptions.None))
		{
			var cells = row.Split(", ", StringSplitOptions.None);
			if (cells.Any(c => c.Length == 0 || c.Contains('[') || c.Contains(']')))
			{
				return null;
			}
			rows.Add(cells);
		}
		return rows.Count > 0 && rows.All(r => r.Count == rows[0].Count) ? new MatrixTable(rows) : null;
	}

	// A tooltip text: the size, then up to 10 x 10 shortened cells padded to line up
	public string Preview()
	{
		int rows = Math.Min(Rows, MaxShown);
		int cols = Math.Min(Cols, MaxShown);
		var cells = Enumerable.Range(0, rows).Select(r => Enumerable.Range(0, cols).Select(c => Short(Cells[r][c])).ToArray()).ToArray();
		var widths = Enumerable.Range(0, cols).Select(c => cells.Max(row => row[c].Length)).ToArray();
		var lines = cells.Select(row => string.Join("   ", row.Select((cell, c) => cell.PadLeft(widths[c])))
			+ (Cols > cols ? "   …" : "")).ToList();
		if (Rows > rows)
		{
			lines.Add("…");
		}
		return Size + "\r\n" + string.Join("\r\n", lines);
	}

	// Every cell in full: tab between cells, a line per row
	public string Tsv()
	{
		return string.Join("\r\n", Cells.Select(row => string.Join("\t", row)));
	}

	// A cell as the table shows it: decimals past 6 significant digits are
	// rounded (complex parts each); other forms (hex, binary) stay as they are
	public static string Short(string cell)
	{
		if (cell.Length > 2 && cell[0] == '(' && cell[^1] == ')')
		{
			var complex = ComplexCell().Match(cell[1..^1]);
			if (!complex.Success)
			{
				return cell;
			}
			string real = complex.Groups["re"].Value;
			string imag = complex.Groups["im"].Value;
			return "(" + Short(real) + complex.Groups["sign"].Value + Short(imag) + "i)";
		}
		if (cell.EndsWith('i') && RealCell().IsMatch(cell[..^1]))
		{
			return Short(cell[..^1]) + "i";
		}
		if (!RealCell().IsMatch(cell) || Significant(cell) <= ShortDigits
			|| !double.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
		{
			return cell;
		}
		string text = value.ToString("G" + ShortDigits, CultureInfo.InvariantCulture);
		int e = text.IndexOf('E');
		if (e < 0)
		{
			return text;
		}
		int exponent = int.Parse(text[(e + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
		return text[..e] + "e" + exponent.ToString(CultureInfo.InvariantCulture);    // 1.3292e68, as the engine writes it
	}

	private static int Significant(string cell)
	{
		string mantissa = cell.Split('e', 'E')[0].TrimStart('-').Replace(".", "").TrimStart('0');
		return mantissa.Length;
	}

	[GeneratedRegex(@"^-?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?$")]
	private static partial Regex RealCell();

	[GeneratedRegex(@"^(?<re>-?[\d.]+(?:e[+-]?\d+)?)(?<sign>[+-])(?<im>[\d.]+(?:e[+-]?\d+)?)i$")]
	private static partial Regex ComplexCell();
}
