// Program.cs - Help pictures from SoftCalc's own windows
//
// Usage: dotnet run --project tools/HelpShots -- <help/img folder> [light|dark]
// Without a theme it runs itself once per theme (the colour mode must be
// set before the first window). Each run starts SoftCalc's main window
// in-process with a scratch data folder and the development engine,
// types the example lines, opens menus and windows, and captures only
// SoftCalc's own windows with PrintWindow (never the screen), so other
// windows on the desktop cannot end up in a picture. Menus are pasted
// onto the window picture at their screen offsets on a transparent
// canvas. Output: <name>.<theme>.png.

using System.Diagnostics;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using SoftCalc.Engine;
using SoftCalc.Logic;
using SoftCalc.Persistence;
using SoftCalc.UI;

namespace HelpShots;

internal static partial class Program
{
	private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

	[STAThread]
	private static int Main(string[] args)
	{
		if (args.Length == 0)
		{
			Console.Error.WriteLine("usage: HelpShots <help/img folder> [light|dark [help]]");
			return 2;
		}
		string outDir = Path.GetFullPath(args[0]);
		if (args.Length == 1)
		{
			int failed = 0;
			foreach (string theme in new[] { "light", "dark" })
			{
				using var child = Process.Start(Environment.ProcessPath!, new[] { outDir, theme });
				child.WaitForExit();
				failed += child.ExitCode == 0 ? 0 : 1;
			}
			return failed;
		}
		return RunTheme(outDir, args[1], args.Length > 2 && args[2] == "help");
	}

	private static int RunTheme(string outDir, string theme, bool helpOnly)
	{
		//==================================================================
		//  BLOCK 1 - Scratch data folder and settings
		//==================================================================
		string data = Path.Combine(Path.GetTempPath(), "softcalc-helpshots-" + theme);
		if (Directory.Exists(data))
		{
			Directory.Delete(data, true);
		}
		Directory.CreateDirectory(data);
		File.WriteAllText(Path.Combine(data, "settings.json"), JsonSerializer.Serialize(new
		{
			theme,
			view = "Advanced",    // a first run's view, size and angle (owner 2026-11-15)
			angle = "deg",
			window = new { x = 60, y = 60, width = 693, height = 728, maximized = false },
			compound = new { principal = "10000", rate = "5", years = "10", compounding = "monthly", contribution = "500", contribution_freq = "monthly", at_start = false },
			graph = new
			{
				shown = new[] { "f", "g", "x^2+y^2=9" },
				function = "g",
				equations = new[] { "x^2+y^2=9" },
				trace = false,
				square = false,
				window = new { x = 500, y = 60, width = 760, height = 560, maximized = false },
			},
		}));
		Environment.SetEnvironmentVariable("SOFTCALC_DATA_DIR", data);
		Directory.CreateDirectory(outDir);

		//==================================================================
		//  BLOCK 2 - SoftCalc's main window with the development engine
		//==================================================================
		Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(false);
		Theme.Apply(theme);
		var folder = DataFolder.Default();
		using var session = new EngineSession(EngineLocator.Locate(folder.EngineRoot));
		var form = new MainForm(session, folder);
		int exitCode = 0;
		form.Shown += async (_, _) =>
		{
			try
			{
				await new Shots(form, session, outDir, theme).RunAsync(helpOnly);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine(ex);
				File.WriteAllText(Path.Combine(data, "error.txt"), ex.ToString());
				exitCode = 1;
			}
			form.Close();
		};
		Application.Run(form);
		return exitCode;
	}   // end RunTheme

	internal static T Field<T>(object owner, string name)
	{
		return (T)owner.GetType().GetField(name, Hidden)!.GetValue(owner)!;
	}

	internal static object? Call(object owner, string name, params object[] args)
	{
		return owner.GetType().GetMethod(name, Hidden)!.Invoke(owner, args);
	}

