// CompoundWindow.cs - Tools > Compound Interest: inputs, totals, growth graph, period table (E11)
//
// Recalculates 300 ms after the last change (owner 2026-11-06). Inputs
// accept any engine expression. A bad input shows its message next to
// the field and the last good result stays. The table is a virtual
// DataGridView, so daily periods over decades stay quick; Ctrl+C copies
// the selected rows as tab-separated text.
//
// Two modes (owner 2026-11-28). Normal works out the future value. Goal
// takes four of Target, Principal, Interest rate, Length and Contribution
// and works out the fifth: the one set longest ago (CompoundPrefs.GoalOrder).
// Typing in a field, or changing its unit, sets it. In Goal the rate's unit
// is the compounding period, and a length may end in a part period.

using System.Text.Json.Nodes;
using SoftCalc.Engine;
using SoftCalc.Logic;
using SoftCalc.Persistence;

namespace SoftCalc.UI;

internal sealed class CompoundWindow : Form
{
	// owner 2026-11-28: weekly too, quarterly kept
	private static readonly string[] CompoundingChoices = { "yearly", "quarterly", "monthly", "weekly", "daily", "continuous" };
	private static readonly string[] ContributionChoices = { "yearly", "quarterly", "monthly", "weekly", "daily" };
	private static readonly string[] RateUnitChoices = { "yearly", "monthly", "weekly", "daily" };
	private static readonly string[] LengthUnitChoices = { "years", "months", "weeks", "days" };
	private const int MostDecimals = 15;

