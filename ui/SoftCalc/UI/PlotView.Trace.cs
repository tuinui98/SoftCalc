// PlotView.Trace.cs - Graph window trace: a dot on the curve at the mouse's x
//
// The x under the mouse (polar: the angle) is rounded to what the status
// line shows, then the engine computes the function there at full
// precision (`value_at`), so the copied or inserted value is exact for
// the x that is displayed. One request at a time; while one runs only
// the newest position waits (design/graph_plan.md, G2).

using System.ComponentModel;
using System.Globalization;
using SoftCalc.Engine;
using SoftCalc.Logic;

namespace SoftCalc.UI;

// T is x (polar: theta); Step is the rounding used to show it
internal readonly record struct TracePoint(string Function, double T, double Step, string? Text, double? Value);

internal sealed partial class PlotView
{
	private const double ThetaStep = 0.001;    // [DEF] trace angle shown with 3 decimals
	private const int ClickSlop = 3;    // pixels a click may move and still count as a click

	private double? _traceWanted;
	private double _traceStep;
	private bool _traceBusy;
	private int _traceGeneration;
	private Point _downAt;
	private TracePoint? _menuTrace;

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public bool TraceEnabled { get; set; } = true;

	public TracePoint? Trace { get; private set; }

	// Sends a value to the main window's Input (set by the Graph window)
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	public Action<string>? InsertValue { get; set; }

	public event EventHandler? TraceChanged;

	// Short messages for the status line ("copied ...")
	public event EventHandler<string>? Notice;

	private void UpdateTrace(Point at)
	{
		if (!TraceEnabled || _dragFrom != null || TraceTarget(at) is not { } target)
		{
			ClearTrace();
			return;
		}
		if (Trace is { } current && current.T == target.T && current.Function == target.Function)
		{
			return;
		}
		_traceStep = target.Step;
		_traceWanted = target.T;
		_ = FetchTraceAsync();
	}

	// The function and the rounded x (polar: theta) under a screen point, or null
	private (string Function, double T, double Step)? TraceTarget(Point at)
	{
		string? function = _settings.Function;
		var screen = PlotArea();
		if (function == null || !_settings.IsShown(function) || !screen.Contains(at))    // trace the selected line
		{
			return null;
		}
		var world = _settings.View;
		double wx = world.MinX + (at.X - screen.Left) / screen.Width * world.Width;
		double wy = world.MinY + (screen.Bottom - at.Y) / screen.Height * world.Height;
		double t;
		double step;
		if (_settings.Mode == GraphMode.Polar)
		{
			step = ThetaStep;
			double? inRange = PlotMath.AngleInRange(Math.Atan2(wy, wx), _settings.MinT, _settings.MaxT);
			if (inRange == null)
			{
				return null;
			}
			t = inRange.Value;
		}
		else
		{
			step = PixelSize();
			t = wx;
		}
		t = double.Parse(PlotMath.FormatTick(t, step), CultureInfo.InvariantCulture);    // the x the status line shows
		return (function, t, step);
	}

	private async Task FetchTraceAsync()
	{
		if (_traceBusy)
		{
			return;    // the running request picks up _traceWanted when it ends
		}
		_traceBusy = true;
		try
		{
			while (_traceWanted is { } t && _settings.Function is { } function)
			{
				_traceWanted = null;
				int generation = _traceGeneration;
				double step = _traceStep;
				try
				{
					var (text, value) = await _session.ValueAtAsync(function, t);
					if (generation == _traceGeneration && _traceWanted == null)
					{
						Trace = new TracePoint(function, t, step, text, value);
						Invalidate();
						TraceChanged?.Invoke(this, EventArgs.Empty);
					}
				}
				catch (EngineException)
				{
					// the function was deleted or the engine restarted; the next move retries
				}
			}
		}
		finally
		{
			_traceBusy = false;
		}
	}

	private void ClearTrace()
	{
		_traceWanted = null;
		_traceGeneration++;
		if (Trace != null)
		{
			Trace = null;
			Invalidate();
			TraceChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	private void DrawTrace(Graphics g, WorldRect world, RectangleF screen)
	{
		if (Trace is not { Value: { } v } trace || trace.Function != _settings.Function)
		{
			return;
		}
		var point = _settings.Mode == GraphMode.Polar
			? PlotMath.ToScreen(v * Math.Cos(trace.T), v * Math.Sin(trace.T), world, screen)
			: PlotMath.ToScreen(trace.T, v, world, screen);
		if (!screen.Contains(point))
		{
			return;
		}
		using (var guide = new Pen(SystemColors.GrayText) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot })
		{
			if (_settings.Mode == GraphMode.XY)
			{
				g.DrawLine(guide, point.X, screen.Top, point.X, screen.Bottom);
			}
		}
		using (var fill = new SolidBrush(Theme.Series(_settings.ColourIndex(trace.Function))))
		using (var ring = new Pen(SystemColors.Window, 1.5f))
		{
			g.FillEllipse(fill, point.X - 4, point.Y - 4, 8, 8);
			g.DrawEllipse(ring, point.X - 4, point.Y - 4, 8, 8);
		}
		string label = v.ToString("G6", CultureInfo.InvariantCulture);
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
	//  Click copies; right-click: Copy Value / Insert Value in Input
	//==================================================================
	private void TraceMouseDown(MouseEventArgs e)
	{
		_downAt = e.Location;
	}

	private void TraceMouseUp(MouseEventArgs e)
	{
		bool click = Math.Abs(e.X - _downAt.X) <= ClickSlop && Math.Abs(e.Y - _downAt.Y) <= ClickSlop;
		if (e.Button == MouseButtons.Left && click && TraceEnabled && TraceTarget(e.Location) is { } target)
		{
			_ = CopyAtAsync(target.Function, target.T, target.Step);
		}
	}

	// A click asks for its own value, so it is copied even before the trace has caught up
	private async Task CopyAtAsync(string function, double t, double step)
	{
		try
		{
			var (text, value) = await _session.ValueAtAsync(function, t);
			CopyValue(new TracePoint(function, t, step, text, value));
		}
		catch (EngineException)
		{
			// engine restarting or function gone: nothing to copy
		}
	}

	private void CopyValue(TracePoint? trace)
	{
		if (trace is { Text: { } text })
		{
			Clipboard.SetText(text);
			Notice?.Invoke(this, $"Copied {text}");
		}
	}

	private ContextMenuStrip BuildTraceMenu()
	{
		var menu = new ContextMenuStrip();
		var copy = new ToolStripMenuItem("&Copy Value", null, (_, _) => CopyValue(_menuTrace));
		var insert = new ToolStripMenuItem("&Insert Value in Input", null, (_, _) =>
		{
			if (_menuTrace is { Text: { } text })
			{
				InsertValue?.Invoke(text);
			}
		});
		menu.Items.AddRange(new ToolStripItem[] { copy, insert });
		menu.Opening += (_, _) =>
		{
			_menuTrace = Trace;    // the mouse leaves the plot while the menu is open
			copy.Enabled = insert.Enabled = _menuTrace is { Text: not null };
			insert.Visible = InsertValue != null;
		};
		return menu;
	}
}
