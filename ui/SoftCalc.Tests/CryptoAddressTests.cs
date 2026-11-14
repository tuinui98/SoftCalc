// CryptoAddressTests.cs - Offline address checks (design/crypto_plan.md C0)

using System.Text;
using NBitcoin;
using NBitcoin.DataEncoders;
using SoftCalc.Logic;

namespace SoftCalc.Tests;

public sealed class CryptoAddressTests
{
	[Theory]
	[InlineData("", "c5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470")]
	[InlineData("abc", "4e03657aea45a94fc7d47ba826c8d667c0d1e6e33a64a036ec44f58fa12d6c45")]
	public void Keccak256MatchesKnownHashes(string text, string hex)
	{
		Assert.Equal(hex, Convert.ToHexString(CryptoAddress.Keccak256(Encoding.ASCII.GetBytes(text))).ToLowerInvariant());
	}

	[Fact]
	public void Keccak256HandlesLongInput()
	{
		// more than one 136-byte block: must differ from a one-block input and be stable
		var a = CryptoAddress.Keccak256(new byte[200]);
		var b = CryptoAddress.Keccak256(new byte[136]);
		Assert.NotEqual(Convert.ToHexString(a), Convert.ToHexString(b));
		Assert.Equal(32, a.Length);
	}

	[Theory]
	// the EIP-55 examples
	[InlineData("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed")]
	[InlineData("0xfB6916095ca1df60bB79Ce92cE3Ea74c37c5d359")]
	[InlineData("0xdbF03B407c01E7cD3CBea99509d93f8DDDC8C6FB")]
	[InlineData("0xD1220A0cf47c7B9Be7A2E6BA89F429762e7b9aDb")]
	public void EthereumChecksumIsChecked(string address)
	{
		var good = CryptoAddress.Check(address);
		Assert.True(good.Valid, good.Error);
		Assert.Equal(("Ethereum", address), (good.Network, good.Normal));
		var lower = CryptoAddress.Check(address.ToLowerInvariant());
		Assert.True(lower.Valid);
		Assert.Equal(address, lower.Normal);    // shows the checksummed form
		Assert.Contains(lower.Warnings, w => w.Contains("no checksum"));
		// one letter's case flipped breaks the checksum
		int i = address.IndexOfAny("abcdefABCDEF".ToCharArray(), 2);
		string flipped = address[..i] + (char.IsUpper(address[i]) ? char.ToLowerInvariant(address[i]) : char.ToUpperInvariant(address[i]))
			+ address[(i + 1)..];
		Assert.False(CryptoAddress.Check(flipped).Valid);
		Assert.False(CryptoAddress.Check(address[..^1]).Valid);
	}

	[Fact]
	public void BitcoinAddressesOfEveryKind()
	{
		Assert.Contains("P2PKH", CryptoAddress.Check("1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa").Kind);    // the genesis block's
		var key = new Key(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());
		foreach (var (type, kind) in new[]
		{
			(ScriptPubKeyType.Legacy, "P2PKH"), (ScriptPubKeyType.SegwitP2SH, "P2SH"), (ScriptPubKeyType.Segwit, "P2WPKH"),
			(ScriptPubKeyType.TaprootBIP86, "P2TR"),
		})
		{
			string address = key.GetAddress(type, Network.Main).ToString();
			var check = CryptoAddress.Check(address);
			Assert.True(check.Valid, address);
			Assert.Contains(kind, check.Kind);
			Assert.Empty(check.Warnings);
			// a changed last character breaks the checksum
			string broken = address[..^1] + (address[^1] == 'q' ? 'p' : 'q');
			Assert.False(CryptoAddress.Check(broken).Valid, broken);
		}
		var test = CryptoAddress.Check(key.GetAddress(ScriptPubKeyType.Segwit, Network.TestNet).ToString());
		Assert.True(test.Valid);
		Assert.Contains(test.Warnings, w => w.Contains("testnet"));
		Assert.True(CryptoAddress.Check("bitcoin:1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa?amount=0.1").Valid);
	}

	[Fact]
	public void TronAddressesAndTheirHex()
	{
		byte[] data = new byte[21];
		data[0] = 0x41;
		for (int i = 1; i < 21; i++)
		{
			data[i] = (byte)(i * 7);
		}
		string address = Encoders.Base58Check.EncodeData(data);
		Assert.StartsWith("T", address);
		var check = CryptoAddress.Check(address);
		Assert.True(check.Valid, check.Error);
		Assert.Equal("Tron", check.Network);
		Assert.Contains(check.Facts, f => f == "Hex: " + Convert.ToHexString(data).ToLowerInvariant());
		Assert.Equal(address, CryptoAddress.Check(Convert.ToHexString(data)).Normal);
		Assert.False(CryptoAddress.Check(address[..^1] + (address[^1] == 'a' ? 'b' : 'a')).Valid);
		Assert.True(CryptoAddress.Check("TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t").Valid);    // the USDT contract
	}

	[Theory]
	[InlineData("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about", "recovery phrase")]
	[InlineData("0x4c0883a69102937d6231471b5dbb6204fe5129617082792ae468d01a3f362318", "private key")]
	public void SecretsAreRefused(string text, string what)
	{
		var check = CryptoAddress.Check(text);
		Assert.True(check.Secret);
		Assert.False(check.Valid);
		Assert.Contains(what, check.Error);
		Assert.DoesNotContain(text, check.Error);
	}

	[Fact]
	public void WifKeysAreRefused()
	{
		var key = new Key(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());
		foreach (var network in new[] { Network.Main, Network.TestNet })
		{
			string wif = key.GetWif(network).ToString();
			var check = CryptoAddress.Check(wif);
			Assert.True(check.Secret, wif);
			Assert.Contains("WIF", check.Error);
		}
	}

	[Fact]
	public void OtherTextIsNotAnAddress()
	{
		Assert.False(CryptoAddress.Check("hello").Valid);
		Assert.False(CryptoAddress.Check("0x1234").Valid);
		Assert.False(CryptoAddress.Check("  ").Valid);
	}
}
