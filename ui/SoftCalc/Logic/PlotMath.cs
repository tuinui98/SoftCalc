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
	public string? Function { get; set; }

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

	// Screen coordinates are floats; keep far-off points from overflowing GDI+
	public static PointF Clamp(PointF point, RectangleF screen)
	{
		float limit = Math.Max(screen.Width, screen.Height) * 20;
		return new PointF(
			Math.Clamp(point.X, screen.Left - limit, screen.Right + limit),
			Math.Clamp(point.Y, screen.Top - limit, screen.Bottom + limit));
	}
}
