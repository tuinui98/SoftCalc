// RandomWindow.Advanced.cs - The Advanced random tab: draws that follow a chosen shape (design/random_plan.md R3)
//
// A shape from the list (bell, skews, triangle, U, two peaks, ...), a
// Strength for it, a whole or decimal range and a count. The shape is
// drawn beside the results; Generate draws the results' bars over it.
// Insert Command puts the engine call (randbeta, randtri, randexp,
// randpeaks) that draws the same way in Input.

using SoftCalc.Logic;
using SoftCalc.Persistence;

namespace SoftCalc.UI;

internal sealed partial class RandomWindow
{
	private readonly ComboBox _shape = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
	private readonly NumericUpDown _shapeStrength = Number(RandomShapes.MinStrength, RandomShapes.MaxStrength, 0);
	private readonly NumericUpDown _peak = Number(-RandomTools.Limit, RandomTools.Limit, 0);
	private readonly NumericUpDown _betaA = Number(RandomShapes.MinBeta, RandomShapes.MaxBeta, 2);
	private readonly NumericUpDown _betaB = Number(RandomShapes.MinBeta, RandomShapes.MaxBeta, 2);
	private readonly RadioButton _shapeWhole = Kind("&Whole numbers", "whole");
	private readonly RadioButton _shapeDecimal = Kind("&Decimals", "decimal");
	private readonly NumericUpDown _shapeFrom = Number(-RandomTools.Limit, RandomTools.Limit, 0);
	private readonly NumericUpDown _shapeTo = Number(-RandomTools.Limit, RandomTools.Limit, 0);
	private readonly NumericUpDown _shapePlaces = Number(0, RandomTools.MaxPlaces, 0);
	private readonly NumericUpDown _shapeCount = Number(1, RandomShapes.MaxCount, 0);
	private readonly ListBox _shapeList = Results(false);
	private readonly Label _shapeNote = Note();
	private readonly ShapePlot _plot = new() { Dock = DockStyle.Fill, Margin = new Padding(6, 6, 0, 0) };
	private bool _shapeLoading;

