// HintLines.cs - One or two lines for the hint under Input (owner 2026-11-26)
//
// The hint line takes two lines when it is too narrow for MinChars
// characters of hint text, and keeps them while the window keeps its
// width: it changes with the window or the font, never with the text,
// so typing does not make the Input row jump (owner 2026-11-26). The
// width of Sample in the hint's font stands for MinChars characters.
// The web app keeps the same rule (web/app/src/lib/hintlines.ts).

namespace SoftCalc.Logic;

internal static class HintLines
{
	public const int MinChars = 80;    // the hints are at most about 82 characters (engine hints.py)
	public const string Sample = "normcdf(x, [mean, sd]) - normal P(X <= x); normcdf(a, b, [mean, sd]) - P(a to b)";

	// sampleWidth: Sample on one line in the hint's font; room: the width the hint line has
	public static int For(int sampleWidth, int room)
	{
		return room >= sampleWidth ? 1 : 2;
	}
}
