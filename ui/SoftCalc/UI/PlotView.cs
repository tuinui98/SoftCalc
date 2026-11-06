// PlotView.cs - Draws the selected function for the sidebar and the Graph window
//
// Asks the engine for points (one per pixel column, polar: per sample),
// caches them, and repaints from the cache. A new request is made when
// the function, the graph settings, the size, or the angle unit change.
// In the Graph window (interactive) it also draws axis numbers and
// handles zoom, pan and keys: PlotView.Navigation.cs.

using SoftCalc.Engine;
using SoftCalc.Logic;

namespace SoftCalc.UI;

internal sealed partial class PlotView : Control
{
	private const int PolarSamples = 720;
	private const int MaxSamples = 2000;

	private readonly EngineSession _session;
	private readonly GraphSettings _settings;
	private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 120 };
	private IReadOnlyList<(double X, double Y)?> _points = Array.Empty<(double X, double Y)?>();
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
		}
		DoubleBuffered = true;
		ResizeRedraw = true;
		BackColor = SystemColors.Window;
		_debounce.Tick += async (_, _) =>
		{
			_debounce.Stop();
			await RefreshPointsAsync();
		};
		_settings.Changed += (_, _) => RequestRefresh();
		Resize += (_, _) => RequestRefresh();
		Disposed += (_, _) => _debounce.Dispose();
	}

	// Coalesces bursts (resizing, typing a range) into one engine request
	public void RequestRefresh()
	{
		_debounce.Stop();
		_debounce.Start();
	}

	private async Task RefreshPointsAsync()
	{
		if (!_session.IsStarted)
		{
			return;    // start-up refreshes the plot once the engine is ready
		}
		int request = ++_requestNumber;
		string? function = _settings.Function;
		if (string.IsNullOrEmpty(function) || Width < 4)
		{
			_points = Array.Empty<(double X, double Y)?>();
			_message = string.IsNullOrEmpty(function) ? "No function selected" : null;
			Invalidate();
			return;
		}
		bool polar = _settings.Mode == GraphMode.Polar;
		int samples = polar ? PolarSamples : Math.Clamp(Width, 2, MaxSamples);
		try
		{
			var points = await _session.PlotAsync(function, polar ? "polar" : "xy",
				polar ? _settings.MinT : _settings.MinX, polar ? _settings.MaxT : _settings.MaxX, samples);
			if (request != _requestNumber)
			{
				return;    // a newer request is on its way
			}
			_points = points;
			_message = null;
		}
		catch (EngineException ex)
		{
			_points = Array.Empty<(double X, double Y)?>();
			_message = ex.Message;
		}
		Invalidate();
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
		var world = _settings.View;
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
		using (var pen = new Pen(SystemColors.Highlight, 1.6f))
		{
			foreach (var segment in PlotMath.Segments(_points, world))
			{
				if (segment.Count < 2)
				{
					continue;
				}
				var line = segment.Select(p => PlotMath.Clamp(PlotMath.ToScreen(p.X, p.Y, world, screen), screen)).ToArray();
				g.SetClip(screen);
				g.DrawLines(pen, line);
				g.ResetClip();
			}
		}
		if (_message != null)
		{
			TextRenderer.DrawText(g, _message, Font, ClientRectangle, SystemColors.GrayText,
				TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
		}
		if (_interactive && Focused)
		{
			ControlPaint.DrawFocusRectangle(g, ClientRectangle);    // keys go to the plot
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
		using var pen = new Pen(SystemColors.ControlLight);
		foreach (double x in PlotMath.GridLines(world.MinX, world.MaxX, _settings.GridX))
		{
			float sx = PlotMath.ToScreen(x, 0, world, screen).X;
			g.DrawLine(pen, sx, screen.Top, sx, screen.Bottom);
		}
		foreach (double y in PlotMath.GridLines(world.MinY, world.MaxY, _settings.GridY))
		{
			float sy = PlotMath.ToScreen(0, y, world, screen).Y;
			g.DrawLine(pen, screen.Left, sy, screen.Right, sy);
		}
	}

	private void DrawPolarGrid(Graphics g, WorldRect world, RectangleF screen)
	{
		using var pen = new Pen(SystemColors.ControlLight);
		var center = PlotMath.ToScreen(0, 0, world, screen);
		float scale = screen.Width / (float)world.Width;
		foreach (double r in PlotMath.GridLines(0, _settings.MaxR, _settings.GridR).Where(r => r > 0))
		{
			float radius = (float)r * scale;
			g.DrawEllipse(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
		}
		float outer = (float)_settings.MaxR * scale;
		for (double degrees = 0; degrees < 360; degrees += PlotMath.PolarSpokeDegrees)
		{
			double rad = degrees * Math.PI / 180;
			g.DrawLine(pen, center.X, center.Y,
				center.X + outer * (float)Math.Cos(rad), center.Y - outer * (float)Math.Sin(rad));
		}
	}

	private static void DrawAxes(Graphics g, WorldRect world, RectangleF screen)
	{
		using var pen = new Pen(SystemColors.ControlDark);
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
