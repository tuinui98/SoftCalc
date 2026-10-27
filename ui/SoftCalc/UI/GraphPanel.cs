// GraphPanel.cs - Graph toolbar, function drop-down and plot (sidebar and Graph window)
//
// Both instances share one GraphSettings, so choosing a function in one
// shows it in the other. Only one-parameter functions can be graphed.

using SoftCalc.Engine;
using SoftCalc.Logic;

namespace SoftCalc.UI;

internal sealed class GraphPanel : Panel
{
	private readonly GraphSettings _settings;
	private readonly DefinitionCommands _commands;
	private readonly ComboBox _functions = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
	private readonly ToolStripButton _edit = new("Edit");
	private readonly ToolStripButton _delete = new("Delete");
	private IReadOnlyList<UserFunction> _list = Array.Empty<UserFunction>();
	private bool _filling;

	public PlotView Plot { get; }

	public GraphPanel(EngineSession session, GraphSettings settings, DefinitionCommands commands, Action? openWindow)
	{
		_settings = settings;
		_commands = commands;
		Plot = new PlotView(session, settings) { Dock = DockStyle.Fill };

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

		_functions.SelectedIndexChanged += (_, _) =>
		{
			if (_filling)
			{
				return;
			}
			_settings.Function = Selected()?.Name;
			_settings.NotifyChanged();
		};
		_settings.Changed += (_, _) => ShowSelection();

		Controls.Add(Plot);
		Controls.Add(_functions);
		Controls.Add(toolbar);
		UpdateButtons();
	}

	public void UpdateFunctions(IReadOnlyList<UserFunction> functions)
	{
		_list = functions;
		_filling = true;
		_functions.Items.Clear();
		foreach (var fn in functions)
		{
			_functions.Items.Add(fn.Head + fn.Body);
		}
		_filling = false;
		// Keep the current choice; otherwise pick the first graphable function
		if (_settings.Function == null || !functions.Any(f => Same(f.Name, _settings.Function)))
		{
			_settings.Function = functions.FirstOrDefault(f => f.Parameters.Count == 1)?.Name;
		}
		ShowSelection();
		Plot.RequestRefresh();
	}

	private void ShowSelection()
	{
		int index = _list.ToList().FindIndex(f => Same(f.Name, _settings.Function));
		if (_functions.SelectedIndex != index)
		{
			_filling = true;
			_functions.SelectedIndex = index;
			_filling = false;
		}
		UpdateButtons();
	}

	private void UpdateButtons()
	{
		_edit.Enabled = _delete.Enabled = Selected() != null;
	}

	private UserFunction? Selected()
	{
		int index = _functions.SelectedIndex;
		return index >= 0 && index < _list.Count ? _list[index] : null;
	}

	private static bool Same(string a, string? b)
	{
		return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
	}
}
