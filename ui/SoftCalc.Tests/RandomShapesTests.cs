// RandomShapesTests.cs - The Advanced random tab's shapes, picture and command (design/random_plan.md R3)

using SoftCalc.Logic;
using SoftCalc.Persistence;
using SoftCalc.UI;

namespace SoftCalc.Tests;

public sealed class RandomShapesTests
{
	private static readonly IRandomSource Secure = new SecureRandom();

	private static ShapeCurve Curve(string id, decimal strength = 5)
	{
		return RandomShapes.Curve(id, strength, 2, 5);
	}

	public static TheoryData<string> AllShapes()
	{
		var data = new TheoryData<string>();
		foreach (var (id, _) in RandomShapes.Shapes)
		{
			data.Add(id);
		}
		return data;
	}

	[Theory]
	[MemberData(nameof(AllShapes))]
	public void WholeRangeKeepsBothEnds(string id)
	{
		var (values, error) = RandomShapes.Values(Secure, Curve(id), 1, 3, 2, whole: true, 2, 6000);    // narrow bell seldom reaches the ends of a wider range
		Assert.Null(error);
		Assert.Equal(new decimal[] { 1, 2, 3 }, values!.Distinct().Order());
	}

	[Theory]
	[MemberData(nameof(AllShapes))]
	public void DecimalsStayInRangeWithThePlaces(string id)
	{
		var (values, error) = RandomShapes.Values(Secure, Curve(id), 10, -5, 2, whole: false, 3, 2000);
		Assert.Null(error);
		Assert.All(values!, v => Assert.InRange(v, -5m, 10m));
		Assert.All(values!, v => Assert.Equal(v, decimal.Round(v, 3)));
	}

	[Fact]
	public void StrengthFiveGivesTheChosenPictures()
	{
		Assert.Equal(new ShapeCurve(ShapeKind.Beta, 4, 4), Curve("bell"));
		Assert.Equal(new ShapeCurve(ShapeKind.Beta, 12, 12), Curve("narrow"));
		Assert.Equal(new ShapeCurve(ShapeKind.Beta, 2, 5), Curve("right"));
		Assert.Equal(new ShapeCurve(ShapeKind.Beta, 5, 2), Curve("left"));
		Assert.Equal(new ShapeCurve(ShapeKind.Beta, 0.6m, 0.6m), Curve("u"));
		Assert.Equal(new ShapeCurve(ShapeKind.Beta, 2, 1), Curve("rising"));
		Assert.Equal(new ShapeCurve(ShapeKind.Exponential, -4, 0), Curve("growth"));
		Assert.Equal(new ShapeCurve(ShapeKind.Beta, 3, 1.5m), RandomShapes.Curve("custom", 5, 3, 1.5m));
		Assert.All(RandomShapes.Shapes, s => Assert.False(string.IsNullOrEmpty(RandomShapes.Hint(s.Id))));
	}

	[Fact]
	public void BetaDrawsHaveTheRightMean()
	{
		foreach (var (a, b) in new[] { (2m, 5m), (0.5m, 0.5m), (12m, 12m) })
		{
			var curve = new ShapeCurve(ShapeKind.Beta, a, b);
			double mean = Enumerable.Range(0, 20000).Average(_ => RandomShapes.Draw(Secure, curve, 0));
			Assert.InRange(mean, (double)(a / (a + b)) - 0.01, (double)(a / (a + b)) + 0.01);
		}
	}

	[Fact]
	public void ShapesLeanTheRightWay()
	{
		int Count(string id, Func<decimal, bool> test)
		{
			return RandomShapes.Values(Secure, Curve(id), 1, 10, 3, true, 0, 5000).Values!.Count(test);
		}
		Assert.True(Count("right", v => v <= 3) > 3 * Count("right", v => v >= 8));
		Assert.True(Count("left", v => v >= 8) > 3 * Count("left", v => v <= 3));
		Assert.True(Count("u", v => v is 1 or 10) > 2 * Count("u", v => v is 5 or 6));
		Assert.True(Count("peaks", v => v is 5 or 6) < Count("peaks", v => v is 2 or 3) / 2);
		Assert.True(Count("decay", v => v == 1) > 4 * Count("decay", v => v == 10));
		var tri = RandomShapes.Values(Secure, Curve("triangle"), 1, 5, 3, true, 0, 5000).Values!;
		Assert.Equal(3m, tri.GroupBy(v => v).MaxBy(g => g.Count())!.Key);
	}

