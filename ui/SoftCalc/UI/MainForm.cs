// MainForm.cs - SoftCalc main window: views, menus, History, Input, keypad
//
// Standard WinForms controls (owner's choice: the original's UX, not its
// look). Behavior follows spec/SPEC.md sections 2-7 and the engine
// protocol in design/protocol.md. Files and persistence: MainForm.Persistence.cs.

using System.Text.Json.Nodes;
using SoftCalc.Engine;
using SoftCalc.Logic;
using SoftCalc.Persistence;

namespace SoftCalc.UI;

internal enum CalcView
{
	Numeric,
	History,
	Advanced,
}

internal sealed partial class MainForm : Form
{
	private const string OperatorSymbols = "+-*/^!%";

	private readonly EngineSession _session;
	private int _hintRequest;    // newest hint update; older literal replies are dropped
	private readonly HistoryView _history = new() { Dock = DockStyle.Fill };
	private readonly TextBox _input = new() { Dock = DockStyle.Top };
	// Second Input line: help for what is being typed, italic and faint (owner 2026-11-06)
	private readonly Label _hint = new()
	{
		Dock = DockStyle.Top, AutoSize = false, AutoEllipsis = true, ForeColor = SystemColors.GrayText,
		UseMnemonic = false, Padding = new Padding(2, 2, 0, 0),
	};
	private readonly ToolStripMenuItem _showHintsItem = new("Show Input &Hints") { Checked = true };
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
	private readonly ToolStripMenuItem _onTopItem = new("Always on &Top");
	private readonly ToolStripMenuItem _showRibbonItem = new("Show &Ribbon") { Checked = true };
	private readonly ToolStripMenuItem _padZerosItem = new("&Pad with Zeros");
	private readonly ToolStripMenuItem _allDigitsItem = new("&All Digits");
	private readonly Ribbon _ribbon = new();
	private List<RibbonGroup> _ribbonLayout = RibbonLayout.Default();
	private bool _ownRibbon;    // this window changed the layout (saved at close as its own)
	private string _ribbonFunctions = "";    // user function names the ribbon was built with
	private readonly Dictionary<int, ToolStripMenuItem> _textSizeItems = new();
	private readonly RatesSource _rates;
	private readonly Dictionary<string, ToolStripMenuItem> _viewItems = new();
	private readonly Dictionary<string, Dictionary<object, ToolStripMenuItem>> _settingItems = new();

	private CalcView _view = CalcView.History;    // owner's default view
	private bool _lastWasResult;
	private bool _busy;
	private EngineState _state = new(new List<NamedValue>(), new List<NamedValue>(), new List<UserFunction>());
	private readonly GraphSettings _graphSettings = new();
	private DefinitionCommands _commands = null!;
	private GraphPanel _graphPanel = null!;
	private VariablesPanel _variablesPanel = null!;
	private GraphWindow? _graphWindow;
	private CompoundWindow? _compoundWindow;
	private RandomWindow? _randomWindow;

	// Set when the window closes to come back with a new theme (Program restarts)
	public bool RestartRequested { get; private set; }

