// EcbRates.cs - ECB daily reference rates: parse the XML and know when the next one is due
//
// The ECB publishes once per TARGET working day at about 16:00 CET
// (weekends, 1 Jan, Good Friday, Easter Monday, 1 May, 25 and 26 Dec
// are closed). A fetch is skipped while the cache holds the latest
// publication; a failed or early fetch is retried after an hour
// (design/m8_plan.md).

using System.Globalization;
using System.Xml.Linq;

namespace SoftCalc.Logic;

internal sealed record EcbRates(DateOnly Date, IReadOnlyDictionary<string, string> PerEur)
{
	// Reads eurofxref-daily.xml; null when it is not a usable rates file
	public static EcbRates? Parse(string xml)
	{
		XDocument doc;
		try
		{
			doc = XDocument.Parse(xml);
		}
		catch (System.Xml.XmlException)
		{
			return null;
		}
		var day = doc.Descendants().FirstOrDefault(e => e.Attribute("time") != null);
		if (day == null || !DateOnly.TryParseExact((string?)day.Attribute("time"), "yyyy-MM-dd",
			CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
		{
			return null;
		}
		var rates = new SortedDictionary<string, string>(StringComparer.Ordinal);
		foreach (var cube in day.Elements())
		{
			string? code = (string?)cube.Attribute("currency");
			string? rate = (string?)cube.Attribute("rate");
			if (code is { Length: 3 } && double.TryParse(rate, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
				&& value > 0)
			{
				rates[code.ToUpperInvariant()] = rate!;
			}
		}
		return rates.Count == 0 ? null : new EcbRates(date, rates);
	}
}

internal static class EcbCalendar
{
	public static readonly TimeSpan RetryDelay = TimeSpan.FromHours(1);
	private static readonly TimeSpan PublishTime = new(16, 0, 0);
	private static readonly TimeZoneInfo Cet = FindCet();

	private static TimeZoneInfo FindCet()
	{
		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
		}
		catch (TimeZoneNotFoundException)
		{
			return TimeZoneInfo.CreateCustomTimeZone("CET", TimeSpan.FromHours(1), "CET", "CET");
		}
	}

	public static bool IsWorkingDay(DateOnly day)
	{
		if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
		{
			return false;
		}
		if ((day.Month, day.Day) is (1, 1) or (5, 1) or (12, 25) or (12, 26))
		{
			return false;
		}
		var easter = EasterSunday(day.Year);
		return day != easter.AddDays(-2) && day != easter.AddDays(1);
	}

	// Anonymous Gregorian algorithm
	public static DateOnly EasterSunday(int year)
	{
		int a = year % 19, b = year / 100, c = year % 100;
		int d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3;
		int h = (19 * a + b - d - g + 15) % 30;
		int i = c / 4, k = c % 4;
		int l = (32 + 2 * e + 2 * i - h - k) % 7;
		int m = (a + 11 * h + 22 * l) / 451;
		int month = (h + l - 7 * m + 114) / 31;
		int dayOfMonth = (h + l - 7 * m + 114) % 31 + 1;
		return new DateOnly(year, month, dayOfMonth);
	}

	// When the publication for `day` is expected, in UTC
	public static DateTime PublicationUtc(DateOnly day)
	{
		var local = day.ToDateTime(TimeOnly.FromTimeSpan(PublishTime), DateTimeKind.Unspecified);
		return TimeZoneInfo.ConvertTimeToUtc(local, Cet);
	}

	// The newest publication that should exist at `nowUtc`
	public static DateOnly LatestPublication(DateTime nowUtc)
	{
		var cet = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, Cet);
		var day = DateOnly.FromDateTime(cet);
		if (IsWorkingDay(day) && cet.TimeOfDay >= PublishTime)
		{
			return day;
		}
		return PreviousWorkingDay(day);
	}

	public static DateOnly NextWorkingDay(DateOnly day)
	{
		do
		{
			day = day.AddDays(1);
		}
		while (!IsWorkingDay(day));
		return day;
	}

	private static DateOnly PreviousWorkingDay(DateOnly day)
	{
		do
		{
			day = day.AddDays(-1);
		}
		while (!IsWorkingDay(day));
		return day;
	}

	// When the rates of `held` are due again: the next publication when
	// they are the latest, otherwise after `retry` (zero at start-up)
	public static DateTime NextCheckUtc(DateOnly held, DateTime nowUtc, TimeSpan retry)
	{
		if (held >= LatestPublication(nowUtc))
		{
			return PublicationUtc(NextWorkingDay(held));
		}
		return nowUtc + retry;
	}
}
