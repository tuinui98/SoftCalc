// SettingsStore.cs - settings.json: calculation, view, window and graph settings
//
// Written on close, read at start; last writer wins between windows
// (owner, design/m7_plan.md). Missing values keep their defaults; an
// unreadable file is kept as settings.json.bak and defaults are used.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SoftCalc.Logic;

namespace SoftCalc.Persistence;

internal sealed class WindowPlacement
{
	public int X { get; set; } = -1;
	public int Y { get; set; } = -1;
	public int Width { get; set; } = 400;
	public int Height { get; set; } = 651;
	public bool Maximized { get; set; }

	public bool HasPosition => X >= 0 || Y >= 0;

	// Moves the window back onto a screen when it no longer overlaps any
	// (a monitor was removed); keeps the size within the screen's work area.
	public static Rectangle FitOnScreen(Rectangle window, IReadOnlyList<Rectangle> workAreas)
	{
		const int minVisible = 80;
		foreach (var area in workAreas)
		{
			var overlap = Rectangle.Intersect(window, area);
			if (overlap.Width >= minVisible && overlap.Height >= minVisible)
			{
				return window;
			}
		}
		var primary = workAreas.Count > 0 ? workAreas[0] : new Rectangle(0, 0, 1024, 768);
		int width = Math.Min(window.Width, primary.Width);
		int height = Math.Min(window.Height, primary.Height);
		return new Rectangle(primary.Left + (primary.Width - width) / 2, primary.Top + (primary.Height - height) / 2, width, height);
	}
}

internal sealed class GraphPrefs
{
	public string Mode { get; set; } = "xy";
	public bool ShowGrid { get; set; } = true;
	public bool Trace { get; set; } = true;
	public string? Function { get; set; }
	public List<string>? Shown { get; set; }    // null in files from before G3: show Function
	public List<string>? Equations { get; set; }    // equations in x and y (G5)
	public Dictionary<string, string>? Colours { get; set; }    // picked line colours, "#RRGGBB" by name (G4)
	public bool Square { get; set; }    // 1:1 lock in the Graph window
	public WindowPlacement? Window { get; set; }    // Graph window; null until it was first used
	public int RangeWidth { get; set; } = 150;
	public double? PlotAspect { get; set; }    // the Graph window's drawing area, width / height
	public double MinX { get; set; } = -10;
	public double MaxX { get; set; } = 10;
	public double GridX { get; set; } = 1;
	public double MinY { get; set; } = -10;
	public double MaxY { get; set; } = 10;
	public double GridY { get; set; } = 1;
	public double MinT { get; set; } = -Math.PI;
	public double MaxT { get; set; } = Math.PI;
	public double MaxR { get; set; } = 10;
	public double GridR { get; set; } = 1;
}

// Last inputs of the Compound Interest window (M8)
internal sealed class CompoundPrefs
{
	public string Principal { get; set; } = "10000";
	public string Rate { get; set; } = "5";
	public string Years { get; set; } = "10";    // the length, in LengthUnit
	public string RateUnit { get; set; } = "yearly";    // owner 2026-11-28: daily, weekly, monthly, yearly
	public string LengthUnit { get; set; } = "years";    // days, weeks, months, years
	public string Compounding { get; set; } = "monthly";
	public string Contribution { get; set; } = "0";
	public string ContributionFreq { get; set; } = "monthly";
	public bool AtStart { get; set; }

	// owner 2026-11-28: the table's own decimal places, null for all
	public int? TableDecimals { get; set; } = 2;

	// Goal mode (owner 2026-11-28): four fields give the fifth. GoalOrder lists the five, the
	// one set longest ago first: that one is worked out.
	public string Mode { get; set; } = "normal";
	public string Target { get; set; } = "1000000";
	public List<string> GoalOrder { get; set; } = new(GoalFields);

	public static readonly string[] GoalFields = { "target", "principal", "annual_rate", "years", "contribution" };

	/// <summary>A field was set: it goes last (the most important); the first is worked out.</summary>
	public void SetGoalField(string field)
	{
		GoalOrder.Remove(field);
		GoalOrder.Add(field);
	}
}

