// Entropy.cs - The Random window's Entropy tab: sources and conditioning (design/random_plan.md R5)
//
// Each source gives a sample: its raw value as text, the bytes behind
// it and an estimate of how many unpredictable bits it holds. A sample
// becomes a 128-bit seed by SHA-256 (the conditioning step of NIST
// SP 800-90B), shown as a whole number of up to 39 digits for the Seed
// box. Raw sources (clock, CPU jitter, mouse, keys, dice) show how close
// two samples can be before hashing hides it; derived ones (System,
// GUID, TPM, beacons) are already the output of a generator.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace SoftCalc.Logic;

// Raw: what the source gave, as shown; Number: the same as a number when
// it is one (the clock), for the change between samples; Bits: the
// estimated unpredictable bits (0 for public values)
internal sealed record EntropySample(string Raw, byte[] Bytes, double Bits, BigInteger? Number = null);

internal static class Entropy
{
	public const int SeedBytes = 16;    // 128-bit seeds: 39 digits at most
	public const double MaxBits = 256;
	public const int JitterRounds = 1024;

	// id, name, what it is (owner 2026-11-12: to try sources and standards side by side)
	public static readonly (string Id, string Name, string About)[] Sources =
	{
		("system", "System (Windows CNG)",
			"BCryptGenRandom: AES-CTR-DRBG (NIST SP 800-90A) fed from the Windows entropy pool. Derived output, not the pool."),
		("tpm", "TPM 2.0 chip",
			"TPM2_GetRandom through the TPM Base Services: the chip's own generator. Derived output."),
		("jitter", "CPU timing jitter",
			"Times a small task 1024 times (the idea behind Linux jitterentropy, SP 800-90B). Raw: the timings themselves."),
		("clock", "Clock",
			"DateTime ticks (100 ns), the classic weak seed. Raw: one sample after another differs by little and can be guessed."),
		("guid", "GUID v4",
			"Guid.NewGuid: 122 random bits from the system generator. Derived output."),
		("mouse", "Mouse movement",
			"Move the mouse in the box below (as VeraCrypt and PuTTYgen ask). Raw: positions and times."),
		("keys", "Key timing",
			"Type anything in the box below. Raw: the keys and the times between them."),
		("dice", "Dice rolls",
			"Roll real dice and type the results (1-6), as Diceware and BIP39 dice do. Raw: about 2.58 bits per roll."),
		("nist", "NIST Randomness Beacon",
			"A public 512-bit value every 60 s from beacon.nist.gov. Anyone can see it: no secret bits. Get x10: the last 10 pulses."),
		("drand", "drand (League of Entropy)",
			"A public 256-bit value every 30 s from api.drand.sh. Anyone can see it: no secret bits. Get x10: the last 10 rounds."),
	};

	public static bool TakesInput(string id)
	{
		return id is "mouse" or "keys" or "dice";
	}

	public static bool IsPublic(string id)
	{
		return id is "nist" or "drand";
	}

	//==================================================================
	//  Conditioning and comparing
	//==================================================================
	// SHA-256 of the raw bytes, cut to 128 bits
	public static byte[] Condition(byte[] raw)
	{
		return SHA256.HashData(raw)[..SeedBytes];
	}

	// 128 bits as the Seed box's whole number
	public static string SeedText(byte[] seed)
	{
		return new BigInteger(seed, isUnsigned: true, isBigEndian: true).ToString(CultureInfo.InvariantCulture);
	}

	// Bits that differ; null when the lengths differ
	public static int? BitsChanged(byte[] a, byte[] b)
	{
		if (a.Length != b.Length)
		{
			return null;
		}
		int bits = 0;
		for (int i = 0; i < a.Length; i++)
		{
			bits += BitOperations.PopCount((uint)(a[i] ^ b[i]));
		}
		return bits;
	}

	// Min-entropy per sample by the most common value (NIST SP 800-90B 6.3.1)
	public static double MinEntropy(IReadOnlyList<long> samples)
	{
		if (samples.Count < 2)
		{
			return 0;
		}
		int top = samples.GroupBy(s => s).Max(g => g.Count());
		double p = (double)top / samples.Count;
		double upper = Math.Min(1, p + 2.576 * Math.Sqrt(p * (1 - p) / (samples.Count - 1)));
		return -Math.Log2(upper);
	}

	//==================================================================
	//  Sources
	//==================================================================
	public static EntropySample FromSystem()
	{
		byte[] bytes = RandomNumberGenerator.GetBytes(SeedBytes);
		return new EntropySample(Convert.ToHexString(bytes), bytes, SeedBytes * 8);
	}

	public static EntropySample FromGuid()
	{
		var guid = System.Guid.NewGuid();
		return new EntropySample(guid.ToString(), guid.ToByteArray(), 122);
	}

