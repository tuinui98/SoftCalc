// KeypadRules.cs - What the Numeric keypad's C key does to the Input text (owner 2026-11-16)
//
// The keypad edits the Input line as text, so History stays a list of
// expressions that give the same result wherever they are recalled.
// The % key only appends %, the engine's x/100 (owner 2026-11-16: a
// rewritten A*(1+B%) was tried and dropped, to avoid confusion later).

namespace SoftCalc.Logic;

internal static class KeypadRules
{
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
}
