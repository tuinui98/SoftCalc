// InputHelp.cs - The hint line under the Input box (owner 2026-11-06)
//
// Looks at the text left of the caret: inside an open call `fv(0.05, `
// it describes that function; on a word being typed it describes the
// name (or lists the names it could become); inside an open `[` it
// shows tag examples; on `$` it explains result references. User
// functions and variables come from `lookupUser`. A number literal at
// the caret (`LiteralAt`) is shown in the output base by the engine
// (owner 2026-11-07). Text is SoftCalc's own wording, not the
// original's help.

using System.Text.RegularExpressions;

namespace SoftCalc.Logic;

internal static partial class InputHelp
{
	private static readonly Dictionary<string, string> Builtins = new(StringComparer.OrdinalIgnoreCase)
	{
		["sqrt"] = "sqrt(x) - square root",
		["abs"] = "abs(x) - absolute value (size of a complex number)",
		["exp"] = "exp(x) - e to the power x",
		["ln"] = "ln(x) - natural logarithm",
		["log10"] = "log10(x) - base-10 logarithm",
		["sin"] = "sin(x) - sine, in the angle unit (Deg / Rad / Grad)",
		["cos"] = "cos(x) - cosine, in the angle unit",
		["tan"] = "tan(x) - tangent, in the angle unit",
		["asin"] = "asin(x) - inverse sine, result in the angle unit",
		["acos"] = "acos(x) - inverse cosine, result in the angle unit",
		["atan"] = "atan(x) - inverse tangent, result in the angle unit",
		["sinh"] = "sinh(x) - hyperbolic sine",
		["cosh"] = "cosh(x) - hyperbolic cosine",
		["tanh"] = "tanh(x) - hyperbolic tangent",
		["asinh"] = "asinh(x) - inverse hyperbolic sine",
		["acosh"] = "acosh(x) - inverse hyperbolic cosine",
		["atanh"] = "atanh(x) - inverse hyperbolic tangent",
		["fv"] = "fv(rate, nper, pmt, [pv], [type]) - future value; paid out is negative, e.g. fv(0.05/12, 120, -100)",
		["pv"] = "pv(rate, nper, pmt, [fv], [type]) - present value; rate per period",
		["pmt"] = "pmt(rate, nper, pv, [fv], [type]) - payment per period; type 1 = at the start",
		["nper"] = "nper(rate, pmt, pv, [fv], [type]) - number of periods",
		["epoch"] = "epoch(date) - Unix seconds, e.g. epoch(today)",
		["fromepoch"] = "fromepoch(seconds) - date and time from Unix seconds",
		["longdate"] = "longdate(date) - e.g. Friday, 6 November 2026",
		["longtime"] = "longtime(date) - e.g. 14:05:09",
		["longdatetime"] = "longdatetime(date) - long date and time",
		["mod"] = "a mod b - remainder of a / b",
		["pi"] = "pi - 3.14159...",
		["e"] = "e - 2.71828...",
		["i"] = "i (or j) - imaginary unit, e.g. 2+3i",
		["j"] = "j (or i) - imaginary unit",
		["today"] = "today - the current date; today+30 is 30 days later",
		["now"] = "now - the current date and time; now [longdatetime]",
		["answer"] = "answer (or ans) - the last result",
		["ans"] = "ans (or answer) - the last result",
	};

	private const string TagHint = "[from->to] converts: [ft->in] [c->f] [h->min] [usd->thb] [utc->local]; [longdate] formats a date";
	private const string RefHint = "$1 = last result, $2 = the one before, ...";

	[GeneratedRegex(@"[A-Za-z_][A-Za-z0-9_]*$")]
	private static partial Regex WordAtEnd();

	// A literal ending at the caret, not part of a name, date, duration or $n
	[GeneratedRegex(@"(?<![A-Za-z0-9_.:$])(0[xX][0-9A-Fa-f]+|0[bBoOdD][0-9]+|[01]+b|(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?)$")]
	private static partial Regex LiteralAtEnd();

	// The number literal left of the caret, for the engine to convert, or null
	public static string? LiteralAt(string text, int caret)
	{
		var match = LiteralAtEnd().Match(text[..Math.Clamp(caret, 0, text.Length)]);
		return match.Success ? match.Value : null;
	}

	public static string? For(string text, int caret, Func<string, string?> lookupUser)
	{
		string left = text[..Math.Clamp(caret, 0, text.Length)];

		//==================================================================
		//  An open [ tag or a $ reference right before the caret
		//==================================================================
		int open = left.LastIndexOf('[');
		if (open >= 0 && left.IndexOf(']', open) < 0)
		{
			return TagHint;
		}
		if (left.EndsWith('$') || Regex.IsMatch(left, @"\$\d*$"))
		{
			return RefHint;
		}

		//==================================================================
		//  The word being typed: exact name, or the names it can become
		//==================================================================
		var word = WordAtEnd().Match(left);
		if (word.Success)
		{
			string? exact = Describe(word.Value, lookupUser);
			if (exact != null)
			{
				return exact;
			}
			var starts = Builtins.Keys.Where(k => k.Length > word.Length && k.StartsWith(word.Value, StringComparison.OrdinalIgnoreCase))
				.Order(StringComparer.Ordinal).ToList();
			if (word.Length >= 2 && starts.Count > 0)
			{
				return starts.Count == 1 ? Builtins[starts[0]] : string.Join("  ", starts.Take(6));
			}
		}

		//==================================================================
		//  Inside an unclosed call: the innermost function
		//==================================================================
		int depth = 0;
		for (int i = left.Length - 1; i >= 0; i--)
		{
			char c = left[i];
			if (c == ')')
			{
				depth++;
			}
			else if (c == '(')
			{
				if (depth == 0)
				{
					var name = WordAtEnd().Match(left[..i]);
					return name.Success ? Describe(name.Value, lookupUser) : null;
				}
				depth--;
			}
		}
		return null;
	}

	private static string? Describe(string name, Func<string, string?> lookupUser)
	{
		return lookupUser(name) ?? (Builtins.TryGetValue(name, out var help) ? help : null);
	}
}