	// Known to within a second, ticks leave about 23 bits to guess
	public static EntropySample FromClock()
	{
		long ticks = DateTime.UtcNow.Ticks;
		var bytes = new byte[8];
		BinaryPrimitives.WriteInt64BigEndian(bytes, ticks);
		return new EntropySample(ticks.ToString(CultureInfo.InvariantCulture), bytes, Math.Log2(TimeSpan.TicksPerSecond), ticks);
	}

	private static readonly int[] Scratch = new int[16384];

	// The time of a small memory walk, many times over; the timings carry the jitter
	public static EntropySample FromJitter(int rounds = JitterRounds)
	{
		var timings = new long[rounds];
		int stride = 1;
		for (int i = 0; i < rounds; i++)
		{
			long start = Stopwatch.GetTimestamp();
			for (int j = 0; j < 512; j++)
			{
				int at = (j * stride * 61) & (Scratch.Length - 1);
				Scratch[at] += j ^ i;
				stride = (stride + Scratch[at]) & 31 | 1;
			}
			timings[i] = Stopwatch.GetTimestamp() - start;
		}
		var bytes = new byte[rounds * 8];
		for (int i = 0; i < rounds; i++)
		{
			BinaryPrimitives.WriteInt64BigEndian(bytes.AsSpan(i * 8), timings[i]);
		}
		// the changes from one timing to the next, which the steady part of the task cancels out of
		var steps = timings.Skip(1).Select((t, i) => t - timings[i]).ToList();
		double bits = Math.Min(MaxBits, MinEntropy(steps) * steps.Count);
		string shown = $"{rounds} timings: " + string.Join(", ", timings.Take(8).Select(t => t.ToString(CultureInfo.InvariantCulture))) + ", ...";
		return new EntropySample(shown, bytes, bits);
	}

	// Mouse positions and times, or key codes and times, as collected; bitsEach per event
	public static EntropySample? FromEvents(string what, IReadOnlyList<long> data, int events, double bitsEach)
	{
		if (events == 0)
		{
			return null;
		}
		var bytes = new byte[data.Count * 8];
		for (int i = 0; i < data.Count; i++)
		{
			BinaryPrimitives.WriteInt64BigEndian(bytes.AsSpan(i * 8), data[i]);
		}
		return new EntropySample($"{events} {what}", bytes, Math.Min(MaxBits, events * bitsEach));
	}

	// Dice results 1-6; spaces and commas between them are fine
	public static (EntropySample? Sample, string? Error) FromDice(string text)
	{
		var rolls = new StringBuilder();
		foreach (char c in text)
		{
			if (c is >= '1' and <= '6')
			{
				rolls.Append(c);
			}
			else if (!char.IsWhiteSpace(c) && c != ',')
			{
				return (null, $"Dice rolls are 1 to 6; '{c}' is not one.");
			}
		}
		if (rolls.Length == 0)
		{
			return (null, "Type the dice results first, e.g. 3 6 1 4 ...");
		}
		string digits = rolls.ToString();
		return (new EntropySample($"{digits.Length} rolls: {digits}", Encoding.ASCII.GetBytes(digits),
			Math.Min(MaxBits, digits.Length * Math.Log2(6))), null);
	}

	// A seed straight from the system generator (New seed)
	public static string NewSeed()
	{
		return SeedText(RandomNumberGenerator.GetBytes(SeedBytes));
	}
}

// The system generator with the user's entropy mixed in: each 64-bit
// draw is HMAC-SHA256(key, 32 fresh system bytes + counter), so it is
// never weaker than the system alone (owner 2026-11-12: Mix into System)
internal sealed class MixedRandom : IRandomSource
{
	private byte[] _key = new byte[32];
	private ulong _counter;

	public double Bits { get; private set; }

	public int Mixes { get; private set; }

	public void Mix(byte[] data, double bits)
	{
		_key = SHA256.HashData(_key.Concat(data).ToArray());
		Bits = Math.Min(Entropy.MaxBits, Bits + bits);
		Mixes++;
	}

	public void Clear()
	{
		_key = new byte[32];
		(Bits, Mixes) = (0, 0);
	}

	public ulong Below(ulong bound)
	{
		return RandomTools.Unbiased(bound, Next);
	}

	private ulong Next()
	{
		Span<byte> input = stackalloc byte[40];
		RandomNumberGenerator.Fill(input[..32]);
		BinaryPrimitives.WriteUInt64BigEndian(input[32..], ++_counter);
		Span<byte> hash = stackalloc byte[32];
		HMACSHA256.HashData(_key, input, hash);
		return BinaryPrimitives.ReadUInt64LittleEndian(hash);
	}
}
