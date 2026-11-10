// SvgIcon.cs - Toolbar icons: draws the SVG that icon sets such as IconMCP use, with GDI+
//
// Icons are one colour: any paint other than "none" draws in the given
// colour. Supported: svg, g, path (all commands, absolute and relative),
// line, circle, ellipse, rect (rx, ry), polyline, polygon; fill, stroke,
// stroke-width, stroke-linecap, stroke-linejoin, fill-rule and transform,
// inherited from svg and g, also from a style attribute. title, desc,
// metadata and defs are skipped. Anything else is refused ("can't draw:
// text"), so an own icon (Customize Ribbon) never draws half right.
// Strokes get a minimum width so thin 16-unit lines stay visible at 20 px.

using System.Drawing.Drawing2D;
using System.Globalization;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;

namespace SoftCalc.UI;

internal sealed class SvgException(string message) : Exception(message);

internal static class SvgIcon
{
	public const int MaxLength = SoftCalc.Logic.RibbonLayout.MaxIconSvg;    // an own icon's SVG text, in characters
	private const float MinStroke = 1.4f;
	private const string ResourcePrefix = "icons/";
	private static readonly Dictionary<(string, int, int), Bitmap> Cache = new();
	private static readonly HashSet<string> Skipped = new() { "title", "desc", "metadata", "defs" };

	//==================================================================
	//  Embedded and own icons
	//==================================================================
	// An embedded icon ("trash" -> icons/trash.svg) at `size` pixels, cached
	public static Bitmap Get(string name, int size, Color color)
	{
		var key = ("name:" + name, size, color.ToArgb());
		if (!Cache.TryGetValue(key, out var bitmap))
		{
			bitmap = Render(Parse(EmbeddedText(name)), size, color);
			Cache[key] = bitmap;
		}
		return bitmap;
	}

	// An own icon from its SVG text, cached; throws SvgException when it cannot be drawn
	public static Bitmap FromText(string svg, int size, Color color)
	{
		var key = ("svg:" + svg, size, color.ToArgb());
		if (!Cache.TryGetValue(key, out var bitmap))
		{
			bitmap = Render(Parse(svg), size, color);
			Cache[key] = bitmap;
		}
		return bitmap;
	}

	// Null when the text draws, else why not
	public static string? Problem(string svg)
	{
		try
		{
			using var bitmap = Render(Parse(svg), 20, Color.Black);
			return null;
		}
		catch (SvgException e)
		{
			return e.Message;
		}
	}

