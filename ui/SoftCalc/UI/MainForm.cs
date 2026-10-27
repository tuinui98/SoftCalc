// MainForm.cs - SoftCalc main window: views, menus, History, Input, keypad
//
// Standard WinForms controls (owner's choice: the original's UX, not its
// look). Behavior follows spec/SPEC.md sections 2-7 and the engine
// protocol in design/protocol.md. Sidebar contents arrive in M6, files
// and persistence in M7.

using System.Text.Json.Nodes;
using SoftCalc.Engine;
using SoftCalc.Logic;

namespace SoftCalc.UI;

internal enum CalcView
{
	Numeric,
	History,
	Advanced,
}

internal sealed class MainForm : Form
{
	private const string OperatorSymbols = "+-*/^!%";

	private readonly EngineSession _session;
	private readonly HistoryView _history = new() { Dock = DockStyle.Fill };
	private readonly TextBox _input = new() { Dock = DockStyle.Fill };
	private readonly TextBox _samples = new()
	{
		Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None,
		BackColor = SystemColors.Info, TabStop = false,
	};
	private readonly Keypad _keypad = new();
	private readonly SplitContainer _split = new() { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1 };
	private readonly TableLayoutPanel _column = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
	private readonly GroupBox _samplesBox = new() { Text = "Samples", Dock = DockStyle.Fill };
	private readonly Panel _keypadHost = new() { Dock = DockStyle.Fill, Height = 150 };
	private readonly GroupBox _inputBox = new() { Text = "Input", Dock = DockStyle.Fill, Height = 52 };
	private readonly InputRecall _recall;
	private readonly MenuStrip _menu = new();
	private readonly ToolStripMenuItem _functionsMenu = new("F&unctions");
	private readonly ToolStripMenuItem _conversionsMenu = new("&Conversions");
	private readonly ToolStripMenuItem _userFunctionsMenu = new("&User Functions");
	private readonly ToolStripMenuItem _showSamplesItem = new("&Show Samples") { Checked = true };
	private readonly Dictionary<string, ToolStripMenuItem> _viewItems = new();
	private readonly Dictionary<string, Dictionary<object, ToolStripMenuItem>> _settingItems = new();

	private CalcView _view = CalcView.Advanced;
	private bool _lastWasResult;
	private bool _busy;
	private EngineState _state = new(new List<NamedValue>(), new List<NamedValue>(), new List<UserFunction>());

	public MainForm(EngineSession session)
	{
		_session = session;
		_recall = new InputRecall(() => _history.Entries
			.Where(entry => entry.Kind != "notice")
			.Select(entry => entry.Input)
			.ToList());
		_session.Restarted += (_, reason) => _history.Append(
			new HistoryEntry("notice", "", "(engine restarted)", reason));
		Text = "SoftCalc";
		Size = new Size(980, 700);
		StartPosition = FormStartPosition.CenterScreen;
		KeyPreview = true;
		BuildLayout();
		BuildMenus();
		_history.EntryClicked += (_, entry) => RecallEntry(entry);
		_input.KeyDown += OnInputKeyDown;
		_input.KeyPress += OnInputKeyPress;
		_keypad.KeyPressedLabel += async (_, label) => await OnKeypad(label);
		ApplyView(CalcView.Advanced);
		Shown += async (_, _) => await StartEngineAsync();
		FormClosed += (_, _) => _session.Dispose();
	}

