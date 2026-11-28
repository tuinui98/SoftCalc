// LiveSplitTests.cs - The Compound window's splitter follows the mouse, without running away

using System.Reflection;
using SoftCalc.UI;

namespace SoftCalc.Tests;

public class LiveSplitTests
{
	private static void Mouse(Control control, string name, int x, int y)
	{
		var method = typeof(LiveSplitContainer).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(MouseEventArgs) })!;
		method.Invoke(control, new object[] { new MouseEventArgs(MouseButtons.Left, 1, x, y, 0) });
	}

	// owner 2026-11-29: a small drag up sent the chart to 0, a small drag down the table
	[Fact]
	public void ASmallDragMovesTheSplitterALittle()
	{
		using var form = new Form { Size = new Size(400, 500) };
		var split = new LiveSplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, Panel1MinSize = 60, Panel2MinSize = 60 };
		form.Controls.Add(split);
		form.CreateControl();
		split.SplitterDistance = 200;
		int middle = split.SplitterDistance + split.SplitterWidth / 2;
		Mouse(split, "OnMouseDown", 50, middle);
		Mouse(split, "OnMouseMove", 50, middle - 10);
		Assert.Equal(190, split.SplitterDistance);
		Mouse(split, "OnMouseMove", 50, middle - 12);
		Assert.Equal(188, split.SplitterDistance);
		Mouse(split, "OnMouseMove", 50, middle + 15);
		Assert.Equal(215, split.SplitterDistance);
		Mouse(split, "OnMouseMove", 50, -500);
		Assert.Equal(60, split.SplitterDistance);    // kept to the panels' minimum sizes
		Mouse(split, "OnMouseMove", 50, 5000);
		Assert.Equal(split.Height - 60 - split.SplitterWidth, split.SplitterDistance);
		Mouse(split, "OnMouseUp", 50, 5000);
		Mouse(split, "OnMouseMove", 50, 100);
		Assert.Equal(split.Height - 60 - split.SplitterWidth, split.SplitterDistance);    // released: stays
	}
}
