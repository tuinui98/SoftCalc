// PlotView.Navigation.cs - Graph window extras: axis numbers, zoom, pan, fit, keys
//
// Only for the interactive PlotView in the Graph window
// (design/graph_plan.md, G1). Wheel zooms around the cursor (Shift: X
// only, Ctrl: Y only), dragging pans, arrows pan 10%, + / - zoom, Home
// resets the range, F fits Y. Polar views only zoom R Max (owner).
// Every change goes to GraphSettings, so the Range panel follows.

using SoftCalc.Logic;

namespace SoftCalc.UI;

internal sealed partial class PlotView
{
	private const double WheelZoom = 0.8;    // [DEF] one wheel notch or + shows 80% of the span
	private const int TickSpacingX = 60;    // [DEF] pixels between axis numbers
	private const int TickSpacingY = 40;

	private readonly bool _interactive;
	private Point? _dragFrom;
	private WorldRect _dragWorld;

	// World point under the mouse (null when it leaves), for the status line
	public event EventHandler<(double X, double Y)?>? PointerMoved;

	//==================================================================
	//  Axis numbers
	//==================================================================
	private double TickStepY()
	{
		return PlotMath.NiceStep(_settings.View.Height, Math.Max(1, Height / TickSpacingY));
	}

	private int TickLabelWidth()
	{
		var world = _settings.View;
		double step = TickStepY();
		return PlotMath.GridLines(world.MinY, world.MaxY, step)
			.Select(y => TextRenderer.MeasureText(PlotMath.FormatTick(y, step), Font).Width)
			.DefaultIfEmpty(0).Max();
	}

	// Left and bottom edges (XY), or the rings along the positive x axis (polar)
	private void DrawTicks(Graphics g, WorldRect world, RectangleF screen)
	{
		var color = SystemColors.GrayText;
		if (_settings.Mode == GraphMode.Polar)
		{
			double rStep = PlotMath.NiceStep(_settings.MaxR, Math.Max(1, (int)(screen.Width / 2 / TickSpacingX)));
			foreach (double r in PlotMath.GridLines(0, _settings.MaxR, rStep).Where(r => r > 0))
			{
				var p = PlotMath.ToScreen(r, 0, world, screen);
				TextRenderer.DrawText(g, PlotMath.FormatTick(r, rStep), Font, new Point((int)p.X + 2, (int)p.Y + 2), color);
			}
			return;
		}
		using var pen = new Pen(SystemColors.ControlDark);
		double xStep = PlotMath.NiceStep(world.Width, Math.Max(1, (int)(screen.Width / TickSpacingX)));
		foreach (double x in PlotMath.GridLines(world.MinX, world.MaxX, xStep))
		{
			float sx = PlotMath.ToScreen(x, 0, world, screen).X;
			g.DrawLine(pen, sx, screen.Bottom, sx, screen.Bottom + 3);
			string text = PlotMath.FormatTick(x, xStep);
			int w = TextRenderer.MeasureText(text, Font).Width;
			TextRenderer.DrawText(g, text, Font, new Point((int)sx - w / 2, (int)screen.Bottom + 4), color);
		}
		double yStep = TickStepY();
		foreach (double y in PlotMath.GridLines(world.MinY, world.MaxY, yStep))
		{
			float sy = PlotMath.ToScreen(0, y, world, screen).Y;
			g.DrawLine(pen, screen.Left - 3, sy, screen.Left, sy);
			string text = PlotMath.FormatTick(y, yStep);
			var size = TextRenderer.MeasureText(text, Font);
			TextRenderer.DrawText(g, text, Font, new Point((int)screen.Left - 4 - size.Width, (int)sy - size.Height / 2), color);
		}
	}

	// Size of one pixel in world units, for showing the pointer's coordinates
	public double PixelSize()
	{
		var screen = PlotArea();
		return _settings.View.Width / Math.Max(1, screen.Width);
	}

	//==================================================================
	//  Zoom, pan, fit, reset
	//==================================================================
	// factor < 1 zooms in; the world point under `at` stays in place
	public void ZoomAt(PointF at, double factor, bool xAxis = true, bool yAxis = true)
	{
		if (_settings.Mode == GraphMode.Polar)
		{
			if (PlotMath.Zoom(0, _settings.MaxR, 0, factor) is { } r)
			{
				SetMaxR(r.Max);
			}
			return;
		}
		var world = _settings.View;
		var screen = PlotArea();
		double wx = world.MinX + (at.X - screen.Left) / screen.Width * world.Width;
		double wy = world.MinY + (screen.Bottom - at.Y) / screen.Height * world.Height;
		var x = xAxis ? PlotMath.Zoom(world.MinX, world.MaxX, wx, factor) : (world.MinX, world.MaxX);
		var y = yAxis ? PlotMath.Zoom(world.MinY, world.MaxY, wy, factor) : (world.MinY, world.MaxY);
		if (x is { } nx && y is { } ny)
		{
			SetXy(nx.Item1, nx.Item2, ny.Item1, ny.Item2);
		}
	}

	public void ZoomCenter(double factor)
	{
		var screen = PlotArea();
		ZoomAt(new PointF(screen.Left + screen.Width / 2, screen.Top + screen.Height / 2), factor);
	}

