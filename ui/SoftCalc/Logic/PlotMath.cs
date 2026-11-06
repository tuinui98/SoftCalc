// PlotMath.cs - Pure geometry for the graph: ranges, grid lines, line segments
//
// No drawing here, so the rules can be unit tested: mapping world to
// screen, where grid lines go, and where a plotted line must break
// (undefined points, and jumps such as tan's poles).

namespace SoftCalc.Logic;

internal readonly record struct WorldRect(double MinX, double MaxX, double MinY, double MaxY)
{
	public double Width => MaxX - MinX;
	public double Height => MaxY - MinY;
}

internal enum GraphMode
{
	XY,
	Polar,
}

// Graph state shared by the sidebar graph and the Graph window (SPEC 4)
internal sealed class GraphSettings
{
	public GraphMode Mode { get; set; } = GraphMode.XY;
	public bool ShowGrid { get; set; } = true;
	public bool Trace { get; set; } = true;    // Graph window trace (G2), saved (owner 2026-11-07)
	// Selected function (Edit, Delete, trace); Shown: every function drawn (G3)
	public string? Function { get; set; }
	public List<string> Shown { get; } = new();

	// One-parameter functions in definition order; a function's colour is its place here
	public IReadOnlyList<string> Graphable { get; set; } = Array.Empty<string>();

