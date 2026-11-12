// EntropyTests.cs - The Entropy tab's sources, conditioning and mixing (design/random_plan.md R5)

using SoftCalc.Engine;
using SoftCalc.Logic;
using SoftCalc.Persistence;
using SoftCalc.UI;
using Xunit.Abstractions;

namespace SoftCalc.Tests;

public sealed class EntropyTests(ITestOutputHelper output)
{
	[Fact]
	public void SeedIs128BitsAsAWholeNumber()
	{
		var top = Enumerable.Repeat((byte)0xFF, 16).ToArray();
		Assert.Equal("340282366920938463463374607431768211455", Entropy.SeedText(top));    // 2^128 - 1: 39 digits
		Assert.Equal(16, Entropy.Condition(new byte[] { 1, 2, 3 }).Length);
		Assert.Equal(Entropy.Condition(new byte[] { 1, 2, 3 }), Entropy.Condition(new byte[] { 1, 2, 3 }));
		Assert.Matches(@"^[0-9]{1,39}$", Entropy.NewSeed());
	}

	[Fact]
	public void BitsChangedCountsDifferingBits()
	{
		Assert.Equal(0, Entropy.BitsChanged(new byte[] { 5 }, new byte[] { 5 }));
		Assert.Equal(8, Entropy.BitsChanged(new byte[] { 0x00, 0x0F }, new byte[] { 0xF0, 0x0F }) * 2);
		Assert.Null(Entropy.BitsChanged(new byte[] { 1 }, new byte[] { 1, 2 }));
	}

	[Fact]
	public void MinEntropyFollowsTheMostCommonValue()
	{
		Assert.Equal(0, Entropy.MinEntropy(Enumerable.Repeat(7L, 500).ToList()), 3);
		var spread = Enumerable.Range(0, 25600).Select(i => (long)(i % 256)).ToList();
		Assert.InRange(Entropy.MinEntropy(spread), 7, 8);
	}

	[Fact]
	public void ClockPullsAreCloseAndSystemPullsAreNot()
	{
		var first = Entropy.FromClock();
		var second = Entropy.FromClock();
		Assert.InRange((double)(second.Number!.Value - first.Number!.Value), 0, TimeSpan.TicksPerSecond);
		Assert.True(Entropy.BitsChanged(first.Bytes, second.Bytes) < 32);    // raw: only the low bits move
		var seeds = Enumerable.Range(0, 60).Select(_ => Entropy.Condition(Entropy.FromSystem().Bytes)).ToList();
		double mean = seeds.Zip(seeds.Skip(1)).Average(p => (double)Entropy.BitsChanged(p.First, p.Second)!);
		Assert.InRange(mean, 54, 74);    // chance gives 64 of 128
		Assert.Equal(122, Entropy.FromGuid().Bits);
	}

	[Fact]
	public void JitterTimesTheTaskManyTimes()
	{
		var sample = Entropy.FromJitter(256);
		Assert.Equal(256 * 8, sample.Bytes.Length);
		Assert.InRange(sample.Bits, 0, Entropy.MaxBits);
		Assert.StartsWith("256 timings:", sample.Raw);
		output.WriteLine($"jitter: {sample.Raw} -> about {sample.Bits:0} bits");
	}

	[Fact]
	public void DiceAreReadOneToSix()
	{
		var (sample, error) = Entropy.FromDice("3 6,1\n4");
		Assert.Null(error);
		Assert.Equal("4 rolls: 3614", sample!.Raw);
		Assert.Equal(4 * Math.Log2(6), sample.Bits, 6);
		Assert.Contains("'7'", Entropy.FromDice("1 7").Error);
		Assert.NotNull(Entropy.FromDice("  ").Error);
		Assert.Null(Entropy.FromEvents("moves", new List<long>(), 0, 1));
		Assert.Equal(3, Entropy.FromEvents("keys", new List<long> { 65, 1, 66, 2, 67, 3 }, 3, 1)!.Bits);
	}

	[Fact]
	public void MixedRandomStaysUnbiasedAndCountsBits()
	{
		var mixed = new MixedRandom();
		Assert.All(Enumerable.Range(0, 600).Select(_ => mixed.Below(6)), v => Assert.InRange(v, 0UL, 5UL));
		Assert.Equal(new ulong[] { 0, 1, 2, 3, 4, 5 }, Enumerable.Range(0, 600).Select(_ => mixed.Below(6)).Distinct().Order());
		mixed.Mix(new byte[] { 1, 2, 3 }, 200);
		mixed.Mix(new byte[] { 4 }, 200);
		Assert.Equal((256d, 2), (mixed.Bits, mixed.Mixes));
		mixed.Clear();
		Assert.Equal((0d, 0), (mixed.Bits, mixed.Mixes));
	}

	[Fact]
	public void TpmAnswerIsRead()
	{
		var reply = new byte[] { 0x80, 0x01, 0, 0, 0, 16, 0, 0, 0, 0, 0, 4, 9, 8, 7, 6 };
		Assert.Equal(new byte[] { 9, 8, 7, 6 }, TpmRandom.Parse(reply).Bytes);
		var refused = new byte[] { 0x80, 0x01, 0, 0, 0, 10, 0, 0, 0x01, 0x01 };
		Assert.Contains("0x101", TpmRandom.Parse(refused).Error);
		var (bytes, error) = TpmRandom.Get(16);    // this machine: bytes, or why not
		Assert.True(bytes?.Length == 16 || error != null);
		output.WriteLine(bytes != null ? "TPM: " + Convert.ToHexString(bytes) : "TPM: " + error);
	}

