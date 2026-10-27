// InputRules.cs - Input box behaviors kept from the original (SPEC 5.3, 6.3, 7)
//
// Pure functions so they can be tested without a window: answer
// auto-insert, and the text the memory keys evaluate.

namespace SoftCalc.Logic;

internal static class InputRules
{
	private const string Operators = "+-*/^";

	// Typing an operator into an empty Input right after a result inserts `answer` first.
	// Returns the new Input text, or null when the keystroke should be handled normally.
	public static string? AutoAnswer(string currentText, bool lastWasResult, bool rpn, char typed)
	{
		if (rpn || !lastWasResult || currentText.Length != 0 || !Operators.Contains(typed))
		{
			return null;
		}
		return "answer" + typed;
	}

	// The definition M+ (subtract: false) or M- (subtract: true) evaluates.
	// An empty Input stands for `answer`; a missing memory counts as 0.
	public static string MemoryAdd(string input, bool hasMemory, bool subtract)
	{
		string trimmed = input.Trim();
		string operand = trimmed.Length == 0 ? "answer" : "(" + trimmed + ")";
		string sign = subtract ? "-" : "+";
		if (hasMemory)
		{
			return $"memory=memory{sign}{operand}";
		}
		return subtract ? $"memory=-{operand}" : $"memory={operand}";
	}

	public const string MemoryClear = "memory=0";
	public const string MemoryRecall = "memory";
}
