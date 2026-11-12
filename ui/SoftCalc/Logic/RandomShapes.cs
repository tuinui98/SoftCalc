// RandomShapes.cs - The Advanced random tab's shapes (design/random_plan.md R3)
//
// Each shape draws x from 0 to 1 and has a density for the picture. The
// range maps x as the engine's random_shapes.py does: whole numbers get
// one slot per number (both ends included), decimals are stretched and
// rounded. Insert Command's engine call is built here too. Doubles are
// enough here: the tab shows at most 10 decimal places; typed in Input,
// the same call draws at full precision.

namespace SoftCalc.Logic;

internal enum ShapeKind
{
	Beta,
	Triangle,
	Exponential,
	Peaks,
}

// One shape of the list: P and Q are Beta's a and b, the rate or the gap
internal sealed record ShapeCurve(ShapeKind Kind, decimal P, decimal Q);

internal static class RandomShapes
{
	public const int MaxCount = 10000;    // enough for a histogram that shows the shape
	public const int PeaksSize = 20;    // a + b of each hump, as in the engine
	public const decimal MinStrength = 1, MaxStrength = 10, DefaultStrength = 5;
	public const decimal MinBeta = 0.05m, MaxBeta = 100;    // Custom's a and b

	// The list (owner 2026-11-12): id, name; Strength is used unless noted
	public static readonly (string Id, string Name)[] Shapes =
	{
		("flat", "Flat"),
		("bell", "Bell"),
		("narrow", "Narrow bell"),
		("right", "Right skew"),
		("left", "Left skew"),
		("triangle", "Triangle"),
		("u", "U shape"),
		("peaks", "Two peaks"),
		("rising", "Rising"),
		("falling", "Falling"),
		("decay", "Decay"),
		("growth", "Growth"),
		("custom", "Custom (Beta a, b)"),
	};

	public static bool UsesStrength(string id)
	{
		return id is not ("flat" or "triangle" or "custom");
	}

	// The line under the buttons before Generate: what the shape does
	public static string Hint(string id)
	{
		return id switch
		{
			"flat" => "Every value as likely as any other.",
			"bell" or "narrow" => "Most values near the middle. Strength: how narrow the bell is.",
			"right" => "Small values most often, a long tail to the right. Strength: how far it leans.",
			"left" => "Large values most often, a long tail to the left. Strength: how far it leans.",
			"triangle" => "Rises in a straight line to Peak at, then falls.",
			"u" => "Most values near both ends, few in the middle. Strength: how deep the dip is.",
			"peaks" => "Two humps with a dip between. Strength: how far apart the humps are.",
			"rising" => "The larger, the more often. Strength: how steep.",
			"falling" => "The smaller, the more often. Strength: how steep.",
			"decay" => "Falls away fast from From. Strength: how fast.",
			"growth" => "Climbs fast towards To. Strength: how fast.",
			_ => "Beta(a, b): a and b above 1 give a hump, below 1 pile up at the ends.",
		};
	}

	// The line under the buttons after Generate
	public static string Summary(IReadOnlyList<decimal> values, int places)
	{
		decimal mean = decimal.Round(values.Average(), Math.Max(2, places));
		return $"{values.Count} values: mean {RandomTools.Text(mean)}, lowest {RandomTools.Text(values.Min())}, "
			+ $"highest {RandomTools.Text(values.Max())}";
	}

	// The shape's parameters for a Strength of 1 to 10; 5 gives the
	// pictures the owner chose (Bell Beta(4, 4), Right skew Beta(2, 5), ...)
	public static ShapeCurve Curve(string id, decimal strength, decimal a, decimal b)
	{
		decimal s = Math.Clamp(strength, MinStrength, MaxStrength);
		return id switch
		{
			"bell" => Beta(1 + 0.6m * s, 1 + 0.6m * s),
			"narrow" => Beta(2.4m * s, 2.4m * s),
			"right" => Beta(2, 2 + 0.6m * s),
			"left" => Beta(2 + 0.6m * s, 2),
			"triangle" => new ShapeCurve(ShapeKind.Triangle, 0, 0),
			"u" => Beta((11 - s) / 10, (11 - s) / 10),
			"peaks" => new ShapeCurve(ShapeKind.Peaks, s / 10, 0),
			"rising" => Beta(1 + 0.2m * s, 1),
			"falling" => Beta(1, 1 + 0.2m * s),
			"decay" => new ShapeCurve(ShapeKind.Exponential, 0.8m * s, 0),
			"growth" => new ShapeCurve(ShapeKind.Exponential, -0.8m * s, 0),
			"custom" => Beta(a, b),
			_ => Beta(1, 1),
		};
	}

