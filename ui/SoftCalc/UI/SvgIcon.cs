// SvgIcon.cs - Toolbar icons: draws the small SVG subset IconMCP uses with GDI+
//
// Supports path (absolute M L H V C Z), line, circle, ellipse and rect
// (with rx); fill="currentColor" fills, everything else is stroked. That
// covers the icons in Icons/ without a third-party SVG library. Strokes
// get a minimum width so thin 16-unit lines stay visible at 20 px.

using System.Drawing.Drawing2D;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SoftCalc.UI;

internal static partial class SvgIcon
{
	private const float MinStroke = 1.4f;
	private static readonly Dictionary<(string, int, int), Bitmap> Cache = new();

	// An embedded icon ("trash" -> icons/trash.svg) at `size` pixels, cached
	public static Bitmap Get(string name, int size, Color color)
	{
		var key = (name, size, color.ToArgb());
		if (!Cache.TryGetValue(key, out var bitmap))
		{
			using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"icons/{name}.svg")
				?? throw new InvalidOperationException($"icon '{name}' is not embedded");
			bitmap = Render(XDocument.Load(stream), size, color);
			Cache[key] = bitmap;
		}
		return bitmap;
	}

	public static Bitmap Render(XDocument doc, int size, Color color)
	{
		var root = doc.Root ?? throw new InvalidOperationException("empty SVG");
		float view = 16f;
		var box = ((string?)root.Attribute("viewBox"))?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (box is { Length: 4 })
		{
			view = Number(box[2]);
		}
		float scale = size / view;
		float stroke = Math.Max(MinStroke, Number((string?)root.Attribute("stroke-width") ?? "1") * scale);
		var bitmap = new Bitmap(size, size);
		using var g = Graphics.FromImage(bitmap);
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.PixelOffsetMode = PixelOffsetMode.HighQuality;
		using var pen = new Pen(color, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
		using var brush = new SolidBrush(color);
		foreach (var element in root.Descendants())
		{
			using var path = Shape(element, scale);
			if (path == null)
			{
				continue;
			}
			if ((string?)element.Attribute("fill") == "currentColor")
			{
				g.FillPath(brush, path);
			}
			else if ((string?)element.Attribute("stroke") != "none")
			{
				g.DrawPath(pen, path);
			}
		}
		return bitmap;
	}

	private static GraphicsPath? Shape(XElement e, float s)
	{
		float A(string name) => Number((string?)e.Attribute(name) ?? "0") * s;
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
				RoundRect(path, A("x"), A("y"), A("width"), A("height"), A("rx"));
				return path;
			case "path":
				AddPathData(path, (string?)e.Attribute("d") ?? "", s);
				return path;
		}
		path.Dispose();
		return null;
	}

	private static void RoundRect(GraphicsPath path, float x, float y, float w, float h, float r)
	{
		if (r <= 0)
		{
			path.AddRectangle(new RectangleF(x, y, w, h));
			return;
		}
		float d = 2 * r;
		path.AddArc(x, y, d, d, 180, 90);
		path.AddArc(x + w - d, y, d, d, 270, 90);
		path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
		path.AddArc(x, y + h - d, d, d, 90, 90);
		path.CloseFigure();
	}

	[GeneratedRegex(@"[MLHVCZ]|-?\d*\.?\d+")]
	private static partial Regex PathToken();

	// Absolute commands only, which is all IconMCP emits
	private static void AddPathData(GraphicsPath path, string data, float s)
	{
		var tokens = PathToken().Matches(data).Select(m => m.Value).ToList();
		int i = 0;
		PointF current = default, start = default;
		char command = 'M';
		float Next() => Number(tokens[i++]) * s;
		while (i < tokens.Count)
		{
			if (char.IsLetter(tokens[i][0]))
			{
				command = tokens[i++][0];
				if (command == 'Z')
				{
					path.CloseFigure();
					current = start;
					continue;
				}
			}
			switch (command)
			{
				case 'M':
					path.StartFigure();
					current = start = new PointF(Next(), Next());
					command = 'L';    // pairs after M are lines
					break;
				case 'L':
					current = Line(path, current, new PointF(Next(), Next()));
					break;
				case 'H':
					current = Line(path, current, new PointF(Next(), current.Y));
					break;
				case 'V':
					current = Line(path, current, new PointF(current.X, Next()));
					break;
				case 'C':
				{
					var c1 = new PointF(Next(), Next());
					var c2 = new PointF(Next(), Next());
					var end = new PointF(Next(), Next());
					path.AddBezier(current, c1, c2, end);
					current = end;
					break;
				}
				default:
					i++;
					break;
			}
		}
	}

	private static PointF Line(GraphicsPath path, PointF from, PointF to)
	{
		path.AddLine(from, to);
		return to;
	}

	private static float Number(string text)
	{
		return float.Parse(text, CultureInfo.InvariantCulture);
	}

	// The program icon (softcalc.ico) for the windows' title bars
	public static Icon ProgramIcon()
	{
		using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("softcalc.ico")
			?? throw new InvalidOperationException("softcalc.ico is not embedded");
		return new Icon(stream);
	}
}
