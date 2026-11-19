// KeypadRulesTests.cs - The Numeric keypad's C key (owner 2026-11-16)

using SoftCalc.Logic;

namespace SoftCalc.Tests;

public sealed class KeypadRulesTests
{
	[Theory]
	[InlineData("12+345", "12+")]
	[InlineData("12+", "12")]
	[InlineData("sin(30", "sin(")]
	[InlineData("2*pi ", "2*")]
	[InlineData("3.25", "")]
	[InlineData("", "")]
	public void ClearEntryTakesTheLastNumber(string text, string expected)
	{
		Assert.Equal(expected, KeypadRules.ClearEntry(text));
	}
}
