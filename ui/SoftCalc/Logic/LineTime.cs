// LineTime.cs - How long a line took, for the hint line under Input (owner 2026-11-24, as the web app)
//
// "Took 0.23 s" after every line (two places under 10 s, one from 10 s);
// "Working... 1.4 s" while a line runs past a second. The hint line shows
// the time when it has no hint of its own for the text.

using System.Globalization;

namespace SoftCalc.Logic;

internal static class LineTime
{
	public static readonly TimeSpan WorkingAfter = TimeSpan.FromSeconds(1);

	public static string Took(TimeSpan time)
	{
		double seconds = time.TotalSeconds;
		return "Took " + seconds.ToString(seconds < 10 ? "0.00" : "0.0", CultureInfo.InvariantCulture) + " s";
	}

	public static string Working(TimeSpan time)
	{
		return "Working... " + time.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";
	}
}
