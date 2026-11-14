// SpinStage.cs - The Spin wheel theme of a draw tab (design/random_plan.md R7)
//
// Owner 2026-11-14: a wheel of up to three rings, a ring for each card of
// the line; the rings turn in turn clockwise, anticlockwise, clockwise and
// stop one by one from the outside in. A marker at the top points down and
// one on the hub points up: each ring's card is the one on that line. Up to
// 3 cards fit one wheel; more cards spin more wheels in turn (4-6 cards: 2
// wheels). Sectors are sized by chance. The cards are drawn first; the
// rings only turn towards them (System.Random for the show only).

using System.Drawing.Drawing2D;
using SoftCalc.Logic;

namespace SoftCalc.UI;

internal sealed class SpinStage : DrawStage
{
	private const int MaxRings = 3;
	private const double Stagger = 0.7;    // seconds between the rings stopping
	private const double Pause = 0.6;    // seconds at rest before the next wheel
	private static readonly Color[] Pastels =
	{
		Color.FromArgb(255, 224, 138), Color.FromArgb(154, 208, 236), Color.FromArgb(244, 166, 166),
		Color.FromArgb(181, 227, 161), Color.FromArgb(215, 184, 243), Color.FromArgb(255, 196, 140),
	};
	private readonly List<(int First, int Count)> _wheels = new();
	private readonly List<double> _spins = new();    // seconds the outer ring of each wheel turns (3-5 s, random)
	private readonly List<double> _finals = new();    // each ring's turn at rest, degrees clockwise (negative: anticlockwise)

	protected override double Duration => Enumerable.Range(0, _wheels.Count).Sum(Length) + 0.2;

	private double Length(int wheel)
	{
		return _spins[wheel] + Stagger * (_wheels[wheel].Count - 1) + (wheel < _wheels.Count - 1 ? Pause : 0);
	}

	protected override void Prepare()
	{
		_wheels.Clear();
		_spins.Clear();
		_finals.Clear();
		int count = Steps.Count, wheels = (count + MaxRings - 1) / MaxRings;
		// cards shared out evenly: 4 cards are 2 + 2, 5 are 3 + 2
		for (int w = 0, first = 0; w < wheels; w++)
		{
			int rings = (count - first + (wheels - w) - 1) / (wheels - w);
			_wheels.Add((first, rings));
			_spins.Add(3 + 2 * Look.NextDouble());
			first += rings;
		}
		for (int i = 0; i < count; i++)
		{
			var (start, sweep) = DartStage.Sector(Steps[i], Steps[i].Drawn);
			double hit = start + sweep * (0.15 + 0.7 * Look.NextDouble());
			int turns = 3 + Look.Next(3);
			// the hit spot ends at the top: clockwise rings turn on to it, anticlockwise ones back
			_finals.Add(Clockwise(i) ? 360 * turns + Mod(-hit) : -(360 * turns + Mod(hit)));
		}
	}

	private int Ring(int pick)
	{
		return pick - _wheels.First(w => pick >= w.First && pick < w.First + w.Count).First;
	}

	private bool Clockwise(int pick)
	{
		return Ring(pick) % 2 == 0;
	}

	private static double Mod(double degrees)
	{
		return (degrees % 360 + 360) % 360;
	}

	// The wheel in play and the seconds into it; at rest the last wheel, MaxValue
	private (int Wheel, double Time) Phase()
	{
		int last = _wheels.Count - 1;
		if (Elapsed == double.MaxValue)
		{
			return (last, double.MaxValue);
		}
		double t = Elapsed;
		for (int w = 0; w < _wheels.Count; w++)
		{
			if (t < Length(w) || w == last)
			{
				return (w, t);
			}
			t -= Length(w);
		}
		return (last, double.MaxValue);
	}

	private double StopTime(int pick)
	{
		int wheel = _wheels.FindIndex(w => pick >= w.First && pick < w.First + w.Count);
		return _spins[wheel] + Stagger * Ring(pick);
	}

