// DefinitionFieldsTests.cs - Definition editor fields: compose, split, field errors

using SoftCalc.Logic;

namespace SoftCalc.Tests;

public sealed class DefinitionFieldsTests
{
	[Fact]
	public void ComposesFunctionsAndVariables()
	{
		Assert.Equal("g(x, y)=x*y", DefinitionFields.Compose(true, " g ", "x ,y", " x*y ").Definition);
		Assert.Equal("k()=42", DefinitionFields.Compose(true, "k", "  ", "42").Definition);
		Assert.Equal("rate=0.05", DefinitionFields.Compose(false, "rate", "ignored", "0.05").Definition);
	}

	[Theory]
	[InlineData("", "x", "1", "Name")]
	[InlineData("2g", "x", "1", "Name")]
	[InlineData("g", "x,", "1", "Parameters")]
	[InlineData("g", "x, X", "1", "Parameters")]
	[InlineData("g", "x", " ", "Body")]
	public void ReportsTheFieldToFix(string name, string parameters, string body, string field)
	{
		var result = DefinitionFields.Compose(true, name, parameters, body);
		Assert.Null(result.Definition);
		Assert.Equal(Enum.Parse<DefinitionField>(field), result.Field);
		Assert.False(string.IsNullOrEmpty(result.Error));
	}

	[Fact]
	public void SplitsTheEnginesHead()
	{
		Assert.Equal(("g", "x, y"), DefinitionFields.SplitHead("g(x,y)="));
		Assert.Equal(("function", "var"), DefinitionFields.SplitHead("function(var)="));
		Assert.Equal(("k", ""), DefinitionFields.SplitHead("k()="));
	}
}
