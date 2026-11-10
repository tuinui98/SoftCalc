// GraphPanel.cs - Graph toolbar, line checklist and plot (sidebar and Graph window)
//
// Both instances share one GraphSettings: a ticked line is drawn in
// both, in its own colour (the swatch in the list); the selected one is
// what Edit and Delete use (design/graph_plan.md, G3). Lines are the
// one-parameter user functions (others are listed greyed out) and the
// equations in x and y typed here (graph2_plan G5). Clicking a swatch
// picks the line's colour with the Windows colour dialog (G4).

using SoftCalc.Engine;
using SoftCalc.Logic;

namespace SoftCalc.UI;

internal sealed class GraphPanel : Panel
{
	private const int MaxRows = 4;    // [DEF] list height before it scrolls
	private const int SwatchSize = 14;

	// One list row: a user function, or an equation (Function null)
	private sealed record Row(string Name, string Text, UserFunction? Function)
	{
		public bool IsEquation => Function == null;
	}

	private readonly EngineSession _session;
	private readonly GraphSettings _settings;
	private readonly DefinitionCommands _commands;
	private readonly ListView _functions = new()
	{
		Dock = DockStyle.Top, View = View.Details, CheckBoxes = true, FullRowSelect = true, MultiSelect = false,
		HeaderStyle = ColumnHeaderStyle.None, HideSelection = false, ShowItemToolTips = true,
	};
	private readonly ImageList _swatches = new() { ImageSize = new Size(SwatchSize, SwatchSize), ColorDepth = ColorDepth.Depth32Bit };
	private readonly ToolStripButton _edit = new("Edit");
	private readonly ToolStripButton _delete = new("Delete");
	private IReadOnlyList<UserFunction> _list = Array.Empty<UserFunction>();
	private List<Row> _rows = new();
	private string _linesKey = "";    // equations and colours the list was filled with
	private bool _filling;
	private (int Index, long Ticks) _lastToggle = (-1, 0);
	private bool _ready;    // list handle created and refilled; before that its check events are replays

	public PlotView Plot { get; }

