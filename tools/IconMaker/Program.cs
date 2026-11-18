// Program.cs - Draws the SoftCalc program icon and writes it as a multi-size .ico
//
// Shape: the IconMCP "Calculator" icon (MIT, see ui/SoftCalc/Icons/LICENSE.txt)
// on its 16-unit grid, with the display line turned into a filled screen
// (owner's choice 2026-11-06: draft 2, dark body, teal screen, orange key).
// Usage: dotnet run --project tools/IconMaker -- <output.ico> [preview.png]
//        dotnet run --project tools/IconMaker -- --android <app/src/main/res>
// The second form writes the Android app's launcher icons (owner 2026-11-18):
// the adaptive icon's foreground (the calculator inside the 66 dp circle that
// every launcher mask keeps) on the web app's dark page colour, and square and
// round icons for Android 7.

using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace IconMaker;

internal static class Program
{
	private static readonly Color Body = Color.FromArgb(47, 58, 74);
	private static readonly Color Screen = Color.FromArgb(59, 199, 168);
	private static readonly Color ScreenText = Color.FromArgb(20, 60, 52);
	private static readonly Color Key = Color.FromArgb(205, 212, 222);
	private static readonly Color Accent = Color.FromArgb(255, 152, 56);
	private static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48, 64, 256 };
	private static readonly Color Page = Color.FromArgb(18, 19, 22);    // #121316, the web app's dark page
	private static readonly (string Name, float Scale)[] Densities =
		{ ("mdpi", 1f), ("hdpi", 1.5f), ("xhdpi", 2f), ("xxhdpi", 3f), ("xxxhdpi", 4f) };

	private static int Main(string[] args)
	{
		if (args.Length == 2 && args[0] == "--android")
		{
			return Android(args[1]);
		}
		if (args.Length < 1)
		{
			Console.Error.WriteLine("usage: IconMaker <output.ico> [preview.png] | --android <res folder>");
			return 2;
		}
		var images = Sizes.Select(size => (size, png: Png(Draw(size)))).ToList();
		using (var file = File.Create(args[0]))
		using (var w = new BinaryWriter(file))
		{
			//==================================================================
			//  ICO header and directory; every image is PNG-compressed
			//==================================================================
			w.Write((short)0);
			w.Write((short)1);
			w.Write((short)images.Count);
			int offset = 6 + 16 * images.Count;
			foreach (var (size, png) in images)
			{
				w.Write((byte)(size >= 256 ? 0 : size));
				w.Write((byte)(size >= 256 ? 0 : size));
				w.Write((byte)0);
				w.Write((byte)0);
				w.Write((short)1);
				w.Write((short)32);
				w.Write(png.Length);
				w.Write(offset);
				offset += png.Length;
			}
			foreach (var (_, png) in images)
			{
				w.Write(png);
			}
		}
		if (args.Length > 1)
		{
			using var preview = Draw(256);
			preview.Save(args[1], ImageFormat.Png);
		}
		Console.WriteLine($"wrote {args[0]} ({string.Join(", ", Sizes)} px)");
		return 0;
	}

	// Launcher icons per density: 108 dp adaptive foreground, 48 dp square and round icons
	private static int Android(string res)
	{
		foreach (var (name, scale) in Densities)
		{
			string folder = Path.Combine(res, "mipmap-" + name);
			Directory.CreateDirectory(folder);
			// The calculator's 11 x 13.5 unit body has a 17.4 unit diagonal; 16 units = 60 dp keeps it in the 66 dp circle
			Save(Canvas((int)Math.Round(108 * scale), 60 * scale, null, false), Path.Combine(folder, "ic_launcher_foreground.png"));
			Save(Canvas((int)Math.Round(48 * scale), 40 * scale, Page, false), Path.Combine(folder, "ic_launcher.png"));
			Save(Canvas((int)Math.Round(48 * scale), 34 * scale, Page, true), Path.Combine(folder, "ic_launcher_round.png"));
		}
		Console.WriteLine($"wrote Android launcher icons in {res}");
		return 0;
	}

	// The icon `iconSize` px wide, centred on a square of `size` px: clear, a rounded square or a circle
	private static Bitmap Canvas(int size, float iconSize, Color? back, bool round)
	{
		var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
		using var g = Graphics.FromImage(bmp);
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.PixelOffsetMode = PixelOffsetMode.HighQuality;
		if (back is Color color)
		{
			if (round)
			{
				using var brush = new SolidBrush(color);
				g.FillEllipse(brush, 0, 0, size, size);
			}
			else
			{
				Fill(g, color, 0, 0, size, size, size * 0.2f);
			}
		}
		using var icon = Draw((int)Math.Round(iconSize));
		float at = (size - icon.Width) / 2f;
		g.DrawImage(icon, at, at, icon.Width, icon.Height);
		return bmp;
	}

	private static void Save(Bitmap bitmap, string path)
	{
		using (bitmap)
		{
			bitmap.Save(path, ImageFormat.Png);
		}
	}

	private static byte[] Png(Bitmap bitmap)
	{
		using (bitmap)
		using (var stream = new MemoryStream())
		{
			bitmap.Save(stream, ImageFormat.Png);
			return stream.ToArray();
		}
	}

	private static Bitmap Draw(int size)
	{
		var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
		using var g = Graphics.FromImage(bmp);
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.PixelOffsetMode = PixelOffsetMode.HighQuality;
		float s = size / 16f;
		Fill(g, Body, 2.5f * s, 1.25f * s, 11f * s, 13.5f * s, 2f * s);
		Fill(g, Screen, 4.25f * s, 3f * s, 7.5f * s, 3.25f * s, 0.75f * s);
		if (size >= 48)
		{
			using var font = new Font("Segoe UI Semibold", 2.1f * s, GraphicsUnit.Pixel);
			using var text = new SolidBrush(ScreenText);
			var area = new RectangleF(4.25f * s, 3f * s, 7.1f * s, 3.25f * s);
			g.DrawString("1.23", font, text, area, new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center });
		}
		for (int row = 0; row < 2; row++)
		{
			for (int col = 0; col < 3; col++)
			{
				var color = row == 1 && col == 2 ? Accent : Key;
				Fill(g, color, (4.25f + col * 2.75f) * s, (7.6f + row * 2.85f) * s, 2f * s, 2f * s, 0.5f * s);
			}
		}
		return bmp;
	}

	private static void Fill(Graphics g, Color color, float x, float y, float w, float h, float r)
	{
		using var path = new GraphicsPath();
		float d = 2 * r;
		path.AddArc(x, y, d, d, 180, 90);
		path.AddArc(x + w - d, y, d, d, 270, 90);
		path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
		path.AddArc(x, y + h - d, d, d, 90, 90);
		path.CloseFigure();
		using var brush = new SolidBrush(color);
		g.FillPath(brush, path);
	}
}
