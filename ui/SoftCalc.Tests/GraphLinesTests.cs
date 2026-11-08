// GraphLinesTests.cs - Graph line colours, equations and the nearest-line trace (graph2_plan G4-G6)

using SoftCalc.Engine;
using SoftCalc.Logic;
using SoftCalc.Persistence;
using SoftCalc.UI;

namespace SoftCalc.Tests;

public sealed class GraphLinesTests
{
	[Fact]
	public void RenameKeepsColourTickAndSelection()
	{
		var settings = new GraphSettings { Function = "f" };
		settings.Colours["f"] = unchecked((int)0xFF112233);
		settings.SetShown("f", true);
		settings.Rename("F", "g");
		Assert.Equal((unchecked((int)0xFF112233), true, "g"), (settings.Colours["g"], settings.IsShown("g"), settings.Function));
		Assert.False(settings.Colours.ContainsKey("f"));
	}

	[Theory]
	[InlineData("#1A2B3C", unchecked((int)0xFF1A2B3C))]
	[InlineData("#ffffff", unchecked((int)0xFFFFFFFF))]
	public void ColoursRoundTripAsHex(string hex, int argb)
	{
		Assert.Equal(argb, PlotMath.ParseColour(hex));
		Assert.Equal(hex.ToUpperInvariant(), PlotMath.FormatColour(argb));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("red")]
	[InlineData("#12345")]
	[InlineData("#GGGGGG")]
	public void BadColoursAreIgnored(string? hex)
	{
		Assert.Null(PlotMath.ParseColour(hex));
	}

	[Fact]
	public void EquationsAndColoursAreSaved()
	{
		string path = Path.Combine(Path.GetTempPath(), "softcalc-lines-" + Guid.NewGuid().ToString("N") + ".json");
		try
		{
			var prefs = new AppSettings();
			prefs.Graph.Equations = new List<string> { "x^2+y^2=r^2" };
			prefs.Graph.Colours = new Dictionary<string, string> { ["g"] = "#FF0000" };
			SettingsStore.Save(path, prefs);
			var loaded = SettingsStore.Load(path).Settings.Graph;
			Assert.Equal(("x^2+y^2=r^2", "#FF0000"), (loaded.Equations![0], loaded.Colours!["g"]));
			File.WriteAllText(path, "{\"graph\":{}}");
			Assert.Null(SettingsStore.Load(path).Settings.Graph.Equations);    // older files: none
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Theory]
	[InlineData("x^2+y^2=4", null)]
	[InlineData("x^2+y^2", "needs '='")]
	[InlineData("Y=X", "already")]
	public void EquationDialogChecks(string text, string? problem)
	{
		string? got = EquationDialog.Check(text, new[] { "y=x" });
		if (problem == null)
		{
			Assert.Null(got);
		}
		else
		{
			Assert.Contains(problem, got);
		}
	}

	private static readonly WorldRect World = new(-10, 10, -10, 10);
	private static readonly RectangleF Screen = new(0, 0, 200, 200);    // 10 pixels per unit

	[Fact]
	public void NearestPointOnAFunctionGivesItsX()
	{
		var points = new List<(double X, double Y)?> { (-10, 0), (0, 0), (10, 0) };    // y = 0
		var line = new PlotSeries("f", points, -10, 10, Array.Empty<(double, double, double, double)>());
		var hit = PlotView.Nearest(line, new Point(150, 90), World, Screen)!.Value;    // x = 5, 1 unit above
		Assert.Equal(10f, hit.Distance, 3);
		Assert.Equal((5.0, 0.0, 5.0), (hit.X, hit.Y, hit.T));
	}

	[Fact]
	public void NearestSkipsJumpsAndGaps()
	{
		var points = new List<(double X, double Y)?> { (-1, -9), (1, 9), null, (5, 0) };    // a pole-like jump, then a gap
		var line = new PlotSeries("tan", points, -1, 5, Array.Empty<(double, double, double, double)>());
		Assert.Null(PlotView.Nearest(line, new Point(100, 100), World, Screen));
	}

	[Fact]
	public void NearestOnAnEquationSegmentKeepsItsDirection()
	{
		var segments = new List<(double, double, double, double)> { (2, -5, 2, 5) };    // x = 2, vertical
		var line = new PlotSeries("x=2", Array.Empty<(double X, double Y)?>(), 0, 0, segments);
		var hit = PlotView.Nearest(line, new Point(125, 100), World, Screen)!.Value;
		Assert.Equal((5f, 2.0, 0.0), (hit.Distance, hit.X, hit.Y));
		Assert.True(Math.Abs(hit.Direction.Y) > Math.Abs(hit.Direction.X));    // steep: the trace fixes y
	}

	[Fact]
	public void SquareWidensTheNarrowerAxisAroundTheCentre()
	{
		var square = PlotMath.Square(new WorldRect(-10, 10, -10, 10), 600, 300);    // 1/30 per pixel on x, 1/15 on y
		Assert.Equal((-20.0, 20.0, -10.0, 10.0), (square.MinX, square.MaxX, square.MinY, square.MaxY));
		var tall = PlotMath.Square(new WorldRect(0, 4, 1, 3), 200, 400);    // centre (2, 2) kept
		Assert.Equal((0.0, 4.0, -2.0, 6.0), (tall.MinX, tall.MaxX, tall.MinY, tall.MaxY));
		var resized = PlotMath.Square(new WorldRect(-1, 1, -1, 1), 300, 100, unitsPerPixel: 0.02);    // window resized: same scale
		Assert.Equal((-3.0, 3.0, -1.0, 1.0), (resized.MinX, resized.MaxX, resized.MinY, resized.MaxY));
	}

	[Fact]
	public void GraphWindowPlaceAndLockAreSaved()
	{
		string path = Path.Combine(Path.GetTempPath(), "softcalc-graphwin-" + Guid.NewGuid().ToString("N") + ".json");
		try
		{
			var prefs = new AppSettings();
			prefs.Graph.Square = true;
			prefs.Graph.RangeWidth = 180;
			prefs.Graph.Window = new WindowPlacement { X = 10, Y = 20, Width = 900, Height = 600, Maximized = true };
			SettingsStore.Save(path, prefs);
			var g = SettingsStore.Load(path).Settings.Graph;
			Assert.Equal((true, 180, 900, true), (g.Square, g.RangeWidth, g.Window!.Width, g.Window.Maximized));
			File.WriteAllText(path, "{\"graph\":{}}");
			var old = SettingsStore.Load(path).Settings.Graph;
			Assert.Equal((false, 150, (WindowPlacement?)null), (old.Square, old.RangeWidth, old.Window));    // older files
		}
		finally
		{
			File.Delete(path);
		}
	}
}