	// The names of the embedded icons, sorted
	public static IReadOnlyList<string> BuiltIn()
	{
		return Assembly.GetExecutingAssembly().GetManifestResourceNames()
			.Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && n.EndsWith(".svg", StringComparison.Ordinal))
			.Select(n => n[ResourcePrefix.Length..^4]).Order(StringComparer.Ordinal).ToList();
	}

	public static string EmbeddedText(string name)
	{
		using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"{ResourcePrefix}{name}.svg")
			?? throw new InvalidOperationException($"icon '{name}' is not embedded");
		using var reader = new StreamReader(stream);
		return reader.ReadToEnd();
	}

	// Outside text: no DTD, no resolver, a size limit, and an <svg> root
	public static XDocument Parse(string text)
	{
		if (text.Length > MaxLength)
		{
			throw new SvgException($"too big (over {MaxLength / 1024} KB)");
		}
		var settings = new XmlReaderSettings
		{
			DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxLength,
			IgnoreComments = true, IgnoreProcessingInstructions = true,
		};
		XDocument doc;
		try
		{
			using var reader = XmlReader.Create(new StringReader(text.Trim()), settings);
			doc = XDocument.Load(reader);
		}
		catch (XmlException e)
		{
			throw new SvgException("not SVG: " + e.Message);
		}
		if (doc.Root?.Name.LocalName != "svg")
		{
			throw new SvgException("not SVG: no <svg> element");
		}
		return doc;
	}

	//==================================================================
	//  Drawing
	//==================================================================
	public static Bitmap Render(XDocument doc, int size, Color color)
	{
		var root = doc.Root ?? throw new SvgException("empty SVG");
		var (minX, minY, width, height) = ViewBox(root);
		if (width <= 0 || height <= 0)
		{
			throw new SvgException("bad viewBox");
		}
		float scale = size / Math.Max(width, height);
		var place = new Matrix();
		place.Translate((size - width * scale) / 2, (size - height * scale) / 2);
		place.Scale(scale, scale);
		place.Translate(-minX, -minY);
		var bitmap = new Bitmap(size, size);
		try
		{
			using var g = Graphics.FromImage(bitmap);
			g.SmoothingMode = SmoothingMode.AntiAlias;
			g.PixelOffsetMode = PixelOffsetMode.HighQuality;
			Draw(g, root, Style.Initial, place, color);
		}
		catch
		{
			bitmap.Dispose();
			throw;
		}
		return bitmap;
	}

	private static (float, float, float, float) ViewBox(XElement root)
	{
		var box = Numbers((string?)root.Attribute("viewBox") ?? "");
		if (box.Count == 4)
		{
			return (box[0], box[1], box[2], box[3]);
		}
		float w = Length((string?)root.Attribute("width")) ?? 16;
		float h = Length((string?)root.Attribute("height")) ?? w;
		return (0, 0, w, h);
	}

	private static float? Length(string? text)
	{
		if (text == null)
		{
			return null;
		}
		var numbers = Numbers(text.Replace("px", ""));
		return numbers.Count == 1 ? numbers[0] : null;
	}

	private static void Draw(Graphics g, XElement element, Style parent, Matrix matrix, Color color)
	{
		var style = parent.With(element);
		using var here = matrix.Clone();
		if ((string?)element.Attribute("transform") is { } transform)
		{
			here.Multiply(Transform(transform));
		}
		string name = element.Name.LocalName;
		if (element.Attribute("mask") != null)
		{
			throw new SvgException("can't draw: mask");
		}
		if (name is "svg" or "g")
		{
			foreach (var child in element.Elements())
			{
				if (!Skipped.Contains(child.Name.LocalName))
				{
					Draw(g, child, style, here, color);
				}
			}
			return;
		}
		using var path = Shape(element) ?? throw new SvgException("can't draw: " + name);
		path.FillMode = style.EvenOdd ? FillMode.Alternate : FillMode.Winding;
		path.Transform(here);
		if (style.Fill && name != "line")    // a line has no inside
		{
			using var brush = new SolidBrush(color);
			g.FillPath(brush, path);
		}
		if (style.Stroke)
		{
			float unit = (float)Math.Sqrt(Math.Abs(here.Elements[0] * here.Elements[3] - here.Elements[1] * here.Elements[2]));
			using var pen = new Pen(color, Math.Max(MinStroke, style.Width * unit))
			{
				StartCap = style.Cap, EndCap = style.Cap, LineJoin = style.Join,
			};
			g.DrawPath(pen, path);
		}
	}

	//==================================================================
	//  Inherited presentation attributes
	//==================================================================
	private sealed record Style(bool Fill, bool Stroke, float Width, LineCap Cap, LineJoin Join, bool EvenOdd)
	{
		// SVG's defaults: filled black, no stroke
		public static readonly Style Initial = new(true, false, 1, LineCap.Flat, LineJoin.Miter, false);

		public Style With(XElement e)
		{
			var values = new Dictionary<string, string>();
			foreach (var name in new[] { "fill", "stroke", "stroke-width", "stroke-linecap", "stroke-linejoin", "fill-rule" })
			{
				if ((string?)e.Attribute(name) is { } value)
				{
					values[name] = value.Trim();
				}
			}
			foreach (var part in ((string?)e.Attribute("style") ?? "").Split(';'))
			{
				int colon = part.IndexOf(':');
				if (colon > 0)
				{
					values[part[..colon].Trim()] = part[(colon + 1)..].Trim();
				}
			}
			var style = this;
			foreach (var (name, value) in values)
			{
				if (value == "inherit")
				{
					continue;
				}
				style = name switch
				{
					"fill" => style with { Fill = value != "none" && value != "transparent" },
					"stroke" => style with { Stroke = value != "none" && value != "transparent" },
					"stroke-width" => style with { Width = Length(value) ?? style.Width },
					"stroke-linecap" => style with { Cap = value switch { "round" => LineCap.Round, "square" => LineCap.Square, _ => LineCap.Flat } },
					"stroke-linejoin" => style with { Join = value switch { "round" => LineJoin.Round, "bevel" => LineJoin.Bevel, _ => LineJoin.Miter } },
					"fill-rule" => style with { EvenOdd = value == "evenodd" },
					_ => style,
				};
			}
			return style;
		}
	}

	private static Matrix Transform(string text)
	{
		var result = new Matrix();
		int i = 0;
		while (i < text.Length)
		{
			int open = text.IndexOf('(', i);
			if (open < 0)
			{
				if (text[i..].Trim(' ', ',', '\t', '\r', '\n').Length > 0)
				{
					throw new SvgException("can't draw: transform " + text.Trim());
				}
				break;
			}
			int close = text.IndexOf(')', open);
			if (close < 0)
			{
				throw new SvgException("can't draw: transform " + text.Trim());
			}
			string kind = text[i..open].Trim(' ', ',', '\t', '\r', '\n');
			var n = Numbers(text[(open + 1)..close]);
			float At(int k, float fallback) => k < n.Count ? n[k] : fallback;
			using var step = kind switch
			{
				"matrix" when n.Count == 6 => new Matrix(n[0], n[1], n[2], n[3], n[4], n[5]),
				"translate" when n.Count is 1 or 2 => new Matrix(1, 0, 0, 1, n[0], At(1, 0)),
				"scale" when n.Count is 1 or 2 => new Matrix(n[0], 0, 0, At(1, n[0]), 0, 0),
				"rotate" when n.Count is 1 or 3 => Rotation(n[0], At(1, 0), At(2, 0)),
				"skewX" when n.Count == 1 => new Matrix(1, 0, (float)Math.Tan(n[0] * Math.PI / 180), 1, 0, 0),
				"skewY" when n.Count == 1 => new Matrix(1, (float)Math.Tan(n[0] * Math.PI / 180), 0, 1, 0, 0),
				_ => throw new SvgException("can't draw: transform " + kind),
			};
			result.Multiply(step);
			i = close + 1;
		}
		return result;
	}

	private static Matrix Rotation(float degrees, float cx, float cy)
	{
		var m = new Matrix();
		m.Translate(cx, cy);
		m.Rotate(degrees);
		m.Translate(-cx, -cy);
		return m;
	}

	//==================================================================
	//  Shapes (in the element's own units)
	//==================================================================
	private static GraphicsPath? Shape(XElement e)
	{
		float A(string name) => Length((string?)e.Attribute(name)) ?? 0;
		var path = new GraphicsPath();
		switch (e.Name.LocalName)
		{
			case "line":
				path.AddLine(A("x1"), A("y1"), A("x2"), A("y2"));
				return path;
			case "circle":
				path.AddEllipse(A("cx") - A("r"), A("cy") - A("r"), 2 * A("r"), 2 * A("r"));
				return path;
			case "ellipse":
				path.AddEllipse(A("cx") - A("rx"), A("cy") - A("ry"), 2 * A("rx"), 2 * A("ry"));
				return path;
			case "rect":
			{
				float rx = e.Attribute("rx") != null ? A("rx") : A("ry");
				float ry = e.Attribute("ry") != null ? A("ry") : rx;
				RoundRect(path, A("x"), A("y"), A("width"), A("height"), rx, ry);
				return path;
			}
			case "polyline":
			case "polygon":
			{
				var n = Numbers((string?)e.Attribute("points") ?? "");
				var points = Enumerable.Range(0, n.Count / 2).Select(k => new PointF(n[2 * k], n[2 * k + 1])).ToArray();
				if (points.Length >= 2)
				{
					path.AddLines(points);
					if (e.Name.LocalName == "polygon")
					{
						path.CloseFigure();
					}
				}
				return path;
			}
			case "path":
				SvgPath.Add(path, (string?)e.Attribute("d") ?? "");
				return path;
		}
		path.Dispose();
		return null;
	}

	private static void RoundRect(GraphicsPath path, float x, float y, float w, float h, float rx, float ry)
	{
		rx = Math.Min(rx, w / 2);
		ry = Math.Min(ry, h / 2);
		if (rx <= 0 || ry <= 0)
		{
			path.AddRectangle(new RectangleF(x, y, w, h));
			return;
		}
		float dx = 2 * rx, dy = 2 * ry;
		path.StartFigure();
		path.AddArc(x, y, dx, dy, 180, 90);
		path.AddArc(x + w - dx, y, dx, dy, 270, 90);
		path.AddArc(x + w - dx, y + h - dy, dx, dy, 0, 90);
		path.AddArc(x, y + h - dy, dx, dy, 90, 90);
		path.CloseFigure();
	}

	internal static List<float> Numbers(string text)
	{
		var result = new List<float>();
		var scan = new SvgPath.Scanner(text);
		while (scan.SkipSeparators() && scan.TryNumber(out float value))
		{
			result.Add(value);
		}
		return result;
	}

	// The program icon (softcalc.ico) for the windows' title bars
	public static Icon ProgramIcon()
	{
		using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("softcalc.ico")
			?? throw new InvalidOperationException("softcalc.ico is not embedded");
		return new Icon(stream);
	}
}

