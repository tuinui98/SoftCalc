// DefinitionEditor.cs - Functions or Variables editor: list plus separate fields
//
// Replaces the one-line Add/Edit dialogs (owner 2026-11-07, spec/ideas.md):
// the definitions of one kind are listed on the left; the right side has
// Name, Parameters (functions) and Definition / Value fields, a preview of
// the line sent to the engine, and a note when the name replaces another
// definition. Apply defines without closing, so an open Graph redraws at
// once; renaming defines the new name and deletes the old one.

using SoftCalc.Engine;
using SoftCalc.Logic;

namespace SoftCalc.UI;

internal sealed class DefinitionEditor : Form
{
	private readonly bool _isFunction;
	private readonly DefinitionCommands _commands;
	private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
	private readonly TextBox _name = new() { Dock = DockStyle.Fill };
	private readonly TextBox _parameters = new() { Dock = DockStyle.Fill };
	private readonly TextBox _body = new() { Dock = DockStyle.Fill };
	private readonly Label _preview = new() { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(3, 6, 3, 0) };
	private readonly Label _note = new() { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(3, 2, 3, 0) };
	private readonly Label _error = new() { AutoSize = true, ForeColor = Theme.Error, Margin = new Padding(3, 6, 3, 0) };
	private readonly Button _delete = new() { Text = "Delete", AutoSize = true };
	private List<(string Name, string Parameters, string Body, string Text)> _items = new();
	private string? _editing;    // name of the definition loaded in the fields; null for a new one
	private bool _dirty;
	private bool _loading;
	private int _shownIndex = -1;

	public DefinitionEditor(bool isFunction, DefinitionCommands commands, string? select)
	{
		_isFunction = isFunction;
		_commands = commands;
		Text = isFunction ? "Functions" : "Variables";
		Icon = SvgIcon.ProgramIcon();
		ShowInTaskbar = false;
		MinimizeBox = false;
		StartPosition = FormStartPosition.CenterParent;
		ClientSize = new Size(620, 300);
		MinimumSize = new Size(520, 280);

		//==================================================================
		//  Left: the list, New and Delete
		//==================================================================
		var newButton = new Button { Text = "New", AutoSize = true };
		newButton.Click += (_, _) =>
		{
			if (ConfirmLeave())
			{
				_list.ClearSelected();
				ShowItem(null);
			}
		};
		_delete.Click += async (_, _) => await DeleteAsync();
		var leftButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
		leftButtons.Controls.AddRange(new Control[] { newButton, _delete });
		var left = new Panel { Dock = DockStyle.Left, Width = 220, Padding = new Padding(8, 8, 4, 8) };
		left.Controls.Add(_list);
		left.Controls.Add(leftButtons);
		_list.SelectedIndexChanged += (_, _) => OnListSelection();

		//==================================================================
		//  Right: fields, preview, note, error, Apply / OK / Cancel
		//==================================================================
		var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(4, 8, 8, 8) };
		fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
		fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
		AddRow(fields, "&Name", _name);
		if (isFunction)
		{
			AddRow(fields, "&Parameters", _parameters);
		}
		AddRow(fields, isFunction ? "&Definition" : "&Value", _body);
		foreach (var label in new[] { _preview, _note, _error })
		{
			fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			fields.Controls.Add(label, 1, fields.RowCount++);
		}
		fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
		fields.RowCount++;

