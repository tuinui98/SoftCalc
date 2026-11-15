// KeypadRules.cs - What the Numeric keypad's C, % and STO keys do to the Input text (owner 2026-11-16)
//
// The keypad edits the Input line as text, so History stays a list of
// expressions that give the same result wherever they are recalled.
// % writes the calculator meaning out in full: after + or - it is a
// share of what came before (50+10% becomes 50*(1+10%), 55); elsewhere
// it stays the engine's x/100. Typed % is never rewritten.

using System.Text.RegularExpressions;

namespace SoftCalc.Logic;

internal static partial class KeypadRules
{
	[GeneratedRegex(@"^\s*[A-Za-z_]\w*\s*$|^\s*\d+(\.\d*)?\s*$|^\s*\.\d+\s*$")]
	private static partial Regex Plain();

	// C: the last number or name (or the last other character) goes, with spaces before it
	public static string ClearEntry(string text)
	{
		string trimmed = text.TrimEnd();
		if (trimmed.Length == 0)
		{
			return "";
		}
		int start = trimmed.Length;
		while (start > 0 && (char.IsLetterOrDigit(trimmed[start - 1]) || trimmed[start - 1] is '.' or '_'))
		{
			start--;
		}
		if (start == trimmed.Length)
		{
			start--;    // an operator or a bracket
		}
		return trimmed[..start].TrimEnd();
	}

	// %: "A+B" -> "A*(1+B%)", "A-B" -> "A*(1-B%)" (A in brackets unless a plain number or name); otherwise "%" appended
	public static string Percent(string text)
	{
		string trimmed = text.TrimEnd();
		int end = trimmed.Length;
		int start = end;
		while (start > 0 && (char.IsDigit(trimmed[start - 1]) || trimmed[start - 1] == '.'))
		{
			start--;
		}
		if (start == end)
		{
			return trimmed + "%";
		}
		string b = trimmed[start..];
		int sign = start - 1;
		while (sign >= 0 && trimmed[sign] == ' ')
		{
			sign--;
		}
		if (sign <= 0 || trimmed[sign] is not ('+' or '-') || !TopLevel(trimmed, sign) || IsUnary(trimmed, sign))
		{
			return trimmed + "%";
		}
		string a = trimmed[..sign].TrimEnd();
		string left = Plain().IsMatch(a) ? a.Trim() : "(" + a.Trim() + ")";
		return $"{left}*(1{trimmed[sign]}{b}%)";
	}

	// Outside every bracket
	private static bool TopLevel(string text, int index)
	{
		int depth = 0;
		for (int i = 0; i < index; i++)
		{
			depth += text[i] == '(' ? 1 : text[i] == ')' ? -1 : 0;
		}
		return depth == 0;
	}

	// A sign, not a minus: at the start, after an operator or "(", or the exponent of 2e-3
	private static bool IsUnary(string text, int index)
	{
		int before = index - 1;
		while (before >= 0 && text[before] == ' ')
		{
			before--;
		}
		if (before < 0 || text[before] is '+' or '-' or '*' or '/' or '^' or '(' or ',' or '=')
		{
			return true;
		}
		return text[before] is 'e' or 'E' && before > 0 && char.IsDigit(text[before - 1])
			&& (before < 2 || !char.IsLetter(text[before - 2]));
	}

	// STO then x: "5*3" -> "x=5*3"; an empty line stores the last answer
	public static string Store(string text, string name)
	{
		string value = text.Trim();
		return $"{name}={(value.Length == 0 ? "ans" : value)}";
	}
}