	public MainForm(EngineSession session, DataFolder folder)
	{
		_session = session;
		_folder = folder;
		(_prefs, _settingsWarning) = SettingsStore.Load(folder.SettingsPath);
		_recall = new InputRecall(() => _history.Entries
			.Where(entry => entry.Kind != "notice")
			.Select(entry => entry.Input)
			.ToList());
		_session.Restarted += (_, reason) => AddNotice("(engine restarted)", reason);
		_session.DefinitionsChanged += (_, text) => OnDefinitionsChanged(text);
		_rates = new RatesSource(folder.RatesPath);
		_session.RatesProvider = FetchRatesAsync;
		Text = $"SoftCalc [v{AboutDialog.Version}]";    // owner 2026-11-10
		Icon = SvgIcon.ProgramIcon();
		StartPosition = FormStartPosition.CenterScreen;
		KeyPreview = true;
		BuildLayout();
		BuildMenus();
		_ribbonLayout = _prefs.Ribbon != null ? RibbonLayout.Clone(_prefs.Ribbon) : RibbonLayout.Default();
		RebuildRibbon();
		_history.EntryClicked += (_, entry) => RecallEntry(entry);
		_history.ResultClicked += (_, entry) => InsertResult(entry);
		_history.DeleteClicked += async (_, entry) => await DeleteHistoryEntryAsync(entry);
		_history.ClearClicked += async (_, _) => await ClearHistoryAsync();
		_input.KeyDown += OnInputKeyDown;
		_input.TextChanged += (_, _) => UpdateHint();
		_input.KeyUp += (_, _) => UpdateHint();    // caret moves
		_input.MouseUp += (_, _) => UpdateHint();
		_input.KeyPress += OnInputKeyPress;
		_input.ContextMenuStrip = BuildInputMenu();
		_keypad.KeyPressedLabel += async (_, label) => await OnKeypad(label);
		ApplySavedWindow();
		ApplyView(Enum.TryParse<CalcView>(_prefs.View, out var view) ? view : CalcView.History);
		Shown += async (_, _) => await StartEngineAsync();
		FormClosing += (_, _) => SaveOnClose();
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
		_inputBox.Controls.Add(_hint);
		_inputBox.Controls.Add(_input);    // added last: docks first, above the hint
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

		// Sidebar: Graph above Variables, each in its own titled box
		_commands = new DefinitionCommands(_session, this, RefreshStateAsync, () => _state);
		_commands.Renamed += (oldName, newName) => _graphSettings.Rename(oldName, newName);
		_graphPanel = new GraphPanel(_session, _graphSettings, _commands, OpenGraphWindow) { Dock = DockStyle.Fill };
		_variablesPanel = new VariablesPanel(_commands) { Dock = DockStyle.Fill };
		var sidebar = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
		sidebar.Panel1.Controls.Add(Titled("Graph", _graphPanel));
		sidebar.Panel2.Controls.Add(Titled("Variables", _variablesPanel));
		Load += (_, _) => sidebar.SplitterDistance = (int)(sidebar.Height * 0.6);
		_split.Panel1.Controls.Add(sidebar);
		_split.Panel2.Controls.Add(_column);

		_ribbon.CustomizeRequested += (_, _) => CustomizeRibbon();
		_ribbon.HideRequested += (_, _) => _showRibbonItem.PerformClick();
		Controls.Add(_split);
		Controls.Add(_ribbon);    // docks under the menu bar
		Controls.Add(_menu);
		MainMenuStrip = _menu;
		Load += (_, _) => _split.SplitterDistance = SidebarStart;
	}

	private const int SidebarStart = 300;

	// Graph + Variables column width the window grows by in Advanced View
	private int SidebarWidth => (IsHandleCreated ? _split.SplitterDistance : SidebarStart) + _split.SplitterWidth;

	// History and Numeric views share one window size; Advanced adds the
	// sidebar on the left, so History and Input stay where they are on
	// screen (owner 2026-11-07). A maximized window only swaps the panels.
	private void ResizeForSidebar(bool show)
	{
		if (WindowState != FormWindowState.Normal)
		{
			return;
		}
		int w = SidebarWidth;
		var bounds = show
			? new Rectangle(Left - w, Top, Width + w, Height)
			: new Rectangle(Left + w, Top, Math.Max(MinimumSize.Width, Width - w), Height);
		var area = Screen.FromRectangle(Bounds).WorkingArea;
		if (bounds.Left < area.Left && StartPosition == FormStartPosition.Manual)
		{
			bounds.X = area.Left;    // no room on the left: the window moves right
		}
		Bounds = bounds;
	}

	private static GroupBox Titled(string title, Control content)
	{
		var box = new GroupBox { Text = title, Dock = DockStyle.Fill };
		box.Controls.Add(content);
		return box;
	}

