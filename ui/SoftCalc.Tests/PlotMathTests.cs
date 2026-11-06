// PlotMathTests.cs - Graph geometry: ranges, grid lines, line breaks, dialog shape checks

using SoftCalc.Logic;
using SoftCalc.UI;

namespace SoftCalc.Tests;

public class PlotMathTests
{
	private static readonly WorldRect Default = new(-10, 10, -10, 10);

	[Fact]
	public void ToScreenMapsCornersAndCenter()
	{
		var screen = new RectangleF(0, 0, 200, 100);
		Assert.Equal(new PointF(0, 100), PlotMath.ToScreen(-10, -10, Default, screen));
		Assert.Equal(new PointF(200, 0), PlotMath.ToScreen(10, 10, Default, screen));
		Assert.Equal(new PointF(100, 50), PlotMath.ToScreen(0, 0, Default, screen));
	}

	[Fact]
	public void GridLinesAreMultiplesInsideTheRange()
	{
		Assert.Equal(new[] { -2.0, 0, 2, 4 }, PlotMath.GridLines(-3, 5, 2));
		Assert.Equal(new[] { -10.0, -5, 0, 5, 10 }, PlotMath.GridLines(-10, 10, 5));
	}

	[Fact]
	public void TooDenseGridIsSkipped()
	{
		Assert.Empty(PlotMath.GridLines(-1000, 1000, 1));
		Assert.Empty(PlotMath.GridLines(0, 1, 0));
	}

	[Fact]
	public void SegmentsBreakAtGapsAndPoles()
	{
		var points = new List<(double X, double Y)?>
		{
			(0, 1), (1, 2), null, (2, 3), (3, 4),
			(4, 500), (5, 5),    // a jump bigger than two view heights (tan near a pole)
		};
		var segments = PlotMath.Segments(points, Default);
		// [(0,1) (1,2)] gap [(2,3) (3,4)] jump [(4,500)] jump [(5,5)]
		Assert.Equal(new[] { 2, 2, 1, 1 }, segments.Select(s => s.Count));
	}

	[Theory]
	[InlineData(-10, 10, 1, null)]
	[InlineData(10, -10, 1, "X Min must be less than X Max")]
	[InlineData(-10, 10, 0, "X Increment must be greater than 0")]
	[InlineData(double.NaN, 10, 1, "X: enter numbers")]
	public void RangeChecks(double min, double max, double step, string? expected)
	{
		Assert.Equal(expected, PlotMath.CheckRange("X", min, max, step));
	}

	[Fact]
	public void PolarViewIsSquareAroundThePole()
	{
		var settings = new GraphSettings { Mode = GraphMode.Polar, MaxR = 4 };
		Assert.Equal(new WorldRect(-4, 4, -4, 4), settings.View);
	}

	[Theory]
	[InlineData("f(x)=x^2", true, null)]
	[InlineData("grow(p, n) = p*2^n", true, null)]
	[InlineData("a=5", true, "Enter a function such as f(x)=x^2")]
	[InlineData("x^2", true, "Enter a function such as f(x)=x^2")]
	[InlineData("rate=0.05", false, null)]
	[InlineData("f(x)=x", false, "Enter a variable such as a=5")]
	[InlineData("5", false, "Enter a variable such as a=5")]
	public void DialogShapeCheck(string definition, bool isFunction, string? expected)
	{
		Assert.Equal(expected, DefinitionCommands.CheckShape(definition, isFunction));
	}

	[Theory]
	[InlineData(20, 10, 2)]
	[InlineData(20, 20, 1)]
	[InlineData(1, 4, 0.5)]
	[InlineData(0.003, 3, 0.001)]
	[InlineData(7000, 7, 1000)]
	public void NiceStepIsOneTwoOrFive(double span, int count, double expected)
	{
		Assert.Equal(expected, PlotMath.NiceStep(span, count), 9);
	}

	[Theory]
	[InlineData(2, 1, "2")]
	[InlineData(-0.5, 0.5, "-0.5")]
	[InlineData(1e-12, 0.1, "0.0")]
	[InlineData(0.126, 0.01, "0.13")]
	[InlineData(3e8, 1e8, "3E+8")]
	public void TickLabelsUseTheStepsDecimals(double value, double step, string expected)
	{
		Assert.Equal(expected, PlotMath.FormatTick(value, step));
	}

	[Fact]
	public void ZoomKeepsThePointUnderTheCursor()
	{
		var zoomed = PlotMath.Zoom(-10, 10, 5, 0.5)!.Value;
		Assert.Equal((-2.5, 7.5), zoomed);
		Assert.Null(PlotMath.Zoom(0, 1e-9, 0, 0.5));    // too small
		Assert.Null(PlotMath.Zoom(-1e12, 1e12, 0, 2));    // too large
	}

	[Fact]
	public void FitCutsPolesAndAddsMargin()
	{
		var values = Enumerable.Range(0, 100).Select(i => (double)i % 10).ToList();
		values[50] = 1e15;    // a pole
		var fit = PlotMath.FitRange(values)!.Value;
		Assert.Equal(-0.45, fit.Min, 9);
		Assert.Equal(9.45, fit.Max, 9);
		Assert.Equal((4.0, 6.0), PlotMath.FitRange(new[] { 5.0, 5.0 }));    // flat line
		Assert.Null(PlotMath.FitRange(new[] { double.NaN }));
	}

	[Fact]
	public void ResetRangeOnlyTouchesTheCurrentMode()
	{
		var s = new GraphSettings { MinX = 1, MaxX = 2, MaxR = 3, Mode = GraphMode.XY };
		s.ResetRange();
		Assert.Equal((-10.0, 10.0, 3.0), (s.MinX, s.MaxX, s.MaxR));
		s.Mode = GraphMode.Polar;
		s.ResetRange();
		Assert.Equal(10, s.MaxR);
	}
}
