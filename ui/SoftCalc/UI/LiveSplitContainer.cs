// LiveSplitContainer.cs - A SplitContainer whose panels follow the splitter while it is dragged
//
// The stock one drags a ghost line and resizes on release; setting
// SplitterDistance from SplitterMoving instead runs away, as the built-in
// drag keeps measuring from where the splitter was (owner 2026-11-29: a
// small drag sent the chart or the table to 0). Here the drag is our own:
// the mouse offset in the splitter stays put, and the distance is kept
// inside the panels' minimum sizes.

namespace SoftCalc.UI;

internal sealed class LiveSplitContainer : SplitContainer
{
	private bool _dragging;
	private int _grip;    // where in the splitter the mouse took it

	private bool Horizontal => Orientation == Orientation.Horizontal;

	private int Along(MouseEventArgs e) => Horizontal ? e.Y : e.X;

	protected override void OnMouseDown(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left && SplitterRectangle.Contains(e.Location) && !IsSplitterFixed)
		{
			_dragging = true;
			_grip = Along(e) - SplitterDistance;
			Capture = true;
			return;    // not the stock ghost-line drag
		}
		base.OnMouseDown(e);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		if (!_dragging)
		{
			base.OnMouseMove(e);    // the split cursor over the splitter
			return;
		}
		Cursor = Horizontal ? Cursors.HSplit : Cursors.VSplit;
		int size = Horizontal ? Height : Width;
		int most = size - Panel2MinSize - SplitterWidth;
		int distance = Math.Clamp(Along(e) - _grip, Panel1MinSize, Math.Max(Panel1MinSize, most));
		if (distance != SplitterDistance)
		{
			SplitterDistance = distance;
			Update();    // draw the chart now, not after the drag
		}
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		if (_dragging)
		{
			_dragging = false;
			Capture = false;
			return;
		}
		base.OnMouseUp(e);
	}

	protected override void OnMouseCaptureChanged(EventArgs e)
	{
		if (!Capture)
		{
			_dragging = false;    // capture taken away (another window, Esc): the drag ends
		}
		base.OnMouseCaptureChanged(e);
	}
}