	private void OpenGraphWindow()
	{
		if (_graphWindow == null || _graphWindow.IsDisposed)
		{
			_graphWindow = new GraphWindow(_session, _graphSettings, _commands, value =>
			{
				Activate();
				InsertValue(value);
			});
			_graphWindow.UpdateFunctions(_state.Functions);
			_graphWindow.Show(this);
		}
		else
		{
			_graphWindow.Activate();
		}
	}

	private void OpenCompoundWindow()
	{
		if (_compoundWindow == null || _compoundWindow.IsDisposed)
		{
			_compoundWindow = new CompoundWindow(_session, _prefs.Compound);
			_compoundWindow.Show(this);
		}
		else
		{
			_compoundWindow.Activate();
		}
	}

	// Tools > Random (design/random_plan.md); a drawn number goes to Input at the caret
	private void OpenRandomWindow()
	{
		if (_randomWindow == null || _randomWindow.IsDisposed)
		{
			// Seed draws in an engine of the window's own, at the main window's precision
			var seeds = new SeedEngine(_session.StartInfo, () => (int?)_session.Settings["precision"] ?? _prefs.Precision);
			_randomWindow = new RandomWindow(_prefs.Random, InsertValue, seeds, new CardStore(_folder));
			_randomWindow.Show(this);
		}
		else
		{
			_randomWindow.Activate();
		}
	}

	// History and Input font (owner 2026-11-06); the Input row grows with it
	// History and Input font: base size from the Font dialog times the Text Size percentage
	private void SetTextFont(string family, float baseSize, int percent)
	{
		_prefs.TextFontFamily = family;
		_prefs.TextFontBase = baseSize;
		_prefs.TextScale = percent;
		var font = new Font(family, baseSize * percent / 100f);
		_history.Font = font;
		_input.Font = font;
		_hint.Font = new Font(font, FontStyle.Italic);
		LayoutInputRow();
		foreach (var (step, item) in _textSizeItems)
		{
			item.Checked = step == percent;
		}
	}

	private void LayoutInputRow()
	{
		int frame = Math.Max(29, _inputBox.Height - _inputBox.DisplayRectangle.Height);
		_hint.Visible = _showHintsItem.Checked;
		_hint.Height = _showHintsItem.Checked ? _hint.Font.Height + 4 : 0;    // Visible reads false before Show
		_column.RowStyles[2].Height = _input.PreferredHeight + _hint.Height + frame + 2;
	}

	private void UpdateHint()
	{
		if (!_showHintsItem.Checked)
		{
			return;
		}
		_hint.Text = InputHelp.For(_input.Text, _input.SelectionStart, DescribeUserName) ?? "";
		int request = ++_hintRequest;
		string? literal = InputHelp.LiteralAt(_input.Text, _input.SelectionStart);
		if (literal != null)
		{
			_ = ShowLiteralHintAsync(literal, request);
		}
	}

	// `0x1F = 31`: replaces the hint when the reply is still for the current text
	private async Task ShowLiteralHintAsync(string literal, int request)
	{
		try
		{
			string? converted = await _session.LiteralAsync(literal);
			if (converted != null && request == _hintRequest && _showHintsItem.Checked)
			{
				_hint.Text = $"{literal} = {converted}";
			}
		}
		catch (EngineException)
		{
			// engine not ready or restarting: keep the plain hint
		}
	}

	// User functions show their definition, variables their value
	private string? DescribeUserName(string name)
	{
		var fn = _state.Functions.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
		if (fn != null)
		{
			return $"{fn.Head}{fn.Body} - your function";
		}
		var variable = _state.Variables.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
		return variable == null ? null : $"{variable.Name} = {variable.Value} - your variable";
	}