	public GraphPanel(EngineSession session, GraphSettings settings, DefinitionCommands commands, Action? openWindow, bool interactive = false)
	{
		_session = session;
		_settings = settings;
		_commands = commands;
		Plot = new PlotView(session, settings, interactive) { Dock = DockStyle.Fill };

		//==================================================================
		//  BLOCK 1 - Toolbar: Add (function or equation), Edit, Delete, Window
		//==================================================================
		var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
		var add = new ToolStripSplitButton("Add") { ToolTipText = "Add a function (arrow: an equation in x and y, or an example curve)" };
		add.ButtonClick += async (_, _) => await _commands.AddFunctionAsync();
		add.DropDownItems.Add(new ToolStripMenuItem("&Function...", null, async (_, _) => await _commands.AddFunctionAsync()));
		add.DropDownItems.Add(new ToolStripMenuItem("&Equation...", null, async (_, _) => await AddEquationAsync()));
		add.DropDownItems.Add(new ToolStripSeparator());
		add.DropDownItems.Add(new ToolStripMenuItem("E&xamples...", null, async (_, _) => await AddExampleAsync()));
		toolbar.Items.Add(add);
		_edit.Click += async (_, _) => await EditSelectedAsync();
		_delete.Click += async (_, _) => await DeleteSelectedAsync();
		toolbar.Items.Add(_edit);
		toolbar.Items.Add(_delete);
		if (openWindow != null)
		{
			toolbar.Items.Add(new ToolStripButton("Window", null, (_, _) => openWindow()));
		}
		// end BLOCK 1

		//==================================================================
		//  BLOCK 2 - The list: ticks, selection, swatch clicks, menu
		//==================================================================
		_functions.Columns.Add("");
		_functions.SmallImageList = _swatches;
		_functions.Resize += (_, _) => FitColumn();
		_functions.ItemCheck += (_, e) =>
		{
			if (_filling)
			{
				return;
			}
			if (!Graphable(e.Index))
			{
				e.NewValue = CheckState.Unchecked;    // only one-parameter functions can be drawn
			}
			else
			{
				_lastToggle = (e.Index, Environment.TickCount64);
			}
		};
		_functions.ItemChecked += (_, e) =>
		{
			if (_filling || !_ready || !Graphable(e.Item.Index))
			{
				return;
			}
			string name = _rows[e.Item.Index].Name;
			if (e.Item.Checked == _settings.IsShown(name))
			{
				return;
			}
			_settings.SetShown(name, e.Item.Checked);
			if (e.Item.Checked)
			{
				_settings.Function = name;    // ticking one also selects it
			}
			Notify();
		};
		// Creating the handle replays item states (unchecked first), which would
		// untick every line; ignore check events until the list is refilled
		_functions.HandleCreated += (_, _) => _functions.BeginInvoke(() =>
		{
			_ready = true;
			FillList();
		});
		_functions.HandleDestroyed += (_, _) => _ready = false;
		_functions.SelectedIndexChanged += (_, _) =>
		{
			if (_filling || !_ready || Selected() is not { } row)
			{
				return;
			}
			_settings.Function = row.Name;
			Notify();
		};
		_functions.DoubleClick += async (_, _) =>
		{
			// The native list toggles an item on double-click; here a double-click
			// means Edit, so put the tick back (the owner lost lines this way)
			int index = _functions.SelectedIndices.Count > 0 ? _functions.SelectedIndices[0] : -1;
			if (index >= 0 && index == _lastToggle.Index
				&& Environment.TickCount64 - _lastToggle.Ticks <= SystemInformation.DoubleClickTime)
			{
				_functions.Items[index].Checked = !_functions.Items[index].Checked;
			}
			await EditSelectedAsync();
		};
		_functions.MouseClick += (_, e) =>
		{
			var hit = _functions.HitTest(e.Location);
			if (e.Button == MouseButtons.Left && hit.Item != null && hit.Location == ListViewHitTestLocations.Image && Graphable(hit.Item.Index))
			{
				ChooseColour(_rows[hit.Item.Index]);
			}
		};
		_functions.ContextMenuStrip = BuildListMenu();
		EventHandler onChanged = (_, _) =>
		{
			if (LinesKey() != _linesKey)
			{
				FillList();    // equations or colours changed in the other panel
			}
			else
			{
				ShowSelection();
			}
		};
		_settings.Changed += onChanged;
		Disposed += (_, _) =>
		{
			_settings.Changed -= onChanged;    // the settings outlive a closed Graph window
			_swatches.Dispose();
		};
		// end BLOCK 2

		Controls.Add(Plot);
		Controls.Add(_functions);
		Controls.Add(toolbar);
		UpdateButtons();
	}

	public void UpdateFunctions(IReadOnlyList<UserFunction> functions)
	{
		_list = functions;
		var before = _settings.Graphable;
		_settings.Graphable = GraphableNames();
		if (before.Count > 0)
		{
			// a function added after start-up is drawn at once (Add from the graph)
			foreach (string name in _settings.Graphable.Where(n => !before.Any(b => Same(b, n))))
			{
				_settings.SetShown(name, true);
			}
		}
		// Forget deleted functions; with nothing shown, draw the first graphable one
		_settings.Shown.RemoveAll(name => !_settings.Graphable.Any(g => Same(g, name)));
		if (_settings.Shown.Count == 0 && _settings.Graphable.Count > 0)
		{
			_settings.Shown.Add(_settings.Graphable[0]);
		}
		if (_settings.Function == null || !_settings.Graphable.Concat(functions.Select(f => f.Name)).Any(n => Same(n, _settings.Function)))
		{
			_settings.Function = _settings.Shown.FirstOrDefault();
		}
		FillList();
		Plot.RequestRefresh();
	}

	private List<string> GraphableNames()
	{
		return _list.Where(f => f.Parameters.Count == 1).Select(f => f.Name).Concat(_settings.Equations).ToList();
	}

	private string LinesKey()
	{
		return string.Join("\n", _settings.Equations) + "|" + string.Join(",", _settings.Colours.Select(c => $"{c.Key}={c.Value}"));
	}

