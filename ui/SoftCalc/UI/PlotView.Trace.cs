// PlotView.Trace.cs - Graph window trace: a dot on the line nearest the mouse
//
// The shown line nearest the mouse within SnapPixels is traced
// (graph2_plan G6): the dot snaps to the nearest point on it. A function
// uses that point's x (polar: angle), rounded to what the status line
// shows, and the engine computes the value there at full precision
// (`value_at`). An equation fixes x where the line is flatter, y where
// it is steeper, and the engine solves the other coordinate near the
// point (`equation_at`). One request at a time; while one runs only the
// newest position waits (design/graph_plan.md G2).

using System.ComponentModel;
using System.Globalization;
using SoftCalc.Engine;
using SoftCalc.Logic;

namespace SoftCalc.UI;

// Function: T is x (polar: theta), Step the rounding used to show it, Text
// and Value the result. Equation: X and Y with their texts; Solved names
// the coordinate the engine found ('x' or 'y'), Text is its text.
internal sealed record TracePoint(string Function, double T, double Step, string? Text, double? Value)
{
	public bool IsEquation { get; init; }
	public string? XText { get; init; }
	public string? YText { get; init; }
	public double X { get; init; }
	public double Y { get; init; }
	public char Solved { get; init; }
}

// Where the mouse points: the line and what to ask the engine
internal readonly record struct TraceTarget(string Line, bool IsEquation, double T, double Step, char Axis, string Fixed, double Guess, double Window)
{
	public bool SameAs(TraceTarget other)
	{
		return Line == other.Line && T == other.T && Axis == other.Axis && Fixed == other.Fixed;
	}
}

internal sealed partial class PlotView
{
	private const double ThetaStep = 0.001;    // [DEF] trace angle shown with 3 decimals
	private const int ClickSlop = 3;    // pixels a click may move and still count as a click
	private const float SnapPixels = 20;    // owner 2026-11-08: farther from every line, no dot
	private const float StayPixels = 4;    // keep the traced line until another is nearer by more
	private const float TiePixels = 1;    // the selected line wins a tie
	private const double SolveWindow = 6;    // pixels around the point where an equation's root may be

	private TraceTarget? _traceWanted;
	private TraceTarget? _traceShown;
	private bool _traceBusy;
	private int _traceGeneration;
	private Point _downAt;
	private TracePoint? _menuTrace;

	private bool TraceEnabled => _settings.Trace;

	public TracePoint? Trace { get; private set; }

	// Sends a value to the main window's Input (set by the Graph window)
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Action<string>? InsertValue { get; set; }

	public event EventHandler? TraceChanged;

	// Short messages for the status line ("copied ...")
	public event EventHandler<string>? Notice;

	private void UpdateTrace(Point at)
	{
		if (!TraceEnabled || _dragFrom != null || TraceTargetAt(at) is not { } target)
		{
			ClearTrace();
			return;
		}
		if (_traceShown is { } shown && shown.SameAs(target) && Trace != null)
		{
			return;
		}
		_traceWanted = target;
		_ = FetchTraceAsync();
	}

	//==================================================================
	//  Nearest line and the point on it
	//==================================================================
	private TraceTarget? TraceTargetAt(Point at)
	{
		var screen = PlotArea();
		if (!screen.Contains(at) || _series.Count == 0)
		{
			return null;
		}
		var world = DrawnView();
		var hits = _series.Select(line => (line, hit: Nearest(line, at, world, screen)))
			.Where(h => h.hit is { } d && d.Distance <= SnapPixels)
			.Select(h => (h.line, hit: h.hit!.Value))
			.ToList();
		if (hits.Count == 0)
		{
			return null;
		}
		var best = hits.MinBy(h => h.hit.Distance);
		var selected = hits.FirstOrDefault(h => Same(h.line.Name, _settings.Function));
		if (selected.line != null && selected.hit.Distance <= best.hit.Distance + TiePixels)
		{
			best = selected;
		}
		var current = hits.FirstOrDefault(h => Trace != null && Same(h.line.Name, Trace.Function));
		if (current.line != null && current.hit.Distance <= best.hit.Distance + StayPixels)
		{
			best = current;
		}
		return MakeTarget(best.line, best.hit, world, screen);
	}