	private void ChooseTextFont()
	{
		using var dialog = new FontDialog
		{
			Font = new Font(_input.Font.FontFamily, _prefs.TextFontBase), ShowEffects = false, AllowVerticalFonts = false, FontMustExist = true,
			MinSize = 6, MaxSize = 48,
		};
		if (dialog.ShowDialog(this) == DialogResult.OK)
		{
			SetTextFont(dialog.Font.FontFamily.Name, dialog.Font.SizeInPoints, 100);    // a chosen size is the new 100%
		}
	}

	// View > Theme: System / Light / Dark; choosing another restarts SoftCalc
	private ToolStripMenuItem BuildThemeMenu()
	{
		var menu = new ToolStripMenuItem("&Theme");
		foreach (var (name, label) in new[] { ("system", "&System"), ("light", "&Light"), ("dark", "&Dark") })
		{
			var item = new ToolStripMenuItem(label) { Checked = _prefs.Theme == name };
			item.Click += (_, _) =>
			{
				if (_prefs.Theme == name)
				{
					return;
				}
				_prefs.Theme = name;
				RestartRequested = true;
				Close();    // saves settings and History, then Program starts a new SoftCalc
			};
			menu.DropDownItems.Add(item);
		}
		return menu;
	}

	private void SetAlwaysOnTop(bool onTop)
	{
		_onTopItem.Checked = onTop;
		TopMost = onTop;    // owned windows (Graph, Compound Interest) stay above it
	}

	// The only network access: runs when a currency conversion needs rates
	private async Task<JsonObject> FetchRatesAsync()
	{
		UseWaitCursor = true;
		try
		{
			return await _rates.FetchAsync();
		}
		finally
		{
			UseWaitCursor = false;
		}
	}

	private void CenterKeypad()
	{
		_keypad.Location = new Point(
			Math.Max(0, (_keypadHost.Width - _keypad.Width) / 2),
			Math.Max(0, (_keypadHost.Height - _keypad.Height) / 2));
	}

