// VariablesPanel.cs - Variables list: Constants and user variables (SPEC 2, 4)
//
// Edit and Delete apply to user variables only; constants are listed for
// reference at the current precision. Double-click edits. In dark mode
// the native group headers are dark blue on grey and unreadable, so the
// sections get plain bold heading rows instead (owner 2026-11-07).

using SoftCalc.Engine;

namespace SoftCalc.UI;

internal sealed class VariablesPanel : Panel
{
	private readonly DefinitionCommands _commands;
	private readonly ListView _list = new()
	{
		Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false,
		ShowGroups = !Theme.IsDark, HeaderStyle = ColumnHeaderStyle.Nonclickable, HideSelection = false,
	};
	private readonly ListViewGroup _constants = new("Constants");
	private readonly ListViewGroup _user = new("User Defined");
	private readonly ToolStripButton _edit = new("Edit");
	private readonly ToolStripButton _delete = new("Delete");

	public VariablesPanel(DefinitionCommands commands)
	{
		_commands = commands;
		_list.Columns.Add("Name", 90);
		_list.Columns.Add("Definition", 400);
		_list.Groups.Add(_constants);
		_list.Groups.Add(_user);

		var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
		toolbar.Items.Add(new ToolStripButton("Add", null, async (_, _) => await _commands.AddVariableAsync()));
		_edit.Click += async (_, _) => await EditSelectedAsync();
		_delete.Click += async (_, _) =>
		{
			if (SelectedUser() is { } variable)
			{
				await _commands.DeleteAsync(variable.Name);
			}
		};
		toolbar.Items.Add(_edit);
		toolbar.Items.Add(_delete);
		_list.SelectedIndexChanged += (_, _) => UpdateButtons();
		// Definition fills the rest of the width, so no horizontal scroll bar
		_list.Resize += (_, _) => _list.Columns[1].Width = Math.Max(60, _list.ClientSize.Width - _list.Columns[0].Width);
		_list.DoubleClick += async (_, _) => await EditSelectedAsync();

		Controls.Add(_list);
		Controls.Add(toolbar);
		UpdateButtons();
	}

	public void UpdateValues(EngineState state)
	{
		_list.BeginUpdate();
		_list.Items.Clear();
		AddHeading(_constants.Header, state.Constants.Count);
		foreach (var constant in state.Constants)
		{
			_list.Items.Add(new ListViewItem(new[] { constant.Name, constant.Value }, _constants));
		}
		AddHeading(_user.Header, state.Variables.Count);
		foreach (var variable in state.Variables)
		{
			_list.Items.Add(new ListViewItem(new[] { variable.Name, variable.Value }, _user) { Tag = variable });
		}
		_list.EndUpdate();
		UpdateButtons();
	}

	// Dark mode only: a section heading row (no Tag, so Edit and Delete ignore it)
	private void AddHeading(string text, int count)
	{
		if (_list.ShowGroups || count == 0)
		{
			return;
		}
		_list.Items.Add(new ListViewItem(text) { Font = new Font(_list.Font, FontStyle.Bold), ForeColor = SystemColors.ControlText });
	}

	private async Task EditSelectedAsync()
	{
		if (SelectedUser() is { } variable)
		{
			await _commands.EditVariableAsync(variable);
		}
	}

	private void UpdateButtons()
	{
		_edit.Enabled = _delete.Enabled = SelectedUser() != null;
	}

	private NamedValue? SelectedUser()
	{
		return _list.SelectedItems.Count == 1 ? _list.SelectedItems[0].Tag as NamedValue : null;
	}
}
