// KeypadRulesTests.cs - The Numeric keypad's C, % and STO keys (owner 2026-11-16)

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

	[Theory]
	[InlineData("50+10", "50*(1+10%)")]
	[InlineData("50-10", "50*(1-10%)")]
	[InlineData("50 + 10", "50*(1+10%)")]
	[InlineData("2*3+10", "(2*3)*(1+10%)")]
	[InlineData("price+7", "price*(1+7%)")]
	[InlineData("50*10", "50*10%")]
	[InlineData("200/10", "200/10%")]
	[InlineData("10", "10%")]
	[InlineData("-10", "-10%")]
	[InlineData("(1+2)*3+5", "((1+2)*3)*(1+5%)")]
	[InlineData("(100+5", "(100+5%")]    // inside a bracket: left alone
	[InlineData("2e-3", "2e-3%")]    // an exponent, not a minus
	[InlineData("50+", "50+%")]
	public void PercentAfterPlusOrMinusIsAShare(string text, string expected)
	{
		Assert.Equal(expected, KeypadRules.Percent(text));
	}

	[Theory]
	[InlineData("5*3", "x", "x=5*3")]
	[InlineData("", "y", "y=ans")]
	public void StoreNamesTheLine(string text, string name, string expected)
	{
		Assert.Equal(expected, KeypadRules.Store(text, name));
	}
}