	//==================================================================
	//  Window capture: PrintWindow with the full DWM content (title bar
	//  in the theme's colours), cropped to the visible frame
	//==================================================================
	[LibraryImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

	[LibraryImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool GetWindowRect(IntPtr hwnd, out Rect rect);

	[LibraryImport("dwmapi.dll")]
	private static partial int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out Rect rect, int size);

	[StructLayout(LayoutKind.Sequential)]
	private struct Rect
	{
		public int Left, Top, Right, Bottom;
		public readonly Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
	}

	// The window's picture and its visible bounds on the screen
	internal static (Bitmap Picture, Rectangle Bounds) Capture(IntPtr hwnd)
	{
		GetWindowRect(hwnd, out var outer);
		var whole = outer.ToRectangle();
		var frame = DwmGetWindowAttribute(hwnd, 9, out var visible, Marshal.SizeOf<Rect>()) == 0    // DWMWA_EXTENDED_FRAME_BOUNDS
			? visible.ToRectangle() : whole;
		using var full = new Bitmap(whole.Width, whole.Height, PixelFormat.Format32bppArgb);
		using (var g = Graphics.FromImage(full))
		{
			IntPtr hdc = g.GetHdc();
			PrintWindow(hwnd, hdc, 2);    // PW_RENDERFULLCONTENT
			g.ReleaseHdc(hdc);
		}
		var crop = new Rectangle(frame.X - whole.X, frame.Y - whole.Y, frame.Width, frame.Height);
		return (full.Clone(crop, PixelFormat.Format32bppArgb), frame);
	}
}

internal sealed class Shots(MainForm form, EngineSession session, string outDir, string theme)
{
	private readonly TextBox _input = Program.Field<TextBox>(form, "_input");
	private readonly MenuStrip _menu = Program.Field<MenuStrip>(form, "_menu");