	private void ApplyView(CalcView view)
	{
		bool wasAdvanced = _view == CalcView.Advanced;
		_view = view;
		if (wasAdvanced != (view == CalcView.Advanced))
		{
			ResizeForSidebar(view == CalcView.Advanced);
		}
		bool samples = _showSamplesItem.Checked && view != CalcView.Numeric;    // Numeric never shows Samples [KEEP]
		_column.RowStyles[1].Height = samples ? 110 : 0;
		_samplesBox.Visible = samples;
		_column.RowStyles[3].Height = view == CalcView.Numeric ? 150 : 0;
		_keypadHost.Visible = view == CalcView.Numeric;
		_ribbon.Visible = _showRibbonItem.Checked && view != CalcView.Numeric;    // Numeric stays plain
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
		file.DropDownItems.Add(new ToolStripMenuItem("&Help", null, (_, _) => HelpWindow.Open("getting-started", this))
		{
			ShortcutKeyDisplayString = "F1",    // the key itself: HelpWindow.HelpKey, in every window
		});
		file.DropDownItems.Add(new ToolStripMenuItem("A&bout SoftCalc...", null, (_, _) =>
		{
			using var about = new AboutDialog(_folder.Root);
			about.ShowDialog(this);
		}));
		file.DropDownItems.Add(new ToolStripSeparator());
		file.DropDownItems.Add(new ToolStripMenuItem("S&ave History...", null, (_, _) => SaveHistoryAs()));
		file.DropDownItems.Add(new ToolStripSeparator());
		file.DropDownItems.Add(new ToolStripMenuItem("&Save Functions && Variables...", null, (_, _) => SaveDefinitionsAs()));
		file.DropDownItems.Add(new ToolStripMenuItem("&Load Functions && Variables...", null,
			async (_, _) => await LoadDefinitionsAsync()));
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
		var wordSize = new ToolStripMenuItem("&Word Size");    // bits for twos(x) / signed(x)
		foreach (int bits in new[] { 8, 16, 32, 64 })
		{
			AddSettingItem(wordSize.DropDownItems, "word_size", bits, $"{bits} bits");
		}
		view.DropDownItems.Add(wordSize);
		view.DropDownItems.Add(new ToolStripSeparator());
		_showSamplesItem.Click += (_, _) =>
		{
			_showSamplesItem.Checked = !_showSamplesItem.Checked;
			ApplyView(_view);
		};
		view.DropDownItems.Add(_showSamplesItem);
		_onTopItem.Click += (_, _) => SetAlwaysOnTop(!_onTopItem.Checked);
		view.DropDownItems.Add(_onTopItem);
		_showRibbonItem.Click += (_, _) =>
		{
			_showRibbonItem.Checked = !_showRibbonItem.Checked;
			ApplyView(_view);
		};
		view.DropDownItems.Add(_showRibbonItem);
		view.DropDownItems.Add(new ToolStripMenuItem("C&ustomize Ribbon...", null, (_, _) => CustomizeRibbon()));
		var decimals = new ToolStripMenuItem("Deci&mal Places");
		decimals.DropDownItems.Add(new ToolStripMenuItem("&More", null, async (_, _) => await StepDecimalsAsync(+1)));
		decimals.DropDownItems.Add(new ToolStripMenuItem("&Fewer", null, async (_, _) => await StepDecimalsAsync(-1)));
		_allDigitsItem.Click += async (_, _) => await ChangeDecimalsAsync(null);
		decimals.DropDownItems.Add(_allDigitsItem);
		decimals.DropDownItems.Add(new ToolStripSeparator());
		_padZerosItem.Click += async (_, _) => await ChangeSettingAsync("pad_zeros", !_padZerosItem.Checked);
		decimals.DropDownItems.Add(_padZerosItem);
		view.DropDownItems.Add(decimals);
		var textSize = new ToolStripMenuItem("Te&xt Size");
		foreach (int percent in new[] { 100, 125, 150, 175, 200 })
		{
			var item = new ToolStripMenuItem($"{percent}%", null, (_, _) => SetTextFont(_prefs.TextFontFamily, _prefs.TextFontBase, percent));
			_textSizeItems[percent] = item;
			textSize.DropDownItems.Add(item);
		}
		textSize.DropDownItems.Add(new ToolStripSeparator());
		textSize.DropDownItems.Add(new ToolStripMenuItem("&Font...", null, (_, _) => ChooseTextFont()));
		view.DropDownItems.Add(textSize);
		_showHintsItem.Click += (_, _) =>
		{
			_showHintsItem.Checked = !_showHintsItem.Checked;
			LayoutInputRow();
			UpdateHint();
		};
		view.DropDownItems.Add(_showHintsItem);
		view.DropDownItems.Add(BuildThemeMenu());
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

		var tools = new ToolStripMenuItem("&Tools");
		tools.DropDownItems.Add(new ToolStripMenuItem("&Graph...", null, (_, _) => OpenGraphWindow()));
		tools.DropDownItems.Add(new ToolStripMenuItem("&Compound Interest...", null, (_, _) => OpenCompoundWindow()));
		tools.DropDownItems.Add(new ToolStripMenuItem("&Random...", null, (_, _) => OpenRandomWindow()));

		_menu.Items.AddRange(new ToolStripItem[] { file, view, _functionsMenu, _conversionsMenu, tools });
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

		FillConversions(_conversionsMenu.DropDownItems, catalog);
		RebuildRibbon();    // functions and conversion pairs can now be found
	}

