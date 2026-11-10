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
	public void DefinitionReadsAsTyped()
	{
		Assert.Equal("parabola(x)=x^2", GraphExamples.Functions[0].Definition);
		Assert.Equal("x^2+y^2=4", GraphExamples.Equations[0].Definition);
	}
}