	[Fact]
	public void RefusesWhatCannotBeDrawn()
	{
		Assert.Contains("Peak at", RandomShapes.Values(Secure, Curve("triangle"), 1, 10, 11, true, 0, 5).Error);
		Assert.Contains("Count", RandomShapes.Values(Secure, Curve("bell"), 1, 10, 0, true, 0, RandomShapes.MaxCount + 1).Error);
		Assert.NotNull(RandomShapes.Values(Secure, new ShapeCurve(ShapeKind.Beta, 0, 1), 1, 10, 0, true, 0, 5).Error);
	}

	[Fact]
	public void PictureAndBarsHaveAreaOne()
	{
		foreach (var (id, _) in RandomShapes.Shapes)
		{
			var line = RandomShapes.Line(Curve(id), 0.3);
			Assert.InRange(line.Average(), 0.999, 1.001);
		}
		var values = RandomShapes.Values(Secure, Curve("bell"), 1, 10, 0, true, 0, 1000).Values!;
		var bars = RandomShapes.Bars(values, 1, 10, whole: true);
		Assert.Equal(10, bars.Length);    // one bar per number
		Assert.InRange(bars.Average(), 0.999, 1.001);
		Assert.Equal(40, RandomShapes.Bars(values, 1, 1000, whole: true).Length);
		Assert.Equal(40, RandomShapes.Bars(new[] { 1.5m, 2m }, 1, 2, whole: false).Length);
	}

	[Theory]
	[InlineData("bell", true, "randbeta(4, 4, 1, 10)")]
	[InlineData("bell", false, "round(randbeta(4, 4, 1.0, 10), 2)")]
	[InlineData("right", true, "randbeta(2, 5, 1, 10)")]
	[InlineData("u", true, "randbeta(0.6, 0.6, 1, 10)")]
	[InlineData("triangle", true, "randtri(3, 1, 10)")]
	[InlineData("decay", true, "randexp(4, 1, 10)")]
	[InlineData("growth", false, "round(randexp(-4, 1.0, 10), 2)")]
	[InlineData("peaks", true, "randpeaks(0.5, 1, 10)")]
	public void CommandIsTheEngineCall(string id, bool whole, string want)
	{
		Assert.Equal(want, RandomShapes.Command(Curve(id), 10, 1, 3, whole, 2));
	}

	[Fact]
	public void WindowHasItsTabsAndSavesTheShape()
	{
		var prefs = new RandomPrefs { Tab = "advanced", Shape = "triangle", ShapeWhole = false, ShapePlaces = 1, ShapePeak = 2.5m };
		using var window = new RandomWindow(prefs, _ => { });
		var tabs = window.Controls.OfType<TabControl>().Single();
		Assert.Equal(new[] { "Simple", "Advanced", "Password", "Entropy", "..." }, tabs.TabPages.Cast<TabPage>().Select(t => t.Text));
		Assert.Equal(1, tabs.SelectedIndex);
		window.SavePrefs();
		Assert.Equal(("advanced", "triangle", false, 2.5m), (prefs.Tab, prefs.Shape, prefs.ShapeWhole, prefs.ShapePeak));
	}

	[Fact]
	public void ShapePrefsAreSanitized()
	{
		string path = Path.Combine(Path.GetTempPath(), "softcalc-shapes-" + Guid.NewGuid().ToString("N") + ".json");
		try
		{
			File.WriteAllText(path, "{\"random\":{\"tab\":\"advanced\",\"shape\":\"bogus\",\"shape_strength\":99,\"shape_count\":0,\"shape_a\":0}}");
			var r = SettingsStore.Load(path).Settings.Random;
			Assert.Equal(("advanced", "bell", 10m, 1, RandomShapes.MinBeta), (r.Tab, r.Shape, r.ShapeStrength, r.ShapeCount, r.ShapeA));
		}
		finally
		{
			File.Delete(path);
		}
	}
}