	private TraceTarget? MakeTarget(PlotSeries line, LineHit hit, WorldRect world, RectangleF screen)
	{
		double pixel = PixelSize();
		if (line.IsEquation)
		{
			// fix the coordinate along which the line runs, solve the other
			bool fixX = Math.Abs(hit.Direction.X) >= Math.Abs(hit.Direction.Y);
			double pixelX = world.Width / screen.Width, pixelY = world.Height / screen.Height;
			string text = PlotMath.FormatTick(fixX ? hit.X : hit.Y, fixX ? pixelX : pixelY);
			return new TraceTarget(line.Name, true, 0, fixX ? pixelX : pixelY, fixX ? 'x' : 'y', text,
				fixX ? hit.Y : hit.X, SolveWindow * (fixX ? pixelY : pixelX));
		}
		if (_settings.Mode == GraphMode.Polar)
		{
			double theta = double.Parse(PlotMath.FormatTick(hit.T, ThetaStep), CultureInfo.InvariantCulture);
			return new TraceTarget(line.Name, false, theta, ThetaStep, ' ', "", 0, 0);
		}
		double x = double.Parse(PlotMath.FormatTick(hit.X, pixel), CultureInfo.InvariantCulture);    // the x the status line shows
		return new TraceTarget(line.Name, false, x, pixel, ' ', "", 0, 0);
	}

	// Distance in pixels, the world point, the parameter there (x or theta) and the line's screen direction
	internal readonly record struct LineHit(float Distance, double X, double Y, double T, PointF Direction);

	internal static LineHit? Nearest(PlotSeries line, Point at, WorldRect world, RectangleF screen)
	{
		LineHit? best = null;
		void Consider(double x1, double y1, double x2, double y2, double t1, double t2)
		{
			var a = PlotMath.ToScreen(x1, y1, world, screen);
			var b = PlotMath.ToScreen(x2, y2, world, screen);
			float dx = b.X - a.X, dy = b.Y - a.Y;
			if (Math.Abs(dy) > screen.Height / 2 || Math.Abs(dx) > screen.Width / 2)
			{
				return;    // a jump (tan's pole), not a line
			}
			float length = dx * dx + dy * dy;
			float u = length == 0 ? 0 : Math.Clamp(((at.X - a.X) * dx + (at.Y - a.Y) * dy) / length, 0, 1);
			float px = a.X + u * dx, py = a.Y + u * dy;
			float distance = MathF.Sqrt((at.X - px) * (at.X - px) + (at.Y - py) * (at.Y - py));
			if (best == null || distance < best.Value.Distance)
			{
				best = new LineHit(distance, x1 + u * (x2 - x1), y1 + u * (y2 - y1), t1 + u * (t2 - t1), new PointF(dx, dy));
			}
		}
		var points = line.Points;
		double step = points.Count > 1 ? (line.End - line.Start) / (points.Count - 1) : 0;
		for (int i = 0; i + 1 < points.Count; i++)
		{
			if (points[i] is { } p && points[i + 1] is { } q)
			{
				Consider(p.X, p.Y, q.X, q.Y, line.Start + i * step, line.Start + (i + 1) * step);
			}
		}
		foreach (var (x1, y1, x2, y2) in line.Segments)
		{
			Consider(x1, y1, x2, y2, 0, 0);
		}
		return best;
	}

	private static bool Same(string? a, string? b)
	{
		return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
	}

