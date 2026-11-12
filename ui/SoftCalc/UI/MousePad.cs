// MousePad.cs - The Entropy tab's box for mouse movement (design/random_plan.md R5)
//
// Double-buffered, so redrawing on every move does not flicker. Shows
// the moves so far and a short trail of the last positions.

namespace SoftCalc.UI;

internal sealed class MousePad : Control
{
	private const int TrailLength = 60;
	private readonly Queue<Point> _trail = new();
	private string _caption = "Move the mouse here";

	public MousePad()
	{
		SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
			| ControlStyles.UserPaint, true);
	}

	[System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
	public string Caption
	{
		get => _caption;
		set
		{
			_caption = value;
			Invalidate();
		}
	}

	public void AddPoint(Point point)
	{
		_trail.Enqueue(point);
		while (_trail.Count > TrailLength)
		{
			_trail.Dequeue();
		}
		Invalidate();
	}

	public void Reset(string caption)
	{
		_trail.Clear();
		Caption = caption;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		var g = e.Graphics;
		g.Clear(SystemColors.Window);
		g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
		if (_trail.Count > 1)
		{
			using var pen = new Pen(Color.FromArgb(110, Theme.Series(1)), 2f);
			g.DrawLines(pen, _trail.ToArray());
		}
		TextRenderer.DrawText(g, _caption, Font, ClientRectangle, SystemColors.GrayText,
			TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
		ControlPaint.DrawBorder(g, ClientRectangle, Theme.PlotAxis, ButtonBorderStyle.Solid);
	}
}
