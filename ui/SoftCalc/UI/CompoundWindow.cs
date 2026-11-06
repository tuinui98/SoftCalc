// CompoundWindow.cs - Tools > Compound Interest: inputs, totals, growth graph, period table (E11)
//
// Recalculates 300 ms after the last change (owner 2026-11-06). Inputs
// accept any engine expression. A bad input shows its message next to
// the field and the last good result stays. The table is a virtual
// DataGridView, so daily periods over decades stay quick; Ctrl+C copies
// the selected rows as tab-separated text.

using System.Text.Json.Nodes;
using SoftCalc.Engine;
using SoftCalc.Logic;
using SoftCalc.Persistence;

namespace SoftCalc.UI;

internal sealed class CompoundWindow : Form
{
	private static readonly string[] CompoundingChoices = { "yearly", "quarterly", "monthly", "daily", "continuous" };
	private static readonly string[] ContributionChoices = { "yearly", "quarterly", "monthly", "daily" };

	private readonly EngineSession _session;
	private readonly CompoundPrefs _prefs;
	private readonly Dictionary<string, TextBox> _boxes = new();
	private readonly Dictionary<string, Label> _errors = new();
	private readonly ComboBox _compounding = Choice(CompoundingChoices);
	private readonly ComboBox _contributionFreq = Choice(ContributionChoices);
	private readonly CheckBox _atStart = new() { Text = "Contribution at the start of each period", AutoSize = true };
	private readonly TextBox _futureValue = Output();
	private readonly TextBox _totalContributions = Output();
	private readonly TextBox _totalInterest = Output();
	private readonly Label _generalError = new() { AutoSize = true, ForeColor = Theme.Error };
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
	private int _request;
	private bool _loading = true;

	public CompoundWindow(EngineSession session, CompoundPrefs prefs)
	{
		_session = session;
		_prefs = prefs;
		Text = "Compound Interest";
		Size = new Size(820, 640);
		MinimumSize = new Size(560, 460);
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

	//==================================================================
	//  Layout
	//==================================================================
	private Control BuildInputs()
	{
		var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, AutoSize = true, Padding = new Padding(6) };
		grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
		grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
		grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
		grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
		AddField(grid, 0, "principal", "&Principal");
		AddField(grid, 1, "annual_rate", "Annual &rate %");
		AddField(grid, 2, "years", "&Years");
		AddField(grid, 3, "contribution", "C&ontribution");
		AddRow(grid, 4, "Compounding", _compounding);
		AddRow(grid, 5, "Contribution every", _contributionFreq);
		grid.Controls.Add(_atStart, 1, 6);
		grid.SetColumnSpan(_atStart, 2);
		grid.Controls.Add(_generalError, 1, 7);
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
		grid.SetRowSpan(box, 6);

		_compounding.SelectedIndexChanged += (_, _) => Changed();
		_contributionFreq.SelectedIndexChanged += (_, _) => Changed();
		_atStart.CheckedChanged += (_, _) => Changed();
		return grid;
	}

	private void AddField(TableLayoutPanel grid, int row, string field, string label)
	{
		var box = new TextBox { Dock = DockStyle.Fill };
		var error = new Label { AutoSize = true, ForeColor = Theme.Error, Anchor = AnchorStyles.Left };
		box.TextChanged += (_, _) => Changed();
		_boxes[field] = box;
		_errors[field] = error;
		AddRow(grid, row, label, box);
		grid.Controls.Add(error, 2, row);
	}

	private static void AddRow(TableLayoutPanel grid, int row, string label, Control control)
	{
		grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
		control.Anchor = AnchorStyles.Left | AnchorStyles.Right;    // centred in the row, like its label
		grid.Controls.Add(control, 1, row);
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
		return bar;
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
		_boxes["principal"].Text = _prefs.Principal;
		_boxes["annual_rate"].Text = _prefs.Rate;
		_boxes["years"].Text = _prefs.Years;
		_boxes["contribution"].Text = _prefs.Contribution;
		_compounding.SelectedItem = _prefs.Compounding;
		_contributionFreq.SelectedItem = _prefs.ContributionFreq;
		_atStart.Checked = _prefs.AtStart;
		_loading = false;
	}

	private void Changed()
	{
		if (_loading)
		{
			return;
		}
		_prefs.Principal = _boxes["principal"].Text;
		_prefs.Rate = _boxes["annual_rate"].Text;
		_prefs.Years = _boxes["years"].Text;
		_prefs.Contribution = _boxes["contribution"].Text;
		_prefs.Compounding = (string?)_compounding.SelectedItem ?? "monthly";
		_prefs.ContributionFreq = (string?)_contributionFreq.SelectedItem ?? "monthly";
		_prefs.AtStart = _atStart.Checked;
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
		};
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
		}
		else
		{
			_generalError.Text = message;
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
		_chart.SetData(reply.Principal, reply.Contributions, reply.Interest, reply.PeriodsPerYear);
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
		using var dialog = new SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", DefaultExt = "csv", FileName = "compound-interest.csv" };
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
