// GraphGallery.cs - Graph > Add > Examples: classic curves with a small graph each (owner 2026-11-10)
//
// Functions and equations from Logic/GraphExamples.cs, one kind at a
// time (toolbar buttons; ListView group headers are unreadable in dark
// mode, so the tiles are drawn here too). The
// thumbnails are drawn by the engine after the window opens (an equation
// grid of 40 x 30 cells; a function is drawn as the equation y = body,
// which drops the jumps at poles), always in radians: in Deg a sine over
// -7..7 is a flat line, so the top line then says the pictures use
// radians. Use (or a double-click) returns the
// example; the caller opens it in the Functions editor or Add Equation
// so it can be changed before it is added. Under the tiles: the form with
// its constants and what they change.

using System.Drawing.Drawing2D;
using SoftCalc.Engine;
using SoftCalc.Logic;

namespace SoftCalc.UI;

internal sealed class GraphGallery : Form
{
	private static readonly Size Thumb = new(120, 90);

	private readonly EngineSession _session;
	private readonly List<GraphExample> _examples = new();
	private readonly ImageList _images = new() { ColorDepth = ColorDepth.Depth32Bit };
	private readonly ListView _list = new()
	{
		Dock = DockStyle.Fill, View = View.LargeIcon, MultiSelect = false, HideSelection = false,
		ShowItemToolTips = true, BorderStyle = BorderStyle.None, OwnerDraw = true,
	};
	private readonly ToolStripButton _functionsButton = new("Functions y = f(x)") { Checked = true };
	private readonly ToolStripButton _equationsButton = new("Equations in x and y");
	private readonly Label _formula = new()
	{
		Dock = DockStyle.Bottom, AutoSize = false, Height = 28, TextAlign = ContentAlignment.MiddleLeft,
		Font = new Font("Consolas", 10f), Padding = new Padding(8, 0, 0, 0), UseMnemonic = false,
	};
	private readonly Label _note = new()
	{
		Dock = DockStyle.Bottom, AutoSize = false, Height = 24, TextAlign = ContentAlignment.TopLeft,
		ForeColor = SystemColors.GrayText, Padding = new Padding(8, 0, 0, 0), UseMnemonic = false,
	};
	private readonly Button _use = new() { Text = "&Use...", AutoSize = true, MinimumSize = new Size(80, 0), Enabled = false };

	public GraphExample? Picked { get; private set; }

	public GraphGallery(EngineSession session)
	{
		_session = session;
		Text = "Graph Examples";
		Icon = SvgIcon.ProgramIcon();
		ShowInTaskbar = false;
		MinimizeBox = false;
		StartPosition = FormStartPosition.CenterParent;
		ClientSize = new Size(760, 560);
		MinimumSize = new Size(420, 320);

		_images.ImageSize = LogicalToDeviceUnits(Thumb);
		_list.LargeImageList = _images;
		foreach (var example in GraphExamples.Functions.Concat(GraphExamples.Equations))
		{
			_images.Images.Add(Blank());
			_examples.Add(example);
		}
		_list.DrawItem += DrawTile;
		var kinds = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
		kinds.Items.AddRange(new ToolStripItem[] { _functionsButton, _equationsButton });
		_functionsButton.Click += (_, _) => ShowKind(equations: false);
		_equationsButton.Click += (_, _) => ShowKind(equations: true);
		ShowKind(equations: false);

		var cancel = new Button { Text = "Cancel", AutoSize = true, MinimumSize = new Size(80, 0), DialogResult = DialogResult.Cancel };
		var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(4, 0, 8, 8) };
		buttons.Controls.AddRange(new Control[] { cancel, _use });
		string angle = (string?)session.Settings["angle"] ?? "rad";
		var hint = new Label
		{
			Text = "Pick a curve: it opens in the editor with its constants as numbers; change them, then OK adds it."
				+ (angle == "rad" ? "" : $"\nPictures use radians; SoftCalc is in {AngleName(angle)}, so sin, cos and tan draw differently."),
			Dock = DockStyle.Top, AutoSize = false, Height = angle == "rad" ? 26 : 42, TextAlign = ContentAlignment.MiddleLeft,
			ForeColor = SystemColors.GrayText, Padding = new Padding(8, 0, 0, 0),
		};
		Controls.Add(_list);
		Controls.Add(_formula);
		Controls.Add(_note);
		Controls.Add(hint);
		Controls.Add(kinds);
		Controls.Add(buttons);
		AcceptButton = _use;
		CancelButton = cancel;

