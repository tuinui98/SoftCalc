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
}
