// Keypad.cs - Numeric Calc keypad: Basic and Scientific, drawn keys that fill the space (owner 2026-11-16)
//
// Basic is a 5 x 5 grid of big keys (AC, C, %, memory, digits; 0 two
// columns wide, = two rows tall). Scientific puts five columns of
// functions, modelled on the owner's TI-89, to the left of the same Basic
// keys. A key with a second meaning shows it small above its label;
// 2nd applies to the next key only. "Sci ▸" / "◂ Basic", a flat text at
// the top left, switches the mode. The keys never take the focus, so
// typing stays in Input. Key ids are handled by MainForm.

using System.Drawing.Drawing2D;

namespace SoftCalc.UI;

internal sealed class Keypad : Control
{
	// Id, label, second id, second label, row, column, rows, columns
	private sealed record Key(string Id, string Label, string? SecondId, string? SecondLabel, int Row, int Col, int Rows = 1, int Cols = 1)
	{
		public bool IsDigit => Id.Length == 1 && (char.IsDigit(Id[0]) || Id == ".");
	}

	private static readonly Key[] BasicKeys =
	{
		new("AC", "AC", null, null, 0, 0), new("C", "C", null, null, 0, 1), new("%", "%", null, null, 0, 2),
		new("/", "÷", null, null, 0, 3), new("*", "×", null, null, 0, 4),
		new("MR", "MR", null, null, 1, 0), new("7", "7", null, null, 1, 1), new("8", "8", null, null, 1, 2), new("9", "9", null, null, 1, 3),
		new("-", "−", null, null, 1, 4),
		new("M+", "M+", null, null, 2, 0), new("4", "4", null, null, 2, 1), new("5", "5", null, null, 2, 2), new("6", "6", null, null, 2, 3),
		new("+", "+", null, null, 2, 4),
		new("M-", "M−", null, null, 3, 0), new("1", "1", null, null, 3, 1), new("2", "2", null, null, 3, 2), new("3", "3", null, null, 3, 3),
		new("=", "=", null, null, 3, 4, Rows: 2),
		new("MC", "MC", null, null, 4, 0), new("0", "0", null, null, 4, 1, Cols: 2), new(".", ".", null, null, 4, 3),
	};

	private static readonly Key[] ScientificKeys =
	{
		new("fn:sin", "sin", "fn:asin", "asin", 0, 0), new("fn:cos", "cos", "fn:acos", "acos", 0, 1),
		new("fn:tan", "tan", "fn:atan", "atan", 0, 2), new("fn:ln", "ln", "fn:exp", "eˣ", 0, 3),
		new("fn:log10", "log", "ins:10^", "10ˣ", 0, 4),
		new("ins:^2", "x²", "ins:^3", "x³", 1, 0), new("ins:^", "xʸ", "ins:^(1/", "ʸ√x", 1, 1),
		new("fn:sqrt", "√", "ins:^(1/3)", "∛", 1, 2), new("ins:^(-1)", "1/x", "fn:abs", "|x|", 1, 3),
		new("ins:!", "n!", "fn:rand", "rand", 1, 4),
		new("ins:pi", "π", "fn:sinh", "sinh", 2, 0), new("ins:e", "e", "fn:floor", "floor", 2, 1),
		new("ins:E", "EE", "fn:ceil", "ceil", 2, 2), new("ins:-", "(−)", "ins: mod ", "mod", 2, 3),
		new("ins:,", ",", "fn:round", "round", 2, 4),
		new("x", "x", "ins:=", "=", 3, 0), new("y", "y", "ins:f(", "f(", 3, 1), new("ins:(", "(", "ins:[", "[", 3, 2),
		new("ins:)", ")", "ins:]", "]", 3, 3), new("ins:ans", "ans", "ins:->", "->", 3, 4),
		new("base", "dec ▸", null, null, 4, 0), new("angle", "deg ▸", null, null, 4, 1), new("func", "func", null, null, 4, 2),
		new("units", "units", null, null, 4, 3), new("2nd", "2nd", null, null, 4, 4),
	};

	private const int Rows = 5;
	private const float Gap = 0.35f;    // between the Scientific and Basic blocks, in key widths
	private readonly List<(Key Key, RectangleF Box)> _boxes = new();
	private RectangleF _toggle;
	private Key? _hot, _down;
	private bool _toggleHot;
	private bool _second;
	private string _base = "dec", _angle = "deg";
	private Font _small;