		_list.SelectedIndexChanged += (_, _) =>
		{
			var example = Selected();
			_formula.Text = example?.FormLine ?? "";
			_note.Text = example?.Note ?? "";
			_use.Enabled = example != null;
		};
		_list.ItemActivate += (_, _) => Use();
		_use.Click += (_, _) => Use();
		Shown += async (_, _) => await DrawThumbnailsAsync();
	}

	public static string AngleName(string angle)
	{
		return angle == "deg" ? "Degrees" : angle == "grad" ? "Grads" : "Radians";
	}

	private GraphExample? Selected()
	{
		return _list.SelectedItems.Count == 1 ? _examples[_list.SelectedItems[0].ImageIndex] : null;
	}

	// One kind in the list; an item's ImageIndex is its place in _examples
	private void ShowKind(bool equations)
	{
		_functionsButton.Checked = !equations;
		_equationsButton.Checked = equations;
		_list.BeginUpdate();
		_list.Items.Clear();
		for (int i = 0; i < _examples.Count; i++)
		{
			if (_examples[i].IsEquation == equations)
			{
				_list.Items.Add(new ListViewItem(_examples[i].Title, i) { ToolTipText = _examples[i].FormLine + "\n" + _examples[i].Note });
			}
		}
		_list.EndUpdate();
		_formula.Text = "";
		_note.Text = "";
		_use.Enabled = false;
	}

	// Thumbnail with a highlight frame when selected, title below
	private void DrawTile(object? sender, DrawListViewItemEventArgs e)
	{
		var bounds = e.Bounds;
		var image = _images.Images[e.Item.ImageIndex];
		int x = bounds.X + (bounds.Width - image.Width) / 2;
		int y = bounds.Y + LogicalToDeviceUnits(2);
		e.Graphics.DrawImage(image, x, y, image.Width, image.Height);
		var textArea = new Rectangle(bounds.X, y + image.Height + LogicalToDeviceUnits(2), bounds.Width, bounds.Bottom - y - image.Height - 2);
		var textColour = SystemColors.WindowText;
		if (e.Item.Selected)
		{
			int width = LogicalToDeviceUnits(3);
			using var frame = new Pen(SystemColors.Highlight, width);
			e.Graphics.DrawRectangle(frame, x - width / 2f - 1, y - width / 2f - 1, image.Width + width + 1, image.Height + width + 1);
			var size = TextRenderer.MeasureText(e.Item.Text, _list.Font);
			var back = new Rectangle(bounds.X + (bounds.Width - size.Width) / 2 - 2, textArea.Y, size.Width + 4, size.Height);
			using var fill = new SolidBrush(SystemColors.Highlight);
			e.Graphics.FillRectangle(fill, back);
			textColour = SystemColors.HighlightText;
		}
		TextRenderer.DrawText(e.Graphics, e.Item.Text, _list.Font, textArea, textColour,
			TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
	}

	private void Use()
	{
		if (Selected() is { } example)
		{
			Picked = example;
			DialogResult = DialogResult.OK;
			Close();
		}
	}

	//==================================================================
	//  Thumbnails
	//==================================================================
	private Bitmap Blank()
	{
		var size = _images.ImageSize;
		var picture = new Bitmap(size.Width, size.Height);
		using var g = Graphics.FromImage(picture);
		g.Clear(SystemColors.Window);
		using var border = new Pen(Theme.PlotGrid);
		g.DrawRectangle(border, 0, 0, size.Width - 1, size.Height - 1);
		return picture;
	}

	private async Task DrawThumbnailsAsync()
	{
		for (int i = 0; i < _examples.Count && !IsDisposed; i++)
		{
			var example = _examples[i];
			IReadOnlyList<(double X1, double Y1, double X2, double Y2)> segments;
			try
			{
				segments = await _session.PlotEquationAsync(example.IsEquation ? example.Text : "y=" + example.Text, example.View, 40, 30, "rad");
			}
			catch (EngineException)
			{
				continue;    // the blank tile stays; the editor will show the problem
			}
			if (IsDisposed)
			{
				return;
			}
			_images.Images[i] = Draw(example.View, segments);
			if (_list.Items.Cast<ListViewItem>().FirstOrDefault(item => item.ImageIndex == i) is { } shown)
			{
				_list.RedrawItems(shown.Index, shown.Index, false);
			}
		}
	}

	private Bitmap Draw(WorldRect view, IReadOnlyList<(double X1, double Y1, double X2, double Y2)> segments)
	{
		var picture = Blank();
		var size = _images.ImageSize;
		float Px(double x) => (float)((x - view.MinX) / (view.MaxX - view.MinX) * (size.Width - 1));
		float Py(double y) => (float)((view.MaxY - y) / (view.MaxY - view.MinY) * (size.Height - 1));
		using var g = Graphics.FromImage(picture);
		g.SmoothingMode = SmoothingMode.AntiAlias;
		using var axis = new Pen(Theme.PlotAxis);
		if (view.MinX < 0 && view.MaxX > 0)
		{
			g.DrawLine(axis, Px(0), 0, Px(0), size.Height);
		}
		if (view.MinY < 0 && view.MaxY > 0)
		{
			g.DrawLine(axis, 0, Py(0), size.Width, Py(0));
		}
		using var line = new Pen(Theme.Series(0), LogicalToDeviceUnits(2) * 0.75f);
		foreach (var (x1, y1, x2, y2) in segments)
		{
			g.DrawLine(line, Px(x1), Py(y1), Px(x2), Py(y2));
		}
		return picture;
	}
}
