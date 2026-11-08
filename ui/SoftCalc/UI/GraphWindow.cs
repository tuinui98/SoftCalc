// GraphWindow.cs - The separate Graph window: XY/Polar, grid, range, save as PNG (SPEC 4)
//
// Shares GraphSettings with the sidebar graph. The Range panel edits the
// XY or Polar range; in Polar mode Theta Increment and R Min are fixed
// (read-only), as in the original. Size, place and the Range panel width
// are kept in GraphSettings and saved (owner 2026-11-09).

using System.Globalization;
using SoftCalc.Engine;
using SoftCalc.Logic;
using SoftCalc.Persistence;

namespace SoftCalc.UI;

internal sealed class GraphWindow : Form
{
	private readonly GraphSettings _settings;
	private readonly GraphPanel _graph;
	private readonly Label[] _labels = new Label[6];
	private readonly TextBox[] _boxes = new TextBox[6];
	private readonly Label _rangeError = new() { Dock = DockStyle.Fill, ForeColor = Theme.Error, AutoSize = false, Height = 40 };
	private readonly ToolStripMenuItem _xyItem = new("&XY Graph");
	private readonly ToolStripMenuItem _polarItem = new("&Polar Graph");
	private readonly ToolStripMenuItem _gridItem = new("&Show Grid");
	private readonly ToolStripMenuItem _traceItem = new("&Trace");
	private readonly ToolStripButton _xyButton = new("XY") { ToolTipText = "XY graph" };
	private readonly ToolStripButton _polarButton = new("Polar") { ToolTipText = "Polar graph" };
	private readonly ToolStripButton _gridButton = new("Grid") { ToolTipText = "Show grid" };
	private readonly ToolStripButton _traceButton = new("Trace") { ToolTipText = "Dot and value on the line nearest the mouse" };
	private readonly ToolStripMenuItem _squareItem = new("&1:1 Scale");
	private readonly ToolStripButton _squareButton = new("1:1") { ToolTipText = "Same scale on x and y, kept while zooming (click again to unlock)" };
	private readonly Label _status = new()
	{
		Dock = DockStyle.Bottom, AutoSize = false, Height = 22, TextAlign = ContentAlignment.MiddleLeft,
		ForeColor = SystemColors.GrayText, Padding = new Padding(4, 0, 0, 0), UseMnemonic = false,
	};
	private const string NavigationHelp = "Wheel: zoom (Shift: X, Ctrl: Y)   Drag: move   Click: copy value   Home: reset   F: fit Y";
	private (double X, double Y)? _lastPointer;
	private const string PolarHelp ="Wheel or + -: zoom R Max   Home: reset   F: fit R";