	// A key's id (2nd already applied) and where it is on the screen (to open a menu under it)
	public event Action<string, Rectangle>? KeyPressed;
	public event EventHandler? ModeChanged;

	public bool Scientific { get; private set; }

	public Keypad()
	{
		SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
			| ControlStyles.ResizeRedraw, true);
		SetStyle(ControlStyles.Selectable, false);    // focus stays in Input
		TabStop = false;
		_small = new Font(Font.FontFamily, Font.Size * 0.62f);
	}

	public void SetScientific(bool on)
	{
		Scientific = on;
		_second = false;
		Arrange();
		Invalidate();
	}

	// The output base and angle unit, shown on their keys
	public void SetModes(string? numberBase, string? angle)
	{
		_base = numberBase ?? "dec";
		_angle = angle ?? "deg";
		Invalidate();
	}

	protected override void OnFontChanged(EventArgs e)
	{
		_small.Dispose();
		_small = new Font(Font.FontFamily, Math.Max(7f, Font.Size * 0.62f));
		base.OnFontChanged(e);
		Arrange();
		Invalidate();
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		Arrange();
	}

	//==================================================================
	//  Layout: the toggle text on top, then the grid
	//==================================================================
	private void Arrange()
	{
		_boxes.Clear();
		int pad = LogicalToDeviceUnits(4);
		var size = TextRenderer.MeasureText(Scientific ? "◂ Basic" : "Sci ▸", _small);
		_toggle = new RectangleF(pad, 0, size.Width + pad, size.Height + LogicalToDeviceUnits(2));
		var area = RectangleF.FromLTRB(pad, _toggle.Bottom, Width - pad, Height - pad);
		if (area.Width <= 0 || area.Height <= 0)
		{
			return;
		}
		float columns = Scientific ? 10 + Gap : 5;
		float cellW = area.Width / columns, cellH = area.Height / Rows;
		float margin = LogicalToDeviceUnits(3);
		void Place(IEnumerable<Key> keys, float left)
		{
			foreach (var key in keys)
			{
				var box = new RectangleF(left + key.Col * cellW + margin / 2, area.Top + key.Row * cellH + margin / 2,
					key.Cols * cellW - margin, key.Rows * cellH - margin);
				_boxes.Add((key, box));
			}
		}
		if (Scientific)
		{
			Place(ScientificKeys, area.Left);
			Place(BasicKeys, area.Left + (5 + Gap) * cellW);
		}
		else
		{
			Place(BasicKeys, area.Left);
		}
	}

	//==================================================================
	//  Drawing
	//==================================================================
	protected override void OnPaint(PaintEventArgs e)
	{
		var g = e.Graphics;
		g.Clear(BackColor);
		g.SmoothingMode = SmoothingMode.AntiAlias;
		TextRenderer.DrawText(g, Scientific ? "◂ Basic" : "Sci ▸", _small, Rectangle.Round(_toggle),
			_toggleHot ? SystemColors.HotTrack : LinkColour, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
		float radius = LogicalToDeviceUnits(5);
		foreach (var (key, box) in _boxes)
		{
			bool lit = key.Id == "2nd" && _second;
			Color face = lit ? SystemColors.Highlight
				: key.Id == "=" ? Accent
				: key.IsDigit ? SystemColors.Window
				: SystemColors.ControlLight;
			if (ReferenceEquals(key, _down))
			{
				face = ControlPaint.Dark(face, 0.1f);
			}
			else if (ReferenceEquals(key, _hot))
			{
				face = Theme.IsDark ? ControlPaint.Light(face, 0.25f) : ControlPaint.Dark(face, 0.03f);
			}
			using (var path = Rounded(box, radius))
			{
				using var brush = new SolidBrush(face);
				g.FillPath(brush, path);
				using var edge = new Pen(SystemColors.ControlDark, 1f);
				g.DrawPath(edge, path);
			}
			Color ink = lit || key.Id == "=" ? SystemColors.HighlightText : key.Id == "2nd" ? SecondInk : SystemColors.ControlText;
			var labelBox = Rectangle.Round(box);
			if (key.SecondLabel != null)
			{
				int top = (int)(box.Height * 0.36f);
				var secondBox = new Rectangle(labelBox.X, labelBox.Y + 1, labelBox.Width, top);
				TextRenderer.DrawText(g, key.SecondLabel, _small, secondBox, _second ? SecondInk : SecondFaint,
					TextFormatFlags.HorizontalCenter | TextFormatFlags.Bottom | TextFormatFlags.NoPrefix);
				labelBox = Rectangle.FromLTRB(labelBox.Left, labelBox.Top + top, labelBox.Right, labelBox.Bottom);
			}
			string label = Label(key);
			const TextFormatFlags centre = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
			int room = labelBox.Width - LogicalToDeviceUnits(6);
			int wide = TextRenderer.MeasureText(g, label, Font, Size.Empty, TextFormatFlags.NoPadding).Width;
			if (wide > room && room > 0)
			{
				// a long label in a narrow key: smaller, not cut
				using var fit = new Font(Font.FontFamily, Math.Max(6f, Font.Size * room / wide));
				TextRenderer.DrawText(g, label, fit, labelBox, ink, centre);
			}
			else
			{
				TextRenderer.DrawText(g, label, Font, labelBox, ink, centre);
			}
		}
	}

	private string Label(Key key)
	{
		return key.Id switch
		{
			"base" => _base + " ▸",
			"angle" => _angle + " ▸",
			_ => key.Label,
		};
	}

	private static Color Accent => SystemColors.Highlight;
	private static Color LinkColour => Theme.IsDark ? Color.FromArgb(120, 180, 255) : Color.FromArgb(0, 90, 190);
	// 2nd in yellow (owner 2026-11-16); a darker yellow on light keys to stay readable
	private static Color SecondInk => Theme.IsDark ? Color.FromArgb(255, 214, 0) : Color.FromArgb(176, 136, 0);
	private static Color SecondFaint => Theme.IsDark ? Color.FromArgb(222, 190, 40) : Color.FromArgb(160, 128, 20);

	private static GraphicsPath Rounded(RectangleF box, float radius)
	{
		var path = new GraphicsPath();
		float d = Math.Min(radius * 2, Math.Min(box.Width, box.Height));
		if (d <= 0)
		{
			path.AddRectangle(box);
			return path;
		}
		path.AddArc(box.Left, box.Top, d, d, 180, 90);
		path.AddArc(box.Right - d, box.Top, d, d, 270, 90);
		path.AddArc(box.Right - d, box.Bottom - d, d, d, 0, 90);
		path.AddArc(box.Left, box.Bottom - d, d, d, 90, 90);
		path.CloseFigure();
		return path;
	}

	//==================================================================
	//  Mouse
	//==================================================================
	private Key? At(Point p)
	{
		foreach (var (key, box) in _boxes)
		{
			if (box.Contains(p))
			{
				return key;
			}
		}
		return null;
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		var hot = At(e.Location);
		bool toggleHot = _toggle.Contains(e.Location);
		Cursor = toggleHot ? Cursors.Hand : Cursors.Default;
		if (!ReferenceEquals(hot, _hot) || toggleHot != _toggleHot)
		{
			(_hot, _toggleHot) = (hot, toggleHot);
			Invalidate();
		}
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		base.OnMouseLeave(e);
		(_hot, _down, _toggleHot) = (null, null, false);
		Invalidate();
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		if (e.Button == MouseButtons.Left)
		{
			_down = At(e.Location);
			Invalidate();
		}
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		base.OnMouseUp(e);
		if (e.Button != MouseButtons.Left)
		{
			return;
		}
		var down = _down;
		_down = null;
		Invalidate();
		if (_toggle.Contains(e.Location))
		{
			SetScientific(!Scientific);
			ModeChanged?.Invoke(this, EventArgs.Empty);
			return;
		}
		if (down == null || !ReferenceEquals(At(e.Location), down))
		{
			return;
		}
		if (down.Id == "2nd")
		{
			_second = !_second;
			Invalidate();
			return;
		}
		string id = _second && down.SecondId != null ? down.SecondId : down.Id;
		_second = false;
		var box = _boxes.First(b => ReferenceEquals(b.Key, down)).Box;
		KeyPressed?.Invoke(id, RectangleToScreen(Rectangle.Round(box)));
		Invalidate();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_small.Dispose();
		}
		base.Dispose(disposing);
	}
}
