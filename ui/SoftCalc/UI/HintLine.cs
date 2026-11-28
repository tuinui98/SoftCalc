// HintLine.cs - The hint line under Input: its text drawn over its whole height (owner 2026-11-26)
//
// A stock Label two lines tall drew a one-line hint cut through the middle
// (about 21 px of its 56, whatever text renderer, ellipsis or repaint) when
// the window opened narrow in the History view. This control draws the
// text itself: from the top, word-wrapped into as many lines as it is
// tall, the last one cut with "...". A running stopwatch, timer or alarm
// shows at the right of the first line (Readout, owner 2026-11-28); a click
// on it raises ReadoutClick.

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

	private string _readout = "";
	private Rectangle _readoutArea;

	public event EventHandler? ReadoutClick;

	[System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
	public string Readout
	{
		get => _readout;
		set
		{
			if (value != _readout)
			{
				_readout = value;
				Invalidate();
			}
		}
	}

	protected override void OnTextChanged(EventArgs e)
	{
		base.OnTextChanged(e);
		Invalidate();
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		Cursor = _readout.Length > 0 && _readoutArea.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		base.OnMouseClick(e);
		if (_readout.Length > 0 && _readoutArea.Contains(e.Location))
		{
			ReadoutClick?.Invoke(this, EventArgs.Empty);
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		var area = new Rectangle(Padding.Left, Padding.Top, Math.Max(0, Width - Padding.Horizontal), Math.Max(0, Height - Padding.Vertical));
		_readoutArea = Rectangle.Empty;
		if (_readout.Length > 0)
		{
			var flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
			var size = TextRenderer.MeasureText(e.Graphics, _readout, Font, Size.Empty, flags);
			_readoutArea = new Rectangle(area.Right - size.Width, area.Top, size.Width, size.Height);
			TextRenderer.DrawText(e.Graphics, _readout, Font, _readoutArea, ForeColor, flags);
			area.Width = Math.Max(0, area.Width - size.Width - Font.Height);    // the hint stops short of it
		}
		TextRenderer.DrawText(e.Graphics, Text, Font, area, ForeColor,
			TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis
			| TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);
	}
}
