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
	public void AngleMovesByWholeTurnsIntoTheRange()
	{
		Assert.Equal(1.0, PlotMath.AngleInRange(1, -Math.PI, Math.PI)!.Value, 12);
		Assert.Equal(-1 + 2 * Math.PI, PlotMath.AngleInRange(-1, 0, 2 * Math.PI)!.Value, 12);
		Assert.Equal(1 + 4 * Math.PI, PlotMath.AngleInRange(1, 4 * Math.PI, 6 * Math.PI)!.Value, 12);
		Assert.Null(PlotMath.AngleInRange(-1, 0, 1));    // range shorter than a turn
	}

	[Fact]
	public void ShownFunctionsAndColours()
	{
		var s = new GraphSettings { Graphable = new[] { "f", "g", "h" } };
		s.SetShown("G", true);
		s.SetShown("h", true);
		s.SetShown("g", true);    // no duplicates, any case
		Assert.Equal(new[] { "h", "g" }, s.Shown);
		Assert.True(s.IsShown("H"));
		s.SetShown("h", false);
		Assert.Equal(new[] { "g" }, s.Shown);
		Assert.Equal(1, s.ColourIndex("G"));    // colour follows the definition order
		Assert.Equal(-1, s.ColourIndex("two_args"));
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

	[Fact]
	public void SidebarShowsTheWindowRangeAtTheWindowScale()
	{
		var world = new WorldRect(-28, 28, -20, 20);    // window drawing area 700 x 500: 0.08 units per pixel both ways
		// sidebar 300 x 150, wider than the window: all of Y, X grown around the centre
		var wide = PlotMath.SameShape(world, 700.0 / 500, 300, 150);
		Assert.Equal(-20, wide.MinY, 9);
		Assert.Equal(20, wide.MaxY, 9);
		Assert.Equal(40, wide.MaxX, 9);    // 300 px at 40/150 units per pixel
		Assert.Equal(-40, wide.MinX, 9);
		// sidebar 260 x 260, taller: all of X, Y grown
		var tall = PlotMath.SameShape(world, 700.0 / 500, 260, 260);
		Assert.Equal(-28, tall.MinX, 9);
		Assert.Equal(28, tall.MaxX, 9);
		Assert.Equal(28, tall.MaxY, 9);
		// the same shape as the window: exactly the window's range
		var same = PlotMath.SameShape(world, 2, 200, 100);
		Assert.Equal((world.MinX, world.MaxX, world.MinY, world.MaxY), (Math.Round(same.MinX, 9), Math.Round(same.MaxX, 9), Math.Round(same.MinY, 9), Math.Round(same.MaxY, 9)));
	}

	[Fact]
	public void SidebarKeepsAStretchedWindowScale()
	{
		// window 600 x 300 showing x -10..10, y -10..10: y squeezed 2:1
		var view = PlotMath.SameShape(new WorldRect(-10, 10, -10, 10), 2, 200, 200);
		Assert.Equal(-10, view.MinX, 9);
		Assert.Equal(10, view.MaxX, 9);
		Assert.Equal(20, view.MaxY, 9);    // same units per pixel ratio (y twice x), so y shows more
		Assert.Equal(-20, view.MinY, 9);
	}
}