	private double Angle(int pick, double t)
	{
		return t == double.MaxValue ? _finals[pick] : _finals[pick] * EaseOut(t / StopTime(pick));
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		var g = e.Graphics;
		g.Clear(SystemColors.Control);
		g.SmoothingMode = SmoothingMode.AntiAlias;
		if (Steps.Count == 0)
		{
			TextRenderer.DrawText(g, "Press Draw", Font, ClientRectangle, SystemColors.GrayText,
				TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
			return;
		}
		var (wheel, t) = Phase();
		var (first, rings) = _wheels[wheel];

		//==================================================================
		//  BLOCK 1 — Results so far : on the left, when there are several cards
		//==================================================================
		int listWidth = Steps.Count > 1 ? Math.Min(200, ClientSize.Width / 3) : 0;
		if (listWidth > 0)
		{
			var done = Enumerable.Range(0, Steps.Count).Where(i => i < first || (i < first + rings && t >= StopTime(i)))
				.Select(i => Steps[i].Drawn.Card.Shown(Language));
			string head = _wheels.Count > 1 ? $"Wheel {wheel + 1} of {_wheels.Count}\n\n" : "";
			TextRenderer.DrawText(g, head + string.Join("\n", done), Font, new Rectangle(8, 8, listWidth - 8, ClientSize.Height - 16),
				SystemColors.ControlText, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
		}
		// end BLOCK 1

		//==================================================================
		//  BLOCK 2 — Rings : outside in, the stopped ones with their card framed
		//==================================================================
		int diameter = Math.Min(ClientSize.Width - listWidth - 16, ClientSize.Height - 30);
		if (diameter < 60)
		{
			return;
		}
		float radius = diameter / 2f;
		var centre = new PointF(listWidth + (ClientSize.Width - listWidth) / 2f, 18 + radius);
		float hub = Math.Max(10, radius * 0.14f), band = (radius - hub) / rings;
		using var labelFont = new Font(Font.FontFamily, 8f);
		for (int k = 0; k < rings; k++)
		{
			int pick = first + k;
			float outer = radius - k * band;
			DrawRing(g, Steps[pick], centre, outer, outer - band, Angle(pick, t), t >= StopTime(pick), labelFont);
		}
		using (var rim = new Pen(Color.FromArgb(60, 60, 60), 2f))
		{
			for (int k = 0; k <= rings; k++)
			{
				float r = radius - k * band;
				g.DrawEllipse(rim, centre.X - r, centre.Y - r, 2 * r, 2 * r);
			}
		}
		// end BLOCK 2

		//==================================================================
		//  BLOCK 3 — Hub and markers : the top one flicks as the outer ring's edges pass
		//==================================================================
		using (var hubFill = new SolidBrush(Color.FromArgb(70, 70, 70)))
		{
			g.FillEllipse(hubFill, centre.X - hub, centre.Y - hub, 2 * hub, 2 * hub);
		}
		using var marker = new SolidBrush(Color.Firebrick);
		g.FillPolygon(marker, new[] { new PointF(centre.X - 5, centre.Y - hub + 6), new PointF(centre.X + 5, centre.Y - hub + 6),
			new PointF(centre.X, centre.Y - hub - 9) });
		float top = centre.Y - radius;
		var state = g.Save();
		g.TranslateTransform(centre.X, top - 14);
		g.RotateTransform((float)Flick(first, t));
		g.FillPolygon(marker, new[] { new PointF(-9, 0), new PointF(9, 0), new PointF(0, 20) });
		g.Restore(state);
		// end BLOCK 3

		//==================================================================
		//  BLOCK 4 — One card at rest : its name over the hub
		//==================================================================
		if (Steps.Count == 1 && t == double.MaxValue)
		{
			string name = Steps[0].Drawn.Card.Shown(Language);
			using var big = new Font(Font.FontFamily, 12f, FontStyle.Bold);
			var size = TextRenderer.MeasureText(name, big);
			var label = new Rectangle((int)centre.X - size.Width / 2 - 4, (int)(centre.Y + hub + 6), size.Width + 8, size.Height + 2);
			using var back = new SolidBrush(Color.FromArgb(230, SystemColors.Window));
			g.FillRectangle(back, label);
			TextRenderer.DrawText(g, name, big, label, SuitColour(Steps[0].Drawn.Card, SystemColors.WindowText),
				TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
		}
		// end BLOCK 4
	}

	// The top marker tilts when a sector edge of the turning outer ring is under it
	private double Flick(int pick, double t)
	{
		if (t == double.MaxValue || t >= StopTime(pick))
		{
			return 0;
		}
		var step = Steps[pick];
		double total = step.Live.Sum(p => step.Weights ? p.Card.Weight : 1), edge = Angle(pick, t), nearest = 360;
		foreach (var p in step.Live)
		{
			double d = Mod(edge);
			nearest = Math.Min(nearest, Math.Min(d, 360 - d));
			edge += 360.0 * (step.Weights ? p.Card.Weight : 1) / total;
		}
		return step.Live.Count < 2 || nearest >= 5 ? 0 : (5 - nearest) * 5 * (Clockwise(pick) ? -1 : 1);
	}

	private void DrawRing(Graphics g, StageStep step, PointF centre, float outer, float inner, double turn, bool stopped, Font font)
	{
		double total = step.Live.Sum(p => step.Weights ? p.Card.Weight : 1);
		double start = turn;
		bool borders = step.Live.Count <= 200;
		GraphicsPath? won = null;
		int index = 0;
		foreach (var p in step.Live)
		{
			double sweep = 360.0 * (step.Weights ? p.Card.Weight : 1) / total;
			var colour = p.Card.Color != null ? ColorTranslator.FromHtml(p.Card.Color) : Pastels[index % Pastels.Length];
			if (p.Card.Color == null && index == step.Live.Count - 1 && index % Pastels.Length == 0 && index > 0)
			{
				colour = Pastels[1];    // the last sector never matches the first one beside it
			}
			var path = RingPath(centre, outer, inner, start, sweep, step.Live.Count == 1);
			using (var fill = new SolidBrush(colour))
			{
				g.FillPath(fill, path);
			}
			if (borders && step.Live.Count > 1)
			{
				g.DrawPath(Pens.Silver, path);
			}
			if (stopped && p.Key == step.Drawn.Key)
			{
				won = path;
			}
			else
			{
				path.Dispose();
			}
			RingLabel(g, centre, outer, inner, start, sweep, p.Card, colour, font);
			start += sweep;
			index++;
		}
		if (won != null)
		{
			using var frame = new Pen(Theme.Series(0), 3f);
			g.DrawPath(frame, won);
			won.Dispose();
		}
	}
}
