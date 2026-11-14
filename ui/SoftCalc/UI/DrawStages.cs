// DrawStages.cs - How a draw tab shows its result: slot machine reels or a dartboard (design/random_plan.md R7)
//
// Owner 2026-11-14: effects while drawing. The cards are drawn first;
// the effect then plays towards them (about 1-2 s), so it never changes
// what comes up. A click skips to the end. Slot machine: a reel for each
// step, stopping one by one from the left. Dartboard: a board for each
// step in turn, a sector for each card sized by its chance, and a dart
// that lands in the drawn card's sector. The filler cards and the spot
// where the dart lands are only for show (System.Random).

using System.Diagnostics;
using System.Drawing.Drawing2D;
using SoftCalc.Logic;

namespace SoftCalc.UI;

// One step's part of a draw: the cards it could give and the one it gave; Row: its line on the slot machine
internal sealed record StageStep(List<PoolCard> Live, bool Weights, PoolCard Drawn, int Row = 0);

internal abstract class DrawStage : Control
{
	protected static readonly Random Look = new();
	private bool _skip;
	protected List<StageStep> Steps { get; private set; } = new();
	protected double Elapsed { get; private set; } = double.MaxValue;    // seconds into the effect; MaxValue: at rest
	[System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
	public CardPictures? Pictures { get; set; }

	[System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
	public string Language { get; set; } = "en";

	protected DrawStage()
	{
		SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
			| ControlStyles.UserPaint, true);
		Click += (_, _) => _skip = true;
	}

	protected abstract double Duration { get; }

	protected virtual void Prepare()
	{
	}

	// The result at rest, no effect
	public void ShowStill(List<StageStep> steps)
	{
		Steps = steps;
		Prepare();
		Elapsed = double.MaxValue;
		Invalidate();
	}

	public void Clear()
	{
		ShowStill(new List<StageStep>());
	}

	// Plays the effect towards the drawn cards; ends early on a click
	public async Task PlayAsync(List<StageStep> steps)
	{
		Steps = steps;
		Prepare();
		_skip = false;
		var clock = Stopwatch.StartNew();
		while (!_skip && !IsDisposed && clock.Elapsed.TotalSeconds < Duration)
		{
			Elapsed = clock.Elapsed.TotalSeconds;
			Invalidate();
			await Task.Delay(15);
		}
		Elapsed = double.MaxValue;
		if (!IsDisposed)
		{
			Invalidate();
		}
	}

	protected static double EaseOut(double t)
	{
		t = Math.Clamp(t, 0, 1);
		return 1 - Math.Pow(1 - t, 3);
	}

	// A card's face in a box: its picture (if any) above its name
	protected void DrawFace(Graphics g, Card card, Rectangle box, Font font, Color text)
	{
		var picture = Pictures?.For(card);
		var textBox = box;
		if (picture != null && box.Height > 50)
		{
			int side = Math.Min(box.Width - 8, box.Height - font.Height * 2 - 8);
			if (side > 16)
			{
				double scale = Math.Min((double)side / picture.Width, (double)side / picture.Height);
				int w = (int)(picture.Width * scale), h = (int)(picture.Height * scale);
				g.DrawImage(picture, box.Left + (box.Width - w) / 2, box.Top + 4 + (side - h) / 2, w, h);
				textBox = new Rectangle(box.Left, box.Top + side + 4, box.Width, box.Height - side - 4);
			}
		}
		TextRenderer.DrawText(g, card.Shown(Language), font, textBox, SuitColour(card, text),
			TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis
			| TextFormatFlags.NoPrefix | TextFormatFlags.PreserveGraphicsClipping);
	}

	// A sector of a ring between two radii, angles clockwise from the top; whole: the full ring
	protected static GraphicsPath RingPath(PointF centre, float outer, float inner, double start, double sweep, bool whole)
	{
		var outerBox = new RectangleF(centre.X - outer, centre.Y - outer, 2 * outer, 2 * outer);
		var innerBox = new RectangleF(centre.X - inner, centre.Y - inner, 2 * inner, 2 * inner);
		var path = new GraphicsPath();
		if (whole)
		{
			path.AddEllipse(outerBox);
			path.AddEllipse(innerBox);
		}
		else
		{
			path.AddArc(outerBox, (float)(start - 90), (float)sweep);
			path.AddArc(innerBox, (float)(start - 90 + sweep), (float)-sweep);
			path.CloseFigure();
		}
		return path;
	}

	// A card's name in its sector where there is room: along the radius in a wide ring, along the arc in a narrow one
	protected void RingLabel(Graphics g, PointF centre, float outer, float inner, double start, double sweep, Card card, Color back, Font font)
	{
		float middle = (outer + inner) / 2;
		double arc = sweep * Math.PI / 180 * middle, across = outer - inner;
		bool along = arc > across * 1.3;
		if (!(along ? across >= 14 && arc >= 24 : arc >= 13 && across >= 24))
		{
			return;
		}
		double degrees = start + sweep / 2 - 90;
		double radians = degrees * Math.PI / 180;
		var state = g.Save();
		g.TranslateTransform(centre.X + (float)(Math.Cos(radians) * middle), centre.Y + (float)(Math.Sin(radians) * middle));
		double rotate = along ? degrees + 90 : degrees;
		double facing = (rotate % 360 + 360) % 360;
		g.RotateTransform((float)(facing > 90 && facing < 270 ? rotate + 180 : rotate));
		string name = card.Shown(Language);
		int room = Math.Max(3, (int)(((along ? arc : across) - 6) / 6));
		name = name.Length > room ? name[..(room - 1)] + "…" : name;
		var size = g.MeasureString(name, font);
		using var text = new SolidBrush(SuitColour(card, back.GetBrightness() > 0.55 ? Color.Black : Color.White));
		g.DrawString(name, font, text, -size.Width / 2, -size.Height / 2);
		g.Restore(state);
	}

	// Hearts and diamonds in red
	protected static Color SuitColour(Card card, Color text)
	{
		return card.Name.EndsWith('♥') || card.Name.EndsWith('♦') || card.Name == "Joker (red)" ? Color.FromArgb(205, 40, 40) : text;
	}
}

//==================================================================
//  Slot machine
//==================================================================
internal sealed class SlotStage : DrawStage
{
	private const int Filler = 24;    // cards that roll past before the drawn one
	private readonly List<List<Card>> _reels = new();