	//==================================================================
	//  Filling the list
	//==================================================================
	private void FillList()
	{
		_linesKey = LinesKey();
		_rows = _list.Select(fn => new Row(fn.Name, fn.Head + fn.Body, fn))
			.Concat(_settings.Equations.Select(eq => new Row(eq, eq, null)))
			.ToList();
		_filling = true;
		_functions.BeginUpdate();
		_functions.Items.Clear();
		_swatches.Images.Clear();
		foreach (var row in _rows)
		{
			var item = new ListViewItem(row.Text) { ToolTipText = row.Text };
			if (row.IsEquation || row.Function!.Parameters.Count == 1)
			{
				_swatches.Images.Add(Swatch(Theme.LineColour(_settings, row.Name)));
				item.ImageIndex = _swatches.Images.Count - 1;
				item.Checked = _settings.IsShown(row.Name);
				item.ToolTipText += "\n(click the colour to change it)";
			}
			else
			{
				item.ForeColor = SystemColors.GrayText;
				item.ToolTipText += " (only one-parameter functions can be graphed)";
			}
			_functions.Items.Add(item);
		}
		_functions.EndUpdate();
		_filling = false;
		int rows = Math.Clamp(_rows.Count, 1, MaxRows);
		// GetItemRect would create the list's handle early; estimate until it exists
		int rowHeight = _ready && _functions.Items.Count > 0 ? _functions.GetItemRect(0).Height : Math.Max(_functions.Font.Height + 6, SwatchSize + 4);
		_functions.Height = rows * rowHeight + 4;
		FitColumn();
		ShowSelection();
	}

	// A filled square with a frame, big enough to click
	private static Bitmap Swatch(Color colour)
	{
		var bitmap = new Bitmap(SwatchSize, SwatchSize);
		using var g = Graphics.FromImage(bitmap);
		using var brush = new SolidBrush(colour);
		using var frame = new Pen(SystemColors.ControlDark);
		g.FillRectangle(brush, 1, 1, SwatchSize - 3, SwatchSize - 3);
		g.DrawRectangle(frame, 1, 1, SwatchSize - 3, SwatchSize - 3);
		return bitmap;
	}

	private void FitColumn()
	{
		if (_functions.Columns.Count > 0)
		{
			_functions.Columns[0].Width = -2;    // LVSCW_AUTOSIZE_USEHEADER: fill the width left beside the scroll bar
		}
	}

	private void ShowSelection()
	{
		if (!_ready)
		{
			UpdateButtons();
			return;    // FillList runs once the handle exists
		}
		if (_inListEvent)
		{
			_functions.BeginInvoke(ShowSelection);    // not inside the list's own notification
			return;
		}
		int index = _rows.FindIndex(r => Same(r.Name, _settings.Function));
		_filling = true;
		for (int i = 0; i < _functions.Items.Count && i < _rows.Count; i++)
		{
			var item = _functions.Items[i];
			item.Selected = i == index;
			if (Graphable(i))
			{
				item.Checked = _settings.IsShown(_rows[i].Name);
			}
		}
		_filling = false;
		UpdateButtons();
	}

	private bool _inListEvent;

	private void Notify()
	{
		_inListEvent = true;
		try
		{
			_settings.NotifyChanged();
		}
		finally
		{
			_inListEvent = false;
		}
	}

	private bool Graphable(int index)
	{
		return index >= 0 && index < _rows.Count && (_rows[index].IsEquation || _rows[index].Function!.Parameters.Count == 1);
	}

	private void UpdateButtons()
	{
		_edit.Enabled = _delete.Enabled = Selected() != null;
	}

	private Row? Selected()
	{
		int index = _functions.SelectedIndices.Count > 0 ? _functions.SelectedIndices[0] : -1;
		return index >= 0 && index < _rows.Count ? _rows[index] : null;
	}

	private static bool Same(string a, string? b)
	{
		return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
	}

	//==================================================================
	//  Colours (G4)
	//==================================================================
	private void ChooseColour(Row row)
	{
		using var dialog = new ColorDialog { Color = Theme.LineColour(_settings, row.Name), FullOpen = true, AnyColor = true };
		if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
		{
			_settings.Colours[row.Name] = dialog.Color.ToArgb() | unchecked((int)0xFF000000);
			FillList();
			_settings.NotifyChanged();
		}
	}

