// GraphPanel.cs - Graph toolbar, function checklist and plot (sidebar and Graph window)
//
// Both instances share one GraphSettings: a ticked function is drawn in
// both, in its own colour (the swatch in the list); the selected one is
// what Edit, Delete and the trace use (design/graph_plan.md, G3). Only
// one-parameter functions can be graphed; others are listed greyed out.

using SoftCalc.Engine;
using SoftCalc.Logic;

namespace SoftCalc.UI;

internal sealed class GraphPanel : Panel
{
	private const int MaxRows = 4;    // [DEF] list height before it scrolls

	private readonly GraphSettings _settings;
	private readonly DefinitionCommands _commands;
	private readonly ListView _functions = new()
	{
		Dock = DockStyle.Top, View = View.Details, CheckBoxes = true, FullRowSelect = true, MultiSelect = false,
		HeaderStyle = ColumnHeaderStyle.None, HideSelection = false, ShowItemToolTips = true,
	};
	private readonly ImageList _swatches = new() { ImageSize = new Size(12, 12), ColorDepth = ColorDepth.Depth32Bit };
	private readonly ToolStripButton _edit = new("Edit");
	private readonly ToolStripButton _delete = new("Delete");
	private IReadOnlyList<UserFunction> _list = Array.Empty<UserFunction>();
	private bool _filling;
	private bool _ready;    // list handle created and refilled; before that its check events are replays

	public PlotView Plot { get; }

	public GraphPanel(EngineSession session, GraphSettings settings, DefinitionCommands commands, Action? openWindow, bool interactive = false)
	{
		_settings = settings;
		_commands = commands;
		Plot = new PlotView(session, settings, interactive) { Dock = DockStyle.Fill };

		var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
		toolbar.Items.Add(new ToolStripButton("Add", null, async (_, _) => await _commands.AddFunctionAsync()));
		_edit.Click += async (_, _) =>
		{
			if (Selected() is { } fn)
			{
				await _commands.EditFunctionAsync(fn);
			}
		};
		_delete.Click += async (_, _) =>
		{
			if (Selected() is { } fn)
			{
				await _commands.DeleteAsync(fn.Name);
			}
		};
		toolbar.Items.Add(_edit);
		toolbar.Items.Add(_delete);
		if (openWindow != null)
		{
			toolbar.Items.Add(new ToolStripButton("Window", null, (_, _) => openWindow()));
		}

		_functions.Columns.Add("");
		_functions.SmallImageList = _swatches;
		_functions.Resize += (_, _) => FitColumn();
		_functions.ItemCheck += (_, e) =>
		{
			if (!_filling && !Graphable(e.Index))
			{
				e.NewValue = CheckState.Unchecked;    // only one-parameter functions can be drawn
			}
		};
		_functions.ItemChecked += (_, e) =>
		{
			if (_filling || !_ready || !Graphable(e.Item.Index))
			{
				return;
			}
			string name = _list[e.Item.Index].Name;
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
			if (_filling || !_ready || Selected() is not { } fn)
			{
				return;
			}
			_settings.Function = fn.Name;
			Notify();
		};
		_functions.DoubleClick += async (_, _) =>
		{
			if (Selected() is { } fn)
			{
				await _commands.EditFunctionAsync(fn);
			}
		};
		EventHandler onChanged = (_, _) => ShowSelection();
		_settings.Changed += onChanged;
		Disposed += (_, _) =>
		{
			_settings.Changed -= onChanged;    // the settings outlive a closed Graph window
			_swatches.Dispose();
		};

		Controls.Add(Plot);
		Controls.Add(_functions);
		Controls.Add(toolbar);
		UpdateButtons();
	}

	public void UpdateFunctions(IReadOnlyList<UserFunction> functions)
	{
		_list = functions;
		_settings.Graphable = functions.Where(f => f.Parameters.Count == 1).Select(f => f.Name).ToList();
		// Forget deleted functions; with nothing shown, draw the first graphable one
		_settings.Shown.RemoveAll(name => !_settings.Graphable.Any(g => Same(g, name)));
		if (_settings.Shown.Count == 0 && _settings.Graphable.Count > 0)
		{
			_settings.Shown.Add(_settings.Graphable[0]);
		}
		if (_settings.Function == null || !functions.Any(f => Same(f.Name, _settings.Function)))
		{
			_settings.Function = _settings.Shown.FirstOrDefault();
		}
		FillList();
		Plot.RequestRefresh();
	}

	private void FillList()
	{
		_filling = true;
		_functions.BeginUpdate();
		_functions.Items.Clear();
		_swatches.Images.Clear();
		foreach (var fn in _list)
		{
			int colour = _settings.ColourIndex(fn.Name);
			var item = new ListViewItem(fn.Head + fn.Body) { ToolTipText = fn.Head + fn.Body };
			if (colour >= 0)
			{
				_swatches.Images.Add(Swatch(Theme.Series(colour)));
				item.ImageIndex = _swatches.Images.Count - 1;
				item.Checked = _settings.IsShown(fn.Name);
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
		int rows = Math.Clamp(_list.Count, 1, MaxRows);
		// GetItemRect would create the list's handle early; estimate until it exists
		int row = _ready && _functions.Items.Count > 0 ? _functions.GetItemRect(0).Height : _functions.Font.Height + 6;
		_functions.Height = rows * row + 4;
		FitColumn();
		ShowSelection();
	}

	private static Bitmap Swatch(Color colour)
	{
		var bitmap = new Bitmap(12, 12);
		using var g = Graphics.FromImage(bitmap);
		using var brush = new SolidBrush(colour);
		g.FillRectangle(brush, 1, 4, 10, 4);
		return bitmap;
	}

	private void FitColumn()
	{
		if (_functions.Columns.Count > 0)
		{
			_functions.Columns[0].Width = Math.Max(20, _functions.ClientSize.Width - 2);
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
		int index = _list.ToList().FindIndex(f => Same(f.Name, _settings.Function));
		_filling = true;
		for (int i = 0; i < _functions.Items.Count; i++)
		{
			var item = _functions.Items[i];
			item.Selected = i == index;
			if (Graphable(i))
			{
				item.Checked = _settings.IsShown(_list[i].Name);
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
		return index >= 0 && index < _list.Count && _list[index].Parameters.Count == 1;
	}

	private void UpdateButtons()
	{
		_edit.Enabled = _delete.Enabled = Selected() != null;
	}

	private UserFunction? Selected()
	{
		int index = _functions.SelectedIndices.Count > 0 ? _functions.SelectedIndices[0] : -1;
		return index >= 0 && index < _list.Count ? _list[index] : null;
	}

	private static bool Same(string a, string? b)
	{
		return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
	}
}
