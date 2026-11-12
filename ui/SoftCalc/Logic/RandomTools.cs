// RandomTools.cs - The generators behind the Random window (design/random_plan.md R1-R2)
//
// Everything draws from an IRandomSource: the system's secure generator
// in the app, fixed numbers in tests. Draws are unbiased (rejection
// sampling, never `%` on raw bytes). No WinForms here.

using System.Globalization;
using System.Security.Cryptography;

namespace SoftCalc.Logic;

internal interface IRandomSource
{
	// A uniform whole number from 0 to bound - 1 (bound >= 1)
	ulong Below(ulong bound);
}

// The system's cryptographic generator (owner 2026-11-12: also for passwords)
internal sealed class SecureRandom : IRandomSource
{
	public ulong Below(ulong bound)
	{
		return RandomTools.Unbiased(bound, () =>
		{
			Span<byte> bytes = stackalloc byte[8];
			RandomNumberGenerator.Fill(bytes);
			return BitConverter.ToUInt64(bytes);
		});
	}
}

internal sealed record PasswordOptions(int Length, bool Upper, bool Lower, bool Digits, bool Symbols, string SymbolSet,
	bool NoLookAlikes);

internal static class RandomTools
{
	public const string UpperSet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
	public const string LowerSet = "abcdefghijklmnopqrstuvwxyz";
	public const string DigitSet = "0123456789";
	// Symbol groups for the Password tab (owner 2026-11-12); the last two are
	// the ones sites most often refuse, so they start unticked
	public static readonly (string Name, string Chars)[] SymbolGroups =
	{
		("Common", "!@#$%&*?"),
		("Dash", "-_"),
		("Maths", "+=^~"),
		("Brackets", "()[]{}<>"),
		("Punctuation", ".,:;"),
		("Quotes", "'\"`"),
		("Slashes", "/\\|"),
	};
	public static readonly string DefaultSymbols = string.Concat(SymbolGroups.Take(5).Select(g => g.Chars));
	public const string LookAlikes = "Il1O0o";
	public const string DefaultHead = "Head";    // owner 2026-11-12
	public const string DefaultTail = "Tail";
	public const int MaxCount = 1000;
	public const int MaxPlaces = 10;
	public const decimal Limit = 1_000_000_000_000_000m;    // |From|, |To| up to 10^15

	// A uniform value below bound from 64-bit draws, dropping the uneven top
	public static ulong Unbiased(ulong bound, Func<ulong> next)
	{
		if (bound == 0)
		{
			throw new ArgumentOutOfRangeException(nameof(bound));
		}
		ulong limit = ulong.MaxValue - ulong.MaxValue % bound;    // values at or above it would favour small results
		while (true)
		{
			ulong value = next();
			if (value < limit)
			{
				return value % bound;
			}
		}
	}

	//==================================================================
	//  Numbers tab
	//==================================================================
	// Whole numbers from min to max, both included; null and a reason when it cannot be done
	public static (List<long>? Values, string? Error) Integers(IRandomSource source, long min, long max, int count,
		bool noRepeats, bool sort)
	{
		if (min > max)
		{
			(min, max) = (max, min);
		}
		if (count < 1 || count > MaxCount)
		{
			return (null, $"Count must be 1 to {MaxCount}.");
		}
		ulong span = (ulong)(max - min) + 1;
		if (noRepeats && (ulong)count > span)
		{
			return (null, $"Only {span} different numbers from {min} to {max}.");
		}
		var values = new List<long>(count);
		var seen = new HashSet<long>();
		while (values.Count < count)
		{
			long value = min + (long)source.Below(span);
			if (noRepeats && !seen.Add(value))
			{
				continue;
			}
			values.Add(value);
		}
		if (sort)
		{
			values.Sort();
		}
		return (values, null);
	}

	// Decimals from min to max in steps of 10^-places, both ends included
	public static (List<decimal>? Values, string? Error) Decimals(IRandomSource source, decimal min, decimal max,
		int places, int count, bool sort)
	{
		if (min > max)
		{
			(min, max) = (max, min);
		}
		if (places < 0 || places > MaxPlaces)
		{
			return (null, $"Decimal places must be 0 to {MaxPlaces}.");
		}
		if (count < 1 || count > MaxCount)
		{
			return (null, $"Count must be 1 to {MaxCount}.");
		}
		decimal step = 1m / Pow10(places);
		decimal steps = decimal.Floor((max - min) / step);
		if (steps >= ulong.MaxValue / 2)
		{
			return (null, "The range is too wide for that many decimal places.");
		}
		var values = new List<decimal>(count);
		for (int i = 0; i < count; i++)
		{
			values.Add(decimal.Round(min + source.Below((ulong)steps + 1) * step, places));
		}
		if (sort)
		{
			values.Sort();
		}
		return (values, null);
	}

	// True = heads; headsPercent 0-100 in steps of 0.01 (owner: P50 or a bias)
	public static List<bool> Coins(IRandomSource source, decimal headsPercent, int count)
	{
		long threshold = (long)decimal.Round(Math.Clamp(headsPercent, 0, 100) * 100);
		return Enumerable.Range(0, count).Select(_ => (long)source.Below(10000) < threshold).ToList();
	}

