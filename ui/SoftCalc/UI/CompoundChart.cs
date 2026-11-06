// CompoundChart.cs - Growth graph of the Compound Interest window (E11)
//
// Stacked areas over time: money paid in (principal and contributions)
// below, interest earned above, so the top edge is the balance. X is in
// years, Y in money; both axes get round-number ticks.

using System.Drawing.Drawing2D;
using System.Globalization;

namespace SoftCalc.UI;

internal sealed class CompoundChart : Control
{
	private static readonly Color PaidColor = Color.FromArgb(110, 150, 205);
	private static readonly Color InterestColor = Color.FromArgb(120, 190, 120);
	private const int MarginLeft = 64;
	private const int MarginRight = 12;
	private const int MarginTop = 26;
	private const int MarginBottom = 26;

	private double _principal;
	private IReadOnlyList<double> _paid = Array.Empty<double>();
	private IReadOnlyList<double> _interest = Array.Empty<double>();
	private int _perYear = 1;

	public CompoundChart()
	{
		DoubleBuffered = true;
		ResizeRedraw = true;
		BackColor = SystemColors.Window;
	}

	// paid and interest are cumulative per period; point 0 is the principal alone
	public void SetData(double principal, IReadOnlyList<double> paid, IReadOnlyList<double> interest, int perYear)
	{
		_principal = principal;
		_paid = paid;
		_interest = interest;
		_perYear = Math.Max(1, perYear);
		Invalidate();
	}

	public static double NiceStep(double range, int maxTicks)
	{
		if (range <= 0 || double.IsNaN(range) || double.IsInfinity(range))
		{
			return 1;
		}
		double raw = range / Math.Max(1, maxTicks);
		double power = Math.Pow(10, Math.Floor(Math.Log10(raw)));
		double unit = raw / power;
		double nice = unit <= 1 ? 1 : unit <= 2 ? 2 : unit <= 5 ? 5 : 10;
		return nice * power;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		var g = e.Graphics;
		var plot = Rectangle.FromLTRB(MarginLeft, MarginTop, Width - MarginRight, Height - MarginBottom);
		if (plot.Width < 40 || plot.Height < 40 || _paid.Count == 0)
		{
			return;
		}
		int count = _paid.Count;
		double years = (double)count / _perYear;
		double top = 0;
		for (int i = 0; i < count; i++)
		{
			top = Math.Max(top, _paid[i] + _interest[i]);
		}
		top = Math.Max(top, _principal);
		if (!(top > 0) || double.IsInfinity(top))
		{
			return;
		}
		double yStep = NiceStep(top, Math.Max(2, plot.Height / 40));
		double yMax = Math.Ceiling(top / yStep) * yStep;
		double xStep = Math.Max(1.0 / _perYear, NiceStep(years, Math.Max(2, plot.Width / 70)));

		PointF At(double year, double money) => new(
			(float)(plot.Left + year / years * plot.Width),
			(float)(plot.Bottom - money / yMax * plot.Height));

		//==================================================================
		//  Grid and axes
		//==================================================================
		using var gridPen = new Pen(Theme.Grid);
		using var axisPen = new Pen(SystemColors.GrayText);
		const TextFormatFlags right = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
		const TextFormatFlags center = TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPrefix;
		for (double y = 0; y <= yMax + yStep / 2; y += yStep)
		{
			var p = At(0, y);
			g.DrawLine(gridPen, plot.Left, p.Y, plot.Right, p.Y);
			TextRenderer.DrawText(g, Short(y), Font, new Rectangle(0, (int)p.Y - 8, MarginLeft - 6, 16), ForeColor, right);
		}
		for (double x = 0; x <= years + xStep / 1000; x += xStep)
		{
			var p = At(x, 0);
			g.DrawLine(gridPen, p.X, plot.Top, p.X, plot.Bottom);
			TextRenderer.DrawText(g, x.ToString("0.##", CultureInfo.InvariantCulture), Font,
				new Rectangle((int)p.X - 30, plot.Bottom + 4, 60, 16), ForeColor, center);
		}

		//==================================================================
		//  Stacked areas (at most one point per pixel column)
		//==================================================================
		int step = Math.Max(1, count / Math.Max(1, plot.Width));
		var lower = new List<PointF> { At(0, 0), At(0, _principal) };
		var upper = new List<PointF> { At(0, _principal) };
		for (int i = 0; i < count; i += step)
		{
			AddPoint(i);
		}
		if ((count - 1) % step != 0)
		{
			AddPoint(count - 1);
		}
		void AddPoint(int i)
		{
			double year = (i + 1.0) / _perYear;
			lower.Add(At(year, _paid[i]));
			upper.Add(At(year, _paid[i] + _interest[i]));
		}
		var paidArea = new List<PointF>(lower) { At(years, 0) };
		var interestArea = new List<PointF>(upper);
		for (int i = lower.Count - 1; i >= 1; i--)
		{
			interestArea.Add(lower[i]);
		}
		g.SmoothingMode = SmoothingMode.AntiAlias;
		using (var paidBrush = new SolidBrush(PaidColor))
		using (var interestBrush = new SolidBrush(InterestColor))
		{
			g.FillPolygon(interestBrush, interestArea.ToArray());
			g.FillPolygon(paidBrush, paidArea.ToArray());
		}
		g.SmoothingMode = SmoothingMode.None;
		g.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
		g.DrawLine(axisPen, plot.Left, plot.Top, plot.Left, plot.Bottom);

		//==================================================================
		//  Legend and axis name
		//==================================================================
		int lx = plot.Left + 6;
		lx = Legend(g, lx, PaidColor, "Paid in");
		Legend(g, lx + 12, InterestColor, "Interest");
		TextRenderer.DrawText(g, "years", Font, new Rectangle(0, plot.Bottom + 4, MarginLeft - 12, 16), SystemColors.GrayText,
			TextFormatFlags.Right | TextFormatFlags.NoPrefix);
	}

	private int Legend(Graphics g, int x, Color color, string text)
	{
		using (var brush = new SolidBrush(color))
		{
			g.FillRectangle(brush, x, 8, 12, 12);
		}
		var size = TextRenderer.MeasureText(g, text, Font);
		TextRenderer.DrawText(g, text, Font, new Point(x + 16, 7), ForeColor, TextFormatFlags.NoPrefix);
		return x + 16 + size.Width;
	}

	// Axis labels: 1500 -> 1.5k, 2000000 -> 2M
	internal static string Short(double value)
	{
		double size = Math.Abs(value);
		if (size >= 1e9)
		{
			return (value / 1e9).ToString("0.##", CultureInfo.InvariantCulture) + "G";
		}
		if (size >= 1e6)
		{
			return (value / 1e6).ToString("0.##", CultureInfo.InvariantCulture) + "M";
		}
		if (size >= 1e3)
		{
			return (value / 1e3).ToString("0.##", CultureInfo.InvariantCulture) + "k";
		}
		return value.ToString("0.##", CultureInfo.InvariantCulture);
	}
}