	public async Task RunAsync(bool helpOnly)
	{
		//==================================================================
		//  BLOCK 1 - Engine ready, definitions for the graph and editor
		//==================================================================
		var functions = Program.Field<ToolStripMenuItem>(form, "_functionsMenu");
		for (int i = 0; i < 200 && functions.DropDownItems.Count == 0; i++)
		{
			await Task.Delay(100);
		}
		if (functions.DropDownItems.Count == 0)
		{
			throw new TimeoutException("the engine did not start");
		}
		foreach (string definition in new[] { "f(x)=x^2/4-3", "g(x)=3sin(x)", "hyp(a, b)=sqrt(a^2+b^2)", "rate=0.07" })
		{
			await session.EvalAsync(definition);
		}
		await (Task)Program.Call(form, "RefreshStateAsync")!;
		if (helpOnly)
		{
			await HelpWindowAsync();
			return;
		}

		//==================================================================
		//  BLOCK 2 - Main window, ribbon
		//==================================================================
		foreach (string line in new[] { "1+2*3", "x=3", "2x+1", "0xFF", "6*7", "answer/2" })
		{
			await SubmitAsync(line);
		}
		await TypeAsync("fv(");
		Save("main", Program.Capture(form.Handle).Picture);
		var ribbon = Program.Field<Control>(form, "_ribbon");
		Save("ribbon", Crop(form, ribbon.RectangleToScreen(ribbon.ClientRectangle)));

		//==================================================================
		//  BLOCK 3 - Menus
		//==================================================================
		await TypeAsync("");
		await MenuShotAsync("menu-view", "View");
		await MenuShotAsync("menu-functions", "Functions", "Trig Functions", "sin");
		await TypeAsync("5");
		await MenuShotAsync("menu-conversions", "Conversions", "Length", "ft", "m");
		await TypeAsync("250");
		await MenuShotAsync("menu-currency", "Conversions", "Currency", "From", "CHF");
		await TypeAsync("");

		//==================================================================
		//  BLOCK 4 - Other windows
		//==================================================================
		var commands = Program.Field<DefinitionCommands>(form, "_commands");
		using (var editor = new DefinitionEditor(true, commands, "f"))
		{
			await WindowShotAsync("editor-functions", editor);
		}
		var layout = Program.Field<List<RibbonGroup>>(form, "_ribbonLayout");
		var list = form.RibbonCommandList();
		using (var customize = new CustomizeRibbonDialog(list, RibbonLayout.Clone(layout), _ => { }))
		{
			await WindowShotAsync("customize-ribbon", customize);
		}
		// Edit Button and Choose Icon are modal: pictured from a timer, then cancelled
		const string angleIcon = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 16\" fill=\"none\" stroke=\"currentColor\" "
			+ "stroke-width=\"1\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M13.5 13.5 H2.5 L11 4\"/>"
			+ "<path d=\"M8 13.5 A5.5 5.5 0 0 0 6.17 9.4\"/></svg>";
		using (var customize = new CustomizeRibbonDialog(list, RibbonLayout.Clone(layout), _ => { }))
		{
			customize.StartPosition = FormStartPosition.Manual;
			customize.Location = new Point(form.Right + 20, form.Top);
			customize.Show(form);
			var degrees = new RibbonItem { Id = "View/Advanced Options/Degrees", Label = "Deg", IconSvg = angleIcon };
			var edit = ModalShotAsync("ribbon-edit-button", "Edit Button", 800);
			Program.Call(customize, "EditItem", degrees, list.Find(degrees.Id)!);
			await edit;
			var pick = ModalShotAsync("ribbon-choose-icon", "Choose Icon", 800);
			IconPicker.Ask(customize, angleIcon, null, out _);
			await pick;
			customize.Close();
		}
		// Random window: a biased coin on Simple, a right skew on Advanced, Password, ten clock pulls on Entropy
		var randomPrefs = new RandomPrefs { Kind = "coin", Count = 6, HeadsPercent = 70, HeadWord = "Yes", TailWord = "No", Shape = "right",
			EntropySource = "clock" };
		using (var random = new RandomWindow(randomPrefs, _ => { }, new SeedEngine(session.StartInfo, () => 32)))    // Seed shown on, never used
		{
			random.StartPosition = FormStartPosition.Manual;
			random.Location = new Point(form.Right + 20, form.Top);
			random.Show(form);
			Program.Call(random, "DrawNumbers");
			await Task.Delay(300);
			Save("random-numbers", Program.Capture(random.Handle).Picture);
			Program.Field<TabControl>(random, "_tabs").SelectedIndex = 1;    // Advanced random: a right skew, drawn
			Program.Call(random, "DrawShaped");
			random.ActiveControl = null;
			Application.DoEvents();
			Save("random-advanced", Program.Capture(random.Handle).Picture);
			Program.Field<TabControl>(random, "_tabs").SelectedIndex = 2;    // Password
			Program.Call(random, "DrawPasswords");
			random.ActiveControl = null;
			await Task.Delay(300);
			Save("random-password", Program.Capture(random.Handle).Picture);
			Program.Field<TabControl>(random, "_tabs").SelectedIndex = 3;    // Entropy: ten clock pulls
			Program.Call(random, "GetEntropy", 10);
			random.ActiveControl = null;
			Application.DoEvents();
			Save("random-entropy", Program.Capture(random.Handle).Picture);
			random.Close();
		}
		await CardShotsAsync();
		using (var gallery = new GraphGallery(session))
		{
			gallery.StartPosition = FormStartPosition.Manual;
			gallery.Location = new Point(form.Right + 20, form.Top);
			gallery.Show(form);
			Program.Call(gallery, "ShowKind", true);    // the equations
			var items = Program.Field<ListView>(gallery, "_list");
			var heart = items.Items.Cast<ListViewItem>().First(i => i.Text == "Heart");
			heart.Selected = true;
			heart.EnsureVisible();
			await OpenedShotAsync("graph-examples", gallery, 5000);    // thumbnails come from the engine, one by one
		}
		// Add Equation is modal and opens inside AskAsync: take the picture from a timer, then cancel it
		var circle = GraphExamples.Equations[0];
		var shot = ModalShotAsync("graph-example-equation", "Add Equation", 800);
		await EquationDialog.AskAsync(form, session, "Add Equation", circle.Text, Array.Empty<string>(), circle.Hint);
		await shot;
		await (Task)Program.Call(form, "ChangeDecimalsAsync", (int?)2)!;    // money with 2 places
		Program.Call(form, "OpenCompoundWindow");
		await OpenedShotAsync("compound", Program.Field<Form>(form, "_compoundWindow"), 1500);
		Program.Call(form, "OpenGraphWindow");
		var graph = Program.Field<Form>(form, "_graphWindow");
		await Task.Delay(500);
		var plot = Program.Field<GraphPanel>(graph, "_graph").Plot;
		plot.MakeSquare();    // 1:1 pressed, circles round
		plot.Focus();    // no selected text in the Range panel
		await OpenedShotAsync("graph", graph, 2500);
	}   // end RunAsync

	// A check of the Help window itself (not a help picture): two topics, a search, an example click
	private async Task HelpWindowAsync()
	{
		HelpWindow.Open("conversions", form);
		var help = Application.OpenForms.OfType<HelpWindow>().First();
		help.StartPosition = FormStartPosition.Manual;
		help.Location = new Point(form.Right + 20, form.Top);
		await Task.Delay(1500);
		Save("check-help-conversions", Program.Capture(help.Handle).Picture);
		HelpWindow.Open("finance", form);
		await Task.Delay(1000);
		Save("check-help-finance", Program.Capture(help.Handle).Picture);
		Program.Field<TextBox>(help, "_search").Text = "rai";
		await Task.Delay(1000);
		Save("check-help-search", Program.Capture(help.Handle).Picture);
		help.PutInInput("6800 [m2->rnw]");
		await Task.Delay(300);
		Console.WriteLine("input after example click: " + _input.Text);
		help.Close();
	}

