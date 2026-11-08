// InputHelpTests.cs - The hint line under the Input box

using SoftCalc.Logic;

namespace SoftCalc.Tests;

public sealed class InputHelpTests
{
	private static string? User(string name)
	{
		return name.ToLowerInvariant() switch
		{
			"f" => "f(x)=x^2 - your function",
			"rate" => "rate = 0.05 - your variable",
			_ => null,
		};
	}

	private static string? Hint(string text, int? caret = null)
	{
		return InputHelp.For(text, caret ?? text.Length, User);
	}

	[Fact]
	public void OpenCallDescribesItsFunction()
	{
		Assert.StartsWith("fv(rate, nper, pmt", Hint("fv(0.05/12, 120, "));
		Assert.StartsWith("sqrt(x)", Hint("1+sqrt(2*"));
	}

	[Fact]
	public void InnermostOpenCallWins()
	{
		Assert.StartsWith("ln(x)", Hint("fv(ln(2"));
		Assert.StartsWith("fv(", Hint("fv(ln(2), "));
	}

	[Fact]
	public void TypedWordIsDescribedOrCompleted()
	{
		Assert.StartsWith("sqrt(x)", Hint("sqrt"));
		Assert.Equal("longdate  longdatetime  longtime", Hint("long"));
		Assert.StartsWith("fromepoch(", Hint("fro"));
		Assert.Null(Hint("q"));    // one letter: too early to list names
	}

	[Fact]
	public void UserNamesComeFirst()
	{
		Assert.Equal("f(x)=x^2 - your function", Hint("f(3"));
		Assert.Equal("rate = 0.05 - your variable", Hint("100*Rate"));
	}

	[Fact]
	public void TagsAndReferences()
	{
		Assert.StartsWith("[from->to]", Hint("100 [usd-"));
		Assert.Null(Hint("100 [usd->thb] + 1"));
		Assert.StartsWith("$1", Hint("2*$"));
		Assert.StartsWith("$1", Hint("$12"));
	}

	[Fact]
	public void CaretDecides()
	{
		Assert.StartsWith("sqrt(x)", Hint("sqrt(2)+ln(3)", 5));
		Assert.Null(Hint("1+2", 3));
		Assert.Null(Hint(""));
	}

	[Theory]
	[InlineData("0x1F", "0x1F")]
	[InlineData("2*101b", "101b")]
	[InlineData("sqrt(0.5", "0.5")]
	[InlineData("1e3", "1e3")]
	[InlineData("0o17", "0o17")]
	[InlineData("x12", null)]             // a name
	[InlineData("$12", null)]             // a result reference
	[InlineData("27.10.2026", null)]      // a date
	[InlineData("1:30", null)]            // a duration
	[InlineData("2+", null)]
	[InlineData("4-1-50 [rnw", "4-1-50")]    // rai-ngan-wa group
	[InlineData("2*4-1-50 [rnw->m2]", "4-1-50")]
	[InlineData("10-4-1-50 [RNW]", "4-1-50")]    // the three before the tag, as the engine reads it
	[InlineData("4-1-50", "50")]    // no tag: just the last number
	public void LiteralAtCaret(string text, string? want)
	{
		Assert.Equal(want, InputHelp.LiteralAt(text, text.Length));
	}
}
