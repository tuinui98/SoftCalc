// ShapePlot.cs - The Advanced random tab's picture (design/random_plan.md R3)
//
// The chosen shape as a filled curve over From-To, and after Generate
// the results as bars on the same scale (both have area 1), so it shows
// at a glance whether the draws follow the shape.

using SoftCalc.Logic;

namespace SoftCalc.UI;

internal sealed class ShapePlot : Control
{
	private double[] _line = Array.Empty<double>();
	private double[] _bars = Array.Empty<double>();
	private string _from = "", _to = "";

	public ShapePlot()
	{
		SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
			| ControlStyles.UserPaint, true);
		MinimumSize = new Size(120, 90);
	}

	internal double[] LineShown => _line;
	internal double[] BarsShown => _bars;

	public void Show(double[] line, double[] bars, string from, string to)
	{
		(_line, _bars, _from, _to) = (line, bars, from, to);
		Invalidate();
	}

	public void ClearBars()
	{
		_bars = Array.Empty<double>();
		Invalidate();
	}

	// The curve's top: its highest point, but not the spikes of a U shape's ends
	private double ScaleTop()
	{
		double top = _bars.DefaultIfEmpty(0).Max();
		if (_line.Length > 0)
		{
			var sorted = _line.Order().ToArray();
			double p90 = sorted[(int)(sorted.Length * 0.9)];
			top = Math.Max(top, Math.Min(sorted[^1], Math.Max(p90 * 2, 1.2)));
		}
		return top <= 0 ? 1 : top * 1.08;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		var g = e.Graphics;
		g.Clear(SystemColors.Window);
		g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
		var font = Font;
		int labelHeight = TextRenderer.MeasureText(g, "0", font).Height;
		var area = new RectangleF(8, 6, Width - 16, Height - 12 - labelHeight);
		if (area.Width < 10 || area.Height < 10)
		{
			return;
		}
		double top = ScaleTop();
		float Y(double v) => area.Bottom - (float)(Math.Min(v, top) / top * area.Height);

		// bars of the results
		if (_bars.Length > 0)
		{
			using var barBrush = new SolidBrush(Color.FromArgb(120, SystemColors.Highlight));
			float slot = area.Width / _bars.Length;
			for (int i = 0; i < _bars.Length; i++)
			{
				float y = Y(_bars[i]);
				float gap = slot > 6 ? 1 : 0;
				g.FillRectangle(barBrush, area.Left + i * slot + gap, y, Math.Max(1, slot - 2 * gap), area.Bottom - y);
			}
		}

		// the shape
		if (_line.Length > 1)
		{
			var points = _line.Select((v, i) => new PointF(area.Left + area.Width * i / (_line.Length - 1), Y(v))).ToArray();
			Color colour = Theme.Series(1);
			if (_bars.Length == 0)
			{
				using var fill = new SolidBrush(Color.FromArgb(50, colour));
				var shape = points.Prepend(new PointF(area.Left, area.Bottom)).Append(new PointF(area.Right, area.Bottom)).ToArray();
				g.FillPolygon(fill, shape);
			}
			using var pen = new Pen(colour, 2f);
			g.DrawLines(pen, points);
		}

		using var axis = new Pen(Theme.PlotAxis);
		g.DrawLine(axis, area.Left, area.Bottom, area.Right, area.Bottom);
		TextRenderer.DrawText(g, _from, font, new Point((int)area.Left, (int)area.Bottom + 2), SystemColors.GrayText);
		var size = TextRenderer.MeasureText(g, _to, font);
		TextRenderer.DrawText(g, _to, font, new Point((int)area.Right - size.Width, (int)area.Bottom + 2), SystemColors.GrayText);
	}
}