	private ContextMenuStrip BuildListMenu()
	{
		var menu = new ContextMenuStrip();
		var colour = new ToolStripMenuItem("&Colour...", null, (_, _) =>
		{
			if (Selected() is { } row)
			{
				ChooseColour(row);
			}
		});
		var reset = new ToolStripMenuItem("&Default Colour", null, (_, _) =>
		{
			if (Selected() is { } row && _settings.Colours.Remove(row.Name))
			{
				FillList();
				_settings.NotifyChanged();
			}
		});
		var edit = new ToolStripMenuItem("&Edit...", null, async (_, _) => await EditSelectedAsync());
		var delete = new ToolStripMenuItem("De&lete", null, async (_, _) => await DeleteSelectedAsync());
		menu.Items.AddRange(new ToolStripItem[] { colour, reset, new ToolStripSeparator(), edit, delete });
		menu.Opening += (_, e) =>
		{
			var at = _functions.PointToClient(Cursor.Position);
			if (_functions.HitTest(at).Item is { } item)
			{
				item.Selected = true;
			}
			int index = _functions.SelectedIndices.Count > 0 ? _functions.SelectedIndices[0] : -1;
			if (index < 0)
			{
				e.Cancel = true;
				return;
			}
			colour.Enabled = Graphable(index);
			reset.Enabled = Graphable(index) && _settings.Colours.ContainsKey(_rows[index].Name);
		};
		return menu;
	}

	//==================================================================
	//  Edit and Delete for both kinds; equations (G5)
	//==================================================================
	private async Task EditSelectedAsync()
	{
		switch (Selected())
		{
			case { IsEquation: true } row:
				await EditEquationAsync(row.Name);
				break;
			case { Function: { } fn }:
				await _commands.EditFunctionAsync(fn);
				break;
		}
	}

	private async Task DeleteSelectedAsync()
	{
		switch (Selected())
		{
			case { IsEquation: true } row:
				_settings.Equations.RemoveAll(e => Same(e, row.Name));
				_settings.SetShown(row.Name, false);
				_settings.Colours.Remove(row.Name);
				if (Same(row.Name, _settings.Function))
				{
					_settings.Function = _settings.Shown.FirstOrDefault();
				}
				RefreshLines();
				break;
			case { Function: { } fn }:
				await _commands.DeleteAsync(fn.Name);
				break;
		}
	}

	// Graph > Add > Examples: pick a curve, then the usual editor to change and add it
	private async Task AddExampleAsync()
	{
		GraphExample? example;
		using (var gallery = new GraphGallery(_session))
		{
			example = gallery.ShowDialog(FindForm()) == DialogResult.OK ? gallery.Picked : null;
		}
		if (example == null)
		{
			return;
		}
		if (example.IsEquation)
		{
			await AddEquationAsync(example.Text);
			return;
		}
		string name = GraphExamples.FreeName(example.Name, _commands.State().Functions.Select(f => f.Name));
		await _commands.AddFunctionAsync(name, example.Text);
	}

	private async Task AddEquationAsync(string start = "x^2+y^2=r^2")
	{
		if (await EquationDialog.AskAsync(FindForm(), _session, "Add Equation", start, _settings.Equations) is not { } text)
		{
			return;
		}
		_settings.Equations.Add(text);
		_settings.SetShown(text, true);
		_settings.Function = text;
		RefreshLines();
	}

	private async Task EditEquationAsync(string old)
	{
		var others = _settings.Equations.Where(e => !Same(e, old)).ToList();
		if (await EquationDialog.AskAsync(FindForm(), _session, "Edit Equation", old, others) is not { } text || text == old)
		{
			return;
		}
		int index = _settings.Equations.FindIndex(e => Same(e, old));
		_settings.Equations[index] = text;
		_settings.Rename(old, text);
		_settings.SetShown(old, false);
		RefreshLines();
	}

	// After the equation list changed: colours by place, both panels, the plot
	private void RefreshLines()
	{
		_settings.Graphable = GraphableNames();
		FillList();
		_settings.NotifyChanged();
	}
}