// Path data ("d"): every SVG command, absolute and relative
internal static class SvgPath
{
	public static void Add(GraphicsPath path, string data)
	{
		var scan = new Scanner(data);
		PointF current = default, start = default, lastControl = default;
		char previous = ' ';
		char command = ' ';
		while (scan.SkipSeparators())
		{
			if (char.IsLetter(scan.Peek))
			{
				command = scan.Take();
			}
			else if (command == ' ')
			{
				throw new SvgException("can't draw: path data");
			}
			bool relative = char.IsLower(command);
			char upper = char.ToUpperInvariant(command);
			PointF P(float x, float y) => relative ? new PointF(current.X + x, current.Y + y) : new PointF(x, y);
			float N() => scan.Number();
			switch (upper)
			{
				case 'Z':
					path.CloseFigure();
					current = start;
					command = ' ';    // a number right after Z is an error
					break;
				case 'M':
					path.StartFigure();
					current = start = P(N(), N());
					command = relative ? 'l' : 'L';    // pairs after M are lines
					break;
				case 'L':
					current = Line(path, current, P(N(), N()));
					break;
				case 'H':
				{
					float x = N();
					current = Line(path, current, new PointF(relative ? current.X + x : x, current.Y));
					break;
				}
				case 'V':
				{
					float y = N();
					current = Line(path, current, new PointF(current.X, relative ? current.Y + y : y));
					break;
				}
				case 'C':
				{
					var c1 = P(N(), N());
					var c2 = P(N(), N());
					var end = P(N(), N());
					path.AddBezier(current, c1, c2, end);
					(current, lastControl) = (end, c2);
					break;
				}
				case 'S':
				{
					var c1 = previous is 'C' or 'S' ? Mirror(lastControl, current) : current;
					var c2 = P(N(), N());
					var end = P(N(), N());
					path.AddBezier(current, c1, c2, end);
					(current, lastControl) = (end, c2);
					break;
				}
				case 'Q':
				{
					var c = P(N(), N());
					var end = P(N(), N());
					Quad(path, current, c, end);
					(current, lastControl) = (end, c);
					break;
				}
				case 'T':
				{
					var c = previous is 'Q' or 'T' ? Mirror(lastControl, current) : current;
					var end = P(N(), N());
					Quad(path, current, c, end);
					(current, lastControl) = (end, c);
					break;
				}
				case 'A':
				{
					float rx = N(), ry = N(), angle = N();
					bool large = scan.Flag(), sweep = scan.Flag();
					var end = P(N(), N());
					Arc(path, current, rx, ry, angle, large, sweep, end);
					current = end;
					break;
				}
				default:
					throw new SvgException($"can't draw: path command {command}");
			}
			previous = upper;
		}
	}