// Last options of the Random window (design/random_plan.md); never the results
internal sealed class RandomPrefs
{
	public string Tab { get; set; } = "numbers";    // numbers, advanced, password, entropy, cards ("...") or draw:<id>
	public string Kind { get; set; } = "whole";    // whole, decimal, coin, dice
	public decimal From { get; set; } = 1;
	public decimal To { get; set; } = 100;
	public int Count { get; set; } = 1;
	public bool NoRepeats { get; set; }
	public int Places { get; set; } = 2;
	public decimal HeadsPercent { get; set; } = 50;
	public int Sides { get; set; } = 6;
	public string HeadWord { get; set; } = "";    // empty: Head
	public string TailWord { get; set; } = "";    // empty: Tail
	public int PasswordLength { get; set; } = 16;
	public bool Upper { get; set; } = true;
	public bool Lower { get; set; } = true;
	public bool Digits { get; set; } = true;
	public bool Symbols { get; set; } = true;
	public string SymbolSet { get; set; } = Logic.RandomTools.DefaultSymbols;
	public bool NoLookAlikes { get; set; }
	public int PasswordCount { get; set; } = 5;
	public string SignaturePlace { get; set; } = "end";    // the signature itself is never saved
	// Advanced random tab (owner 2026-11-12)
	public string Shape { get; set; } = "bell";
	public decimal ShapeStrength { get; set; } = Logic.RandomShapes.DefaultStrength;
	public decimal ShapePeak { get; set; } = 3;
	public decimal ShapeA { get; set; } = 2;
	public decimal ShapeB { get; set; } = 5;
	public bool ShapeWhole { get; set; } = true;
	public decimal ShapeFrom { get; set; } = 1;
	public decimal ShapeTo { get; set; } = 10;
	public int ShapePlaces { get; set; } = 2;
	public int ShapeCount { get; set; } = 1000;
	public string EntropySource { get; set; } = "system";    // Entropy tab: the source shown; samples are never saved
	public WindowPlacement? Window { get; set; }
}

// Tools > Cryptocurrency (design/crypto_plan.md): the tab, the services and the window; never an address typed in it
internal sealed class CryptoPrefs
{
	public string Tab { get; set; } = "address";    // address, balance, watch or fees
	public string Esplora { get; set; } = Logic.CryptoEndpoints.Default.Esplora;
	public string EthereumRpc { get; set; } = Logic.CryptoEndpoints.Default.EthereumRpc;
	public string TronGrid { get; set; } = Logic.CryptoEndpoints.Default.TronGrid;
	public WindowPlacement? Window { get; set; }

	// Watch (C2a, owner 2026-11-16): every 10 minutes (2-60), each minute while a payment waits;
	// confirmations asked for; dust below this many USD; keep watching in the tray after closing
	public int WatchMinutes { get; set; } = 10;
	public int BtcConfirmations { get; set; } = 2;
	public int TronConfirmations { get; set; } = 1;
	public decimal DustUsd { get; set; } = 1;
	public bool KeepInTray { get; set; }
}

internal sealed class AppSettings
{
	public int Precision { get; set; } = 32;
	public string Angle { get; set; } = "rad";
	public string Base { get; set; } = "dec";
	public bool Rpn { get; set; }
	public int? Decimals { get; set; }
	public bool PadZeros { get; set; }
	public int WordSize { get; set; } = 32;    // bits for twos(x) / signed(x) (owner 2026-11-07)
	public string View { get; set; } = "History";
	public const int KeypadLeast = 260;    // owner 2026-11-16: no flatter than this
	public int KeypadHeight { get; set; } = 260;    // Numeric keypad height in pixels at 96 dpi, dragged by its top edge
	public bool KeypadScientific { get; set; }    // Numeric keypad in Scientific mode (owner 2026-11-16)
	public bool ShowSamples { get; set; } = true;
	public int HistoryCap { get; set; } = 1000;
	public WindowPlacement Window { get; set; } = new();
	public GraphPrefs Graph { get; set; } = new();
	public bool AlwaysOnTop { get; set; }
	public bool ShowRibbon { get; set; } = true;
	public bool ShowHints { get; set; } = true;
	public string Theme { get; set; } = "system";    // system, light or dark; applied at start