	// Moves the content by a screen distance (polar views do not pan)
	public void PanPixels(float dx, float dy)
	{
		PanFrom(_settings.View, dx, dy);
	}

	private void PanFrom(WorldRect from, float dx, float dy)
	{
		if (_settings.Mode == GraphMode.Polar)
		{
			return;
		}
		var screen = PlotArea();
		double mx = -dx / screen.Width * from.Width;
		double my = dy / screen.Height * from.Height;
		SetXy(from.MinX + mx, from.MaxX + mx, from.MinY + my, from.MaxY + my, keepGrid: true);
	}

	// Y Min/Max (polar: R Max) from the plotted points
	public void FitY()
	{
		var points = AllPoints().ToList();    // every shown line
		if (_settings.Mode == GraphMode.Polar)
		{
			if (PlotMath.FitRange(points.Select(p => Math.Sqrt(p.X * p.X + p.Y * p.Y))) is { } r && r.Max > 0)
			{
				SetMaxR(r.Max);
			}
			return;
		}
		if (PlotMath.FitRange(points.Select(p => p.Y)) is { } y)
		{
			var world = _settings.View;
			SetXy(world.MinX, world.MaxX, y.Min, y.Max);
		}
	}

	public void ResetRange()
	{
		_settings.ResetRange();
		_settings.NotifyChanged();
	}

	private void SetMaxR(double maxR)
	{
		_settings.MaxR = maxR;
		_settings.GridR = PlotMath.NiceStep(maxR, 10);
		_settings.NotifyChanged();
	}

	// Zooming resets the grid increments to round steps so the grid stays readable
	private void SetXy(double minX, double maxX, double minY, double maxY, bool keepGrid = false)
	{
		(_settings.MinX, _settings.MaxX, _settings.MinY, _settings.MaxY) = (minX, maxX, minY, maxY);
		if (!keepGrid)
		{
			_settings.GridX = PlotMath.NiceStep(maxX - minX, 20);
			_settings.GridY = PlotMath.NiceStep(maxY - minY, 20);
		}
		_settings.NotifyChanged();
	}

	//==================================================================
	//  Mouse and keys
	//==================================================================
	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		if (!_interactive)
		{
			return;
		}
		Focus();
		TraceMouseDown(e);
		if (e.Button == MouseButtons.Left && _settings.Mode == GraphMode.XY)
		{
			_dragFrom = e.Location;
			_dragWorld = _settings.View;
			Cursor = Cursors.SizeAll;
		}
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		if (!_interactive)
		{
			return;
		}
		if (_dragFrom is { } from)
		{
			PanFrom(_dragWorld, e.X - from.X, e.Y - from.Y);
		}
		var world = _settings.View;
		var screen = PlotArea();
		if (screen.Contains(e.Location))
		{
			double x = world.MinX + (e.X - screen.Left) / screen.Width * world.Width;
			double y = world.MinY + (screen.Bottom - e.Y) / screen.Height * world.Height;
			PointerMoved?.Invoke(this, (x, y));
		}
		else
		{
			PointerMoved?.Invoke(this, null);
		}
		UpdateTrace(e.Location);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		base.OnMouseUp(e);
		if (_dragFrom != null)
		{
			_dragFrom = null;
			Cursor = Cursors.Cross;
		}
		if (_interactive)
		{
			TraceMouseUp(e);
		}
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		base.OnMouseLeave(e);
		if (_interactive)
		{
			ClearTrace();
			PointerMoved?.Invoke(this, null);
		}
	}

	protected override void OnMouseWheel(MouseEventArgs e)
	{
		base.OnMouseWheel(e);
		if (!_interactive || e.Delta == 0)
		{
			return;
		}
		double factor = Math.Pow(WheelZoom, e.Delta / 120.0);
		var keys = ModifierKeys;
		ZoomAt(e.Location, factor, xAxis: !keys.HasFlag(Keys.Control), yAxis: !keys.HasFlag(Keys.Shift));
	}

	protected override bool IsInputKey(Keys keyData)
	{
		bool arrow = (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down;
		return (_interactive && arrow) || base.IsInputKey(keyData);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);
		if (!_interactive || e.Modifiers != Keys.None)
		{
			return;
		}
		var screen = PlotArea();
		float stepX = screen.Width / 10;
		float stepY = screen.Height / 10;
		switch (e.KeyCode)
		{
			case Keys.Left:
				PanPixels(stepX, 0);    // see further left: the content moves right
				break;
			case Keys.Right:
				PanPixels(-stepX, 0);
				break;
			case Keys.Up:
				PanPixels(0, stepY);
				break;
			case Keys.Down:
				PanPixels(0, -stepY);
				break;
			case Keys.Add or Keys.Oemplus:
				ZoomCenter(WheelZoom);
				break;
			case Keys.Subtract or Keys.OemMinus:
				ZoomCenter(1 / WheelZoom);
				break;
			case Keys.Home:
				ResetRange();
				break;
			case Keys.F:
				FitY();
				break;
			default:
				return;
		}
		e.Handled = true;
	}

	protected override void OnGotFocus(EventArgs e)
	{
		base.OnGotFocus(e);
		Invalidate();
	}

	protected override void OnLostFocus(EventArgs e)
	{
		base.OnLostFocus(e);
		Invalidate();
	}
}