	private static PointF Line(GraphicsPath path, PointF from, PointF to)
	{
		path.AddLine(from, to);
		return to;
	}

	private static PointF Mirror(PointF control, PointF about)
	{
		return new PointF(2 * about.X - control.X, 2 * about.Y - control.Y);
	}

	private static void Quad(GraphicsPath path, PointF from, PointF c, PointF to)
	{
		var c1 = new PointF(from.X + 2f / 3 * (c.X - from.X), from.Y + 2f / 3 * (c.Y - from.Y));
		var c2 = new PointF(to.X + 2f / 3 * (c.X - to.X), to.Y + 2f / 3 * (c.Y - to.Y));
		path.AddBezier(from, c1, c2, to);
	}

	// SVG 1.1 F.6.5: endpoint to centre form, then Bezier pieces of at most 90 degrees
	internal static void Arc(GraphicsPath path, PointF from, float rxIn, float ryIn, float angle, bool large, bool sweep, PointF to)
	{
		if (from == to)
		{
			return;
		}
		double rx = Math.Abs(rxIn), ry = Math.Abs(ryIn);
		if (rx == 0 || ry == 0)
		{
			path.AddLine(from, to);
			return;
		}
		double phi = angle * Math.PI / 180, cos = Math.Cos(phi), sin = Math.Sin(phi);
		double dx = (from.X - to.X) / 2.0, dy = (from.Y - to.Y) / 2.0;
		double x1 = cos * dx + sin * dy, y1 = -sin * dx + cos * dy;
		double lambda = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
		if (lambda > 1)
		{
			rx *= Math.Sqrt(lambda);
			ry *= Math.Sqrt(lambda);
		}
		double num = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
		double den = rx * rx * y1 * y1 + ry * ry * x1 * x1;
		double k = Math.Sqrt(Math.Max(0, num / den)) * (large == sweep ? -1 : 1);
		double cx1 = k * rx * y1 / ry, cy1 = -k * ry * x1 / rx;
		double cx = cos * cx1 - sin * cy1 + (from.X + to.X) / 2.0;
		double cy = sin * cx1 + cos * cy1 + (from.Y + to.Y) / 2.0;
		double start = Math.Atan2((y1 - cy1) / ry, (x1 - cx1) / rx);
		double end = Math.Atan2((-y1 - cy1) / ry, (-x1 - cx1) / rx);
		double delta = end - start;
		if (sweep && delta < 0)
		{
			delta += 2 * Math.PI;
		}
		else if (!sweep && delta > 0)
		{
			delta -= 2 * Math.PI;
		}
		int pieces = (int)Math.Ceiling(Math.Abs(delta) / (Math.PI / 2) - 1e-9);
		double step = delta / pieces;
		double t = 4.0 / 3 * Math.Tan(step / 4);
		PointF At(double a, double ex, double ey) => new(
			(float)(cx + cos * (rx * Math.Cos(a) - ex) - sin * (ry * Math.Sin(a) + ey)),
			(float)(cy + sin * (rx * Math.Cos(a) - ex) + cos * (ry * Math.Sin(a) + ey)));
		var p0 = from;
		for (int i = 0; i < pieces; i++)
		{
			double a0 = start + i * step, a1 = a0 + step;
			// control points: the end points pushed along the tangent
			var c1 = At(a0, t * rx * Math.Sin(a0), t * ry * Math.Cos(a0));
			var c2 = At(a1, -t * rx * Math.Sin(a1), -t * ry * Math.Cos(a1));
			var p1 = i == pieces - 1 ? to : At(a1, 0, 0);
			path.AddBezier(p0, c1, c2, p1);
			p0 = p1;
		}
	}

