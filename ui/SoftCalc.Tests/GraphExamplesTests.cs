// GraphExamplesTests.cs - The example list behind Graph > Add > Examples

using SoftCalc.Logic;
using SoftCalc.UI;

namespace SoftCalc.Tests;

public sealed class GraphExamplesTests
{
	[Fact]
	public void ExamplesAreWellFormedAndDistinct()
	{
		var all = GraphExamples.Functions.Concat(GraphExamples.Equations).ToList();
		Assert.Equal(all.Count, all.Select(e => e.Title).Distinct().Count());
		Assert.Equal(all.Count, all.Select(e => e.Definition).Distinct().Count());
		Assert.All(GraphExamples.Functions, e => Assert.Null(DefinitionCommands.CheckShape(e.Definition, isFunction: true)));
		Assert.All(GraphExamples.Equations, e => Assert.True(GraphSettings.IsEquation(e.Text), e.Text));
		Assert.All(all, e => Assert.True(e.View.MaxX > e.View.MinX && e.View.MaxY > e.View.MinY));
	}

	[Fact]
	public void EquationViewsAreFourByThree()
	{
		Assert.All(GraphExamples.Equations, e =>
			Assert.Equal(4.0 / 3, (e.View.MaxX - e.View.MinX) / (e.View.MaxY - e.View.MinY), 9));
	}

	[Fact]
	public void FreeNameAddsANumberWhenTaken()
	{
		Assert.Equal("sinc", GraphExamples.FreeName("sinc", new[] { "f" }));
		Assert.Equal("sinc2", GraphExamples.FreeName("sinc", new[] { "SINC" }));
		Assert.Equal("sinc3", GraphExamples.FreeName("sinc", new[] { "sinc", "sinc2" }));
	}

	[Fact]
	public void DefinitionHasTheDefaultsAsNumbers()
	{
		Assert.Equal("parabola(x)=1*(x-0)^2+0", GraphExamples.Functions[0].Definition);
		Assert.Equal("(x-0)^2+(y-0)^2=2^2", GraphExamples.Equations[0].Definition);
		Assert.Equal("parabola(x)=a*(x-h)^2+k", GraphExamples.Functions[0].FormLine);
		Assert.Equal("Form: (x-a)^2+(y-b)^2=r^2" + Environment.NewLine + "centre (a, b)" + Environment.NewLine + "r = radius",
			GraphExamples.Equations[0].Hint);
	}

	[Fact]
	public void WithDefaultsReplacesWholeNamesOnly()
	{
		var defaults = new[] { ("a", "-1"), ("b", "1/3") };
		Assert.Equal("abs(x)+(-1)*x+(1/3)", GraphExamples.WithDefaults("abs(x)+a*x+b", defaults));
		Assert.Equal("ab+a2", GraphExamples.WithDefaults("ab+a2", defaults));
	}

	[Fact]
	public void TrigExamplesAreMarkedAsUsingAngles()
	{
		var marked = GraphExamples.Functions.Concat(GraphExamples.Equations).Where(e => e.UsesAngles).Select(e => e.Title).ToList();
		Assert.Equal(new[] { "Sine", "Cosine", "Tangent", "Sinc", "x sin(1/x)", "Two waves", "Egg crate" }, marked);
	}

	[Fact]
	public void EveryConstantIsInItsFormAndGoneFromItsText()
	{
		var reserved = new[] { "e", "i", "j", "pi", "x", "y" };
		foreach (var example in GraphExamples.Functions.Concat(GraphExamples.Equations))
		{
			var words = System.Text.RegularExpressions.Regex.Matches(example.Text, "[A-Za-z_][A-Za-z_0-9]*").Select(m => m.Value).ToList();
			Assert.False(string.IsNullOrWhiteSpace(example.Note), example.Title);
			foreach (var (name, value) in example.Defaults)
			{
				Assert.DoesNotContain(name, reserved);
				Assert.Matches(@"\b" + name + @"\b", example.Form);
				Assert.DoesNotContain(name, words);
				Assert.True(double.TryParse(value.Replace("1/3", "0.3"), System.Globalization.CultureInfo.InvariantCulture, out _), value);
			}
		}
	}
}
