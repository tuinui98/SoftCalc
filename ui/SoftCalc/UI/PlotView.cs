// PlotView.cs - Draws the shown functions and equations for the sidebar and the Graph window
//
// Asks the engine for points (one per pixel column, polar: per sample)
// of each function and for line segments of each equation in x and y
// (graph2_plan G5), caches them, and repaints from the cache. A new
// request is made when the lines, the graph settings, the size, or the
// angle unit change.
// In the Graph window (interactive) it also draws axis numbers and
// handles zoom, pan and keys: PlotView.Navigation.cs.

using SoftCalc.Engine;
using SoftCalc.Logic;

namespace SoftCalc.UI;

// One drawn line: a function's points (Start/End: its x or theta range)
// or an equation's segments
internal sealed record PlotSeries(string Name, IReadOnlyList<(double X, double Y)?> Points, double Start, double End,
	IReadOnlyList<(double X1, double Y1, double X2, double Y2)> Segments)
{
	public bool IsEquation => GraphSettings.IsEquation(Name);
}

internal sealed partial class PlotView : Control
{
	private const int PolarSamples = 720;
	private const int MaxSamples = 2000;

	private readonly EngineSession _session;
	private readonly GraphSettings _settings;
	private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 120 };
	private const int EquationCell = 10;    // [DEF] pixels per coarse equation cell
	private static readonly IReadOnlyList<(double X1, double Y1, double X2, double Y2)> NoSegments =
		Array.Empty<(double, double, double, double)>();
	private static readonly IReadOnlyList<(double X, double Y)?> NoPoints = Array.Empty<(double X, double Y)?>();

	// One per shown line, in GraphSettings.Shown order (G3)
	private IReadOnlyList<PlotSeries> _series = Array.Empty<PlotSeries>();
	private string? _message;
	private int _requestNumber;

	public PlotView(EngineSession session, GraphSettings settings, bool interactive = false)
	{
		_session = session;
		_settings = settings;
		_interactive = interactive;
		if (interactive)
		{
			SetStyle(ControlStyles.Selectable, true);
			TabStop = true;
			Cursor = Cursors.Cross;
			ContextMenuStrip = BuildTraceMenu();
		}
		DoubleBuffered = true;
		ResizeRedraw = true;
		BackColor = SystemColors.Window;
		_debounce.Tick += async (_, _) =>
		{
			_debounce.Stop();
			await RefreshPointsAsync();
		};
		EventHandler onChanged = (_, _) =>
		{
			Invalidate();    // redraw the cached lines in the new range at once (drag, zoom)
			RequestRefresh();
		};
		_settings.Changed += onChanged;
		Disposed += (_, _) => _settings.Changed -= onChanged;    // the settings outlive a closed Graph window
		Resize += (_, _) => RequestRefresh();
		Disposed += (_, _) => _debounce.Dispose();
	}

	// At most one engine request per interval during a burst (resizing,
	// dragging), so a drag keeps fetching the newly shown part
	public void RequestRefresh()
	{
		if (!_debounce.Enabled)
		{
			_debounce.Start();
		}
	}

	private async Task RefreshPointsAsync()
	{
		if (!_session.IsStarted)
		{
			return;    // start-up refreshes the plot once the engine is ready
		}
		int request = ++_requestNumber;
		var names = _settings.Shown.ToList();
		if (names.Count == 0 || Width < 4)
		{
			_series = Array.Empty<PlotSeries>();
			_message = names.Count == 0 ? "No function selected" : null;
			Invalidate();
			return;
		}
		bool polar = _settings.Mode == GraphMode.Polar;
		int samples = polar ? PolarSamples : Math.Clamp(Width, 2, MaxSamples);
		double start = polar ? _settings.MinT : _settings.MinX, end = polar ? _settings.MaxT : _settings.MaxX;
		var found = new Dictionary<string, PlotSeries>(StringComparer.OrdinalIgnoreCase);
		string? error = null;
		//==================================================================
		//  BLOCK 1 - Functions, then the view they fit (sidebar)
		//==================================================================
		foreach (string name in names.Where(n => !GraphSettings.IsEquation(n)))
		{
			try
			{
				found[name] = new PlotSeries(name, await _session.PlotAsync(name, polar ? "polar" : "xy", start, end, samples), start, end, NoSegments);
			}
			catch (EngineException ex)
			{
				error ??= ex.Message;
			}
			if (request != _requestNumber)
			{
				return;    // a newer request is on its way
			}
		}
		// end BLOCK 1
		//==================================================================
		//  BLOCK 2 - Equations in the drawn rectangle
		//==================================================================
		var world = DrawnView(found.Values);
		var area = PlotArea();
		int cols = Math.Clamp((int)(area.Width / EquationCell), 4, 150);
		int rows = Math.Clamp((int)(area.Height / EquationCell), 4, 150);
		foreach (string name in names.Where(GraphSettings.IsEquation))
		{
			try
			{
				found[name] = new PlotSeries(name, NoPoints, 0, 0, await _session.PlotEquationAsync(name, world, cols, rows));
			}
			catch (EngineException ex)
			{
				error ??= $"{name}: {ex.Message}";
			}
			if (request != _requestNumber)
			{
				return;
			}
		}
		// end BLOCK 2
		_series = names.Where(found.ContainsKey).Select(n => found[n]).ToList();
		_message = _series.Count == 0 ? error : null;
		Invalidate();
	}

	private IEnumerable<(double X, double Y)> AllPoints()
	{
		return PointsOf(_series);
	}

	private static IEnumerable<(double X, double Y)> PointsOf(IEnumerable<PlotSeries> series)
	{
		return series.SelectMany(s => s.Points).Where(p => p != null).Select(p => p!.Value);
	}

	// Sidebar (owner, G3): the X range as set, Y fitted to the lines so their
	// shape shows (polar: R fitted); the Graph window shows the set range
	// Equations do not take part: they are drawn in the rectangle the functions fit
	private WorldRect DrawnView()
	{
		return DrawnView(_series);
	}

	private WorldRect DrawnView(IEnumerable<PlotSeries> series)
	{
		var world = _settings.View;
		if (_interactive)
		{
			return world;
		}
		var points = PointsOf(series).ToList();
		if (_settings.Mode == GraphMode.Polar)
		{
			var r = PlotMath.FitRange(points.Select(p => Math.Sqrt(p.X * p.X + p.Y * p.Y)));
			return r is { Max: > 0 } fit ? new WorldRect(-fit.Max, fit.Max, -fit.Max, fit.Max) : world;
		}
		var y = PlotMath.FitRange(points.Select(p => p.Y));
		return y is { } fy ? world with { MinY = fy.Min, MaxY = fy.Max } : world;
	}

	public void SaveImage(string path)
	{
		using var bitmap = new Bitmap(Math.Max(1, Width), Math.Max(1, Height));
		DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
		bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
	}

	//==================================================================
	//  Drawing
	//==================================================================
	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		var g = e.Graphics;
		g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
		var screen = PlotArea();
		var world = DrawnView();
		if (_settings.ShowGrid)
		{
			if (_settings.Mode == GraphMode.XY)
			{
				DrawXyGrid(g, world, screen);
			}
			else
			{
				DrawPolarGrid(g, world, screen);
			}
		}
		DrawAxes(g, world, screen);
		if (_interactive)
		{
			DrawTicks(g, world, screen);
		}
		g.SetClip(screen);
		foreach (var line in _series)
		{
			bool selected = string.Equals(line.Name, _settings.Function, StringComparison.OrdinalIgnoreCase);
			using var pen = new Pen(Theme.LineColour(_settings, line.Name), selected && _series.Count > 1 ? 2.4f : 1.6f);
			foreach (var segment in PlotMath.Segments(line.Points, world))
			{
				if (segment.Count >= 2)
				{
					g.DrawLines(pen, segment.Select(p => PlotMath.Clamp(PlotMath.ToScreen(p.X, p.Y, world, screen), screen)).ToArray());
				}
			}
			foreach (var (x1, y1, x2, y2) in line.Segments)
			{
				g.DrawLine(pen, PlotMath.ToScreen(x1, y1, world, screen), PlotMath.ToScreen(x2, y2, world, screen));
			}
		}
		g.ResetClip();
		if (_interactive && _series.Count > 1)
		{
			DrawLegend(g, screen);
		}
		if (_message != null)
		{
			TextRenderer.DrawText(g, _message, Font, ClientRectangle, SystemColors.GrayText,
				TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
		}
		if (_interactive)
		{
			DrawTrace(g, world, screen);
		}
		if (_interactive && Focused)
		{
			ControlPaint.DrawFocusRectangle(g, ClientRectangle);    // keys go to the plot
		}
	}

	// Graph window with several lines: colour and name of each, top left
	private void DrawLegend(Graphics g, RectangleF screen)
	{
		int row = Font.Height + 2;
		int width = _series.Max(s => TextRenderer.MeasureText(s.Name, Font).Width) + 30;
		var box = new Rectangle((int)screen.Left + 6, (int)screen.Top + 6, width, _series.Count * row + 6);
		using (var back = new SolidBrush(SystemColors.Window))
		using (var border = new Pen(SystemColors.ControlDark))
		{
			g.FillRectangle(back, box);
			g.DrawRectangle(border, box);
		}
		for (int i = 0; i < _series.Count; i++)
		{
			string name = _series[i].Name;
			int y = box.Top + 3 + i * row;
			using var pen = new Pen(Theme.LineColour(_settings, name), 2.4f);
			g.DrawLine(pen, box.Left + 6, y + row / 2, box.Left + 22, y + row / 2);
			TextRenderer.DrawText(g, name, Font, new Point(box.Left + 26, y), SystemColors.WindowText);
		}
	}

	// Polar views keep a square drawing area so circles stay round; the
	// interactive XY view leaves room for the axis numbers
	private RectangleF PlotArea()
	{
		var area = new RectangleF(4, 4, Math.Max(1, Width - 8), Math.Max(1, Height - 8));
		if (_interactive && _settings.Mode == GraphMode.XY)
		{
			float left = TickLabelWidth() + 8;
			float bottom = Font.Height + 6;
			area = new RectangleF(left, 6, Math.Max(1, Width - left - 10), Math.Max(1, Height - bottom - 8));
		}
		if (_settings.Mode == GraphMode.Polar)
		{
			float side = Math.Min(area.Width, area.Height);
			area = new RectangleF(area.Left + (area.Width - side) / 2, area.Top + (area.Height - side) / 2, side, side);
		}
		return area;
	}

	private void DrawXyGrid(Graphics g, WorldRect world, RectangleF screen)
	{
		using var pen = new Pen(Theme.PlotGrid);
		foreach (double x in PlotMath.GridLines(world.MinX, world.MaxX, _settings.GridX))
		{
			float sx = PlotMath.ToScreen(x, 0, world, screen).X;
			g.DrawLine(pen, sx, screen.Top, sx, screen.Bottom);
		}
		double gridY = _interactive ? _settings.GridY : PlotMath.NiceStep(world.Height, 10);    // fitted sidebar Y
		foreach (double y in PlotMath.GridLines(world.MinY, world.MaxY, gridY))
		{
			float sy = PlotMath.ToScreen(0, y, world, screen).Y;
			g.DrawLine(pen, screen.Left, sy, screen.Right, sy);
		}
	}

	private void DrawPolarGrid(Graphics g, WorldRect world, RectangleF screen)
	{
		using var pen = new Pen(Theme.PlotGrid);
		var center = PlotMath.ToScreen(0, 0, world, screen);
		float scale = screen.Width / (float)world.Width;
		double maxR = world.MaxX;    // the set R Max, or the fitted one in the sidebar
		double gridR = _interactive ? _settings.GridR : PlotMath.NiceStep(maxR, 5);
		foreach (double r in PlotMath.GridLines(0, maxR, gridR).Where(r => r > 0))
		{
			float radius = (float)r * scale;
			g.DrawEllipse(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
		}
		float outer = (float)maxR * scale;
		for (double degrees = 0; degrees < 360; degrees += PlotMath.PolarSpokeDegrees)
		{
			double rad = degrees * Math.PI / 180;
			g.DrawLine(pen, center.X, center.Y,
				center.X + outer * (float)Math.Cos(rad), center.Y - outer * (float)Math.Sin(rad));
		}
	}

	private static void DrawAxes(Graphics g, WorldRect world, RectangleF screen)
	{
		using var pen = new Pen(Theme.PlotAxis);
		if (world.MinX <= 0 && world.MaxX >= 0)
		{
			float sx = PlotMath.ToScreen(0, 0, world, screen).X;
			g.DrawLine(pen, sx, screen.Top, sx, screen.Bottom);
		}
		if (world.MinY <= 0 && world.MaxY >= 0)
		{
			float sy = PlotMath.ToScreen(0, 0, world, screen).Y;
			g.DrawLine(pen, screen.Left, sy, screen.Right, sy);
		}
		g.DrawRectangle(pen, screen.X, screen.Y, screen.Width, screen.Height);
	}
}
