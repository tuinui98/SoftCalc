// SettingsStore.cs - settings.json: calculation, view, window and graph settings
//
// Written on close, read at start; last writer wins between windows
// (owner, design/m7_plan.md). Missing values keep their defaults; an
// unreadable file is kept as settings.json.bak and defaults are used.

using System.Text.Json;
using System.Text.Json.Serialization;

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
	public string? Function { get; set; }
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
	public string Years { get; set; } = "10";
	public string Compounding { get; set; } = "monthly";
	public string Contribution { get; set; } = "0";
	public string ContributionFreq { get; set; } = "monthly";
	public bool AtStart { get; set; }
}

internal sealed class AppSettings
{
	public int Precision { get; set; } = 32;
	public string Angle { get; set; } = "rad";
	public string Base { get; set; } = "dec";
	public bool Rpn { get; set; }
	public int? Decimals { get; set; }
	public bool PadZeros { get; set; }
	public string View { get; set; } = "History";
	public bool ShowSamples { get; set; } = true;
	public int HistoryCap { get; set; } = 1000;
	public WindowPlacement Window { get; set; } = new();
	public GraphPrefs Graph { get; set; } = new();
	public bool AlwaysOnTop { get; set; }
	public bool ShowRibbon { get; set; } = true;

	// History and Input text; 13.5 pt = 150% of the 9 pt window font (owner 2026-11-06)
	public string TextFontFamily { get; set; } = "Segoe UI";
	public float TextFontSize { get; set; } = 13.5f;
	public CompoundPrefs Compound { get; set; } = new();
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
			return (new AppSettings(), null);    // first run: defaults
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

	// Values the engine would refuse go back to defaults instead of failing later
	private static void Sanitize(AppSettings s)
	{
		var defaults = new AppSettings();
		if (s.Precision is not (32 or 64 or 128 or 512))
		{
			s.Precision = defaults.Precision;
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
		s.Window ??= new WindowPlacement();
		s.Graph ??= new GraphPrefs();
		s.Compound ??= new CompoundPrefs();
		if (string.IsNullOrWhiteSpace(s.TextFontFamily))
		{
			s.TextFontFamily = defaults.TextFontFamily;
		}
		if (s.TextFontSize is < 6 or > 48 || float.IsNaN(s.TextFontSize))
		{
			s.TextFontSize = defaults.TextFontSize;
		}
		var c = s.Compound;
		c.Principal ??= "";
		c.Rate ??= "";
		c.Years ??= "";
		c.Contribution ??= "";
		if (c.Compounding is not ("yearly" or "quarterly" or "monthly" or "daily" or "continuous"))
		{
			c.Compounding = "monthly";
		}
		if (c.ContributionFreq is not ("yearly" or "quarterly" or "monthly" or "daily"))
		{
			c.ContributionFreq = "monthly";
		}
	}
}
