// MainForm.Sizes.cs - The smallest window each view allows, and the sidebar's width (owner 2026-11-20)
//
// Every view is at least as wide as the menu bar and the widest ribbon
// group, and as tall as the menu,
// the whole ribbon when shown, the Samples and Input rows and one History
// entry. Numeric adds the keypad at its lowest height and the width its
// labels need. Advanced adds the sidebar: the Graph box with its toolbar,
// line list and a plot as tall as the Variables box at its smallest, and
// the Variables box with its toolbar and one row. Narrowing Advanced View
// first narrows History and Input; once they are as narrow as the sidebar,
// both narrow together. When the window is short, the keypad gives up
// height (down to its lowest) before History goes below one entry.

namespace SoftCalc.UI;

internal sealed partial class MainForm
{
	private int _sidebarWant = SidebarStart;    // the sidebar's width when there is room (the splitter's last drag)
	private bool _placingSidebar;

	private void WireSizes()
	{
		_split.SplitterMoved += (_, _) =>
		{
			if (!_placingSidebar && !_split.Panel1Collapsed)
			{
				_sidebarWant = _split.SplitterDistance;
			}
		};
		_split.SizeChanged += (_, _) => PlaceSidebar();
		_sidebar.SizeChanged += (_, _) => PlaceSidebarSplit();
		_sidebar.SplitterMoved += (_, _) => PlaceSidebarSplit();
		_column.SizeChanged += (_, _) => FitKeypadRow();
		Resize += (_, _) => UpdateMinimumSize();    // the ribbon wraps onto more rows when narrower
		_graphPanel.ListResized += (_, _) => UpdateMinimumSize();
		Shown += (_, _) => UpdateMinimumSize();
		DpiChanged += (_, _) => UpdateMinimumSize();
	}

	// A group box's title and borders
	private static Size Frame(GroupBox box)
	{
		return new Size(box.Width - box.DisplayRectangle.Width, box.Height - box.DisplayRectangle.Height);
	}

	private int VariablesBoxLeast => Frame(_variablesBox).Height + _variablesPanel.LeastHeight;

	// The plot no smaller than the Variables box at its smallest (owner 2026-11-20)
	private int GraphBoxLeast => Frame(_graphBox).Height + _graphPanel.ListsHeight + _variablesPanel.LeastHeight;

	private int SidebarLeast => Math.Max(Frame(_graphBox).Width + _graphPanel.ToolbarWidth,
		Frame(_variablesBox).Width + _variablesPanel.ToolbarWidth) + LogicalToDeviceUnits(6);

	//==================================================================
	//  The sidebar: its chosen width, or half the room when that is less
	//==================================================================
	private void PlaceSidebar()
	{
		if (!IsHandleCreated || _split.Panel1Collapsed || _split.Width <= 0)
		{
			return;
		}
		int room = _split.Width - _split.SplitterWidth;
		int distance = Math.Min(_sidebarWant, room / 2);
		distance = Math.Clamp(Math.Max(distance, Math.Min(SidebarLeast, room / 2)), _split.Panel1MinSize, Math.Max(_split.Panel1MinSize, room - _split.Panel2MinSize));
		if (distance == _split.SplitterDistance)
		{
			return;
		}
		_placingSidebar = true;
		try
		{
			_split.SplitterDistance = distance;
		}
		finally
		{
			_placingSidebar = false;
		}
	}

	// Graph and Variables each keep their smallest height
	private void PlaceSidebarSplit()
	{
		if (!IsHandleCreated || _sidebar.Height <= 0)
		{
			return;
		}
		int most = _sidebar.Height - _sidebar.SplitterWidth - VariablesBoxLeast;
		if (most < GraphBoxLeast)
		{
			return;
		}
		int distance = Math.Clamp(_sidebar.SplitterDistance, GraphBoxLeast, most);
		if (distance != _sidebar.SplitterDistance)
		{
			_sidebar.SplitterDistance = distance;
		}
	}

	//==================================================================
	//  The keypad's height: its own, less what History needs
	//==================================================================
	private void FitKeypadRow()
	{
		if (_view != CalcView.Numeric || _column.Height <= 0)
		{
			return;
		}
		int least = LogicalToDeviceUnits(Persistence.AppSettings.KeypadLeast);
		int want = LogicalToDeviceUnits(_prefs.KeypadHeight);
		int room = _column.Height - (int)_column.RowStyles[1].Height - (int)_column.RowStyles[2].Height - HistoryLeast
			- LogicalToDeviceUnits(6) * 3;    // the cells' margins
		int height = Math.Clamp(room, least, Math.Max(least, want));
		if ((int)_column.RowStyles[3].Height != height)
		{
			_column.RowStyles[3].Height = height;
		}
	}

	// One entry in the History box, with the cells' margins
	private int HistoryLeast => _history.ItemHeight + Frame(_historyBox).Height + LogicalToDeviceUnits(4);

	//==================================================================
	//  The smallest window
	//==================================================================
	private void UpdateMinimumSize()
	{
		if (!IsHandleCreated || WindowState == FormWindowState.Minimized)
		{
			return;
		}
		int menu = _menu.Items.Cast<ToolStripItem>().Where(item => item.Available)
			.Sum(item => item.GetPreferredSize(Size.Empty).Width + item.Margin.Horizontal) + _menu.Padding.Horizontal + LogicalToDeviceUnits(12);
		int margins = LogicalToDeviceUnits(6);    // a cell's margins, above and below or left and right
		int width = menu;
		if (_ribbon.Visible)
		{
			// the widest group whole: groups wrap onto rows, a group itself does not
			int group = _ribbon.Controls.Cast<Control>().Select(c => c.GetPreferredSize(Size.Empty).Width + c.Margin.Horizontal).DefaultIfEmpty(0).Max();
			width = Math.Max(width, group + _ribbon.Padding.Horizontal + LogicalToDeviceUnits(4));
		}
		int rows = _column.RowStyles[1].Height > 0 ? 3 : 2;    // History, Samples when shown, Input
		int height = HistoryLeast + (int)_column.RowStyles[1].Height + (int)_column.RowStyles[2].Height + rows * margins;
		if (_view == CalcView.Numeric)
		{
			width = Math.Max(width, _keypad.LeastWidth() + margins);
			height += LogicalToDeviceUnits(Persistence.AppSettings.KeypadLeast) + margins;
		}
		else if (_view == CalcView.Advanced)
		{
			width = Math.Max(width, 2 * SidebarLeast + _split.SplitterWidth);
			height = Math.Max(height, GraphBoxLeast + VariablesBoxLeast + _sidebar.SplitterWidth + 2 * margins);
		}
		// the ribbon's rows at the width the window will have
		int ribbon = _ribbon.Visible ? _ribbon.GetPreferredSize(new Size(Math.Max(ClientSize.Width, width), 0)).Height : 0;
		var least = new Size(width + Width - ClientSize.Width, _menu.Height + ribbon + height + Height - ClientSize.Height);
		if (least != MinimumSize)
		{
			MinimumSize = least;
		}
	}
}