	public GraphWindow(EngineSession session, GraphSettings settings, DefinitionCommands commands, Action<string> insertValue)
	{
		_settings = settings;
		Text = "Graph";
		Size = new Size(760, 560);
		StartPosition = FormStartPosition.CenterParent;
		if (settings.WindowBounds is { } bounds)
		{
			StartPosition = FormStartPosition.Manual;
			Bounds = WindowPlacement.FitOnScreen(bounds, Screen.AllScreens.Select(s => s.WorkingArea).ToList());
		}
		if (settings.WindowMaximized)
		{
			WindowState = FormWindowState.Maximized;
		}
		ShowInTaskbar = false;
		Icon = SvgIcon.ProgramIcon();
		_graph = new GraphPanel(session, settings, commands, openWindow: null, interactive: true) { Dock = DockStyle.Fill };

		var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1 };
		split.Panel1.Controls.Add(BuildRangePanel());
		split.Panel2.Controls.Add(_graph);
		split.Panel2.Controls.Add(_status);    // docks first, at the bottom
		_graph.Plot.PointerMoved += (_, point) => ShowPointer(point);
		_graph.Plot.TraceChanged += (_, _) => ShowPointer(_lastPointer);
		_graph.Plot.InsertValue = insertValue;
		_graph.Plot.Notice += (_, text) => SetStatus(text);
		split.Panel2.SizeChanged += (_, _) =>
		{
			if (IsHandleCreated)
			{
				BeginInvoke(() => SetStatus(_statusText));    // after the layout
			}
		};
		ShowPointer(null);
		Controls.Add(split);
		Controls.Add(BuildToolbar());
		Controls.Add(BuildMenu());
		Load += (_, _) =>
		{
			split.SplitterDistance = _settings.RangeWidth;    // owner 2026-11-07: narrower than the original's panel
			split.SplitterMoved += (_, _) => _settings.RangeWidth = split.SplitterDistance;
		};
		// kept as it changes: the main window saves settings before this window closes
		ResizeEnd += (_, _) => KeepPlacement();
		Move += (_, _) => KeepPlacement();
		Resize += (_, _) => KeepPlacement();
		_settings.Changed += OnSettingsChanged;
		FormClosed += (_, _) => _settings.Changed -= OnSettingsChanged;
		ShowRange();
	}

	public void UpdateFunctions(IReadOnlyList<UserFunction> functions)
	{
		_graph.UpdateFunctions(functions);
	}

	private MenuStrip BuildMenu()
	{
		var menu = new MenuStrip();
		var file = new ToolStripMenuItem("&File");
		file.DropDownItems.Add(new ToolStripMenuItem("&Save as bitmap...", null, (_, _) => SaveBitmap()));
		var view = new ToolStripMenuItem("&View");
		_xyItem.Click += (_, _) => SetMode(GraphMode.XY);
		_polarItem.Click += (_, _) => SetMode(GraphMode.Polar);
		_gridItem.Click += (_, _) =>
		{
			_settings.ShowGrid = !_settings.ShowGrid;
			_settings.NotifyChanged();
		};
		var fit = new ToolStripMenuItem("&Fit Y", null, (_, _) => _graph.Plot.FitY()) { ShortcutKeys = Keys.Control | Keys.F };
		// Home is handled by the plot; a menu shortcut would take it from the range boxes
		var reset = new ToolStripMenuItem("&Reset Range", null, (_, _) => _graph.Plot.ResetRange()) { ShortcutKeyDisplayString = "Home" };
		var zoomIn = new ToolStripMenuItem("Zoom &In", null, (_, _) => _graph.Plot.ZoomCenter(0.8)) { ShortcutKeyDisplayString = "+" };
		var zoomOut = new ToolStripMenuItem("Zoom &Out", null, (_, _) => _graph.Plot.ZoomCenter(1 / 0.8)) { ShortcutKeyDisplayString = "-" };
		_traceItem.Click += (_, _) => SetTrace(!_traceItem.Checked);
		_squareItem.Click += (_, _) => ToggleSquare();
		view.DropDownItems.AddRange(new ToolStripItem[]
		{
			_xyItem, _polarItem, new ToolStripSeparator(), _gridItem, _traceItem, new ToolStripSeparator(), zoomIn, zoomOut, fit, _squareItem, reset,
		});
		menu.Items.AddRange(new ToolStripItem[] { file, view });
		MainMenuStrip = menu;
		return menu;
	}

	// Buttons for the View menu's commands (owner 2026-11-07)
	private ToolStrip BuildToolbar()
	{
		var bar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(4, 2, 4, 2) };
		_xyButton.Click += (_, _) => SetMode(GraphMode.XY);
		_polarButton.Click += (_, _) => SetMode(GraphMode.Polar);
		_gridButton.Click += (_, _) =>
		{
			_settings.ShowGrid = !_settings.ShowGrid;
			_settings.NotifyChanged();
		};
		_traceButton.Click += (_, _) => SetTrace(!_traceButton.Checked);
		_squareButton.Click += (_, _) => ToggleSquare();
		bar.Items.AddRange(new ToolStripItem[]
		{
			_xyButton, _polarButton, new ToolStripSeparator(), _gridButton, _traceButton, new ToolStripSeparator(),
			new ToolStripButton("Zoom +", null, (_, _) => _graph.Plot.ZoomCenter(0.8)) { ToolTipText = "Zoom in (+, mouse wheel)" },
			new ToolStripButton("Zoom -", null, (_, _) => _graph.Plot.ZoomCenter(1 / 0.8)) { ToolTipText = "Zoom out (-)" },
			new ToolStripButton("Fit", null, (_, _) => _graph.Plot.FitY()) { ToolTipText = "Fit Y to the lines (F, Ctrl+F); polar: R Max" },
			_squareButton,
			new ToolStripButton("Reset", null, (_, _) => _graph.Plot.ResetRange()) { ToolTipText = "Start-up range (Home)" },
		});
		return bar;
	}

	private void ToggleSquare()
	{
		if (_settings.Square)
		{
			_settings.Square = false;
			_settings.NotifyChanged();
		}
		else
		{
			_graph.Plot.MakeSquare();
		}
	}

	private void KeepPlacement()
	{
		if (WindowState == FormWindowState.Normal && IsHandleCreated)
		{
			_settings.WindowBounds = Bounds;
		}
		if (WindowState != FormWindowState.Minimized)
		{
			_settings.WindowMaximized = WindowState == FormWindowState.Maximized;
		}
	}

	private void SetTrace(bool on)
	{
		_settings.Trace = on;
		_settings.NotifyChanged();    // ShowRange updates the menu item and button
	}

	private Control BuildRangePanel()
	{
		var box = new GroupBox { Text = "Range", Dock = DockStyle.Fill };
		var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoScroll = true };
		for (int i = 0; i < 6; i++)
		{
			_labels[i] = new Label { AutoSize = true, Margin = new Padding(3, 8, 3, 0) };
			_boxes[i] = new TextBox { Dock = DockStyle.Top };
			layout.Controls.Add(_labels[i]);
			layout.Controls.Add(_boxes[i]);
		}
		var apply = new Button { Text = "Apply", AutoSize = true, Anchor = AnchorStyles.Right };
		apply.Click += (_, _) => ApplyRange();
		layout.Controls.Add(apply);
		layout.Controls.Add(_rangeError);
		box.Controls.Add(layout);
		return box;
	}

	private void SetMode(GraphMode mode)
	{
		_settings.Mode = mode;
		if (mode == GraphMode.XY && _settings.Square)
		{
			_graph.Plot.ApplySquareLock();
		}
		_settings.NotifyChanged();
	}

	private void OnSettingsChanged(object? sender, EventArgs e)
	{
		ShowRange();
		ShowPointer(null);    // mode may have changed; the next mouse move shows coordinates again
	}

	// Shows the active mode's range with 6 decimals, as the original did
	private void ShowRange()
	{
		bool polar = _settings.Mode == GraphMode.Polar;
		string[] names = polar
			? new[] { "Theta Min", "Theta Max", "Theta Increment", "R Min", "R Max", "R Increment" }
			: new[] { "X Min", "X Max", "X Increment", "Y Min", "Y Max", "Y Increment" };
		double[] values = polar
			? new[] { _settings.MinT, _settings.MaxT, _settings.GridT, _settings.MinR, _settings.MaxR, _settings.GridR }
			: new[] { _settings.MinX, _settings.MaxX, _settings.GridX, _settings.MinY, _settings.MaxY, _settings.GridY };
		for (int i = 0; i < 6; i++)
		{
			_labels[i].Text = names[i];
			_boxes[i].Text = values[i].ToString("F6", CultureInfo.InvariantCulture);
			_boxes[i].ReadOnly = polar && (i == 2 || i == 3);
		}
		_xyItem.Checked = !polar;
		_polarItem.Checked = polar;
		_gridItem.Checked = _settings.ShowGrid;
		_xyButton.Checked = !polar;
		_polarButton.Checked = polar;
		_gridButton.Checked = _settings.ShowGrid;
		_traceItem.Checked = _traceButton.Checked = _settings.Trace;
		_squareItem.Checked = _squareButton.Checked = _settings.Square && !polar;
		_squareItem.Enabled = _squareButton.Enabled = !polar;    // polar is square already
		_rangeError.Text = "";
	}

	private void ApplyRange()
	{
		var values = new double[6];
		for (int i = 0; i < 6; i++)
		{
			if (!double.TryParse(_boxes[i].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
			{
				_rangeError.Text = $"{_labels[i].Text}: enter a number";
				return;
			}
		}
		bool polar = _settings.Mode == GraphMode.Polar;
		string? error = polar
			? PlotMath.CheckRange("Theta", values[0], values[1], _settings.GridT)
				?? PlotMath.CheckRange("R", _settings.MinR, values[4], values[5])
			: PlotMath.CheckRange("X", values[0], values[1], values[2])
				?? PlotMath.CheckRange("Y", values[3], values[4], values[5]);
		if (error != null)
		{
			_rangeError.Text = error;
			return;
		}
		if (polar)
		{
			(_settings.MinT, _settings.MaxT, _settings.MaxR, _settings.GridR) = (values[0], values[1], values[4], values[5]);
		}
		else
		{
			_settings.Square = false;    // a typed range ends 1:1
			(_settings.MinX, _settings.MaxX, _settings.GridX) = (values[0], values[1], values[2]);
			(_settings.MinY, _settings.MaxY, _settings.GridY) = (values[3], values[4], values[5]);
		}
		_settings.NotifyChanged();
	}

	private string _statusText = "";

	// One line that fits: a full-precision value can be hundreds of digits,
	// and the label would wrap it out of sight; the end is cut with "..."
	private void SetStatus(string text)
	{
		_statusText = text;
		const TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
		int room = (_status.Parent?.ClientSize.Width ?? _status.Width) - _status.Padding.Horizontal - 4;
		string shown = text;
		if (room > 0 && TextRenderer.MeasureText(text, _status.Font, Size.Empty, flags).Width > room)
		{
			int low = 0, high = text.Length;
			while (low < high)
			{
				int mid = (low + high + 1) / 2;
				if (TextRenderer.MeasureText(text[..mid] + "...", _status.Font, Size.Empty, flags).Width <= room)
				{
					low = mid;
				}
				else
				{
					high = mid - 1;
				}
			}
			shown = text[..low] + "...";
		}
		_status.Text = shown;
	}

	// Coordinates under the mouse, rounded to the pixel size; help text otherwise
	private void ShowPointer((double X, double Y)? point)
	{
		_lastPointer = point;
		if (point != null && _graph.Plot.Trace is { } trace)
		{
			if (trace.IsEquation)
			{
				SetStatus($"{trace.Function}:   x = {trace.XText}   y = {trace.YText}");    // full precision
				return;
			}
			// the traced value, full precision: g(2.35) = 0.9977...
			string at = PlotMath.FormatTick(trace.T, trace.Step);
			SetStatus($"{trace.Function}({at}) = {trace.Text ?? "undefined"}");
			return;
		}
		if (point is not { } p)
		{
			SetStatus(_settings.Mode == GraphMode.Polar ? PolarHelp : NavigationHelp);
			return;
		}
		double pixel = _graph.Plot.PixelSize();
		string x = PlotMath.FormatTick(p.X, pixel), y = PlotMath.FormatTick(p.Y, pixel);
		if (_settings.Mode == GraphMode.Polar)
		{
			string r = PlotMath.FormatTick(Math.Sqrt(p.X * p.X + p.Y * p.Y), pixel);
			string theta = Math.Atan2(p.Y, p.X).ToString("F3", CultureInfo.InvariantCulture);
			SetStatus($"x = {x}   y = {y}   r = {r}   theta = {theta} rad");
			return;
		}
		SetStatus($"x = {x}   y = {y}");
	}

	private void SaveBitmap()
	{
		using var dialog = new SaveFileDialog { Filter = "PNG image (*.png)|*.png", DefaultExt = "png", FileName = "graph.png" };
		if (dialog.ShowDialog(this) == DialogResult.OK)
		{
			_graph.Plot.SaveImage(dialog.FileName);
		}
	}
}