	//==================================================================
	//  Layout
	//==================================================================
	private void BuildLayout()
	{
		var historyBox = new GroupBox { Text = "History", Dock = DockStyle.Fill };
		historyBox.Controls.Add(_history);
		_samplesBox.Controls.Add(_samples);
		_inputBox.Controls.Add(_input);
		_keypadHost.Controls.Add(_keypad);
		_keypadHost.Resize += (_, _) => CenterKeypad();

		_column.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
		_column.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
		_column.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
		_column.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
		_column.Controls.Add(historyBox, 0, 0);
		_column.Controls.Add(_samplesBox, 0, 1);
		_column.Controls.Add(_inputBox, 0, 2);
		_column.Controls.Add(_keypadHost, 0, 3);

		// Sidebar placeholders; Graph and Variables arrive in M6
		var sidebar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
		sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
		sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
		sidebar.Controls.Add(Placeholder("Graph"), 0, 0);
		sidebar.Controls.Add(Placeholder("Variables"), 0, 1);
		_split.Panel1.Controls.Add(sidebar);
		_split.Panel2.Controls.Add(_column);

		Controls.Add(_split);
		Controls.Add(_menu);
		MainMenuStrip = _menu;
		Load += (_, _) => _split.SplitterDistance = 300;
	}

	private static GroupBox Placeholder(string title)
	{
		var box = new GroupBox { Text = title, Dock = DockStyle.Fill };
		box.Controls.Add(new Label
		{
			Text = "(coming in the next milestone)", Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleCenter, ForeColor = SystemColors.GrayText,
		});
		return box;
	}

	private void CenterKeypad()
	{
		_keypad.Location = new Point(
			Math.Max(0, (_keypadHost.Width - _keypad.Width) / 2),
			Math.Max(0, (_keypadHost.Height - _keypad.Height) / 2));
	}

	private void ApplyView(CalcView view)
	{
		_view = view;
		bool samples = _showSamplesItem.Checked && view != CalcView.Numeric;    // Numeric never shows Samples [KEEP]
		_column.RowStyles[1].Height = samples ? 110 : 0;
		_samplesBox.Visible = samples;
		_column.RowStyles[3].Height = view == CalcView.Numeric ? 150 : 0;
		_keypadHost.Visible = view == CalcView.Numeric;
		_split.Panel1Collapsed = view != CalcView.Advanced;
		foreach (var (name, item) in _viewItems)
		{
			item.Checked = name == view.ToString();
		}
		CenterKeypad();
	}

	//==================================================================
	//  Menus
	//==================================================================
	private void BuildMenus()
	{
		var file = new ToolStripMenuItem("&File");
		file.DropDownItems.Add(new ToolStripMenuItem("&Help") { Enabled = false });
		file.DropDownItems.Add(new ToolStripSeparator());
		file.DropDownItems.Add(new ToolStripMenuItem("S&ave History") { Enabled = false });
		file.DropDownItems.Add(new ToolStripSeparator());
		file.DropDownItems.Add(new ToolStripMenuItem("&Save Functions && Variables") { Enabled = false });
		file.DropDownItems.Add(new ToolStripMenuItem("&Load Functions && Variables") { Enabled = false });
		file.DropDownItems.Add(new ToolStripSeparator());
		file.DropDownItems.Add(new ToolStripMenuItem("E&xit", null, (_, _) => Close()));

		var view = new ToolStripMenuItem("&View");
		view.DropDownItems.Add(new ToolStripMenuItem("&Clear History", null, async (_, _) => await ClearHistoryAsync()));
		view.DropDownItems.Add(new ToolStripSeparator());
		AddViewItem(view, "&Numeric Calc", CalcView.Numeric);
		AddViewItem(view, "H&istory View", CalcView.History);
		AddViewItem(view, "&Advanced View", CalcView.Advanced);
		view.DropDownItems.Add(new ToolStripSeparator());
		AddSettingItem(view.DropDownItems, "base", "hex", "&Hexadecimal Output");
		AddSettingItem(view.DropDownItems, "base", "dec", "&Decimal Output");
		AddSettingItem(view.DropDownItems, "base", "oct", "&Octal Output");
		AddSettingItem(view.DropDownItems, "base", "bin", "&Binary Output");
		view.DropDownItems.Add(new ToolStripSeparator());
		_showSamplesItem.Click += (_, _) =>
		{
			_showSamplesItem.Checked = !_showSamplesItem.Checked;
			ApplyView(_view);
		};
		view.DropDownItems.Add(_showSamplesItem);
		view.DropDownItems.Add(new ToolStripSeparator());
		var options = new ToolStripMenuItem("Advanced &Options");
		AddSettingItem(options.DropDownItems, "angle", "deg", "&Degrees");
		AddSettingItem(options.DropDownItems, "angle", "rad", "&Radians");
		AddSettingItem(options.DropDownItems, "angle", "grad", "&Grads");
		options.DropDownItems.Add(new ToolStripSeparator());
		AddSettingItem(options.DropDownItems, "rpn", true, "R&PN Mode");
		options.DropDownItems.Add(new ToolStripSeparator());
		AddSettingItem(options.DropDownItems, "precision", 32, "&Low Precision (32)");
		AddSettingItem(options.DropDownItems, "precision", 64, "&Medium Precision (64)");
		AddSettingItem(options.DropDownItems, "precision", 128, "&High Precision (128)");
		AddSettingItem(options.DropDownItems, "precision", 512, "&Extreme Precision (512) ** slow");
		view.DropDownItems.Add(options);

		_menu.Items.AddRange(new ToolStripItem[] { file, view, _functionsMenu, _conversionsMenu });
	}