	[Fact]
	public void BeaconAnswersAreRead()
	{
		var nist = RandomBeacons.ParseNist("{\"pulse\":{\"chainIndex\":2,\"pulseIndex\":2024851,"
			+ "\"timeStamp\":\"2026-11-12T17:53:00.000Z\",\"outputValue\":\"ABCD\"}}");
		Assert.Equal((2L, 2024851L, "2026-11-12 17:53 UTC", "ABCD"), nist);
		Assert.Equal((6635836L, "c6d7"), RandomBeacons.ParseDrand("{\"round\":6635836,\"randomness\":\"c6d7\"}"));
		Assert.Throws<FormatException>(() => RandomBeacons.ParseDrand("{\"round\":1}"));
	}

	// Goes on the network: only with SOFTCALC_NET_TESTS=1
	[Fact]
	public async Task BeaconsAnswerWhenAsked()
	{
		if (Environment.GetEnvironmentVariable("SOFTCALC_NET_TESTS") != "1")
		{
			return;
		}
		foreach (string id in new[] { "nist", "drand" })
		{
			var (samples, error) = await RandomBeacons.GetAsync(id, 3);
			Assert.Null(error);
			Assert.Equal(3, samples!.Count);
			Assert.All(samples, s => Assert.Equal(0, s.Bits));
			output.WriteLine($"{id}: " + string.Join(" | ", samples.Select(s => s.Raw)));
		}
	}

	private static T Field<T>(RandomWindow window, string name)
	{
		return (T)typeof(RandomWindow).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
			.GetValue(window)!;
	}

	private static object? Call(RandomWindow window, string name, params object[] args)
	{
		return typeof(RandomWindow).GetMethod(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
			.Invoke(window, args);
	}

	[Fact]
	public async Task TabGetsComparesSeedsAndMixes()
	{
		var prefs = new RandomPrefs { EntropySource = "clock" };
		using var window = new RandomWindow(prefs, _ => { });
		Assert.Equal("Entropy", Field<TabControl>(window, "_tabs").TabPages[3].Text);
		await (Task)Call(window, "GetEntropy", 10)!;
		var rows = Field<ListView>(window, "_entropyRows");
		Assert.Equal(10, rows.Items.Count);
		Assert.Equal("", rows.Items[0].SubItems[3].Text);    // nothing before the first
		Assert.StartsWith("+", rows.Items[1].SubItems[3].Text);    // the clock moved on a little
		Assert.Contains("of 128", rows.Items[1].SubItems[5].Text);
		Assert.Contains("on average", Field<Label>(window, "_entropyNote").Text);

		Call(window, "UseAsSeed");
		Assert.Equal(rows.Items[9].SubItems[2].Text, Field<TextBox>(window, "_seed").Text);
		Call(window, "MixIntoSystem");
		Assert.StartsWith("System + entropy", Field<Label>(window, "_mixed").Text);
		Assert.Equal(10, Field<MixedRandom>(window, "_secure").Mixes);
		window.SavePrefs();
		Assert.Equal("clock", prefs.EntropySource);
	}

	[Fact]
	public async Task NewSeedFollowsTheEntropyTabOnlyThere()
	{
		using var window = new RandomWindow(new RandomPrefs { EntropySource = "clock" }, _ => { });
		var tabs = Field<TabControl>(window, "_tabs");
		_ = tabs.Handle;
		var seed = Field<TextBox>(window, "_seed");
		var rows = Field<ListView>(window, "_entropyRows");
		await (Task)Call(window, "NewSeed")!;    // Simple tab: the system generator, no row
		Assert.Matches(@"^[0-9]{1,39}$", seed.Text);
		Assert.Empty(rows.Items);
		tabs.SelectedIndex = 3;
		await (Task)Call(window, "NewSeed")!;    // Entropy tab: Get from the clock, then Use as Seed
		Assert.Single(rows.Items);
		Assert.Equal(rows.Items[0].SubItems[2].Text, seed.Text);
		Field<ComboBox>(window, "_entropySource").SelectedIndex = Array.FindIndex(Entropy.Sources, s => s.Id == "dice");
		string kept = seed.Text;
		await (Task)Call(window, "NewSeed")!;    // no dice typed: the seed stays
		Assert.Equal(kept, seed.Text);
		Assert.Contains("dice", Field<Label>(window, "_entropyNote").Text);
	}

	[Fact]
	public void EntropyPrefsAreSanitized()
	{
		string path = Path.Combine(Path.GetTempPath(), "softcalc-entropy-" + Guid.NewGuid().ToString("N") + ".json");
		try
		{
			File.WriteAllText(path, "{\"random\":{\"tab\":\"entropy\",\"entropy_source\":\"bogus\"}}");
			var r = SettingsStore.Load(path).Settings.Random;
			Assert.Equal(("entropy", "system"), (r.Tab, r.EntropySource));
		}
		finally
		{
			File.Delete(path);
		}
	}
}
