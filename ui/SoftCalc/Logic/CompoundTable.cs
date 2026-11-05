// CompoundTable.cs - Text forms of the Compound Interest table and input error routing (E11)
//
// CSV for Export (RFC 4180 quoting, CRLF), tab-separated text for the
// clipboard, and which input field an engine error message belongs to
// (the engine starts those messages with the field name).

using System.Text;
using SoftCalc.Engine;

namespace SoftCalc.Logic;

internal static class CompoundTable
{
	public static readonly string[] Header = { "Period", "Start balance", "Interest", "Contribution", "End balance" };
	public static readonly string[] Fields = { "principal", "annual_rate", "years", "contribution" };

	public static IEnumerable<string> Cells(ScheduleRow row)
	{
		yield return row.Period.ToString(System.Globalization.CultureInfo.InvariantCulture);
		yield return row.Start;
		yield return row.Interest;
		yield return row.Contribution;
		yield return row.End;
	}

	public static string ToCsv(IEnumerable<ScheduleRow> rows)
	{
		var text = new StringBuilder();
		text.Append(string.Join(",", Header.Select(Quote))).Append("\r\n");
		foreach (var row in rows)
		{
			text.Append(string.Join(",", Cells(row).Select(Quote))).Append("\r\n");
		}
		return text.ToString();
	}

	public static string ToTabs(IEnumerable<ScheduleRow> rows, bool header)
	{
		var text = new StringBuilder();
		if (header)
		{
			text.Append(string.Join("\t", Header)).Append("\r\n");
		}
		foreach (var row in rows)
		{
			text.Append(string.Join("\t", Cells(row))).Append("\r\n");
		}
		return text.ToString();
	}

	internal static string Quote(string cell)
	{
		if (cell.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
		{
			return cell;
		}
		return "\"" + cell.Replace("\"", "\"\"") + "\"";
	}

	// "years: must be positive" -> ("years", "must be positive"); unknown field -> (null, whole message)
	public static (string? Field, string Message) RouteError(string error)
	{
		int colon = error.IndexOf(':');
		if (colon > 0)
		{
			string field = error[..colon];
			if (Fields.Contains(field))
			{
				return (field, error[(colon + 1)..].Trim());
			}
		}
		return (null, error);
	}
}