	private async Task SubmitAsync(string line)
	{
		_input.Text = line;
		await (Task)Program.Call(form, "SubmitAsync")!;
		await Task.Delay(150);
	}

	// Text left in Input with the caret at its end, hint line updated
	private async Task TypeAsync(string text)
	{
		_input.Focus();
		_input.Text = text;
		_input.SelectionStart = text.Length;
		Program.Call(form, "UpdateHint");    // TextChanged saw the caret at 0
		await Task.Delay(400);    // the hint may ask the engine
	}

	private async Task MenuShotAsync(string name, params string[] path)
	{
		form.Activate();
		await Task.Delay(200);
		ToolStripItemCollection items = _menu.Items;
		var opened = new List<ToolStripDropDown>();
		ToolStripMenuItem? top = null;
		foreach (string text in path)
		{
			var item = items.OfType<ToolStripMenuItem>().First(i => i.Text!.Replace("&", "") == text);
			top ??= item;
			item.Select();
			if (item.HasDropDownItems)
			{
				item.ShowDropDown();
				opened.Add(item.DropDown);
				items = item.DropDownItems;
			}
			await Task.Delay(250);
		}
		await Task.Delay(300);

		var (picture, bounds) = Program.Capture(form.Handle);
		var parts = new List<(Bitmap, Rectangle)> { (picture, bounds) };
		foreach (var drop in opened)
		{
			parts.Add(Program.Capture(drop.Handle));
		}
		var all = parts.Select(p => p.Item2).Aggregate(Rectangle.Union);
		// Keep the window's top part down to below the lowest menu
		int bottom = Math.Min(bounds.Bottom, all.Bottom + 12);
		var area = Rectangle.FromLTRB(bounds.Left, bounds.Top, Math.Max(bounds.Right, all.Right), Math.Max(bottom, all.Bottom));
		using var canvas = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb);
		using (var g = Graphics.FromImage(canvas))
		{
			foreach (var (part, where) in parts)
			{
				g.DrawImage(part, where.X - area.X, where.Y - area.Y);
				part.Dispose();
			}
		}
		top!.HideDropDown();
		_menu.Items.OfType<ToolStripMenuItem>().ToList().ForEach(i => i.HideDropDown());
		await Task.Delay(200);
		Save(name, canvas);
	}

	private async Task WindowShotAsync(string name, Form window)
	{
		window.StartPosition = FormStartPosition.Manual;
		window.Location = new Point(form.Right + 20, form.Top);
		window.Show(form);
		await OpenedShotAsync(name, window, 800);
	}

