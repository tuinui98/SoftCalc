// HintLine.cs - The hint line under Input: its text drawn over its whole height (owner 2026-11-26)
//
// A stock Label two lines tall drew a one-line hint cut through the middle
// (about 21 px of its 56, whatever text renderer, ellipsis or repaint) when
// the window opened narrow in the History view. This control draws the
// text itself: from the top, word-wrapped into as many lines as it is
// tall, the last one cut with "...".

namespace SoftCalc.UI;

internal sealed class HintLine : Control
{
	public HintLine()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
			| ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
		SetStyle(ControlStyles.Selectable, false);
		TabStop = false;
	}

	protected override void OnTextChanged(EventArgs e)
	{
		base.OnTextChanged(e);
		Invalidate();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		var area = new Rectangle(Padding.Left, Padding.Top, Math.Max(0, Width - Padding.Horizontal), Math.Max(0, Height - Padding.Vertical));
		TextRenderer.DrawText(e.Graphics, Text, Font, area, ForeColor,
			TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis
			| TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);
	}
}
