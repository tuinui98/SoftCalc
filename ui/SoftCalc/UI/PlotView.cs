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
	private const int WindowInterval = 40;    // [DEF] Graph window: at most one round per 40 ms while dragging
	private const int SidebarQuiet = 400;    // [DEF] sidebar: waits until changes stop for this long

	private readonly System.Windows.Forms.Timer _debounce = new();
	private const int EquationCell = 10;    // [DEF] pixels per coarse equation cell
	private static readonly IReadOnlyList<(double X1, double Y1, double X2, double Y2)> NoSegments =
		Array.Empty<(double, double, double, double)>();
	private static readonly IReadOnlyList<(double X, double Y)?> NoPoints = Array.Empty<(double X, double Y)?>();

	// One per shown line, in GraphSettings.Shown order (G3)
	private IReadOnlyList<PlotSeries> _series = Array.Empty<PlotSeries>();
	private string? _message;
	private float _drawnWidth;    // drawing area width at the last paint (1:1 keeps its scale on resize)
	private bool _refreshing;    // one round of engine requests at a time
	private bool _refreshAgain;    // something changed during the round: one more, with the newest state
	private bool _seriesPolar;    // mode the cached lines were made in
	private bool _missedWhileHidden;    // sidebar hidden: refresh when shown again

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
		_debounce.Interval = interactive ? WindowInterval : SidebarQuiet;
		_debounce.Tick += async (_, _) =>
		{
			_debounce.Stop();
			await RefreshPointsAsync();
		};
		VisibleChanged += (_, _) =>
		{
			if (Visible && _missedWhileHidden)
			{
				_missedWhileHidden = false;
				RequestRefresh();
			}
		};
		EventHandler onChanged = (_, _) =>
		{
			Invalidate();    // redraw the cached lines in the new range at once (drag, zoom)
			RequestRefresh();
		};
		_settings.Changed += onChanged;
		Disposed += (_, _) => _settings.Changed -= onChanged;    // the settings outlive a closed Graph window
		Resize += (_, _) =>
		{
			if (_settings.Square && _interactive && _settings.Mode == GraphMode.XY && _drawnWidth > 0)
			{
				KeepSquare(_settings.View.Width / _drawnWidth);    // the scale before the resize
				_settings.NotifyChanged();
			}
			RequestRefresh();
		};
		Disposed += (_, _) => _debounce.Dispose();
	}

	// Graph window: at most one round per interval during a burst (resizing,
	// dragging), so a drag keeps fetching the newly shown part. Sidebar:
	// only once the changes stop, and not while it is hidden.
	public void RequestRefresh()
	{
		if (!_interactive && !Visible)
		{
			_missedWhileHidden = true;
			return;
		}
		if (!_interactive)
		{
			_debounce.Stop();    // restart: wait for quiet
			_debounce.Start();
		}
		else if (!_debounce.Enabled)
		{
			_debounce.Start();
		}
	}

	// Rounds never overlap: requests made during one are folded into a
	// single next round with the newest range (before, they queued up in
	// the engine and the lines lagged behind the mouse)
	private async Task RefreshPointsAsync()
	{
		if (_refreshing)
		{
			_refreshAgain = true;
			return;
		}
		_refreshing = true;
		try
		{
			do
			{
				_refreshAgain = false;
				await RefreshOnceAsync();
			}
			while (_refreshAgain && !IsDisposed);
		}
		finally
		{
			_refreshing = false;
		}
	}

	private async Task RefreshOnceAsync()
	{
		if (!_session.IsStarted)
		{
			return;    // start-up refreshes the plot once the engine is ready
		}
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
		var world = DrawnView();
		double start = polar ? _settings.MinT : world.MinX, end = polar ? _settings.MaxT : world.MaxX;
		// lines still in view are kept and replaced one by one as they arrive
		var found = new Dictionary<string, PlotSeries>(StringComparer.OrdinalIgnoreCase);
		if (_seriesPolar == polar)
		{
			foreach (var old in _series.Where(s => names.Contains(s.Name, StringComparer.OrdinalIgnoreCase)))
			{
				found[old.Name] = old;
			}
		}
		_seriesPolar = polar;
		var fresh = new List<PlotSeries>();
		string? error = null;
		//==================================================================
		//  BLOCK 1 - Functions over the drawn x range
		//==================================================================
		foreach (string name in names.Where(n => !GraphSettings.IsEquation(n)))
		{
			try
			{
				var line = new PlotSeries(name, await _session.PlotAsync(name, polar ? "polar" : "xy", start, end, samples), start, end, NoSegments);
				found[name] = line;
				fresh.Add(line);
			}
			catch (EngineException ex)
			{
				found.Remove(name);
				error ??= ex.Message;
			}
			Show(names, found, error);
		}
		// end BLOCK 1
		//==================================================================
		//  BLOCK 2 - Equations in the drawn rectangle
		//==================================================================
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
				found.Remove(name);
				error ??= $"{name}: {ex.Message}";
			}
			Show(names, found, error);
		}
		// end BLOCK 2
		Show(names, found, error);
	}

	// Each line shows as soon as it arrives
	private void Show(List<string> names, Dictionary<string, PlotSeries> found, string? error)
	{
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

	// The Graph window shows the set range. The sidebar (owner 2026-11-11,
	// before: Y fitted to the lines) shows the same range at the window's
	// x to y scale: all of it, the other axis grown around the centre.
	// Polar: both draw a square area, so the range as set.
	private WorldRect DrawnView()
	{
		var world = _settings.View;
		if (_interactive || _settings.Mode == GraphMode.Polar)
		{
			return world;
		}
		var area = PlotArea();
		return PlotMath.SameShape(world, _settings.PlotAspect ?? area.Width / area.Height, area.Width, area.Height);
	}

	// Graph window, XY: keep the drawing area's shape for the sidebar
	private void NoteAspect(RectangleF screen)
	{
		if (!_interactive || _settings.Mode != GraphMode.XY || screen.Width < 20 || screen.Height < 20)
		{
			return;
		}
		double aspect = screen.Width / screen.Height;
		if (_settings.PlotAspect is not { } old || Math.Abs(aspect / old - 1) > 0.005)
		{
			_settings.PlotAspect = aspect;
			_settings.NotifyChanged();
		}
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
		_drawnWidth = screen.Width;
		NoteAspect(screen);
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
		foreach (double x in PlotMath.GridLines(world.MinX, world.MaxX, SidebarStep(_settings.GridX, world.Width, screen.Width)))
		{
			float sx = PlotMath.ToScreen(x, 0, world, screen).X;
			g.DrawLine(pen, sx, screen.Top, sx, screen.Bottom);
		}
		double gridY = SidebarStep(_settings.GridY, world.Height, screen.Height);
		foreach (double y in PlotMath.GridLines(world.MinY, world.MaxY, gridY))
		{
			float sy = PlotMath.ToScreen(0, y, world, screen).Y;
			g.DrawLine(pen, screen.Left, sy, screen.Right, sy);
		}
	}

	// The set grid step, or in the small sidebar a coarser one when the lines would be under 4 px apart
	private double SidebarStep(double set, double span, float pixels)
	{
		return _interactive || set <= 0 || span / set * 4 <= pixels ? set : PlotMath.NiceStep(span, 10);
	}

	private void DrawPolarGrid(Graphics g, WorldRect world, RectangleF screen)
	{
		using var pen = new Pen(Theme.PlotGrid);
		var center = PlotMath.ToScreen(0, 0, world, screen);
		float scale = screen.Width / (float)world.Width;
		double maxR = world.MaxX;    // the set R Max
		double gridR = SidebarStep(_settings.GridR, maxR, screen.Width / 2);
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