	private static ShapeCurve Beta(decimal a, decimal b)
	{
		return new ShapeCurve(ShapeKind.Beta, a, b);
	}

	//==================================================================
	//  Drawing values
	//==================================================================
	// Where the triangle's peak sits on 0-1; whole numbers: the middle of its slot
	public static double PeakAt(decimal from, decimal to, decimal peak, bool whole)
	{
		(from, to) = from <= to ? (from, to) : (to, from);
		if (whole)
		{
			return (double)((decimal.Truncate(peak) - decimal.Truncate(from) + 0.5m) / (decimal.Truncate(to) - decimal.Truncate(from) + 1));
		}
		return to == from ? 0.5 : (double)((peak - from) / (to - from));
	}

	// count values from from to to; null and a reason when it cannot be done
	public static (List<decimal>? Values, string? Error) Values(IRandomSource source, ShapeCurve curve, decimal from,
		decimal to, decimal peak, bool whole, int places, int count)
	{
		if (count < 1 || count > MaxCount)
		{
			return (null, $"Count must be 1 to {MaxCount}.");
		}
		if (places < 0 || places > RandomTools.MaxPlaces)
		{
			return (null, $"Decimal places must be 0 to {RandomTools.MaxPlaces}.");
		}
		if (whole)
		{
			(from, to, peak) = (decimal.Truncate(from), decimal.Truncate(to), decimal.Truncate(peak));
		}
		(from, to) = from <= to ? (from, to) : (to, from);
		if (curve.Kind == ShapeKind.Triangle && (peak < from || peak > to))
		{
			return (null, $"Peak at must be from {RandomTools.Text(from)} to {RandomTools.Text(to)}.");
		}
		if (curve.Kind == ShapeKind.Beta && (curve.P <= 0 || curve.Q <= 0))
		{
			return (null, "a and b must be above 0.");
		}
		double peakAt = PeakAt(from, to, peak, whole);
		decimal slots = to - from + 1;
		var values = new List<decimal>(count);
		for (int i = 0; i < count; i++)
		{
			double x = Draw(source, curve, peakAt);
			if (whole)
			{
				values.Add(from + Math.Min(decimal.Floor((decimal)x * slots), slots - 1));
			}
			else
			{
				values.Add(decimal.Round(from + (decimal)x * (to - from), places, MidpointRounding.AwayFromZero));
			}
		}
		return (values, null);
	}

	// One draw on 0 to 1
	public static double Draw(IRandomSource source, ShapeCurve curve, double peakAt)
	{
		switch (curve.Kind)
		{
			case ShapeKind.Triangle:
				double u = Uniform(source);
				return u < peakAt ? Math.Sqrt(u * peakAt) : 1 - Math.Sqrt((1 - u) * (1 - peakAt));
			case ShapeKind.Exponential:
				double rate = (double)curve.P;
				double v = Uniform(source);
				return rate == 0 ? v : -Math.Log(1 - v * (1 - Math.Exp(-rate))) / rate;
			case ShapeKind.Peaks:
				double gap = (double)curve.P;
				double middle = source.Below(2) == 0 ? (1 - gap) / 2 : (1 + gap) / 2;
				return BetaDraw(source, 1 + middle * (PeaksSize - 2), 1 + (1 - middle) * (PeaksSize - 2));
			default:
				return BetaDraw(source, (double)curve.P, (double)curve.Q);
		}
	}

	// Strictly between 0 and 1, 53 random bits
	private static double Uniform(IRandomSource source)
	{
		const ulong top = 1UL << 53;
		return (source.Below(top - 1) + 1) / (double)top;
	}

	private static double BetaDraw(IRandomSource source, double a, double b)
	{
		double x = Gamma(source, a);
		return x / (x + Gamma(source, b));
	}