	private readonly EngineSession _session;
	private readonly CompoundPrefs _prefs;
	private readonly Dictionary<string, TextBox> _boxes = new();
	private readonly Dictionary<string, Label> _errors = new();
	private readonly Dictionary<string, List<Control>> _rowParts = new();    // to hide Target / Compounding
	private readonly ComboBox _compounding = Choice(CompoundingChoices);
	private readonly ComboBox _contributionFreq = Choice(ContributionChoices);
	private readonly ComboBox _rateUnit = Choice(RateUnitChoices);
	private readonly ComboBox _lengthUnit = Choice(LengthUnitChoices);
	private readonly RadioButton _normal = new() { Text = "&Normal", AutoSize = true };
	private readonly RadioButton _goal = new() { Text = "&Goal", AutoSize = true };
	private readonly CheckBox _atStart = new() { Text = "Contribution at the start of each period", AutoSize = true };
	private readonly TextBox _futureValue = Output();
	private readonly TextBox _totalContributions = Output();
	private readonly TextBox _totalInterest = Output();
	private readonly Label _generalError = new() { AutoSize = true, ForeColor = Theme.Error };
	private readonly Label _decimals = new() { AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 0, 0) };
	private readonly CompoundChart _chart = new() { Dock = DockStyle.Fill };
	private readonly DataGridView _table = new()
	{
		Dock = DockStyle.Fill, VirtualMode = true, ReadOnly = true, AllowUserToAddRows = false,
		AllowUserToDeleteRows = false, AllowUserToResizeRows = false, RowHeadersVisible = false,
		SelectionMode = DataGridViewSelectionMode.FullRowSelect,
		ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText,
		AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = SystemColors.Window,
	};
	private readonly System.Windows.Forms.Timer _delay = new() { Interval = 300 };
	private IReadOnlyList<ScheduleRow> _rows = Array.Empty<ScheduleRow>();
	private string _lengthYmd = "";
	private int _request;
	private bool _loading = true;
	private bool _writing;    // a worked-out value going into its box: not "set"

	public CompoundWindow(EngineSession session, CompoundPrefs prefs)
	{
		_session = session;
		_prefs = prefs;
		Text = "Compound Interest";
		Size = new Size(860, 680);
		MinimumSize = new Size(600, 500);
		StartPosition = FormStartPosition.CenterParent;
		ShowInTaskbar = false;
		Icon = SvgIcon.ProgramIcon();

		var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
		split.Panel1.Controls.Add(_chart);
		split.Panel2.Controls.Add(_table);
		var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
		layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
		layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		layout.Controls.Add(BuildInputs(), 0, 0);
		layout.Controls.Add(split, 0, 1);
		layout.Controls.Add(BuildButtons(), 0, 2);
		Controls.Add(layout);
		Load += (_, _) => split.SplitterDistance = split.Height / 2;
		// owner 2026-11-28: the chart and the table follow the splitter as it moves, not only on release
		split.SplitterMoving += (_, e) =>
		{
			if (e.SplitY > 0 && e.SplitY != split.SplitterDistance)
			{
				split.SplitterDistance = e.SplitY;
			}
		};

		foreach (string header in CompoundTable.Header)
		{
			_table.Columns.Add(new DataGridViewTextBoxColumn
			{
				HeaderText = header, SortMode = DataGridViewColumnSortMode.NotSortable,
				DefaultCellStyle = { Alignment = header == "Period" ? DataGridViewContentAlignment.MiddleCenter : DataGridViewContentAlignment.MiddleRight },
			});
		}
		_table.Columns[0].FillWeight = 40;
		if (Theme.IsDark)
		{
			// visual-style headers stay white in dark mode; draw them flat in theme colours
			_table.EnableHeadersVisualStyles = false;
			_table.ColumnHeadersDefaultCellStyle.BackColor = SystemColors.Control;
			_table.ColumnHeadersDefaultCellStyle.ForeColor = SystemColors.ControlText;
			_table.ColumnHeadersDefaultCellStyle.SelectionBackColor = SystemColors.Control;
		}
		_table.CellValueNeeded += OnCellValueNeeded;

		LoadPrefs();
		_delay.Tick += async (_, _) => await RecalculateAsync();
		Shown += async (_, _) => await RecalculateAsync();
		FormClosed += (_, _) => _delay.Dispose();
	}

	private bool IsGoal => _prefs.Mode == "goal";

	private string Solving => _prefs.GoalOrder[0];

	//==================================================================
	//  Layout
	//==================================================================
	private Control BuildInputs()
	{
		var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, AutoSize = true, Padding = new Padding(6) };
		grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
		grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
		grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
		grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
		var modes = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
		modes.Controls.Add(_normal);
		modes.Controls.Add(_goal);
		grid.Controls.Add(new Label { Text = "Mode", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
		grid.Controls.Add(modes, 1, 0);
		grid.SetColumnSpan(modes, 2);
		AddField(grid, 1, "target", "&Target");
		AddField(grid, 2, "principal", "&Principal");
		AddField(grid, 3, "annual_rate", "Interest &rate %", _rateUnit);
		AddField(grid, 4, "years", "&Length", _lengthUnit);
		AddField(grid, 5, "contribution", "C&ontribution");
		_rowParts["compounding"] = AddRow(grid, 6, "Compounding", _compounding);
		AddRow(grid, 7, "Contribution every", _contributionFreq);
		grid.Controls.Add(_atStart, 1, 8);
		grid.SetColumnSpan(_atStart, 2);
		grid.Controls.Add(_generalError, 1, 9);
		grid.SetColumnSpan(_generalError, 3);

		var totals = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4 };
		totals.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
		totals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
		for (int row = 0; row < 3; row++)
		{
			totals.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
		}
		totals.RowStyles.Add(new RowStyle(SizeType.Percent, 100));    // spare height stays below the rows
		AddRow(totals, 0, "Future value", _futureValue);
		AddRow(totals, 1, "Total contributions", _totalContributions);
		AddRow(totals, 2, "Total interest", _totalInterest);
		var box = new GroupBox { Text = "Result", Dock = DockStyle.Fill };
		box.Controls.Add(totals);
		grid.Controls.Add(box, 3, 0);
		grid.SetRowSpan(box, 8);

		_normal.CheckedChanged += (_, _) => ModeChanged();
		_compounding.SelectedIndexChanged += (_, _) => Changed();
		_contributionFreq.SelectedIndexChanged += (_, _) => Changed("contribution");
		_rateUnit.SelectedIndexChanged += (_, _) => Changed("annual_rate");
		_lengthUnit.SelectedIndexChanged += (_, _) => Changed("years");
		_atStart.CheckedChanged += (_, _) => Changed();
		return grid;
	}

	// A field, with its unit's choice beside it when it has one (rate per..., length in...)
	private void AddField(TableLayoutPanel grid, int row, string field, string label, ComboBox? unit = null)
	{
		var box = new TextBox { Dock = DockStyle.Fill };
		var error = new Label { AutoSize = true, ForeColor = Theme.Error, Anchor = AnchorStyles.Left };
		box.TextChanged += (_, _) => Changed(field);
		_boxes[field] = box;
		_errors[field] = error;
		Control cell = box;
		if (unit != null)
		{
			var pair = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Margin = new Padding(3, 0, 3, 0) };
			pair.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
			pair.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
			box.Margin = new Padding(0, 3, 3, 3);
			unit.Dock = DockStyle.Fill;
			unit.Margin = new Padding(0, 3, 0, 3);
			pair.Controls.Add(box, 0, 0);
			pair.Controls.Add(unit, 1, 0);
			cell = pair;
		}
		var parts = AddRow(grid, row, label, cell);
		grid.Controls.Add(error, 2, row);
		parts.Add(error);
		_rowParts[field] = parts;
	}

	private static List<Control> AddRow(TableLayoutPanel grid, int row, string label, Control control)
	{
		var name = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left };
		grid.Controls.Add(name, 0, row);
		control.Anchor = AnchorStyles.Left | AnchorStyles.Right;    // centred in the row, like its label
		grid.Controls.Add(control, 1, row);
		return new List<Control> { name, control };
	}

	private Control BuildButtons()
	{
		var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(4) };
		var export = new Button { Text = "&Export CSV...", AutoSize = true };
		var copyAll = new Button { Text = "&Copy all", AutoSize = true };
		export.Click += (_, _) => ExportCsv();
		copyAll.Click += (_, _) =>
		{
			if (_rows.Count > 0)
			{
				Clipboard.SetText(CompoundTable.ToTabs(_rows, header: true));
			}
		};
		bar.Controls.Add(export);
		bar.Controls.Add(copyAll);

		// owner 2026-11-28: the table's own decimal places, as View > Decimal Places: fewer, more, all
		var places = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 24, 0) };
		var fewer = new Button { Text = "Fewer", AutoSize = true };
		var more = new Button { Text = "More", AutoSize = true };
		var all = new Button { Text = "All", AutoSize = true };
		fewer.Click += (_, _) => SetDecimals(_prefs.TableDecimals is int n ? Math.Max(0, n - 1) : MostDecimals);
		more.Click += (_, _) => SetDecimals(_prefs.TableDecimals is int n && n < MostDecimals ? n + 1 : null);
		all.Click += (_, _) => SetDecimals(null);
		places.Controls.Add(new Label { Text = "Table decimals:", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 0, 0) });
		places.Controls.Add(_decimals);
		places.Controls.Add(fewer);
		places.Controls.Add(more);
		places.Controls.Add(all);
		bar.Controls.Add(places);
		return bar;
	}

	private void SetDecimals(int? places)
	{
		_prefs.TableDecimals = places;
		ShowDecimals();
		Changed();
	}

	private void ShowDecimals()
	{
		_decimals.Text = _prefs.TableDecimals?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "All";
	}

	private static ComboBox Choice(string[] items)
	{
		var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
		combo.Items.AddRange(items);
		return combo;
	}

	private static TextBox Output()
	{
		return new TextBox { ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = SystemColors.Control };
	}

	//==================================================================
	//  Inputs and recalculation
	//==================================================================
	private void LoadPrefs()
	{
		_boxes["target"].Text = _prefs.Target;
		_boxes["principal"].Text = _prefs.Principal;
		_boxes["annual_rate"].Text = _prefs.Rate;
		_boxes["years"].Text = _prefs.Years;
		_boxes["contribution"].Text = _prefs.Contribution;
		_compounding.SelectedItem = _prefs.Compounding;
		_contributionFreq.SelectedItem = _prefs.ContributionFreq;
		_rateUnit.SelectedItem = _prefs.RateUnit;
		_lengthUnit.SelectedItem = _prefs.LengthUnit;
		_atStart.Checked = _prefs.AtStart;
		_normal.Checked = !IsGoal;
		_goal.Checked = IsGoal;
		ShowDecimals();
		ShowMode();
		_loading = false;
	}

	private void ModeChanged()
	{
		if (_loading)
		{
			return;
		}
		_prefs.Mode = _goal.Checked ? "goal" : "normal";
		ShowMode();
		Changed();
	}

	// Goal: Target shows, Compounding goes (the rate's unit compounds); Normal the other way
	private void ShowMode()
	{
		foreach (var control in _rowParts["target"])
		{
			control.Visible = IsGoal;
		}
		foreach (var control in _rowParts["compounding"])
		{
			control.Visible = !IsGoal;
		}
		ShowMarks();
	}

	// field: the one the person just set (goal mode: it becomes the most important)
	private void Changed(string? field = null)
	{
		if (_loading)
		{
			return;
		}
		_prefs.Target = _boxes["target"].Text;
		_prefs.Principal = _boxes["principal"].Text;
		_prefs.Rate = _boxes["annual_rate"].Text;
		_prefs.Years = _boxes["years"].Text;
		_prefs.Contribution = _boxes["contribution"].Text;
		_prefs.Compounding = (string?)_compounding.SelectedItem ?? "monthly";
		_prefs.ContributionFreq = (string?)_contributionFreq.SelectedItem ?? "monthly";
		_prefs.RateUnit = (string?)_rateUnit.SelectedItem ?? "yearly";
		_prefs.LengthUnit = (string?)_lengthUnit.SelectedItem ?? "years";
		_prefs.AtStart = _atStart.Checked;
		if (_writing)
		{
			return;    // a worked-out value: kept, not set, no new round
		}
		if (field != null && IsGoal)
		{
			if (field == Solving)
			{
				_lengthYmd = "";    // typed into the result: now set, the next oldest is worked out
			}
			_prefs.SetGoalField(field);
		}
		ShowMarks();
		_delay.Stop();
		_delay.Start();
	}

	// Settings changed in the main window (precision, base, decimals)
	public void SettingsChanged()
	{
		Changed();
	}

	private async Task RecalculateAsync()
	{
		_delay.Stop();
		if (!_session.IsStarted)
		{
			return;
		}
		int request = ++_request;
		var arguments = new JsonObject
		{
			["principal"] = Blank(_prefs.Principal), ["annual_rate"] = Blank(_prefs.Rate), ["years"] = Blank(_prefs.Years),
			["contribution"] = Blank(_prefs.Contribution, "0"), ["compounding"] = _prefs.Compounding,
			["contribution_freq"] = _prefs.ContributionFreq, ["at_start"] = _prefs.AtStart,
			["rate_unit"] = _prefs.RateUnit, ["length_unit"] = _prefs.LengthUnit,
			["table_decimals"] = _prefs.TableDecimals is int places ? places : "all",
		};
		if (IsGoal)
		{
			arguments["mode"] = "goal";
			arguments["solve"] = Solving;
			arguments["target"] = Blank(_prefs.Target);
		}
		ScheduleReply? reply = null;
		string? error = null;
		try
		{
			reply = await _session.ScheduleAsync(arguments);
		}
		catch (EngineException ex)
		{
			error = ex.Message;
		}
		if (request != _request || IsDisposed)
		{
			return;    // a newer change is on its way
		}
		ShowErrors(error);
		if (reply != null)
		{
			ShowResult(reply);
		}
		ShowMarks();
	}

	private static string Blank(string text, string empty = "")
	{
		return string.IsNullOrWhiteSpace(text) ? empty : text;
	}

	private void ShowErrors(string? error)
	{
		foreach (var label in _errors.Values)
		{
			label.Text = "";
		}
		_generalError.Text = "";
		if (error == null)
		{
			return;
		}
		var (field, message) = CompoundTable.RouteError(error);
		if (field != null)
		{
			_errors[field].Text = message;
			_errors[field].ForeColor = Theme.Error;
		}
		else
		{
			_generalError.Text = message;
		}
	}

	// Goal: the worked-out field in bold, "= result" (the length also as 7 y 3 m 18 d) beside it
	private void ShowMarks()
	{
		foreach (var (field, box) in _boxes)
		{
			bool solved = IsGoal && field == Solving;
			var style = solved ? FontStyle.Bold : FontStyle.Regular;
			if (box.Font.Style != style)
			{
				box.Font = new Font(Font, style);
			}
			var label = _errors[field];
			if (label.ForeColor != Theme.Error || label.Text.Length == 0)
			{
				label.ForeColor = SystemColors.GrayText;
				label.Text = !solved ? "" : field == "years" && _lengthYmd.Length > 0 ? "= result, " + _lengthYmd : "= result";
			}
		}
	}

	private void ShowResult(ScheduleReply reply)
	{
		_futureValue.Text = reply.FutureValue;
		_totalContributions.Text = reply.TotalContributions;
		_totalInterest.Text = reply.TotalInterest;
		_rows = reply.Rows;
		_table.RowCount = 0;
		_table.RowCount = _rows.Count;
		_table.Invalidate();
		_chart.SetData(reply.Principal, reply.Contributions, reply.Interest,
			reply.PeriodsPerUnit > 0 ? reply.PeriodsPerUnit : reply.PeriodsPerYear, reply.LengthUnit, reply.Partial);
		if (reply.Solved is { } solved && _boxes.TryGetValue(solved.Field, out var box))
		{
			_lengthYmd = solved.LengthYmd;
			_writing = true;
			box.Text = solved.Value;
			_writing = false;
		}
	}

	private void OnCellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
	{
		if (e.RowIndex < 0 || e.RowIndex >= _rows.Count)
		{
			return;
		}
		e.Value = CompoundTable.Cells(_rows[e.RowIndex]).ElementAt(e.ColumnIndex);
	}

	private void ExportCsv()
	{
		if (_rows.Count == 0)
		{
			return;
		}
		using var dialog = new SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", DefaultExt = "csv", FileName = HistoryStore.ExportName("compound", "csv", DateTime.Now) };    // as the web app's
		if (dialog.ShowDialog(this) != DialogResult.OK)
		{
			return;
		}
		try
		{
			DataFolder.WriteAtomic(dialog.FileName, CompoundTable.ToCsv(_rows));
		}
		catch (IOException ex)
		{
			MessageBox.Show(this, ex.Message, "Export CSV", MessageBoxButtons.OK, MessageBoxIcon.Warning);
		}
	}
}
