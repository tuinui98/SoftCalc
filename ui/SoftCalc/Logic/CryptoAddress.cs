// CryptoAddress.cs - Offline checks of Bitcoin, Ethereum and Tron addresses (design/crypto_plan.md C0)
//
// Owner 2026-11-15: SoftCalc checks addresses and (later) balances; it
// never takes a seed or a private key. Bitcoin addresses are read by
// NBitcoin (Base58Check, Bech32, Bech32m); Ethereum's EIP-55 checksum
// uses Keccak-256 (below); Tron is Base58Check with version 0x41. Text
// that looks like a secret is refused and not kept.

using System.Text;
using System.Text.RegularExpressions;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace SoftCalc.Logic;

// The verdict on a text: Secret is true for a recovery phrase or a key, which is refused
internal sealed record AddressCheck(bool Valid, string? Network, string? Kind, string? Normal, IReadOnlyList<string> Facts,
	IReadOnlyList<string> Warnings, string? Error, bool Secret = false);

internal static partial class CryptoAddress
{
	public const string EthereumCoins = "ETH and tokens such as USDT (ERC-20)";
	public const string TronCoins = "TRX and tokens such as USDT (TRC-20)";

	public static AddressCheck Check(string input)
	{
		string text = input.Trim();
		int colon = text.IndexOf(':');
		if (colon > 0 && text[..colon].ToLowerInvariant() is "bitcoin" or "ethereum" or "tron")
		{
			text = text[(colon + 1)..];    // a payment URI: bitcoin:bc1q...?amount=...
		}
		int query = text.IndexOf('?');
		if (query > 0)
		{
			text = text[..query];    // ...?amount=0.1
		}
		if (text.Length == 0)
		{
			return Invalid("Type or paste an address.");
		}
		if (SecretKind(input.Trim()) is { } secret)
		{
			return new AddressCheck(false, null, null, null, Array.Empty<string>(), Array.Empty<string>(),
				$"This looks like {secret}, not an address. It is a secret: never type or paste it anywhere but your own wallet. "
				+ "SoftCalc did not keep it.", Secret: true);
		}
		if (ExtendedPublic(text) is { } extended)
		{
			return new AddressCheck(true, "Bitcoin", extended, text, new[] { "A watch-only key for a whole wallet, not one address" },
				new[] { "Anyone with it can see every address and payment of that wallet (no spending)." }, null);
		}
		if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || HexForty().IsMatch(text))
		{
			return Ethereum(text);
		}
		if (text.StartsWith('T') || text.StartsWith("41", StringComparison.Ordinal) && text.Length == 42)
		{
			return Tron(text);
		}
		return Bitcoin(text);
	}

	//==================================================================
	//  Bitcoin
	//==================================================================
	private static AddressCheck Bitcoin(string text)
	{
		foreach (var (network, label) in new[] { (Network.Main, "mainnet"), (Network.TestNet, "testnet"), (Network.RegTest, "regtest") })
		{
			BitcoinAddress address;
			try
			{
				address = BitcoinAddress.Create(text, network);
			}
			catch (FormatException)
			{
				continue;
			}
			string kind = address switch
			{
				BitcoinPubKeyAddress => "P2PKH (legacy, 1...)",
				BitcoinScriptAddress => "P2SH (script, 3...)",
				BitcoinWitPubKeyAddress => "P2WPKH (native SegWit, bc1q...)",
				BitcoinWitScriptAddress => "P2WSH (SegWit script, bc1q...)",
				TaprootAddress => "P2TR (Taproot, bc1p...)",
				_ => "other script",
			};
			var warnings = new List<string>();
			if (label != "mainnet")
			{
				warnings.Add($"A {label} address: test coins only, not real bitcoin.");
			}
			return new AddressCheck(true, "Bitcoin", kind, address.ToString(),
				new[] { $"Network: Bitcoin {label}", "Takes: BTC" }, warnings, null);
		}
		return Invalid(text.StartsWith("bc1", StringComparison.OrdinalIgnoreCase) || text.StartsWith('1') || text.StartsWith('3')
			? "Not a valid Bitcoin address: its checksum or length is wrong (a typo, or a character missing)."
			: "Not a Bitcoin, Ethereum or Tron address.");
	}

	private static string? ExtendedPublic(string text)
	{
		return text.Length > 100 && text.Length < 120 && text[1..4] == "pub" && "xyzXYZtuvUV".Contains(text[0])
			&& Decodes(text) ? $"{text[..4]} (extended public key)" : null;
	}

	private static bool Decodes(string text)
	{
		try
		{
			return Encoders.Base58Check.DecodeData(text).Length == 78;
		}
		catch (FormatException)
		{
			return false;
		}
	}

	//==================================================================
	//  Ethereum: 0x and 40 hex digits; mixed case carries the EIP-55 checksum
	//==================================================================
	private static AddressCheck Ethereum(string text)
	{
		string hex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
		if (!HexForty().IsMatch(hex))
		{
			return Invalid($"Not a valid Ethereum address: it needs 0x and 40 hex digits (this has {hex.Length} characters after 0x).");
		}
		string checksummed = "0x" + Eip55(hex);
		var warnings = new List<string>();
		bool mixed = hex.Any(char.IsUpper) && hex.Any(char.IsLower);
		if (mixed && checksummed[2..] != hex)
		{
			return Invalid("Not a valid Ethereum address: the upper and lower case letters do not match its checksum "
				+ "(EIP-55), so a character is wrong.");
		}
		if (!mixed)
		{
			warnings.Add("All one case: there is no checksum to check, so a typo would not show. Prefer the mixed-case form below.");
		}
		if (!text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
		{
			warnings.Add("Written without 0x; wallets expect 0x in front.");
		}
		return new AddressCheck(true, "Ethereum", mixed ? "account (EIP-55 checksum checked)" : "account", checksummed,
			new[] { "Network: Ethereum (the same address works on other EVM chains)", "Takes: " + EthereumCoins },
			warnings.Append("USDT sent here must be sent on Ethereum (ERC-20); USDT on Tron (TRC-20) cannot reach it.").ToList(), null);
	}

	// EIP-55: a letter is upper case where the Keccak-256 of the lower-case hex has a nibble of 8 or more
	public static string Eip55(string hex)
	{
		string lower = hex.ToLowerInvariant();
		byte[] hash = Keccak256(Encoding.ASCII.GetBytes(lower));
		var text = new StringBuilder(40);
		for (int i = 0; i < lower.Length; i++)
		{
			int nibble = i % 2 == 0 ? hash[i / 2] >> 4 : hash[i / 2] & 0x0F;
			text.Append(char.IsLetter(lower[i]) && nibble >= 8 ? char.ToUpperInvariant(lower[i]) : lower[i]);
		}
		return text.ToString();
	}

	//==================================================================
	//  Tron: Base58Check of 0x41 and 20 bytes (T...), or that in hex (41...)
	//==================================================================
	private static AddressCheck Tron(string text)
	{
		byte[] data;
		if (text.StartsWith("41", StringComparison.Ordinal) && text.Length == 42 && HexDigits().IsMatch(text))
		{
			data = Convert.FromHexString(text);
			string base58 = Encoders.Base58Check.EncodeData(data);
			return TronValid(base58, data, new[] { "Written in hex; wallets show it as " + base58 });
		}
		try
		{
			data = Encoders.Base58Check.DecodeData(text);
		}
		catch (FormatException)
		{
			return Invalid("Not a valid Tron address: its checksum is wrong (a typo, or a character missing).");
		}
		if (data.Length != 21 || data[0] != 0x41)
		{
			return Invalid("Not a Tron address: Tron addresses are 34 characters starting with T.");
		}
		return TronValid(text, data, Array.Empty<string>());
	}

	private static AddressCheck TronValid(string base58, byte[] data, IEnumerable<string> notes)
	{
		return new AddressCheck(true, "Tron", "account", base58,
			new[] { "Network: Tron", "Takes: " + TronCoins, "Hex: " + Convert.ToHexString(data).ToLowerInvariant() },
			notes.Append("USDT sent here must be sent on Tron (TRC-20); USDT on Ethereum (ERC-20) cannot reach it.").ToList(), null);
	}

	//==================================================================
	//  Secrets: refused, never shown back
	//==================================================================
	public static string? SecretKind(string text)
	{
		string[] words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
		if (words.Length is 12 or 15 or 18 or 21 or 24 && words.All(w => w.Length is >= 3 and <= 8 && w.All(c => c is >= 'a' and <= 'z')))
		{
			return "a recovery phrase (seed words)";
		}
		if (text.Length > 100 && text[1..4] == "prv" && "xyzXYZtuvUV".Contains(text[0]))
		{
			return "an extended private key";
		}
		string bare = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
		if (bare.Length == 64 && HexDigits().IsMatch(bare))
		{
			return "a private key (or a transaction id)";
		}
		if (text.Length is 51 or 52 && "5KLc9".Contains(text[0]))
		{
			try
			{
				byte[] data = Encoders.Base58Check.DecodeData(text);
				if (data.Length is 33 or 34 && data[0] is 0x80 or 0xEF)
				{
					return "a private key (WIF)";
				}
			}
			catch (FormatException)
			{
			}
		}
		return null;
	}

	private static AddressCheck Invalid(string why)
	{
		return new AddressCheck(false, null, null, null, Array.Empty<string>(), Array.Empty<string>(), why);
	}

	[GeneratedRegex("^[0-9a-fA-F]{40}$")]
	private static partial Regex HexForty();

	[GeneratedRegex("^[0-9a-fA-F]+$")]
	private static partial Regex HexDigits();

	//==================================================================
	//  Keccak-256 (the original Keccak padding 0x01, not NIST SHA3-256's 0x06)
	//==================================================================
	private static readonly ulong[] RoundConstants =
	{
		0x0000000000000001, 0x0000000000008082, 0x800000000000808A, 0x8000000080008000, 0x000000000000808B, 0x0000000080000001,
		0x8000000080008081, 0x8000000000008009, 0x000000000000008A, 0x0000000000000088, 0x0000000080008009, 0x000000008000000A,
		0x000000008000808B, 0x800000000000008B, 0x8000000000008089, 0x8000000000008003, 0x8000000000008002, 0x8000000000000080,
		0x000000000000800A, 0x800000008000000A, 0x8000000080008081, 0x8000000000008080, 0x0000000080000001, 0x8000000080008008,
	};
	private static readonly int[] Rotations = { 1, 3, 6, 10, 15, 21, 28, 36, 45, 55, 2, 14, 27, 41, 56, 8, 25, 43, 62, 18, 39, 61, 20, 44 };
	private static readonly int[] Lanes = { 10, 7, 11, 17, 18, 3, 5, 16, 8, 21, 24, 4, 15, 23, 19, 13, 12, 2, 20, 14, 22, 9, 6, 1 };

	public static byte[] Keccak256(byte[] message)
	{
		const int rate = 136;    // bytes: 1600 - 2 x 256 bits
		var state = new ulong[25];
		int blocks = message.Length / rate + 1;
		var padded = new byte[blocks * rate];
		message.CopyTo(padded, 0);
		padded[message.Length] ^= 0x01;
		padded[^1] ^= 0x80;
		for (int b = 0; b < blocks; b++)
		{
			for (int i = 0; i < rate / 8; i++)
			{
				state[i] ^= BitConverter.ToUInt64(padded, b * rate + i * 8);
			}
			Permute(state);
		}
		var output = new byte[32];
		for (int i = 0; i < 4; i++)
		{
			BitConverter.GetBytes(state[i]).CopyTo(output, i * 8);
		}
		return output;
	}

	private static void Permute(ulong[] a)
	{
		var c = new ulong[5];
		foreach (ulong constant in RoundConstants)
		{
			for (int x = 0; x < 5; x++)
			{
				c[x] = a[x] ^ a[x + 5] ^ a[x + 10] ^ a[x + 15] ^ a[x + 20];
			}
			for (int x = 0; x < 5; x++)
			{
				ulong d = c[(x + 4) % 5] ^ ulong.RotateLeft(c[(x + 1) % 5], 1);
				for (int y = 0; y < 25; y += 5)
				{
					a[y + x] ^= d;
				}
			}
			ulong carry = a[1];
			for (int i = 0; i < 24; i++)
			{
				int lane = Lanes[i];
				(a[lane], carry) = (ulong.RotateLeft(carry, Rotations[i]), a[lane]);
			}
			for (int y = 0; y < 25; y += 5)
			{
				ulong a0 = a[y], a1 = a[y + 1], a2 = a[y + 2], a3 = a[y + 3], a4 = a[y + 4];
				a[y] = a0 ^ (~a1 & a2);
				a[y + 1] = a1 ^ (~a2 & a3);
				a[y + 2] = a2 ^ (~a3 & a4);
				a[y + 3] = a3 ^ (~a4 & a0);
				a[y + 4] = a4 ^ (~a0 & a1);
			}
			a[0] ^= constant;
		}
	}
}
