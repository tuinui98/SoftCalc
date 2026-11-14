// BoardRings.cs - How a dartboard shares its cards out over rings (design/random_plan.md R7)
//
// Owner 2026-11-14: when sectors would be narrower than 15 degrees, the
// board gets rings, at most 4. Every card's area stays its share of the
// chances (equal weights: equal areas). The rings start equally wide;
// each boundary then moves (at most 5% of its radius) so a whole number
// of cards fills each ring. The most important cards (the heaviest, then
// the first listed) go in the middle. The fewest rings whose sectors are
// all 15 degrees or more are used; with 4 rings the outer ones may be
// narrower (77 cards: 7/15/23/32, outer 11.2 degrees).

namespace SoftCalc.Logic;

internal static class BoardRings
{
	public const double Hub = 1.0 / 14;    // the bull's radius, the board's radius being 1
	public const int MaxRings = 4;
	public const double MinAngle = 15;
	public const double MaxShift = 0.05;

	// A ring: its cards (indices, in their listed order) and its radii (0-1)
	public sealed record Ring(List<int> Cards, double Inner, double Outer);

	public static List<Ring> Layout(IReadOnlyList<double> weights)
	{
		int n = weights.Count;
		var order = Enumerable.Range(0, n).OrderByDescending(i => weights[i]).ToList();    // stable: ties keep the listed order
		var prefix = new double[n + 1];
		for (int c = 0; c < n; c++)
		{
			prefix[c + 1] = prefix[c] + weights[order[c]];
		}
		List<Ring>? wide = null, last = null;
		for (int k = 1; k <= Math.Min(MaxRings, n); k++)
		{
			var (rings, shift) = Try(weights, order, prefix, k);
			bool roomy = rings.All(r => 360 * r.Cards.Min(i => weights[i]) / r.Cards.Sum(i => weights[i]) >= MinAngle - 1e-9);
			if (roomy && shift <= MaxShift + 1e-9)
			{
				return rings;
			}
			if (roomy)
			{
				wide ??= rings;
			}
			last = rings;
		}
		return wide ?? last ?? new List<Ring>();
	}

	// k rings: each boundary at the whole number of cards nearest the even split; the largest shift (fraction)
	private static (List<Ring> Rings, double Shift) Try(IReadOnlyList<double> weights, List<int> order, double[] prefix, int k)
	{
		int n = order.Count;
		double Radius(int cards) => Math.Sqrt(Hub * Hub + (1 - Hub * Hub) * prefix[cards] / prefix[n]);
		var cut = new List<int> { 0 };
		double shift = 0;
		for (int j = 1; j < k; j++)
		{
			double nominal = Hub + (1 - Hub) * j / k;
			int best = cut[^1] + 1;
			for (int c = best; c <= n - (k - j); c++)
			{
				if (Math.Abs(Radius(c) - nominal) < Math.Abs(Radius(best) - nominal))
				{
					best = c;
				}
			}
			shift = Math.Max(shift, Math.Abs(Radius(best) - nominal) / nominal);
			cut.Add(best);
		}
		cut.Add(n);
		var rings = new List<Ring>();
		for (int j = 0; j < k; j++)
		{
			var cards = order.GetRange(cut[j], cut[j + 1] - cut[j]).Order().ToList();
			rings.Add(new Ring(cards, j == 0 ? Hub : Radius(cut[j]), j == k - 1 ? 1 : Radius(cut[j + 1])));
		}
		return (rings, shift);
	}
}