	//==================================================================
	//  Engine requests
	//==================================================================
	private async Task FetchTraceAsync()
	{
		if (_traceBusy)
		{
			return;    // the running request picks up _traceWanted when it ends
		}
		_traceBusy = true;
		try
		{
			while (_traceWanted is { } target)
			{
				_traceWanted = null;
				int generation = _traceGeneration;
				try
				{
					var point = await AskAsync(target);
					if (generation == _traceGeneration && _traceWanted == null)
					{
						Trace = point;
						_traceShown = target;
						Invalidate();
						TraceChanged?.Invoke(this, EventArgs.Empty);
					}
				}
				catch (EngineException)
				{
					// the line was deleted or the engine restarted; the next move retries
				}
			}
		}
		finally
		{
			_traceBusy = false;
		}
	}

	// null: an equation has no point near there
	private async Task<TracePoint?> AskAsync(TraceTarget target)
	{
		if (!target.IsEquation)
		{
			var (text, value) = await _session.ValueAtAsync(target.Line, target.T);
			return new TracePoint(target.Line, target.T, target.Step, text, value);
		}
		var (x, y, xValue, yValue) = await _session.EquationAtAsync(target.Line, target.Axis.ToString(), target.Fixed, target.Guess, target.Window);
		if (xValue is not { } px || yValue is not { } py)
		{
			return null;
		}
		char solved = target.Axis == 'x' ? 'y' : 'x';
		return new TracePoint(target.Line, 0, target.Step, solved == 'y' ? y : x, null)
		{
			IsEquation = true, XText = x, YText = y, X = px, Y = py, Solved = solved,
		};
	}