	public int ColourIndex(string name)
	{
		for (int i = 0; i < Graphable.Count; i++)
		{
			if (string.Equals(Graphable[i], name, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}
		return -1;
	}

	public bool IsShown(string name)
	{
		return Shown.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
	}

	public void SetShown(string name, bool shown)
	{
		Shown.RemoveAll(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
		if (shown)
		{
			Shown.Add(name);
		}
	}

	// XY range
	public double MinX { get; set; } = -10;
	public double MaxX { get; set; } = 10;
	public double GridX { get; set; } = 1;
	public double MinY { get; set; } = -10;
	public double MaxY { get; set; } = 10;
	public double GridY { get; set; } = 1;

	// Polar range; GridT and MinR are fixed, as in the original
	public double MinT { get; set; } = -Math.PI;
	public double MaxT { get; set; } = Math.PI;
	public double GridT { get; } = 1;
	public double MinR { get; } = 0;
	public double MaxR { get; set; } = 10;
	public double GridR { get; set; } = 1;

	public event EventHandler? Changed;

	public void NotifyChanged()
	{
		Changed?.Invoke(this, EventArgs.Empty);
	}

	// Home: the start-up range of the current mode
	public void ResetRange()
	{
		var d = new GraphSettings();
		if (Mode == GraphMode.XY)
		{
			(MinX, MaxX, GridX, MinY, MaxY, GridY) = (d.MinX, d.MaxX, d.GridX, d.MinY, d.MaxY, d.GridY);
		}
		else
		{
			(MinT, MaxT, MaxR, GridR) = (d.MinT, d.MaxT, d.MaxR, d.GridR);
		}
	}

	// The world rectangle shown: XY range, or a square around the pole for polar
	public WorldRect View => Mode == GraphMode.XY
		? new WorldRect(MinX, MaxX, MinY, MaxY)
		: new WorldRect(-MaxR, MaxR, -MaxR, MaxR);
}

internal static class PlotMath
{
	public const int MaxGridLines = 200;
	public const double PolarSpokeDegrees = 15;    // [DEF] spoke spacing; the original fixed it too

	// Checks one min/max/increment triple; returns an error message or null
	public static string? CheckRange(string axis, double min, double max, double step)
	{
		if (double.IsNaN(min) || double.IsNaN(max) || double.IsInfinity(min) || double.IsInfinity(max))
		{
			return $"{axis}: enter numbers";
		}
		if (min >= max)
		{
			return $"{axis} Min must be less than {axis} Max";
		}
		if (!(step > 0))
		{
			return $"{axis} Increment must be greater than 0";
		}
		return null;
	}

	public static PointF ToScreen(double x, double y, WorldRect world, RectangleF screen)
	{
		float sx = (float)(screen.Left + (x - world.MinX) / world.Width * screen.Width);
		float sy = (float)(screen.Bottom - (y - world.MinY) / world.Height * screen.Height);
		return new PointF(sx, sy);
	}

	// Multiples of step inside [min, max]; empty when they would be too dense
	public static IReadOnlyList<double> GridLines(double min, double max, double step)
	{
		var lines = new List<double>();
		if (!(step > 0) || (max - min) / step > MaxGridLines)
		{
			return lines;
		}
		for (double k = Math.Ceiling(min / step); k * step <= max + step * 1e-9; k++)
		{
			lines.Add(k * step);
		}
		return lines;
	}

	// Splits plotted points into drawable runs: breaks at gaps and at jumps
	// larger than two view heights (poles such as tan(pi/2))
	public static IReadOnlyList<IReadOnlyList<(double X, double Y)>> Segments(
		IReadOnlyList<(double X, double Y)?> points, WorldRect world)
	{
		var segments = new List<IReadOnlyList<(double X, double Y)>>();
		var current = new List<(double X, double Y)>();
		double jumpLimit = world.Height * 2;
		foreach (var point in points)
		{
			bool breakHere = point == null
				|| (current.Count > 0 && Math.Abs(point.Value.Y - current[^1].Y) > jumpLimit);
			if (breakHere && current.Count > 0)
			{
				segments.Add(current);
				current = new List<(double X, double Y)>();
			}
			if (point != null)
			{
				current.Add(point.Value);
			}
		}
		if (current.Count > 0)
		{
			segments.Add(current);
		}
		return segments;
	}

	//==================================================================
	//  Graph window navigation (design/graph_plan.md, G1)
	//==================================================================
	public const double MinSpan = 1e-9;
	public const double MaxSpan = 1e12;

	// A round step (1, 2 or 5 x 10^n) giving at most `count` intervals over span
	public static double NiceStep(double span, int count)
	{
		if (!(span > 0) || count < 1)
		{
			return 1;
		}
		double raw = span / count;
		double power = Math.Pow(10, Math.Floor(Math.Log10(raw)));
		foreach (double m in new[] { 1.0, 2, 5, 10 })
		{
			if (m * power >= raw)
			{
				return m * power;
			}
		}
		return 10 * power;
	}

	// Tick label text with just enough decimals for the step; no "-0"
	public static string FormatTick(double value, double step)
	{
		int decimals = Math.Clamp((int)Math.Ceiling(-Math.Log10(step) - 1e-9), 0, 12);
		if (Math.Abs(value) < step * 1e-6)
		{
			value = 0;
		}
		double size = Math.Max(Math.Abs(value), step);
		if (size >= 1e7 || size < 1e-5)
		{
			return value.ToString("0.###E+0", System.Globalization.CultureInfo.InvariantCulture);
		}
		return value.ToString("F" + decimals, System.Globalization.CultureInfo.InvariantCulture);
	}

	// Scales [min, max] by factor around `at`; null when the span would leave limits
	public static (double Min, double Max)? Zoom(double min, double max, double at, double factor)
	{
		double span = (max - min) * factor;
		if (!(span >= MinSpan && span <= MaxSpan))
		{
			return null;
		}
		double newMin = at - (at - min) * factor;
		return (newMin, newMin + span);
	}

	// Y range that shows the points: 2nd-98th percentile (cuts poles), 5% margin
	public static (double Min, double Max)? FitRange(IEnumerable<double> values)
	{
		var finite = values.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).Order().ToList();
		if (finite.Count == 0)
		{
			return null;
		}
		int cut = finite.Count >= 50 ? finite.Count / 50 : 0;
		double low = finite[cut];
		double high = finite[finite.Count - 1 - cut];
		if (high - low < MinSpan * 10)
		{
			double pad = Math.Max(1, Math.Abs(low) * 0.1);
			return (low - pad, high + pad);
		}
		double margin = (high - low) * 0.05;
		return (low - margin, high + margin);
	}

	// An angle moved by whole turns into [min, max], or null when no turn fits (polar trace)
	public static double? AngleInRange(double angle, double min, double max)
	{
		double turn = 2 * Math.PI;
		double shifted = angle + Math.Ceiling((min - angle) / turn) * turn;
		return shifted <= max ? shifted : null;
	}

	// Screen coordinates are floats; keep far-off points from overflowing GDI+
	public static PointF Clamp(PointF point, RectangleF screen)
	{
		float limit = Math.Max(screen.Width, screen.Height) * 20;
		return new PointF(
			Math.Clamp(point.X, screen.Left - limit, screen.Right + limit),
			Math.Clamp(point.Y, screen.Top - limit, screen.Bottom + limit));
	}
}