	private void AddViewItem(ToolStripMenuItem parent, string text, CalcView target)
	{
		var item = new ToolStripMenuItem(text, null, (_, _) => ApplyView(target));
		_viewItems[target.ToString()] = item;
		parent.DropDownItems.Add(item);
	}

	private void AddSettingItem(ToolStripItemCollection parent, string key, object value, string text)
	{
		var item = new ToolStripMenuItem(text);
		item.Click += async (_, _) => await ChangeSettingAsync(key, value);
		if (!_settingItems.TryGetValue(key, out var items))
		{
			items = new Dictionary<object, ToolStripMenuItem>();
			_settingItems[key] = items;
		}
		items[value] = item;
		parent.Add(item);
	}

	private void BuildCatalogMenus(Catalog catalog)
	{
		_functionsMenu.DropDownItems.Clear();
		foreach (var (group, names) in catalog.FunctionGroups)
		{
			var groupItem = new ToolStripMenuItem(group);
			foreach (string name in names)
			{
				groupItem.DropDownItems.Add(new ToolStripMenuItem(name, null,
					async (_, _) => await InsertFunctionOrOperatorAsync(name)));
			}
			_functionsMenu.DropDownItems.Add(groupItem);
		}
		_functionsMenu.DropDownItems.Add(new ToolStripSeparator());
		_functionsMenu.DropDownItems.Add(_userFunctionsMenu);

		_conversionsMenu.DropDownItems.Clear();
		foreach (var (category, units) in catalog.ConversionGroups)
		{
			var categoryItem = new ToolStripMenuItem(category);
			foreach (string source in units)
			{
				var sourceItem = new ToolStripMenuItem(source);
				foreach (string target in units.Where(u => u != source))
				{
					sourceItem.DropDownItems.Add(new ToolStripMenuItem(target, null, async (_, _) =>
						await InsertFromMenuAsync(new JsonObject
						{
							["kind"] = "conversion", ["source"] = source, ["target"] = target,
						})));
				}
				categoryItem.DropDownItems.Add(sourceItem);
			}
			_conversionsMenu.DropDownItems.Add(categoryItem);
		}
		var formats = new ToolStripMenuItem("Date Format");
		foreach (string name in catalog.Formats)
		{
			formats.DropDownItems.Add(new ToolStripMenuItem(name, null, async (_, _) =>
				await InsertFromMenuAsync(new JsonObject { ["kind"] = "format", ["name"] = name })));
		}
		_conversionsMenu.DropDownItems.Add(new ToolStripSeparator());
		_conversionsMenu.DropDownItems.Add(formats);
	}

	private void BuildUserFunctionsMenu()
	{
		_userFunctionsMenu.DropDownItems.Clear();
		foreach (var fn in _state.Functions)
		{
			string label = $"{fn.Name}({string.Join(", ", fn.Parameters)})";
			_userFunctionsMenu.DropDownItems.Add(new ToolStripMenuItem(label, null, async (_, _) =>
				await InsertFromMenuAsync(new JsonObject { ["kind"] = "function", ["name"] = fn.Name })));
		}
		_userFunctionsMenu.Enabled = _state.Functions.Count > 0;
	}

