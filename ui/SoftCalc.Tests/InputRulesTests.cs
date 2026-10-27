// InputRulesTests.cs - Answer auto-insert and memory key rules (SPEC 5.3, 7)

using SoftCalc.Logic;

namespace SoftCalc.Tests;

public class InputRulesTests
{
	[Theory]
	[InlineData('+', "answer+")]
	[InlineData('-', "answer-")]
	[InlineData('*', "answer*")]
	[InlineData('/', "answer/")]
	[InlineData('^', "answer^")]
	public void OperatorAfterResultInsertsAnswer(char typed, string expected)
	{
		Assert.Equal(expected, InputRules.AutoAnswer("", lastWasResult: true, rpn: false, typed));
	}

	[Theory]
	[InlineData("1", true, false, '+')]    // Input not empty
	[InlineData("", false, false, '+')]    // last line was not a result
	[InlineData("", true, true, '+')]      // RPN: an operator alone is not a valid start
	[InlineData("", true, false, '5')]     // not an operator
	[InlineData("", true, false, '(')]
	public void OtherCasesTypeNormally(string text, bool lastWasResult, bool rpn, char typed)
	{
		Assert.Null(InputRules.AutoAnswer(text, lastWasResult, rpn, typed));
	}

	[Theory]
	[InlineData("5", false, false, "memory=(5)")]
	[InlineData("5", true, false, "memory=memory+(5)")]
	[InlineData("2*3", true, true, "memory=memory-(2*3)")]
	[InlineData("", true, false, "memory=memory+answer")]
	[InlineData("  ", false, true, "memory=-answer")]
	public void MemoryAddBuildsDefinition(string input, bool hasMemory, bool subtract, string expected)
	{
		Assert.Equal(expected, InputRules.MemoryAdd(input, hasMemory, subtract));
	}

	[Theory]
	[InlineData("12", "12 ", false)]           // after a number: separate
	[InlineData("12 3", "12 3 ", false)]
	[InlineData("1.5", "1.5 ", false)]
	[InlineData("12 ", "12 ", true)]           // already separated: evaluate
	[InlineData("12 3 +", "12 3 +", true)]     // after an operator: evaluate
	[InlineData("5 sqrt", "5 sqrt", true)]     // after a function: evaluate
	[InlineData("", "", true)]
	public void RpnEnterSeparatesOrEvaluates(string text, string expectedText, bool expectedSubmit)
	{
		Assert.Equal((expectedText, expectedSubmit), InputRules.RpnEnter(text));
	}

	[Theory]
	[InlineData("12 3", '+', "12 3 +")]
	[InlineData("12 3 ", '*', "12 3 *")]
	[InlineData("", '-', "-")]
	public void RpnOperatorIsItsOwnItem(string text, char symbol, string expected)
	{
		Assert.Equal(expected, InputRules.RpnOperator(text, symbol));
	}
}
