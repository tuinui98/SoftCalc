// DecimalSteps.cs - Display decimals buttons and the sample in the ribbon caption (E9)
//
// `decimals` null means all significant digits. From there, More or
// Fewer starts at 2 places [DEF]; then each press moves one place,
// 0 to MaxPlaces. The caption sample shows the current format.

namespace SoftCalc.Logic;

internal static class DecimalSteps
{
	public const int Start = 2;
	public const int MaxPlaces = 30;

	public static int Next(int? current, int delta)
	{
		if (current == null)
		{
			return Start;
		}
		return Math.Clamp(current.Value + delta, 0, MaxPlaces);
	}

	// "all", "0", "0.00"; long runs are shortened: "0.000000 (12)"
	public static string Sample(int? decimals)
	{
		if (decimals == null)
		{
			return "all";
		}
		if (decimals == 0)
		{
			return "0";
		}
		return decimals <= 6 ? "0." + new string('0', decimals.Value) : $"0.000000 ({decimals})";
	}
}