	protected override double Duration => 1.0 + 0.35 * Math.Max(0, Steps.Count - 1);

	protected override void Prepare()
	{
		_reels.Clear();
		foreach (var step in Steps)
		{
			var reel = Enumerable.Range(0, Filler).Select(_ => step.Live[Look.Next(step.Live.Count)].Card).ToList();
			reel.Add(step.Drawn.Card);
			reel.Add(step.Live[Look.Next(step.Live.Count)].Card);    // the one shown below the drawn card
			_reels.Add(reel);
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		var g = e.Graphics;
		g.Clear(SystemColors.Control);
		if (Steps.Count == 0)
		{
			TextRenderer.DrawText(g, "Press Draw", Font, ClientRectangle, SystemColors.GrayText,
				TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
			return;
		}
		// owner 2026-11-14: several lines are rows of reels, top to bottom, each stopping left to right
		int gap = 8, rows = Steps.Max(s => s.Row) + 1;
		int height = (ClientSize.Height - gap * (rows + 1)) / rows;
		using var big = new Font(Font.FontFamily, Math.Max(9f, Math.Min(15f, height / 8f)), FontStyle.Bold);
		using var small = new Font(Font.FontFamily, Math.Min(9f, Math.Max(7f, height / 10f)));
		for (int i = 0; i < Steps.Count; i++)
		{
			int row = Steps[i].Row, count = Steps.Count(s => s.Row == row), column = Steps.Take(i).Count(s => s.Row == row);
			int width = (ClientSize.Width - gap * (count + 1)) / count;
			var reel = new Rectangle(gap + column * (width + gap), gap + row * (height + gap), width, height);
			using (var window = new SolidBrush(SystemColors.Window))
			{
				g.FillRectangle(window, reel);
			}
			// position along the reel: 0 = first filler, Filler = the drawn card
			double stop = 0.7 + 0.35 * i;
			double position = Elapsed == double.MaxValue ? Filler : Filler * EaseOut(Elapsed / stop);
			int at = (int)Math.Floor(position);
			double fraction = position - at;
			int cell = height / 2;
			g.SetClip(reel);
			for (int k = -1; k <= 1; k++)
			{
				int index = Math.Clamp(at + k, 0, _reels[i].Count - 1);
				int top = reel.Top + cell / 2 + (int)((k - fraction) * cell);
				var box = new Rectangle(reel.Left + 2, top, reel.Width - 4, cell);
				// at rest the cards above and below are small and faint; while rolling all look alike
				bool rest = Elapsed == double.MaxValue;
				if (rest && k != 0)
				{
					var edge = k < 0 ? new Rectangle(box.Left, reel.Top, box.Width, cell / 2)
						: new Rectangle(box.Left, reel.Top + cell / 2 + cell, box.Width, reel.Bottom - (reel.Top + cell / 2 + cell));
					TextRenderer.DrawText(g, _reels[i][index].Shown(Language), small, edge, SystemColors.GrayText,
						TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix
						| TextFormatFlags.PreserveGraphicsClipping);
					continue;
				}
				DrawFace(g, _reels[i][index], box, big, rest ? SystemColors.WindowText : SystemColors.GrayText);
			}
			g.ResetClip();
			// the pay line
			using var line = new Pen(Theme.Series(0), 2f);
			g.DrawRectangle(line, reel.Left + 1, reel.Top + cell / 2, reel.Width - 3, cell);
			ControlPaint.DrawBorder(g, reel, Theme.PlotAxis, ButtonBorderStyle.Solid);
		}
	}
}

//==================================================================
//  Dartboard
//==================================================================
internal sealed class DartStage : DrawStage
{
	private const double Flight = 0.25;    // seconds for the dart to fly to a board (owner 2026-11-14)
	private const double Pause = 0.5;    // seconds at rest before the next board
	private readonly List<PointF> _hits = new();    // where each dart lands on the board: angle (degrees) and radius (0-1)
	private readonly List<double> _spins = new();    // seconds each board spins (owner 2026-11-14: 2-4 s, random)
	private readonly List<double> _throws = new();    // seconds into the spin the dart is thrown (owner: 1-3 s, may be before the board stops)
	private readonly List<double> _turns = new();    // degrees each board has turned when it stops
	private readonly List<List<BoardRings.Ring>> _rings = new();    // each board's rings (owner 2026-11-14: over 24 cards)

	protected override double Duration => Enumerable.Range(0, Steps.Count).Sum(Length) + 0.2;

	// One board's time: its spin, or the dart's flight if that ends later, and a pause between boards
	private double Length(int board)
	{
		return Math.Max(_spins[board], _throws[board] + Flight) + (board < Steps.Count - 1 ? Pause : 0);
	}

	protected override void Prepare()
	{
		_hits.Clear();
		_spins.Clear();
		_turns.Clear();
		_rings.Clear();
		foreach (var step in Steps)
		{
			var rings = BoardRings.Layout(step.Live.Select(p => Weight(step, p)).ToList());
			_rings.Add(rings);
			// the dart's spot: inside the drawn card's sector of its ring, away from the edges
			int drawn = Math.Max(0, step.Live.FindIndex(p => p.Key == step.Drawn.Key));
			var ring = rings.FirstOrDefault(r => r.Cards.Contains(drawn)) ?? new BoardRings.Ring(new List<int> { drawn }, BoardRings.Hub, 1);
			double ringTotal = ring.Cards.Sum(i => Weight(step, step.Live[i]));
			double start = 360 * ring.Cards.TakeWhile(i => i != drawn).Sum(i => Weight(step, step.Live[i])) / ringTotal;
			double sweep = 360 * Weight(step, step.Live[drawn]) / ringTotal;
			float angle = (float)(start + sweep * (0.15 + 0.7 * Look.NextDouble()));
			double inner = ring.Inner * ring.Inner, outer = ring.Outer * ring.Outer;
			_hits.Add(new PointF(angle, (float)Math.Sqrt(inner + (outer - inner) * (0.2 + 0.6 * Look.NextDouble()))));
			// owner 2026-11-14: the board spins 2-4 s; the dart is thrown 1-3 s after it starts, each random
			_spins.Add(2 + 2 * Look.NextDouble());
			_throws.Add(1 + 2 * Look.NextDouble());
			_turns.Add(360 * (_spins[^1] + Look.NextDouble()));    // about a turn a second, slowing down
		}
	}

	// The board in play, how far it has turned (degrees), how far the dart has flown (0-1),
	// the turn at the moment the dart lands, and whether the board has stopped
	private (int Board, double Turn, double Flown, double Landing, bool Still) Phase()
	{
		int last = Steps.Count - 1;
		if (Elapsed == double.MaxValue)
		{
			return (last, _turns[last], 1, _turns[last], true);
		}
		double t = Elapsed;
		for (int i = 0; i < Steps.Count; i++)
		{
			if (t < Length(i) || i == last)
			{
				double landing = Turn(i, _throws[i] + Flight);
				double flown = t < _throws[i] ? 0 : Math.Clamp((t - _throws[i]) / Flight, 0, 1);
				return (i, Turn(i, t), flown, landing, t >= _spins[i]);
			}
			t -= Length(i);
		}
		return (last, _turns[last], 1, _turns[last], true);
	}

	// How far board i has turned t seconds into its spin
	private double Turn(int board, double t)
	{
		return _turns[board] * EaseOut(t / _spins[board]);
	}

	private static double Weight(StageStep step, PoolCard card)
	{
		return step.Weights ? card.Card.Weight : 1;
	}

	// The angles (degrees, clockwise from the top) of a card's sector on a one-ring wheel
	internal static (double Start, double Sweep) Sector(StageStep step, PoolCard card)
	{
		double total = step.Live.Sum(p => step.Weights ? p.Card.Weight : 1);
		double start = 0;
		foreach (var p in step.Live)
		{
			double sweep = 360.0 * (step.Weights ? p.Card.Weight : 1) / total;
			if (p.Key == card.Key)
			{
				return (start, sweep);
			}
			start += sweep;
		}
		return (0, 360);
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
		// the board in play: during the effect the one the dart flies to, at rest the last
		var (board, turn, flight, landing, still) = Phase();
		var step = Steps[board];

		// the results so far, on the left, when there are several steps
		int listWidth = Steps.Count > 1 ? Math.Min(200, ClientSize.Width / 3) : 0;
		if (listWidth > 0)
		{
			var done = Steps.Take(flight >= 1 && still ? board + 1 : board).Select(s => s.Drawn.Card.Shown(Language));
			TextRenderer.DrawText(g, $"Step {board + 1} of {Steps.Count}\n\n" + string.Join("\n", done), Font,
				new Rectangle(8, 8, listWidth - 8, ClientSize.Height - 16), SystemColors.ControlText,
				TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
		}

		int diameter = Math.Min(ClientSize.Width - listWidth - 16, ClientSize.Height - 16);
		if (diameter < 40)
		{
			return;
		}
		var rect = new Rectangle(listWidth + (ClientSize.Width - listWidth - diameter) / 2, (ClientSize.Height - diameter) / 2, diameter, diameter);
		DrawBoard(g, board, rect, turn);

		// the dart flies in from the lower right towards where its spot will be when it lands,
		// then sticks and turns with the board (owner 2026-11-14: it may land before the board stops)
		if (flight <= 0)
		{
			return;
		}
		var hit = _hits[board];
		double radians = (hit.X + (flight >= 1 ? turn : landing) - 90) * Math.PI / 180;
		var target = new PointF(rect.Left + rect.Width / 2f + (float)(Math.Cos(radians) * hit.Y * rect.Width / 2),
			rect.Top + rect.Height / 2f + (float)(Math.Sin(radians) * hit.Y * rect.Height / 2));
		double t = EaseOut(flight);
		var from = new PointF(ClientSize.Width + 40, ClientSize.Height + 40);
		var tip = new PointF((float)(from.X + (target.X - from.X) * t), (float)(from.Y + (target.Y - from.Y) * t));
		// owner 2026-11-14: the point goes into the board; the flights spread out behind
		using var point = new Pen(Color.Silver, 1.5f);
		g.DrawLine(point, tip, new PointF(tip.X + 8, tip.Y + 8));
		using var shaft = new Pen(SystemColors.ControlText, 3f) { StartCap = LineCap.Triangle, EndCap = LineCap.Round };
		g.DrawLine(shaft, new PointF(tip.X + 7, tip.Y + 7), new PointF(tip.X + 30, tip.Y + 30));
		using var flights = new SolidBrush(Theme.Series(1));
		g.FillPolygon(flights, new[] { new PointF(tip.X + 22, tip.Y + 22), new PointF(tip.X + 40, tip.Y + 30),
			new PointF(tip.X + 36, tip.Y + 36), new PointF(tip.X + 30, tip.Y + 40) });
		if (flight >= 1 && still)
		{
			g.FillEllipse(Brushes.Black, target.X - 3, target.Y - 3, 6, 6);
			var name = step.Drawn.Card.Shown(Language);
			using var big = new Font(Font.FontFamily, 12f, FontStyle.Bold);
			var size = TextRenderer.MeasureText(name, big);
			var label = new Rectangle((int)target.X - size.Width / 2 - 4, (int)target.Y - size.Height - 10, size.Width + 8, size.Height + 2);
			using var back = new SolidBrush(Color.FromArgb(230, SystemColors.Window));
			g.FillRectangle(back, label);
			TextRenderer.DrawText(g, name, big, label, SuitColour(step.Drawn.Card, SystemColors.WindowText),
				TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
		}
	}

	private void DrawBoard(Graphics g, int board, Rectangle rect, double turn)
	{
		var step = Steps[board];
		var centre = new PointF(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
		float radius = rect.Width / 2f;
		bool borders = step.Live.Count <= 200;
		using var font = new Font(Font.FontFamily, 8f);
		using var wire = new Pen(Color.FromArgb(150, 150, 150), 1.5f);
		int index = 0;
		foreach (var ring in _rings[board])
		{
			float outer = (float)(ring.Outer * radius), inner = (float)(ring.Inner * radius);
			double ringTotal = ring.Cards.Sum(i => Weight(step, step.Live[i]));
			double start = turn % 360;
			foreach (int i in ring.Cards)
			{
				var p = step.Live[i];
				double sweep = 360 * Weight(step, p) / ringTotal;
				var colour = p.Card.Color != null ? ColorTranslator.FromHtml(p.Card.Color) : (index % 4) switch
				{
					0 => Color.FromArgb(40, 40, 40),
					1 => Color.FromArgb(200, 50, 50),
					2 => Color.FromArgb(238, 226, 196),
					_ => Color.FromArgb(40, 140, 70),
				};
				using (var path = RingPath(centre, outer, inner, start, sweep, ring.Cards.Count == 1))
				{
					using (var fill = new SolidBrush(colour))
					{
						g.FillPath(fill, path);
					}
					if (borders && ring.Cards.Count > 1)
					{
						g.DrawPath(Pens.Silver, path);
					}
				}
				RingLabel(g, centre, outer, inner, start, sweep, p.Card, colour, font);
				start += sweep;
				index++;
			}
			index += 1;    // the next ring starts on another colour
			g.DrawEllipse(wire, centre.X - outer, centre.Y - outer, 2 * outer, 2 * outer);
		}
		// the bull and the rim
		float bull = Math.Max(6, radius * (float)BoardRings.Hub);
		g.FillEllipse(Brushes.ForestGreen, centre.X - bull, centre.Y - bull, bull * 2, bull * 2);
		g.FillEllipse(Brushes.Firebrick, centre.X - bull / 2, centre.Y - bull / 2, bull, bull);
		using var rim = new Pen(Color.FromArgb(30, 30, 30), 3f);
		g.DrawEllipse(rim, rect);
	}
}