	private void FillConversions(ToolStripItemCollection items, Catalog catalog)
	{
		items.Clear();
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
			if (category == "Currency")
			{
				AddCurrencyHalves(categoryItem, catalog.Currencies);
			}
			items.Add(categoryItem);
		}
		var formats = new ToolStripMenuItem("Date Format");
		foreach (string name in catalog.Formats)
		{
			formats.DropDownItems.Add(new ToolStripMenuItem(name, null, async (_, _) =>
				await InsertFromMenuAsync(new JsonObject { ["kind"] = "format", ["name"] = name })));
		}
		items.Add(new ToolStripSeparator());
		items.Add(formats);
	}

	// Below the common pairs: From inserts " [THB", To inserts "->USD]" (owner 2026-11-06)
	private void AddCurrencyHalves(ToolStripMenuItem category, IReadOnlyList<string> codes)
	{
		category.DropDownItems.Add(new ToolStripSeparator());
		foreach (var (label, kind) in new[] { ("&From", "currency_from"), ("&To", "currency_to") })
		{
			var group = new ToolStripMenuItem(label);
			foreach (string code in codes)
			{
				group.DropDownItems.Add(new ToolStripMenuItem(code, null, async (_, _) =>
					await InsertFromMenuAsync(new JsonObject { ["kind"] = kind, ["code"] = code })));
			}
			category.DropDownItems.Add(group);
		}
	}

	private void BuildUserFunctionsMenu()
	{
		_userFunctionsMenu.DropDownItems.Clear();
		foreach (var fn in _state.Functions)
		{
			string label = $"{fn.Name}({string.Join(", ", fn.Parameters)})";
			_userFunctionsMenu.DropDownItems.Add(new ToolStripMenuItem(label, null, async (_, _) =>
				await InsertFromMenuAsync(new JsonObject { ["kind"] = "function", ["name"] = fn.Name }))
			{
				Tag = fn.Name,    // ribbon command id: the name, so new parameters keep the button
			});
		}
		_userFunctionsMenu.Enabled = _state.Functions.Count > 0;
		string names = string.Join(",", _state.Functions.Select(fn => fn.Name));
		if (names != _ribbonFunctions)
		{
			_ribbonFunctions = names;
			RebuildRibbon();
		}
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
		_ribbon.ShowSettings(settings);
		_padZerosItem.Checked = (bool?)settings["pad_zeros"] == true;
		_allDigitsItem.Checked = settings["decimals"] == null;
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
			await RestoreSessionAsync();
			_ribbonHeld = true;    // one ribbon build once catalog and user functions are in
			try
			{
				BuildCatalogMenus(await _session.CatalogAsync());
				await RefreshStateAsync();
			}
			finally
			{
				_ribbonHeld = false;
			}
			RebuildRibbon();
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
		_graphPanel.UpdateFunctions(_state.Functions);
		_variablesPanel.UpdateValues(_state);
		if (_graphWindow != null && !_graphWindow.IsDisposed)
		{
			_graphWindow.UpdateFunctions(_state.Functions);
		}
	}

	private async Task SubmitAsync()
	{
		if (_busy || !_engineReady)
		{
			return;    // before start-up finishes, Enter waits; the typed text stays
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
			_history.Append(Stamp(new HistoryEntry(reply.Kind, reply.Input, reply.DisplayInput, reply.Text) { Note = reply.Note }));
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
		if (!_engineReady)
		{
			return;
		}
		JsonNode? node = value switch
		{
			bool flag => JsonValue.Create(key == "rpn" ? !((bool?)_session.Settings["rpn"] == true) : flag),
			int number => JsonValue.Create(number),
			_ => JsonValue.Create((string)value),
		};
		await _session.ChangeSettingsAsync(new JsonObject { [key] = node });
		ShowSettingChecks();
		await RefreshStateAsync();    // constants and values follow precision and base
		_graphSettings.NotifyChanged();    // the angle unit changes trig plots
		NotifyToolWindows();
	}

	private void NotifyToolWindows()
	{
		if (_compoundWindow != null && !_compoundWindow.IsDisposed)
		{
			_compoundWindow.SettingsChanged();    // amounts follow precision, base and decimals
		}
	}

	// E9 display decimals: More/Fewer step from the current setting (DecimalSteps)
	private async Task StepDecimalsAsync(int delta)
	{
		await ChangeDecimalsAsync(DecimalSteps.Next((int?)_session.Settings["decimals"], delta));
	}

	private async Task ChangeDecimalsAsync(int? decimals)
	{
		if (!_engineReady)
		{
			return;
		}
		await _session.ChangeSettingsAsync(new JsonObject { ["decimals"] = decimals });
		ShowSettingChecks();
		await RefreshStateAsync();
		NotifyToolWindows();
	}

	// One History line goes; a result line also leaves `$n`, so later ones renumber
	private async Task DeleteHistoryEntryAsync(HistoryEntry entry)
	{
		if (!_engineReady)
		{
			return;
		}
		int n = _history.ResultNumber(entry);
		if (n > 0)
		{
			try
			{
				await _session.DeleteResultAsync(n);
			}
			catch (EngineException ex)
			{
				AddNotice("(entry not deleted)", ex.Message);
				return;
			}
		}
		_history.RemoveEntry(entry);
		if (entry.Id.Length > 0)
		{
			_deletedIds[entry.Id] = entry.Time;    // merged at close so the file and other windows drop it too
			_sessionEntries.RemoveAll(e => e.Id == entry.Id);
		}
		_lastWasResult = _history.Entries.Count > 0 && _history.Entries[^1].Kind == "result";
	}

	private async Task ClearHistoryAsync()
	{
		if (!_engineReady)
		{
			return;
		}
		_history.ClearEntries();
		_clearedAt = DateTime.UtcNow;    // merged at close so other windows' older entries go too
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

	// The standard edit commands (a custom menu replaces the system one) plus Clear Input
	private ContextMenuStrip BuildInputMenu()
	{
		var menu = new ContextMenuStrip();
		var undo = new ToolStripMenuItem("&Undo", null, (_, _) => _input.Undo());
		var cut = new ToolStripMenuItem("Cu&t", null, (_, _) => _input.Cut());
		var copy = new ToolStripMenuItem("&Copy", null, (_, _) => _input.Copy());
		var paste = new ToolStripMenuItem("&Paste", null, (_, _) => _input.Paste());
		var delete = new ToolStripMenuItem("&Delete", null, (_, _) => _input.SelectedText = "");
		var selectAll = new ToolStripMenuItem("Select &All", null, (_, _) => _input.SelectAll());
		var clear = new ToolStripMenuItem("C&lear Input", null, (_, _) =>
		{
			_input.Clear();
			_input.Focus();
		});
		menu.Items.AddRange(new ToolStripItem[]
		{
			undo, new ToolStripSeparator(), cut, copy, paste, delete, new ToolStripSeparator(),
			selectAll, clear,
		});
		menu.Opening += (_, _) =>
		{
			bool editable = !_input.ReadOnly;
			bool selection = _input.SelectionLength > 0;
			undo.Enabled = editable && _input.CanUndo;
			cut.Enabled = editable && selection;
			copy.Enabled = selection;
			paste.Enabled = editable && Clipboard.ContainsText();
			delete.Enabled = editable && selection;
			selectAll.Enabled = _input.TextLength > 0;
			clear.Enabled = editable && _input.TextLength > 0;
		};
		return menu;
	}

	// Only results insert; errors and definitions have no value to use
	private void InsertResult(HistoryEntry entry)
	{
		if (entry.Kind != "result")
		{
			return;
		}
		InsertValue(entry.Text);
	}

	// A result text into the Input at the caret (History Shift+click, Graph trace)
	private void InsertValue(string value)
	{
		bool rpn = (bool?)_session.Settings["rpn"] == true;
		var (text, caret) = InputRules.InsertResult(_input.Text, _input.SelectionStart, _input.SelectionLength, value, rpn);
		_input.Text = text;
		_input.SelectionStart = caret;
		_input.Focus();
	}

	// A Help example clicked: its line replaces Input
	internal void PutInInput(string line)
	{
		if (WindowState == FormWindowState.Minimized)
		{
			WindowState = FormWindowState.Normal;
		}
		Activate();
		_input.Text = line;
		_input.SelectionStart = _input.TextLength;
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