	private TabPage AdvancedTab()
	{
		var page = new TabPage("Advanced") { Padding = new Padding(8) };
		_shape.Items.AddRange(RandomShapes.Shapes.Select(s => (object)s.Name).ToArray());
		_shapeStrength.Width = 60;
		_betaA.Width = 80;
		_betaB.Width = 80;
		_shapePlaces.Width = 60;
		new ToolTip().SetToolTip(_shapeStrength, "1 to 10: how strong the shape is");

		var fields = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 4 };
		AddPair(fields, "S&hape", _shape, "St&rength", _shapeStrength);
		AddPair(fields, "&a", _betaA, "&b", _betaB);
		int peakRow = fields.RowCount++;
		var peakCaption = Caption("P&eak at");
		_captions[_peak] = peakCaption;
		fields.Controls.Add(peakCaption, 0, peakRow);
		fields.Controls.Add(_peak, 1, peakRow);
		var kinds = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty, WrapContents = false };
		var placesCaption = Caption("&Places");
		_captions[_shapePlaces] = placesCaption;
		kinds.Controls.AddRange(new Control[] { _shapeWhole, _shapeDecimal, placesCaption, _shapePlaces });
		fields.Controls.Add(kinds, 0, fields.RowCount++);
		fields.SetColumnSpan(kinds, 4);
		AddPair(fields, "&From", _shapeFrom, "&To", _shapeTo);
		int countRow = fields.RowCount++;
		fields.Controls.Add(Caption("&Count"), 0, countRow);
		fields.Controls.Add(_shapeCount, 1, countRow);

		foreach (Control control in new Control[] { _shape, _shapeStrength, _peak, _betaA, _betaB, _shapePlaces, _shapeFrom, _shapeTo })
		{
			switch (control)
			{
				case ComboBox box:
					box.SelectedIndexChanged += (_, _) => ShapeChanged();
					break;
				case NumericUpDown number:
					number.ValueChanged += (_, _) => ShapeChanged();
					break;
			}
		}
		_shapeWhole.CheckedChanged += (_, _) => ShapeChanged();

		var draw = new Button { Text = "&Generate", AutoSize = true, MinimumSize = new Size(90, 0) };
		draw.Click += async (_, _) => await DrawShaped();
		var sort = new Button { Text = "S&ort", AutoSize = true };
		sort.Click += (_, _) => SortNumbers(_shapeList);
		var copy = new Button { Text = "Cop&y", AutoSize = true };
		copy.Click += (_, _) => CopyLines(_shapeList, all: false, secret: false);
		var copyAll = new Button { Text = "Copy &All", AutoSize = true };
		copyAll.Click += (_, _) => CopyLines(_shapeList, all: true, secret: false);
		var insert = new Button { Text = "&Insert into Input", AutoSize = true };
		insert.Click += (_, _) => InsertSelected(_shapeList);
		_shapeList.DoubleClick += (_, _) => InsertSelected(_shapeList);
		var command = new Button { Text = "Insert Co&mmand", AutoSize = true };
		new ToolTip().SetToolTip(command, "Put the engine call for this shape in Input, to draw there at full precision");
		command.Click += (_, _) => _insert(ShapeCommand());
		var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 6, 0, 0) };
		buttons.Controls.AddRange(new Control[] { draw, sort, copy, copyAll, insert, command });

		// results on the left, the picture on the right
		var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
		bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
		bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
		bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
		bottom.Controls.Add(_shapeList, 0, 0);
		bottom.Controls.Add(_plot, 1, 0);

		page.Controls.Add(bottom);
		page.Controls.Add(_shapeNote);
		page.Controls.Add(buttons);
		page.Controls.Add(fields);
		return page;
	}

	private string ShapeId => RandomShapes.Shapes[Math.Max(0, _shape.SelectedIndex)].Id;

	private bool ShapeWhole => _shapeWhole.Checked;

	private ShapeCurve CurrentCurve()
	{
		return RandomShapes.Curve(ShapeId, _shapeStrength.Value, _betaA.Value, _betaB.Value);
	}

	// A setting changed: show its fields, redraw the shape, and drop the
	// bars, which belong to the settings they were drawn with
	private void ShapeChanged()
	{
		if (_shapeLoading)
		{
			return;
		}
		string id = ShapeId;
		SetShown(_shapeStrength, RandomShapes.UsesStrength(id));
		SetShown(_betaA, id == "custom");
		SetShown(_betaB, id == "custom");
		SetShown(_peak, id == "triangle");
		SetShown(_shapePlaces, !ShapeWhole);
		int places = ShapeWhole ? 0 : (int)_shapePlaces.Value;    // whole numbers: the range takes no fraction
		_shapeFrom.DecimalPlaces = places;
		_shapeTo.DecimalPlaces = places;
		_peak.DecimalPlaces = places;
		_shapeNote.ForeColor = SystemColors.GrayText;
		_shapeNote.Text = RandomShapes.Hint(id);
		ShowShape(Array.Empty<double>());
	}

	private void ShowShape(double[] bars)
	{
		double peakAt = Math.Clamp(RandomShapes.PeakAt(_shapeFrom.Value, _shapeTo.Value, _peak.Value, ShapeWhole), 0, 1);
		decimal low = Math.Min(_shapeFrom.Value, _shapeTo.Value), high = Math.Max(_shapeFrom.Value, _shapeTo.Value);
		int places = ShapeWhole ? 0 : (int)_shapePlaces.Value;
		_plot.Show(RandomShapes.Line(CurrentCurve(), peakAt), bars, RandomTools.Text(decimal.Round(low, places)),
			RandomTools.Text(decimal.Round(high, places)));
	}

	private async Task DrawShaped()
	{
		if (_drawing)
		{
			return;
		}
		_shapeList.Items.Clear();
		// System draws here; Seed checks the settings here too, then the window's engine draws
		var (values, error) = RandomShapes.Values(_secure, CurrentCurve(), _shapeFrom.Value, _shapeTo.Value, _peak.Value,
			ShapeWhole, (int)_shapePlaces.Value, SeedInUse == null ? (int)_shapeCount.Value : 1);
		if (values == null)
		{
			_shapeNote.ForeColor = Theme.Error;
			_shapeNote.Text = error;
			ShowShape(Array.Empty<double>());
			return;
		}
		List<string> texts;
		string summarySuffix = "";
		if (SeedInUse is { } seed)
		{
			var drawn = await SeedDrawsAsync(seed, ShapeCommand(), (int)_shapeCount.Value, false, _shapeNote);
			if (drawn == null)
			{
				ShowShape(Array.Empty<double>());
				return;
			}
			texts = drawn;
			values = drawn.Select(t => decimal.TryParse(t, System.Globalization.NumberStyles.Float,
				System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0).ToList();
			summarySuffix = $" (seed {seed})";
		}
		else
		{
			texts = values.Select(RandomTools.Text).ToList();
		}
		_shapeList.BeginUpdate();
		_shapeList.Items.AddRange(texts.Cast<object>().ToArray());
		_shapeList.EndUpdate();
		_shapeList.SelectedIndex = 0;
		_shapeNote.ForeColor = SystemColors.GrayText;
		_shapeNote.Text = RandomShapes.Summary(values, ShapeWhole ? 0 : (int)_shapePlaces.Value) + summarySuffix;
		ShowShape(RandomShapes.Bars(values, _shapeFrom.Value, _shapeTo.Value, ShapeWhole));
	}

	private string ShapeCommand()
	{
		return RandomShapes.Command(CurrentCurve(), _shapeFrom.Value, _shapeTo.Value, _peak.Value, ShapeWhole,
			(int)_shapePlaces.Value);
	}

	private void LoadShapePrefs(RandomPrefs p)
	{
		_shapeLoading = true;
		_shape.SelectedIndex = Math.Max(0, Array.FindIndex(RandomShapes.Shapes, s => s.Id == p.Shape));
		(_shapeWhole.Checked, _shapeDecimal.Checked) = (p.ShapeWhole, !p.ShapeWhole);
		_shapePlaces.Value = p.ShapePlaces;
		foreach (var box in new[] { _shapeFrom, _shapeTo, _peak })
		{
			box.DecimalPlaces = p.ShapeWhole ? 0 : p.ShapePlaces;    // before the values, which it would round
		}
		(_shapeStrength.Value, _betaA.Value, _betaB.Value) = (p.ShapeStrength, p.ShapeA, p.ShapeB);
		(_shapeFrom.Value, _shapeTo.Value, _peak.Value, _shapeCount.Value) = (p.ShapeFrom, p.ShapeTo, p.ShapePeak, p.ShapeCount);
		_shapeLoading = false;
		ShapeChanged();
	}

	private void SaveShapePrefs(RandomPrefs p)
	{
		p.Shape = ShapeId;
		(p.ShapeStrength, p.ShapeA, p.ShapeB) = (_shapeStrength.Value, _betaA.Value, _betaB.Value);
		(p.ShapeWhole, p.ShapePlaces) = (ShapeWhole, (int)_shapePlaces.Value);
		(p.ShapeFrom, p.ShapeTo, p.ShapePeak, p.ShapeCount) = (_shapeFrom.Value, _shapeTo.Value, _peak.Value, (int)_shapeCount.Value);
	}
}