	// Insert Command (owner 2026-11-12): one draw as a line for Input.
	// Coin: 1 = head, 0 = tail; 50% is round(rand()), otherwise
	// floor(rand()+p), which is 1 with chance p.
	public static string Command(string kind, decimal from, decimal to, int places, decimal headsPercent, int sides)
	{
		if (from > to)
		{
			(from, to) = (to, from);
		}
		switch (kind)
		{
			case "decimal":
				return $"round(rand({WithPoint(from)}, {Plain(to)}), {places})";
			case "coin":
				decimal p = Math.Clamp(headsPercent, 0, 100) / 100;
				return p == 0.5m ? "round(rand())" : $"floor(rand()+{Plain(p)})";
			case "dice":
				return $"rand(1, {sides})";
			default:
				return $"rand({Plain(decimal.Truncate(from))}, {Plain(decimal.Truncate(to))})";
		}
	}

	// `1` -> `1.0`, so rand() draws decimals (owner 2026-11-12)
	private static string WithPoint(decimal value)
	{
		string text = Plain(value);
		return text.Contains('.') ? text : text + ".0";
	}

	// Without trailing zeros: 2.00 -> 2, 0.70 -> 0.7
	private static string Plain(decimal value)
	{
		return Text(value / 1.000000000000000000000000000000000m);
	}

	public static List<int> Dice(IRandomSource source, int sides, int count)
	{
		return Enumerable.Range(0, count).Select(_ => 1 + (int)source.Below((ulong)sides)).ToList();
	}

	public static string Text(decimal value)
	{
		return value.ToString(CultureInfo.InvariantCulture);
	}

	private static decimal Pow10(int places)
	{
		decimal result = 1;
		for (int i = 0; i < places; i++)
		{
			result *= 10;
		}
		return result;
	}

	//==================================================================
	//  Password tab
	//==================================================================
	// A group ticked or unticked: its symbols are taken out of the text, then
	// put back at the end when ticked, so a hand-edited box never doubles them
	public static string ToggleSymbols(string text, string chars, bool on)
	{
		string kept = new string(text.Where(c => !chars.Contains(c)).ToArray());
		return on ? kept + chars : kept;
	}

	// The character sets chosen, look-alikes and doubles left out
	public static List<string> Kinds(PasswordOptions options)
	{
		var kinds = new List<string>();
		void Add(bool on, string set)
		{
			if (!on)
			{
				return;
			}
			string chars = new string(set.Where(c => !char.IsWhiteSpace(c) && !(options.NoLookAlikes && LookAlikes.Contains(c)))
				.Distinct().ToArray());
			if (chars.Length > 0)
			{
				kinds.Add(chars);
			}
		}
		Add(options.Upper, UpperSet);
		Add(options.Lower, LowerSet);
		Add(options.Digits, DigitSet);
		Add(options.Symbols, options.SymbolSet);
		return kinds;
	}

	public static int PoolSize(PasswordOptions options)
	{
		return Kinds(options).SelectMany(k => k).Distinct().Count();
	}

	// Bits of a password drawn uniformly: length * log2(pool)
	public static double StrengthBits(PasswordOptions options)
	{
		int pool = PoolSize(options);
		return pool < 2 ? 0 : options.Length * Math.Log2(pool);
	}

	// One password with at least one character of each chosen kind
	public static (string? Password, string? Error) Password(IRandomSource source, PasswordOptions options)
	{
		var kinds = Kinds(options);
		if (kinds.Count == 0)
		{
			return (null, "Choose at least one kind of character.");
		}
		if (options.Length < kinds.Count)
		{
			return (null, $"Length must be at least {kinds.Count} for {kinds.Count} kinds.");
		}
		string pool = new string(kinds.SelectMany(k => k).Distinct().ToArray());
		var chars = new char[options.Length];
		for (int i = 0; i < chars.Length; i++)
		{
			chars[i] = i < kinds.Count ? Pick(source, kinds[i]) : Pick(source, pool);
		}
		for (int i = chars.Length - 1; i > 0; i--)    // Fisher-Yates: the sure ones end up anywhere
		{
			int j = (int)source.Below((ulong)i + 1);
			(chars[i], chars[j]) = (chars[j], chars[i]);
		}
		return (new string(chars), null);
	}

	// Signature (owner 2026-11-12): the owner's own mark written into each
	// password as typed, at the start, the end or anywhere; Length counts it,
	// the strength counts only the random characters around it
	public static readonly string[] Places = { "end", "start", "anywhere" };

	public static (string? Password, string? Error) Signed(IRandomSource source, PasswordOptions options, string signature,
		string place)
	{
		if (signature.Length == 0)
		{
			return Password(source, options);
		}
		int kinds = Kinds(options).Count;
		int randomLength = options.Length - signature.Length;
		if (kinds > 0 && randomLength < Math.Max(kinds, 1))
		{
			return (null, $"Length must be at least {signature.Length + kinds}: the signature is {signature.Length} long.");
		}
		var (part, error) = Password(source, options with { Length = randomLength });
		if (part == null)
		{
			return (null, error);
		}
		return place switch
		{
			"start" => (signature + part, null),
			"anywhere" => (part.Insert((int)source.Below((ulong)part.Length + 1), signature), null),
			_ => (part + signature, null),
		};
	}

	// The random part's options: the signature's characters are not random
	public static PasswordOptions RandomPart(PasswordOptions options, string signature)
	{
		return options with { Length = Math.Max(0, options.Length - signature.Length) };
	}

	private static char Pick(IRandomSource source, string set)
	{
		return set[(int)source.Below((ulong)set.Length)];
	}
}
