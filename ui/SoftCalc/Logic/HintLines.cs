// HintLines.cs - One or two lines for the hint under Input (owner 2026-11-26)
//
// A hint shows on one line when that line holds at least ShownShare of it;
// otherwise it takes two lines, so a long hint is not cut to a stub. The
// web app keeps the same rule (web/app/src/lib/hintlines.ts).

namespace SoftCalc.Logic;

internal static class HintLines
{
	public const double ShownShare = 0.75;

	// textWidth: the whole hint on one line; room: the width the hint line has
	public static int For(int textWidth, int room)
	{
		return textWidth <= 0 || room >= textWidth * ShownShare ? 1 : 2;
	}
}