	private void ClearTrace()
	{
		_traceWanted = null;
		_traceShown = null;
		_traceGeneration++;
		if (Trace != null)
		{
			Trace = null;
			Invalidate();
			TraceChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	//==================================================================
	//  Drawing
	//==================================================================
	private void DrawTrace(Graphics g, WorldRect world, RectangleF screen)
	{
		if (!TraceEnabled || Trace is not { } trace || !_series.Any(s => Same(s.Name, trace.Function)))
		{
			return;
		}
		PointF point;
		string label;
		if (trace.IsEquation)
		{
			point = PlotMath.ToScreen(trace.X, trace.Y, world, screen);
			label = $"({trace.X.ToString("G6", CultureInfo.InvariantCulture)}, {trace.Y.ToString("G6", CultureInfo.InvariantCulture)})";
		}
		else if (trace.Value is { } v)
		{
			point = _settings.Mode == GraphMode.Polar
				? PlotMath.ToScreen(v * Math.Cos(trace.T), v * Math.Sin(trace.T), world, screen)
				: PlotMath.ToScreen(trace.T, v, world, screen);
			label = v.ToString("G6", CultureInfo.InvariantCulture);
		}
		else
		{
			return;
		}
		if (!screen.Contains(point))
		{
			return;
		}
		using (var guide = new Pen(SystemColors.GrayText) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot })
		{
			if (trace.IsEquation && trace.Solved == 'x')
			{
				g.DrawLine(guide, screen.Left, point.Y, screen.Right, point.Y);    // y was fixed
			}
			else if (trace.IsEquation || _settings.Mode == GraphMode.XY)
			{
				g.DrawLine(guide, point.X, screen.Top, point.X, screen.Bottom);
			}
		}
		using (var fill = new SolidBrush(Theme.LineColour(_settings, trace.Function)))
		using (var ring = new Pen(SystemColors.Window, 1.5f))
		{
			g.FillEllipse(fill, point.X - 4, point.Y - 4, 8, 8);
			g.DrawEllipse(ring, point.X - 4, point.Y - 4, 8, 8);
		}
		var size = TextRenderer.MeasureText(label, Font);
		float x = point.X + 8 + size.Width > screen.Right ? point.X - 8 - size.Width : point.X + 8;
		float y = point.Y - 8 - size.Height < screen.Top ? point.Y + 8 : point.Y - 8 - size.Height;
		var box = new Rectangle((int)x, (int)y, size.Width, size.Height);
		using (var back = new SolidBrush(SystemColors.Window))
		{
			g.FillRectangle(back, box);
		}
		TextRenderer.DrawText(g, label, Font, box, SystemColors.WindowText);
	}

	//==================================================================
	//  Click copies; right-click: Copy / Insert in Input
	//==================================================================
	private void TraceMouseDown(MouseEventArgs e)
	{
		_downAt = e.Location;
	}

	private void TraceMouseUp(MouseEventArgs e)
	{
		bool click = Math.Abs(e.X - _downAt.X) <= ClickSlop && Math.Abs(e.Y - _downAt.Y) <= ClickSlop;
		if (e.Button == MouseButtons.Left && click && TraceEnabled && TraceTargetAt(e.Location) is { } target)
		{
			_ = CopyAtAsync(target);
		}
	}

	// A click asks for its own value, so it is copied even before the trace
	// has caught up; an equation copies the solved coordinate
	private async Task CopyAtAsync(TraceTarget target)
	{
		try
		{
			Copy((await AskAsync(target))?.Text);
		}
		catch (EngineException)
		{
			// engine restarting or line gone: nothing to copy
		}
	}

	private void Copy(string? text)
	{
		if (text != null)
		{
			Clipboard.SetText(text);
			Notice?.Invoke(this, $"Copied {text}");
		}
	}

	private ContextMenuStrip BuildTraceMenu()
	{
		var menu = new ContextMenuStrip();
		var copy = new ToolStripMenuItem("&Copy Value", null, (_, _) => Copy(_menuTrace?.Text));
		var insert = new ToolStripMenuItem("&Insert Value in Input", null, (_, _) => Insert(_menuTrace?.Text));
		// equations (owner 2026-11-08): each coordinate or both
		var copyX = new ToolStripMenuItem("Copy &x", null, (_, _) => Copy(_menuTrace?.XText));
		var copyY = new ToolStripMenuItem("Copy &y", null, (_, _) => Copy(_menuTrace?.YText));
		var copyBoth = new ToolStripMenuItem("Copy (x, y)", null, (_, _) => Copy(Pair(_menuTrace)));
		var insertX = new ToolStripMenuItem("Insert x in Input", null, (_, _) => Insert(_menuTrace?.XText));
		var insertY = new ToolStripMenuItem("Insert y in Input", null, (_, _) => Insert(_menuTrace?.YText));
		var insertBoth = new ToolStripMenuItem("Insert (x, y) in Input", null, (_, _) => Insert(Pair(_menuTrace)));
		var separator = new ToolStripSeparator();
		menu.Items.AddRange(new ToolStripItem[] { copy, insert, copyX, copyY, copyBoth, separator, insertX, insertY, insertBoth });
		menu.Opening += (_, _) =>
		{
			_menuTrace = Trace;    // the mouse leaves the plot while the menu is open
			bool equation = _menuTrace is { IsEquation: true };
			bool has = _menuTrace is { Text: not null };
			copy.Visible = insert.Visible = !equation;
			foreach (var item in new ToolStripItem[] { copyX, copyY, copyBoth, separator, insertX, insertY, insertBoth })
			{
				item.Visible = equation;
			}
			foreach (var item in new ToolStripItem[] { copy, insert, copyX, copyY, copyBoth, insertX, insertY, insertBoth })
			{
				item.Enabled = has;
			}
			if (InsertValue == null)
			{
				insert.Visible = insertX.Visible = insertY.Visible = insertBoth.Visible = separator.Visible = false;
			}
		};
		return menu;
	}

	private void Insert(string? text)
	{
		if (text != null)
		{
			InsertValue?.Invoke(text);
		}
	}

	private static string? Pair(TracePoint? trace)
	{
		return trace is { XText: { } x, YText: { } y } ? $"({x}, {y})" : null;
	}
}
