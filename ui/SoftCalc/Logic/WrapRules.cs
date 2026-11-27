// WrapRules.cs - A keypad key put around the selection in Input (owner 2026-11-27)
//
// With text selected, a bracket key wraps it ((2+3), [2+3]), a function key
// makes it the argument (sin(2+3)), a power or postfix key brackets it first
// ((2+3)^2, (2+3)!), 10^x and (-) take it as their operand (10^(2+3), -(2+3)).
// A lone number or name, or a selection already in brackets, gets no second
// pair. Without a selection the keys type as before. web/app/src/lib/wrap.ts
// is the same rule.

using System.Text.RegularExpressions;

namespace SoftCalc.Logic;

internal static partial class WrapRules
{
	// keys that follow their operand
	private static readonly string[] After = { "^2", "^3", "^(-1)", "!", "^(1/3)", "^", "^(1/" };

	// keys that lead their operand
	private static readonly string[] Before = { "10^", "-" };

	/// <summary>The line and caret after the key wraps the selection, or null when it does not.</summary>
	public static (string Text, int Caret)? Wrap(string text, int start, int length, string id)
	{
		if (length <= 0 || start < 0 || start + length > text.Length)
		{
			return null;
		}
		string selected = text.Substring(start, length);
		string? put = Wrapped(selected, id);
		if (put == null)
		{
			return null;
		}
		return (text[..start] + put + text[(start + length)..], start + put.Length);
	}

	private static string? Wrapped(string selected, string id)
	{
		if (id.StartsWith("fn:", StringComparison.Ordinal))
		{
			string name = id[3..];
			if (!NameRegex().IsMatch(name) || name.Equals("mod", StringComparison.OrdinalIgnoreCase))
			{
				return null;    // mod and the operators go in as before
			}
			return name + (Enclosed(selected, '(', ')') ? selected : "(" + selected + ")");
		}
		if (!id.StartsWith("ins:", StringComparison.Ordinal))
		{
			return null;
		}
		string piece = id[4..];
		switch (piece)
		{
			case "(":
			case ")":
				return "(" + selected + ")";
			case "[":
			case "]":
				return "[" + selected + "]";
		}
		if (After.Contains(piece))
		{
			return Operand(selected) + piece;
		}
		if (Before.Contains(piece))
		{
			return piece + Operand(selected);
		}
		return null;
	}

	// The selection as one operand: brackets unless it already is one
	private static string Operand(string selected) => Simple(selected) ? selected : "(" + selected + ")";

	/// <summary>A number, a name, a call, or a whole bracketed or listed part: needs no brackets.</summary>
	public static bool Simple(string selected)
	{
		string s = selected.Trim();
		if (AtomRegex().IsMatch(s) || Enclosed(s, '(', ')') || Enclosed(s, '[', ']'))
		{
			return true;
		}
		var call = CallRegex().Match(s);
		return call.Success && Enclosed(s[call.Length..], '(', ')');
	}

	// True when the first character opens the pair that the last one closes
	private static bool Enclosed(string s, char open, char close)
	{
		if (s.Length < 2 || s[0] != open || s[^1] != close)
		{
			return false;
		}
		int depth = 0;
		for (int i = 0; i < s.Length; i++)
		{
			if (s[i] == open)
			{
				depth++;
			}
			else if (s[i] == close)
			{
				depth--;
				if (depth == 0 && i < s.Length - 1)
				{
					return false;    // (1)+(2): the first pair closes early
				}
			}
		}
		return depth == 0;
	}

	[GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
	private static partial Regex NameRegex();

	[GeneratedRegex(@"^([A-Za-z_][A-Za-z0-9_]*|\d+(\.\d*)?|\.\d+|0x[0-9A-Fa-f]+|0b[01]+|0o[0-7]+)$")]
	private static partial Regex AtomRegex();

	[GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(?=\()")]
	private static partial Regex CallRegex();
}
