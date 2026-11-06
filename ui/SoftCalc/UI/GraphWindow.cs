// GraphWindow.cs - The separate Graph window: XY/Polar, grid, range, save as PNG (SPEC 4)
//
// Shares GraphSettings with the sidebar graph. The Range panel edits the
// XY or Polar range; in Polar mode Theta Increment and R Min are fixed
// (read-only), as in the original.

using System.Globalization;
using SoftCalc.Engine;
using SoftCalc.Logic;

namespace SoftCalc.UI;

internal sealed class GraphWindow : Form
{
	private readonly GraphSettings _settings;
	private readonly GraphPanel _graph;
	private readonly Label[] _labels = new Label[6];
	private readonly TextBox[] _boxes = new TextBox[6];
	private readonly Label _rangeError = new() { Dock = DockStyle.Fill, ForeColor = Color.Firebrick, AutoSize = false, Height = 40 };
	private readonly ToolStripMenuItem _xyItem = new("&XY Graph");
	private readonly ToolStripMenuItem _polarItem = new("&Polar Graph");
	private readonly ToolStripMenuItem _gridItem = new("&Show Grid");

	public GraphWindow(EngineSession session, GraphSettings settings, DefinitionCommands commands)
	{
		_settings = settings;
		Text = "Graph";
		Size = new Size(760, 560);
		StartPosition = FormStartPosition.CenterParent;
		ShowInTaskbar = false;
		Icon = SvgIcon.ProgramIcon();
		_graph = new GraphPanel(session, settings, commands, openWindow: null) { Dock = DockStyle.Fill };

		var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1 };
		split.Panel1.Controls.Add(BuildRangePanel());
		split.Panel2.Controls.Add(_graph);
		Controls.Add(split);
		Controls.Add(BuildMenu());
		Load += (_, _) => split.SplitterDistance = 230;
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
		view.DropDownItems.AddRange(new ToolStripItem[] { _xyItem, _polarItem, new ToolStripSeparator(), _gridItem });
		menu.Items.AddRange(new ToolStripItem[] { file, view });
		MainMenuStrip = menu;
		return menu;
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
		_settings.NotifyChanged();
	}

	private void OnSettingsChanged(object? sender, EventArgs e)
	{
		ShowRange();
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
			(_settings.MinX, _settings.MaxX, _settings.GridX) = (values[0], values[1], values[2]);
			(_settings.MinY, _settings.MaxY, _settings.GridY) = (values[3], values[4], values[5]);
		}
		_settings.NotifyChanged();
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