		var apply = new Button { Text = "&Apply", AutoSize = true };
		var ok = new Button { Text = "OK", AutoSize = true, MinimumSize = new Size(80, 0) };
		var cancel = new Button { Text = "Cancel", AutoSize = true, MinimumSize = new Size(80, 0), DialogResult = DialogResult.Cancel };
		apply.Click += async (_, _) => await ApplyAsync();
		ok.Click += async (_, _) =>
		{
			if (!_dirty || await ApplyAsync())
			{
				DialogResult = DialogResult.OK;
				Close();
			}
		};
		var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(4, 0, 8, 8) };
		buttons.Controls.AddRange(new Control[] { cancel, ok, apply });
		AcceptButton = ok;
		CancelButton = cancel;

		var right = new Panel { Dock = DockStyle.Fill };
		right.Controls.Add(fields);
		right.Controls.Add(buttons);
		Controls.Add(right);
		Controls.Add(left);

		foreach (var box in new[] { _name, _parameters, _body })
		{
			box.TextChanged += (_, _) =>
			{
				if (!_loading)
				{
					_dirty = true;
					ShowPreview();
				}
			};
		}
		Reload(select);
		Shown += (_, _) => (_editing == null ? _name : _body).Focus();
	}

	private static void AddRow(TableLayoutPanel table, string label, Control box)
	{
		table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 6, 3) }, 0, table.RowCount);
		table.Controls.Add(box, 1, table.RowCount);
		table.RowCount++;
	}

	//==================================================================
	//  List and fields
	//==================================================================
	// Re-reads the engine state; selects `select`, or opens an empty new one
	private void Reload(string? select)
	{
		var state = _commands.State();
		_items = _isFunction
			? state.Functions.Select(f =>
			{
				var (name, parameters) = DefinitionFields.SplitHead(f.Head);
				return (name, parameters, f.Body, f.Head + f.Body);
			}).ToList()
			: state.Variables.Select(v => (v.Name, "", v.Value, $"{v.Name} = {v.Value}")).ToList();
		_loading = true;
		_list.BeginUpdate();
		_list.Items.Clear();
		foreach (var item in _items)
		{
			_list.Items.Add(item.Text);
		}
		_list.EndUpdate();
		int index = select == null ? -1 : _items.FindIndex(i => Same(i.Name, select));
		_list.SelectedIndex = index;
		_shownIndex = index;
		_loading = false;
		ShowItem(index >= 0 ? _items[index] : null);
	}

	private void ShowItem((string Name, string Parameters, string Body, string Text)? item)
	{
		_loading = true;
		_editing = item?.Name;
		_name.Text = item?.Name ?? "";
		_parameters.Text = item?.Parameters ?? (_isFunction ? "x" : "");
		_body.Text = item?.Body ?? "";
		_loading = false;
		_dirty = false;
		_error.Text = "";
		_delete.Enabled = _editing != null;
		ShowPreview();
	}

	private void OnListSelection()
	{
		if (_loading || _list.SelectedIndex == _shownIndex)
		{
			return;
		}
		if (!ConfirmLeave())
		{
			_loading = true;
			_list.SelectedIndex = _shownIndex;    // stay on the unsaved one
			_loading = false;
			return;
		}
		_shownIndex = _list.SelectedIndex;
		ShowItem(_shownIndex >= 0 ? _items[_shownIndex] : null);
	}

	// Unsaved changes: Yes applies them first (sync wait is fine, the engine answers fast)
	private bool ConfirmLeave()
	{
		if (!_dirty)
		{
			return true;
		}
		var answer = MessageBox.Show(this, "Apply the changes first?", Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
		if (answer == DialogResult.Yes)
		{
			_ = ApplyAsync();
			return false;    // the list is reloaded after Apply
		}
		return answer == DialogResult.No;
	}

	// The line the engine will get, and whether the name replaces something
	private void ShowPreview()
	{
		var (definition, _, _) = DefinitionFields.Compose(_isFunction, _name.Text, _parameters.Text, _body.Text);
		_preview.Text = definition ?? "";
		string name = _name.Text.Trim();
		var other = _items.FirstOrDefault(i => Same(i.Name, name) && !Same(i.Name, _editing));
		_note.Text = other.Name != null
			? $"Replaces {other.Text}"
			: _editing != null && name.Length > 0 && !Same(name, _editing) ? $"Renames {_editing} to {name}" : "";
	}

	//==================================================================
	//  Apply and Delete
	//==================================================================
	private async Task<bool> ApplyAsync()
	{
		var (definition, field, error) = DefinitionFields.Compose(_isFunction, _name.Text, _parameters.Text, _body.Text);
		if (definition == null)
		{
			_error.Text = error;
			(field switch { DefinitionField.Name => _name, DefinitionField.Parameters => _parameters, _ => _body }).Focus();
			return false;
		}
		string? engineError = await _commands.DefineAsync(definition, _isFunction);
		if (engineError != null)
		{
			_error.Text = engineError;
			_body.Focus();
			return false;
		}
		string name = _name.Text.Trim();
		if (_editing != null && !Same(_editing, name))
		{
			await _commands.DeleteAsync(_editing);    // renamed
		}
		else
		{
			await _commands.RefreshAsync();
		}
		Reload(name);
		return true;
	}

	private async Task DeleteAsync()
	{
		if (_editing == null)
		{
			return;
		}
		await _commands.DeleteAsync(_editing);
		Reload(null);
	}

	private static bool Same(string? a, string? b)
	{
		return a != null && b != null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
	}
}