	// Numbers as SVG writes them: "1.5.5" is 1.5 0.5, "1e-3", "-2-3"
	internal sealed class Scanner(string text)
	{
		private int _at;

		public char Peek => text[_at];

		public char Take()
		{
			return text[_at++];
		}

		// False at the end
		public bool SkipSeparators()
		{
			while (_at < text.Length && (char.IsWhiteSpace(text[_at]) || text[_at] == ','))
			{
				_at++;
			}
			return _at < text.Length;
		}

		public float Number()
		{
			SkipSeparators();
			if (!TryNumber(out float value))
			{
				throw new SvgException("can't draw: path data");
			}
			return value;
		}

		// An arc flag is one digit and may touch the next number ("a1 1 0 011 1")
		public bool Flag()
		{
			SkipSeparators();
			if (_at < text.Length && text[_at] is '0' or '1')
			{
				return text[_at++] == '1';
			}
			throw new SvgException("can't draw: arc flag");
		}

		public bool TryNumber(out float value)
		{
			int begin = _at, i = _at;
			if (i < text.Length && text[i] is '+' or '-')
			{
				i++;
			}
			int digits = 0;
			while (i < text.Length && char.IsAsciiDigit(text[i]))
			{
				i++;
				digits++;
			}
			if (i < text.Length && text[i] == '.')
			{
				i++;
				while (i < text.Length && char.IsAsciiDigit(text[i]))
				{
					i++;
					digits++;
				}
			}
			if (digits == 0)
			{
				value = 0;
				return false;
			}
			if (i < text.Length && text[i] is 'e' or 'E')
			{
				int j = i + 1;
				if (j < text.Length && text[j] is '+' or '-')
				{
					j++;
				}
				if (j < text.Length && char.IsAsciiDigit(text[j]))
				{
					while (j < text.Length && char.IsAsciiDigit(text[j]))
					{
						j++;
					}
					i = j;
				}
			}
			_at = i;
			value = float.Parse(text.AsSpan(begin, i - begin), NumberStyles.Float, CultureInfo.InvariantCulture);
			return true;
		}
	}
}
