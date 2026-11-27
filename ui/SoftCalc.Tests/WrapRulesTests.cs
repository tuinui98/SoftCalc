// WrapRulesTests.cs - Keypad keys around the selection (owner 2026-11-27); web/app/tests/wrap.test.ts has the same table

using SoftCalc.Logic;

namespace SoftCalc.Tests;

public class WrapRulesTests
{
	[Theory]
	[InlineData("2+3", "ins:(", "(2+3)")]
	[InlineData("2+3", "ins:)", "(2+3)")]
	[InlineData("2+3", "ins:[", "[2+3]")]
	[InlineData("2+3", "ins:]", "[2+3]")]
	[InlineData("(2+3)", "ins:(", "((2+3))")]
	[InlineData("2+3", "fn:sin", "sin(2+3)")]
	[InlineData("(2+3)", "fn:sqrt", "sqrt(2+3)")]
	[InlineData("x", "fn:log10", "log10(x)")]
	[InlineData("2+3", "ins:^2", "(2+3)^2")]
	[InlineData("x", "ins:^2", "x^2")]
	[InlineData("2.5", "ins:^3", "2.5^3")]
	[InlineData("sqrt(2)", "ins:^2", "sqrt(2)^2")]
	[InlineData("(1)+(2)", "ins:^2", "((1)+(2))^2")]
	[InlineData("[1, 2]", "ins:^2", "[1, 2]^2")]
	[InlineData("-3", "ins:^2", "(-3)^2")]
	[InlineData("2+3", "ins:^(-1)", "(2+3)^(-1)")]
	[InlineData("2+3", "ins:!", "(2+3)!")]
	[InlineData("2+3", "ins:^(1/3)", "(2+3)^(1/3)")]
	[InlineData("2+3", "ins:^", "(2+3)^")]
	[InlineData("2+3", "ins:^(1/", "(2+3)^(1/")]
	[InlineData("2+3", "ins:10^", "10^(2+3)")]
	[InlineData("x", "ins:10^", "10^x")]
	[InlineData("2+3", "ins:-", "-(2+3)")]
	[InlineData("5", "ins:-", "-5")]
	public void KeyGoesAroundTheSelection(string selected, string id, string want)
	{
		var wrapped = WrapRules.Wrap("1*" + selected + "+4", 2, selected.Length, id);
		Assert.NotNull(wrapped);
		Assert.Equal("1*" + want + "+4", wrapped.Value.Text);
		Assert.Equal(2 + want.Length, wrapped.Value.Caret);    // after what went in
	}

	[Theory]
	[InlineData("ins:7")]
	[InlineData("ins:pi")]
	[InlineData("ins:,")]
	[InlineData("ins: mod ")]
	[InlineData("ins:->")]
	[InlineData("fn:mod")]
	[InlineData("fn:+")]
	[InlineData("x")]
	public void OtherKeysDoNotWrap(string id)
	{
		Assert.Null(WrapRules.Wrap("2+3", 0, 3, id));
	}

	[Fact]
	public void NoSelectionNoWrap()
	{
		Assert.Null(WrapRules.Wrap("2+3", 1, 0, "ins:("));
	}
}