	// Marsaglia and Tsang (2000); alpha below 1 boosted to alpha + 1
	private static double Gamma(IRandomSource source, double alpha)
	{
		if (alpha < 1)
		{
			return Gamma(source, alpha + 1) * Math.Pow(Uniform(source), 1 / alpha);
		}
		double d = alpha - 1.0 / 3, c = 1 / Math.Sqrt(9 * d);
		while (true)
		{
			double x = Math.Sqrt(-2 * Math.Log(Uniform(source))) * Math.Cos(2 * Math.PI * Uniform(source));
			double v = 1 + c * x;
			if (v <= 0)
			{
				continue;
			}
			v = v * v * v;
			if (Math.Log(Uniform(source)) < x * x / 2 + d - d * v + d * Math.Log(v))
			{
				return d * v;
			}
		}
	}

	//==================================================================
	//  The picture
	//==================================================================
	// The density at x (0 to 1), not scaled
	public static double Density(ShapeCurve curve, double x, double peakAt)
	{
		switch (curve.Kind)
		{
			case ShapeKind.Triangle:
				return x < peakAt ? (peakAt <= 0 ? 0 : x / peakAt) : (peakAt >= 1 ? 0 : (1 - x) / (1 - peakAt));
			case ShapeKind.Exponential:
				return Math.Exp(-(double)curve.P * x);
			case ShapeKind.Peaks:
				// the humps are mirror images, Beta(a, b) and Beta(b, a), so they share a scale
				double a = 1 + (1 - (double)curve.P) / 2 * (PeaksSize - 2), b = PeaksSize - a;
				return BetaShape(x, a, b) + BetaShape(x, b, a);
			default:
				return BetaShape(x, (double)curve.P, (double)curve.Q);
		}
	}

	private static double BetaShape(double x, double a, double b)
	{
		return Math.Pow(x, a - 1) * Math.Pow(1 - x, b - 1);
	}

	// The density at points + 1 places from 0 to 1, scaled so its area is 1;
	// the ends are taken a little inside, where a U shape is infinite
	public static double[] Line(ShapeCurve curve, double peakAt, int points = 200)
	{
		var ys = new double[points + 1];
		for (int i = 0; i <= points; i++)
		{
			double x = Math.Clamp((double)i / points, 0.004, 0.996);
			ys[i] = Density(curve, x, peakAt);
		}
		double area = ys.Average();
		return area > 0 && double.IsFinite(area) ? ys.Select(y => y / area).ToArray() : ys;
	}

	// The results as bars over 0-1, scaled like Line (area 1): one bar per
	// whole number when there are few, else 40
	public static double[] Bars(IReadOnlyList<decimal> values, decimal from, decimal to, bool whole)
	{
		(from, to) = from <= to ? (from, to) : (to, from);
		decimal slots = whole ? to - from + 1 : 0;
		int count = whole && slots <= 60 ? (int)slots : 40;
		var bars = new double[count];
		if (values.Count == 0 || (!whole && to == from))
		{
			return bars;
		}
		foreach (decimal v in values)
		{
			double x = whole ? (double)((v - from + 0.5m) / slots) : (double)((v - from) / (to - from));
			bars[Math.Clamp((int)(x * count), 0, count - 1)]++;
		}
		return bars.Select(b => b / values.Count * count).ToArray();
	}

	//==================================================================
	//  Insert Command
	//==================================================================
	// The engine call that draws one value the same way
	public static string Command(ShapeCurve curve, decimal from, decimal to, decimal peak, bool whole, int places)
	{
		if (whole)
		{
			(from, to, peak) = (decimal.Truncate(from), decimal.Truncate(to), decimal.Truncate(peak));
		}
		(from, to) = from <= to ? (from, to) : (to, from);
		string range = whole ? $"{RandomTools.Plain(from)}, {RandomTools.Plain(to)}"
			: $"{RandomTools.WithPoint(from)}, {RandomTools.Plain(to)}";
		string call = curve.Kind switch
		{
			ShapeKind.Triangle => $"randtri({RandomTools.Plain(peak)}, {range})",
			ShapeKind.Exponential => $"randexp({RandomTools.Plain(curve.P)}, {range})",
			ShapeKind.Peaks => $"randpeaks({RandomTools.Plain(curve.P)}, {range})",
			_ => $"randbeta({RandomTools.Plain(curve.P)}, {RandomTools.Plain(curve.Q)}, {range})",
		};
		return whole ? call : $"round({call}, {places})";
	}
}