	// A modal dialog opened right after this starts: found by its caption, pictured, cancelled
	// Card decks and draw tabs (owner 2026-11-14): the "..." tab, a deck, a draw's steps, the slot machine and the dartboard
	private async Task CardShotsAsync()
	{
		string folder = Path.Combine(Path.GetTempPath(), "softcalc-helpshots-cards-" + theme);
		if (Directory.Exists(folder))
		{
			Directory.Delete(folder, true);
		}
		var cards = new CardStore(new DataFolder(folder));
		cards.File.Draws.Clear();    // no first-run Spin tab: the pictures set up their own
		var food = new Deck { Id = "food01", Name = "Food", Cards = CardText.ParseLines(
			"Fried rice | 2\nNoodles\nSom tam\nPad thai\nGreen curry\nTom yum").Cards! };
		var tints = new[] { Color.Goldenrod, Color.Peru, Color.YellowGreen, Color.Coral, Color.SeaGreen, Color.Tomato };
		for (int i = 0; i < food.Cards.Count; i++)
		{
			string picture = Path.Combine(folder, $"food{i}.png");
			using (var bitmap = new Bitmap(96, 96))
			{
				using var g = Graphics.FromImage(bitmap);
				g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
				using var plate = new SolidBrush(tints[i]);
				g.FillEllipse(Brushes.WhiteSmoke, 4, 4, 88, 88);
				g.FillEllipse(plate, 20, 20, 56, 56);
				bitmap.Save(picture, ImageFormat.Png);
			}
			food.Cards[i].Image = cards.ImportImage(picture).Name;
		}
		var drinks = new Deck { Id = "drink1", Name = "Drinks", Cards = CardText.ParseLines("Iced tea\nWater\nCoffee\nOrange juice").Cards! };
		cards.File.Decks.AddRange(new[] { food, drinks });
		string days = ReadyDecks.Prefix + "days";
		var lunch = new DrawSetup
		{
			Id = "lunch1", Name = "What to eat", Theme = "slot", Effects = false,
			Steps =
			{
				new DrawStep { Decks = { days }, Extra = "End of the month, Payday>>Drinks*2", Off = { days + "/Saturday", days + "/Sunday" } },
				new DrawStep { Decks = { "food01" } },
				new DrawStep { Decks = { "drink1" }, NoRepeat = true },
			},
		};
		// the dartboard with rings (77 provinces) and a two-ring spin wheel
		var provinces = new DrawSetup { Id = "prov01", Name = "Provinces", Theme = "dart", Effects = false, StageHeight = 290,
			Steps = { new DrawStep { Decks = { ReadyDecks.Prefix + "provinces" } } } };
		var spin = new DrawSetup { Id = "spin01", Name = "Spin", Theme = "spin", Effects = false, StageHeight = 300,
			Steps = { new DrawStep { Decks = { ReadyDecks.Prefix + "zodiac" } }, new DrawStep { Decks = { ReadyDecks.Prefix + "animal-years" } } } };
		cards.File.Draws.AddRange(new[] { lunch, provinces, spin });
		using var random = new RandomWindow(new RandomPrefs { Tab = "cards" }, _ => { }, null, cards);
		random.StartPosition = FormStartPosition.Manual;
		random.Location = new Point(form.Right + 20, form.Top);
		random.Show(form);
		var tabs = Program.Field<TabControl>(random, "_tabs");
		Program.Field<ListView>(random, "_myDecks").Items[0].Selected = true;
		random.ActiveControl = null;
		await Task.Delay(300);
		Save("random-cards", Program.Capture(random.Handle).Picture);
		var deck = ModalShotAsync("random-deck", "Edit deck", 800);
		Program.Call(random, "OpenDeckEditor", food, false);
		await deck;
		var pages = tabs.TabPages.OfType<DrawPage>().ToList();
		var steps = ModalShotAsync("random-draw-edit", "Edit tab", 800);
		Program.Call(random, "EditDraw", pages[0]);
		await steps;
		foreach (var (page, name) in new[] { (pages[0], "random-draw-slot"), (pages[1], "random-draw-dart"), (pages[2], "random-draw-spin") })
		{
			tabs.SelectedTab = page;
			for (int i = 0; i < 4; i++)
			{
				await (Task)Program.Call(page, "DrawAsync")!;
			}
			random.ActiveControl = null;
			await Task.Delay(300);
			Save(name, Program.Capture(random.Handle).Picture);
		}
		random.Close();
	}

	private async Task ModalShotAsync(string name, string caption, int settle)
	{
		await Task.Delay(settle);
		var window = Application.OpenForms.Cast<Form>().First(f => f.Text == caption);
		window.ActiveControl = null;
		await Task.Delay(200);
		var (picture, _) = Program.Capture(window.Handle);
		using (picture)
		{
			Save(name, picture);
		}
		window.DialogResult = DialogResult.Cancel;
		await Task.Delay(200);
	}

	private async Task OpenedShotAsync(string name, Form window, int settle)
	{
		window.Activate();
		window.ActiveControl = null;    // no selected text in the first field
		await Task.Delay(settle);
		var (picture, _) = Program.Capture(window.Handle);
		using (picture)
		{
			Save(name, picture);
		}
		window.Close();
		await Task.Delay(200);
	}

	private static Bitmap Crop(Form owner, Rectangle screenArea)
	{
		var (picture, bounds) = Program.Capture(owner.Handle);
		using (picture)
		{
			var area = screenArea with { X = screenArea.X - bounds.X, Y = screenArea.Y - bounds.Y };
			return picture.Clone(area, PixelFormat.Format32bppArgb);
		}
	}

	private void Save(string name, Bitmap picture)
	{
		string path = Path.Combine(outDir, $"{name}.{theme}.png");
		picture.Save(path, ImageFormat.Png);
		Console.WriteLine(path);
	}
}
