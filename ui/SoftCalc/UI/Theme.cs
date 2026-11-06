// Theme.cs - Light, dark or Windows-matching colours (View > Theme)
//
// WinForms sets its colour mode once, before the first window, so a
// change restarts SoftCalc (owner 2026-11-06). Almost everything uses
// SystemColors, which the dark mode remaps; the few fixed colours
// that would be unreadable on a dark background come from here.

namespace SoftCalc.UI;

internal static class Theme
{
	public static readonly string[] Names = { "system", "light", "dark" };

	public static void Apply(string name)
	{
#pragma warning disable WFO5001    // colour mode API is marked experimental
		Application.SetColorMode(name switch
		{
			"light" => SystemColorMode.Classic,
			"dark" => SystemColorMode.Dark,
			_ => SystemColorMode.System,
		});
	}

	public static bool IsDark => Application.IsDarkModeEnabled;
#pragma warning restore WFO5001

	// Error text: Firebrick on light, a lighter red on dark
	public static Color Error => IsDark ? Color.FromArgb(255, 120, 110) : Color.Firebrick;

	// Graph line colours, readable on light and dark; the first is the system highlight
	private static readonly Color[] SeriesColours =
	{
		Color.FromArgb(230, 120, 30), Color.FromArgb(40, 160, 70), Color.FromArgb(200, 60, 140),
		Color.FromArgb(130, 100, 210), Color.FromArgb(20, 160, 175), Color.FromArgb(180, 150, 20),
	};

	public static Color Series(int index)
	{
		return index <= 0 ? SystemColors.Highlight : SeriesColours[(index - 1) % SeriesColours.Length];
	}

	// Faint chart grid lines
	public static Color Grid => IsDark ? Color.FromArgb(70, 70, 70) : Color.FromArgb(225, 225, 225);
}