	private void ShowSettingChecks()
	{
		var settings = _session.Settings;
		foreach (var (key, items) in _settingItems)
		{
			var current = settings[key];
			foreach (var (value, item) in items)
			{
				item.Checked = current != null && current.ToJsonString() == JsonValue.Create(value)?.ToJsonString();
			}
		}
		bool rpn = (bool?)settings["rpn"] == true;
		_samples.Text = rpn ? Samples.Rpn : Samples.Infix;
		_inputBox.Text = rpn ? "Input  -  RPN mode" : "Input";    // visible in every view
	}

	//==================================================================
	//  Engine actions
	//==================================================================
	private async Task StartEngineAsync()
	{
		_input.Enabled = false;
		try
		{
			await _session.StartAsync();
			BuildCatalogMenus(await _session.CatalogAsync());
			await RefreshStateAsync();
			ShowSettingChecks();
			_input.Enabled = true;
			_input.Focus();
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "The calculation engine could not start.\r\n\r\n" + ex.Message,
				"SoftCalc", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private async Task RefreshStateAsync()
	{
		_state = await _session.StateAsync();
		BuildUserFunctionsMenu();
	}

	private async Task SubmitAsync()
	{
		if (_busy)
		{
			return;
		}
		_busy = true;
		_input.ReadOnly = true;
		try
		{
			string? lastInput = _history.Entries.LastOrDefault(entry => entry.Kind != "notice")?.Input;
			string? line = InputRules.ResolveSubmit(_input.Text, lastInput);
			if (line == null)
			{
				return;
			}
			var reply = await _session.EvalAsync(line);
			if (reply.Kind == "empty")
			{
				return;
			}
			_history.Append(new HistoryEntry(reply.Kind, reply.Input, reply.DisplayInput, reply.Text));
			_recall.Reset();
			_lastWasResult = reply.Kind == "result";
			if (reply.Kind != "error")
			{
				_input.Clear();    // an error keeps the text for fixing [KEEP]
			}
			if (reply.Kind == "definition")
			{
				await RefreshStateAsync();
			}
		}
		finally
		{
			_input.ReadOnly = false;
			_busy = false;
			_input.SelectionStart = _input.TextLength;
		}
	}

	private async Task ChangeSettingAsync(string key, object value)
	{
		JsonNode? node = value switch
		{
			bool flag => JsonValue.Create(key == "rpn" ? !((bool?)_session.Settings["rpn"] == true) : flag),
			int number => JsonValue.Create(number),
			_ => JsonValue.Create((string)value),
		};
		await _session.ChangeSettingsAsync(new JsonObject { [key] = node });
		ShowSettingChecks();
		await RefreshStateAsync();    // constants and values follow precision and base
	}

	private async Task ClearHistoryAsync()
	{
		_history.ClearEntries();
		await _session.ClearHistoryAsync();
	}

	private async Task InsertFunctionOrOperatorAsync(string name)
	{
		if (name == "mod")
		{
			await InsertFromMenuAsync(new JsonObject { ["kind"] = "operator", ["symbol"] = " mod " });
		}
		else if (name.Length == 1 && OperatorSymbols.Contains(name[0]))
		{
			await InsertFromMenuAsync(new JsonObject { ["kind"] = "operator", ["symbol"] = name });
		}
		else
		{
			await InsertFromMenuAsync(new JsonObject { ["kind"] = "function", ["name"] = name });
		}
	}

	private async Task InsertFromMenuAsync(JsonObject arguments)
	{
		arguments["text"] = _input.Text;
		arguments["caret"] = _input.SelectionStart;
		var (text, caret) = await _session.MenuInsertAsync(arguments);
		_input.Text = text;
		_input.SelectionStart = caret;
		_input.Focus();
	}

	private void RecallEntry(HistoryEntry entry)
	{
		if (entry.Kind == "notice")
		{
			return;
		}
		_input.Text = entry.Input;
		_input.SelectionStart = _input.TextLength;
		_input.Focus();
	}

	//==================================================================
	//  Keyboard and keypad
	//==================================================================
	private async void OnInputKeyDown(object? sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Enter)
		{
			e.SuppressKeyPress = true;    // no beep from a single-line TextBox
			await SubmitAsync();
		}
		else if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
		{
			e.Handled = true;
			string? text = e.KeyCode == Keys.Up ? _recall.Up(_input.Text) : _recall.Down();
			if (text != null)
			{
				_input.Text = text;
				_input.SelectionStart = _input.TextLength;
			}
		}
	}

	private void OnInputKeyPress(object? sender, KeyPressEventArgs e)
	{
		if (TypeOperator(e.KeyChar))
		{
			e.Handled = true;
		}
	}

	// Applies the answer auto-insert rule; true when it handled the character
	private bool TypeOperator(char symbol)
	{
		bool rpn = (bool?)_session.Settings["rpn"] == true;
		string? replaced = InputRules.AutoAnswer(_input.Text, _lastWasResult, rpn, symbol);
		if (replaced == null)
		{
			return false;
		}
		_input.Text = replaced;
		_input.SelectionStart = _input.TextLength;
		return true;
	}

	private async Task OnKeypad(string label)
	{
		_input.Focus();
		bool rpn = (bool?)_session.Settings["rpn"] == true;
		if (rpn && (label == "Ent" || (label.Length == 1 && OperatorSymbols.Contains(label[0]))))
		{
			await OnRpnKeypad(label);
			return;
		}
		switch (label)
		{
			case "Ent":
				await SubmitAsync();
				break;
			case "Clr":
				_input.Clear();
				break;
			case "M+":
			case "M-":
				await MemoryAddAsync(label == "M-");
				break;
			case "MC":
				await EvaluateDefinitionAsync(InputRules.MemoryClear);
				break;
			case "MR":
				_input.SelectedText = InputRules.MemoryRecall;
				break;
			default:
				if (label.Length == 1 && OperatorSymbols.Contains(label[0]) && TypeOperator(label[0]))
				{
					break;
				}
				_input.SelectedText = label;
				break;
		}
	}

	// RPN keypad: Ent separates numbers, an operator is appended and evaluates the line
	private async Task OnRpnKeypad(string label)
	{
		if (label == "Ent")
		{
			var (text, submit) = InputRules.RpnEnter(_input.Text);
			_input.Text = text;
			_input.SelectionStart = _input.TextLength;
			if (submit)
			{
				await SubmitAsync();
			}
			return;
		}
		_input.Text = InputRules.RpnOperator(_input.Text, label[0]);
		_input.SelectionStart = _input.TextLength;
		await SubmitAsync();
	}

	private async Task MemoryAddAsync(bool subtract)
	{
		bool rpn = (bool?)_session.Settings["rpn"] == true;
		string operandText = _input.Text;
		if (rpn && operandText.Trim().Length > 0)
		{
			// An RPN line cannot sit inside an infix definition: evaluate it first, then use answer
			await SubmitAsync();
			if (!_lastWasResult)
			{
				return;
			}
			operandText = "";
		}
		bool hasMemory = _state.Variables.Any(v => string.Equals(v.Name, "memory", StringComparison.OrdinalIgnoreCase));
		await EvaluateDefinitionAsync(InputRules.MemoryAdd(operandText, hasMemory, subtract));
	}

	// Runs a memory definition through History like any typed definition
	private async Task EvaluateDefinitionAsync(string definition)
	{
		string kept = _input.Text;
		_input.Text = definition;
		await SubmitAsync();
		if (_input.TextLength > 0 && _input.Text == definition)
		{
			_input.Text = kept;    // the definition failed; give the user's text back
		}
		else
		{
			_input.Clear();
		}
	}
}