	// A first run (owner 2026-11-15): degrees, the Advanced view at the owner's window size, and a
	// graph with two lines to try, over a full turn each way since the angle is in degrees (a sine
	// and a gentle line from -1 to 1); files from earlier runs keep their own values
	public static AppSettings ForNewUser()
	{
		var settings = new AppSettings { Angle = "deg", View = "Advanced", ShowSamples = true };
		settings.Window.Width = 693;
		settings.Window.Height = 728;
		settings.Graph.Equations = new List<string> { "y=sin(x)", "y=x/360" };
		settings.Graph.Shown = new List<string>(settings.Graph.Equations);
		settings.Graph.Function = "y=sin(x)";
		(settings.Graph.MinX, settings.Graph.MaxX, settings.Graph.GridX) = (-360, 360, 90);
		(settings.Graph.MinY, settings.Graph.MaxY, settings.Graph.GridY) = (-2, 2, 0.5);
		settings.Graph.Window = new WindowPlacement { Width = 839, Height = 730 };
		return settings;
	}

	// History and Input text: the Font dialog sets family and base size
	// (= 100%), View > Text Size scales it (owner 2026-11-06)
	public string TextFontFamily { get; set; } = "Segoe UI";
	public float TextFontBase { get; set; } = 9f;
	public int TextScale { get; set; } = 150;

	// Before text_scale: one absolute size; read as base at 100%
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public float? TextFontSize { get; set; }
	public CompoundPrefs Compound { get; set; } = new();
	public RandomPrefs Random { get; set; } = new();
	public CryptoPrefs Crypto { get; set; } = new();
	public TimerState Timers { get; set; } = new();    // stopwatch, timers, alarms (owner 2026-11-28)

	// Customize Ribbon layout; null = the default (design/ribbon_plan.md)
	public List<RibbonGroup>? Ribbon { get; set; }
}

internal static class SettingsStore
{
	private static readonly JsonSerializerOptions Options = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
		DefaultIgnoreCondition = JsonIgnoreCondition.Never,
	};

	// Returns the settings and a warning to show, or null when all was fine
	public static (AppSettings Settings, string? Warning) Load(string path)
	{
		if (!File.Exists(path))
		{
			return (AppSettings.ForNewUser(), null);    // first run: defaults
		}
		try
		{
			var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options);
			if (settings != null)
			{
				Sanitize(settings);
				return (settings, null);
			}
		}
		catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException)
		{
		}
		File.Copy(path, path + ".bak", overwrite: true);
		return (new AppSettings(), $"Settings could not be read and were reset (old file kept as {Path.GetFileName(path)}.bak).");
	}

	public static void Save(string path, AppSettings settings)
	{
		DataFolder.WriteAtomic(path, JsonSerializer.Serialize(settings, Options));
	}

	// Close of a window: the ribbon layout another window saved since
	// this one started stays, unless this window changed it itself
	public static void SaveKeepingRibbon(string path, AppSettings settings, bool ownRibbon)
	{
		if (!ownRibbon && TryReadRibbon(path, out var ribbon))
		{
			settings.Ribbon = ribbon;
		}
		Save(path, settings);
	}

	// Customize Ribbon OK / Apply: writes only the "ribbon" key at once
	public static void SaveRibbon(string path, List<RibbonGroup>? ribbon)
	{
		JsonObject? root = null;
		if (File.Exists(path))
		{
			try
			{
				root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
			}
			catch (Exception ex) when (ex is JsonException or IOException)
			{
			}
			if (root == null)
			{
				return;    // unreadable: the window writes the whole file when it closes
			}
		}
		else
		{
			root = JsonSerializer.SerializeToNode(new AppSettings(), Options)!.AsObject();
		}
		root["ribbon"] = JsonSerializer.SerializeToNode(ribbon, Options);
		DataFolder.WriteAtomic(path, root.ToJsonString(Options));
	}

	private static bool TryReadRibbon(string path, out List<RibbonGroup>? ribbon)
	{
		ribbon = null;
		try
		{
			if (File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject root)
			{
				ribbon = root["ribbon"]?.Deserialize<List<RibbonGroup>>(Options);
				if (ribbon != null)
				{
					RibbonLayout.Sanitize(ribbon);
				}
				return true;
			}
		}
		catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException or InvalidOperationException)
		{
		}
		return false;
	}

	// Values the engine would refuse go back to defaults instead of failing later
	private static void Sanitize(AppSettings s)
	{
		var defaults = new AppSettings();
		if (s.Precision is not (32 or 64 or 128 or 512))
		{
			s.Precision = defaults.Precision;
		}
		if (s.WordSize is not (8 or 16 or 32 or 64))
		{
			s.WordSize = defaults.WordSize;
		}
		if (s.Angle is not ("rad" or "deg" or "grad"))
		{
			s.Angle = defaults.Angle;
		}
		if (s.Base is not ("dec" or "hex" or "bin" or "oct"))
		{
			s.Base = defaults.Base;
		}
		if (s.Decimals is < 0 or > 512)
		{
			s.Decimals = null;
		}
		if (s.View is not ("Numeric" or "History" or "Advanced"))
		{
			s.View = defaults.View;
		}
		if (s.HistoryCap < 1)
		{
			s.HistoryCap = defaults.HistoryCap;
		}
		if (s.Theme is not ("system" or "light" or "dark"))
		{
			s.Theme = defaults.Theme;
		}
		s.Window ??= new WindowPlacement();
		s.Graph ??= new GraphPrefs();
		s.Compound ??= new CompoundPrefs();
		s.Random ??= new RandomPrefs();
		s.KeypadHeight = Math.Clamp(s.KeypadHeight, AppSettings.KeypadLeast, 1000);
		s.Crypto ??= new CryptoPrefs();
		s.Timers ??= new TimerState();
		s.Timers.Laps ??= new List<Lap>();
		s.Timers.Pauses ??= new List<double>();
		s.Timers.Countdowns ??= new List<Countdown>();
		s.Timers.Countdowns.RemoveAll(c => c == null);
		foreach (string kind in new[] { "timer", "alarm" })
		{
			var slots = s.Timers.Slots(kind);
			bool good = slots is { Count: 3 } && slots.All(slot => slot is { Length: 2 }
				&& TimerState.Shows.Contains(slot[0]) && TimerState.FormatsOf(slot[0]).Contains(slot[1]));
			if (!good)
			{
				if (kind == "alarm")
				{
					s.Timers.AlarmSlots = TimerState.DefaultSlots(kind);
				}
				else
				{
					s.Timers.TimerSlots = TimerState.DefaultSlots(kind);
				}
			}
		}
		if (!TimerState.Sounds.Contains(s.Timers.Sound))
		{
			s.Timers.Sound = "once";
		}
		if (s.Crypto.Tab is not ("address" or "balance" or "watch" or "fees"))
		{
			s.Crypto.Tab = "address";
		}
		static string Url(string? url, string fallback) =>
			Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? url!.TrimEnd('/') : fallback;
		s.Crypto.Esplora = Url(s.Crypto.Esplora, Logic.CryptoEndpoints.Default.Esplora);
		s.Crypto.EthereumRpc = Url(s.Crypto.EthereumRpc, Logic.CryptoEndpoints.Default.EthereumRpc);
		s.Crypto.TronGrid = Url(s.Crypto.TronGrid, Logic.CryptoEndpoints.Default.TronGrid);
		s.Crypto.WatchMinutes = Math.Clamp(s.Crypto.WatchMinutes, 2, 60);
		s.Crypto.BtcConfirmations = Math.Clamp(s.Crypto.BtcConfirmations, 1, 6);
		s.Crypto.TronConfirmations = Math.Clamp(s.Crypto.TronConfirmations, 1, 19);
		s.Crypto.DustUsd = Math.Clamp(s.Crypto.DustUsd, 0, 1000);
		SanitizeRandom(s.Random);
		if (s.Ribbon != null)
		{
			RibbonLayout.Sanitize(s.Ribbon);
		}
		if (string.IsNullOrWhiteSpace(s.TextFontFamily))
		{
			s.TextFontFamily = defaults.TextFontFamily;
		}
		if (s.TextFontSize is { } legacy)
		{
			(s.TextFontBase, s.TextScale, s.TextFontSize) = (legacy, 100, null);
		}
		if (s.TextFontBase is < 6 or > 48 || float.IsNaN(s.TextFontBase))
		{
			s.TextFontBase = defaults.TextFontBase;
		}
		if (s.TextScale is not (100 or 125 or 150 or 175 or 200))
		{
			s.TextScale = defaults.TextScale;
		}
		var c = s.Compound;
		c.Principal ??= "";
		c.Rate ??= "";
		c.Years ??= "";
		c.Contribution ??= "";
		if (c.Compounding is not ("yearly" or "quarterly" or "monthly" or "weekly" or "daily" or "continuous"))
		{
			c.Compounding = "monthly";
		}
		if (c.ContributionFreq is not ("yearly" or "quarterly" or "monthly" or "weekly" or "daily"))
		{
			c.ContributionFreq = "monthly";
		}
		if (c.RateUnit is not ("yearly" or "monthly" or "weekly" or "daily"))
		{
			c.RateUnit = "yearly";
		}
		if (c.LengthUnit is not ("years" or "months" or "weeks" or "days"))
		{
			c.LengthUnit = "years";
		}
		if (c.TableDecimals is < 0 or > 15)
		{
			c.TableDecimals = 2;
		}
		c.Target ??= "";
		if (c.Mode is not ("normal" or "goal"))
		{
			c.Mode = "normal";
		}
		// the five fields once each, in their order; unknown or missing ones go first (worked out first)
		var order = (c.GoalOrder ?? new List<string>()).Where(CompoundPrefs.GoalFields.Contains).Distinct().ToList();
		c.GoalOrder = CompoundPrefs.GoalFields.Where(field => !order.Contains(field)).Concat(order).ToList();
	}

	private static void SanitizeRandom(RandomPrefs r)
	{
		var d = new RandomPrefs();
		if (r.Tab is not ("numbers" or "advanced" or "password" or "entropy" or "cards")
			&& !System.Text.RegularExpressions.Regex.IsMatch(r.Tab ?? "", "^draw:[0-9a-f]{1,32}$"))
		{
			r.Tab = d.Tab;
		}
		if (r.Kind is not ("whole" or "decimal" or "coin" or "dice"))
		{
			r.Kind = d.Kind;
		}
		r.From = Math.Clamp(r.From, -Logic.RandomTools.Limit, Logic.RandomTools.Limit);
		r.To = Math.Clamp(r.To, -Logic.RandomTools.Limit, Logic.RandomTools.Limit);
		r.Count = Math.Clamp(r.Count, 1, Logic.RandomTools.MaxCount);
		r.Places = Math.Clamp(r.Places, 0, Logic.RandomTools.MaxPlaces);
		r.HeadsPercent = Math.Clamp(r.HeadsPercent, 0, 100);
		r.Sides = Math.Clamp(r.Sides, 2, 1000);
		r.PasswordLength = Math.Clamp(r.PasswordLength, 4, 128);
		r.PasswordCount = Math.Clamp(r.PasswordCount, 1, 50);
		r.SymbolSet ??= d.SymbolSet;
		r.HeadWord ??= "";
		if (!Logic.RandomTools.Places.Contains(r.SignaturePlace))
		{
			r.SignaturePlace = d.SignaturePlace;
		}
		r.TailWord ??= "";
		if (!Logic.RandomShapes.Shapes.Any(s => s.Id == r.Shape))
		{
			r.Shape = d.Shape;
		}
		r.ShapeStrength = Math.Clamp(r.ShapeStrength, Logic.RandomShapes.MinStrength, Logic.RandomShapes.MaxStrength);
		r.ShapePeak = Math.Clamp(r.ShapePeak, -Logic.RandomTools.Limit, Logic.RandomTools.Limit);
		r.ShapeA = Math.Clamp(r.ShapeA, Logic.RandomShapes.MinBeta, Logic.RandomShapes.MaxBeta);
		r.ShapeB = Math.Clamp(r.ShapeB, Logic.RandomShapes.MinBeta, Logic.RandomShapes.MaxBeta);
		r.ShapeFrom = Math.Clamp(r.ShapeFrom, -Logic.RandomTools.Limit, Logic.RandomTools.Limit);
		r.ShapeTo = Math.Clamp(r.ShapeTo, -Logic.RandomTools.Limit, Logic.RandomTools.Limit);
		r.ShapePlaces = Math.Clamp(r.ShapePlaces, 0, Logic.RandomTools.MaxPlaces);
		r.ShapeCount = Math.Clamp(r.ShapeCount, 1, Logic.RandomShapes.MaxCount);
		if (!Logic.Entropy.Sources.Any(s => s.Id == r.EntropySource))
		{
			r.EntropySource = d.EntropySource;
		}
	}
}
