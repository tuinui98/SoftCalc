// RandomToolsTests.cs - The Random window's generators and settings (design/random_plan.md R1-R2)

using SoftCalc.Logic;
using SoftCalc.Persistence;
using SoftCalc.UI;

namespace SoftCalc.Tests;

public sealed class RandomToolsTests
{
	// Gives the numbers it was made with, in turn (each taken modulo the bound)
	private sealed class Fixed(params ulong[] values) : IRandomSource
	{
		private int _at;

		public ulong Below(ulong bound)
		{
			return values[_at++ % values.Length] % bound;
		}
	}

	private static readonly IRandomSource Secure = new SecureRandom();

	[Fact]
	public void UnbiasedDropsTheUnevenTop()
	{
		var draws = new Queue<ulong>(new[] { ulong.MaxValue, 7UL });    // MaxValue is in the dropped top for bound 10
		Assert.Equal(7UL, RandomTools.Unbiased(10, draws.Dequeue));
		Assert.Throws<ArgumentOutOfRangeException>(() => RandomTools.Unbiased(0, () => 1));
	}

	[Fact]
	public void IntegersIncludeBothEnds()
	{
		var (values, error) = RandomTools.Integers(Secure, 1, 6, 600, noRepeats: false, sort: false);
		Assert.Null(error);
		Assert.Equal(new long[] { 1, 2, 3, 4, 5, 6 }, values!.Distinct().Order());
		var (swapped, _) = RandomTools.Integers(new Fixed(0, 9), 10, 1, 2, false, false);
		Assert.Equal(new long[] { 1, 10 }, swapped);
	}

	[Fact]
	public void NoRepeatsAreDistinctAndRefuseTooMany()
	{
		var (values, _) = RandomTools.Integers(Secure, 1, 10, 10, noRepeats: true, sort: true);
		Assert.Equal(Enumerable.Range(1, 10).Select(i => (long)i), values);
		var (none, error) = RandomTools.Integers(Secure, 1, 10, 11, noRepeats: true, sort: false);
		Assert.Null(none);
		Assert.Contains("Only 10", error);
	}

	[Fact]
	public void DecimalsKeepThePlacesAndTheRange()
	{
		var (values, error) = RandomTools.Decimals(Secure, 1, 2, 2, 500, sort: false);
		Assert.Null(error);
		Assert.All(values!, v => Assert.InRange(v, 1m, 2m));
		Assert.All(values!, v => Assert.Equal(v, decimal.Round(v, 2)));
		var (ends, _) = RandomTools.Decimals(new Fixed(0, 100), 1, 2, 2, 2, false);
		Assert.Equal(new[] { 1m, 2m }, ends);    // 101 steps of 0.01: both ends can come up
		Assert.NotNull(RandomTools.Decimals(Secure, 0, 1, 11, 1, false).Error);
	}

	[Fact]
	public void CoinFollowsTheBias()
	{
		Assert.All(RandomTools.Coins(Secure, 100, 200), Assert.True);
		Assert.All(RandomTools.Coins(Secure, 0, 200), Assert.False);
		int heads = RandomTools.Coins(Secure, 70, 20000).Count(h => h);
		Assert.InRange(heads, 13600, 14400);    // 70% give or take 3%
		Assert.Equal(new[] { true, false }, RandomTools.Coins(new Fixed(4999, 5000), 50, 2));
	}

	[Theory]
	[InlineData("whole", 1, 100, "rand(1, 100)")]
	[InlineData("whole", 10, -5, "rand(-5, 10)")]
	[InlineData("decimal", 1, 2, "round(rand(1.0, 2), 2)")]
	[InlineData("decimal", 0.5, 2, "round(rand(0.5, 2), 2)")]
	[InlineData("dice", 0, 0, "rand(1, 6)")]
	public void CommandDrawsTheSameKind(string kind, double from, double to, string want)
	{
		Assert.Equal(want, RandomTools.Command(kind, (decimal)from, (decimal)to, 2, 50, 6));
	}

	[Fact]
	public void CoinCommandIsOneForHeads()
	{
		Assert.Equal("round(rand())", RandomTools.Command("coin", 0, 0, 0, 50, 6));
		Assert.Equal("floor(rand()+0.7)", RandomTools.Command("coin", 0, 0, 0, 70, 6));
		Assert.Equal("floor(rand()+0)", RandomTools.Command("coin", 0, 0, 0, 0, 6));
		Assert.Equal("floor(rand()+1)", RandomTools.Command("coin", 0, 0, 0, 100, 6));
	}

	[Fact]
	public void CoinWordsDefaultToHeadAndTail()
	{
		Assert.Equal(("Head", "Tail"), (RandomTools.DefaultHead, RandomTools.DefaultTail));
		var prefs = new RandomPrefs { Kind = "coin", HeadWord = "Yes", TailWord = "No" };
		using var window = new RandomWindow(prefs, _ => { });
		window.SavePrefs();
		Assert.Equal(("Yes", "No"), (prefs.HeadWord, prefs.TailWord));
	}

	[Fact]
	public void DiceRollOneToSides()
	{
		var rolls = RandomTools.Dice(Secure, 6, 600);
		Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, rolls.Distinct().Order());
	}

	private static PasswordOptions All(int length = 16, bool noLookAlikes = false)
	{
		return new PasswordOptions(length, true, true, true, true, RandomTools.DefaultSymbols, noLookAlikes);
	}

	[Fact]
	public void PasswordHasEveryChosenKind()
	{
		for (int i = 0; i < 200; i++)
		{
			var (password, error) = RandomTools.Password(Secure, All(4));
			Assert.Null(error);
			Assert.Equal(4, password!.Length);
			Assert.Contains(password, char.IsUpper);
			Assert.Contains(password, char.IsLower);
			Assert.Contains(password, char.IsDigit);
			Assert.Contains(password, c => RandomTools.DefaultSymbols.Contains(c));
		}
	}

	[Fact]
	public void LookAlikesAreLeftOut()
	{
		for (int i = 0; i < 100; i++)
		{
			var (password, _) = RandomTools.Password(Secure, All(64, noLookAlikes: true));
			Assert.DoesNotContain(password!, c => RandomTools.LookAlikes.Contains(c));
		}
	}

	[Fact]
	public void PasswordRefusesWhatCannotBeMade()
	{
		var none = new PasswordOptions(16, false, false, false, false, "", false);
		Assert.Contains("at least one kind", RandomTools.Password(Secure, none).Error);
		Assert.Contains("at least 4", RandomTools.Password(Secure, All(3)).Error);
		var emptySymbols = new PasswordOptions(8, false, false, false, true, "  ", false);
		Assert.NotNull(RandomTools.Password(Secure, emptySymbols).Error);
	}

	[Fact]
	public void SymbolGroupTickRemovesThenAppends()
	{
		Assert.Equal("!@#$%&*?-_", RandomTools.ToggleSymbols("!@#$%&*?", "-_", on: true));
		Assert.Equal("!@#$%&*?-_", RandomTools.ToggleSymbols("-!@#$%&*?", "-_", on: true));    // a hand-typed - is not doubled
		Assert.Equal("!@#", RandomTools.ToggleSymbols("!-@_#", "-_", on: false));
		var all = string.Concat(RandomTools.SymbolGroups.Select(g => g.Chars));
		Assert.Equal(all.Length, all.Distinct().Count());    // no symbol in two groups
		Assert.Equal(string.Concat(RandomTools.SymbolGroups.Take(5).Select(g => g.Chars)), RandomTools.DefaultSymbols);
		Assert.DoesNotContain('"', RandomTools.DefaultSymbols);    // quotes and slashes start unticked
	}

	[Fact]
	public void SymbolGroupsFollowTheBox()
	{
		var prefs = new RandomPrefs { SymbolSet = "-_!@#$%&*?" };
		using var window = new RandomWindow(prefs, _ => { });
		var groups = (List<(CheckBox Box, string Chars)>)typeof(RandomWindow)
			.GetField("_symbolGroups", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(window)!;
		Assert.Equal(new[] { true, true, false, false, false, false, false }, groups.Select(g => g.Box.Checked));
		groups[2].Box.Checked = true;    // Maths
		window.SavePrefs();
		Assert.Equal("-_!@#$%&*?+=^~", prefs.SymbolSet);
		groups[0].Box.Checked = false;    // Common
		window.SavePrefs();
		Assert.Equal("-_+=^~", prefs.SymbolSet);
	}

	[Fact]
	public void StrengthIsLengthTimesLog2Pool()
	{
		var digits = new PasswordOptions(10, false, false, true, false, "", false);
		Assert.Equal(10, RandomTools.PoolSize(digits));
		Assert.Equal(10 * Math.Log2(10), RandomTools.StrengthBits(digits), 9);
		Assert.Equal(26 + 26 + 10 + RandomTools.DefaultSymbols.Length, RandomTools.PoolSize(All()));
		Assert.Equal(26 + 26 + 10 + RandomTools.DefaultSymbols.Length - 6, RandomTools.PoolSize(All(noLookAlikes: true)));
	}

	[Fact]
	public void SignatureIsWrittenInAndCountsInLength()
	{
		var options = All(16);
		var end = RandomTools.Signed(Secure, options, "Tui", "end").Password!;
		Assert.Equal(16, end.Length);
		Assert.EndsWith("Tui", end);
		Assert.StartsWith("Tui", RandomTools.Signed(Secure, options, "Tui", "start").Password!);
		for (int i = 0; i < 50; i++)
		{
			Assert.Contains("Tui", RandomTools.Signed(Secure, options, "Tui", "anywhere").Password!);
		}
		var (atFront, _) = RandomTools.Signed(new Fixed(0), new PasswordOptions(6, false, false, true, false, "", false), "ab", "anywhere");
		Assert.Equal("ab0000", atFront);    // place 0 drawn: the very start
		Assert.Equal(13, RandomTools.RandomPart(options, "Tui").Length);    // strength counts 13 random characters
		Assert.Equal(16, RandomTools.Signed(Secure, options, "", "end").Password!.Length);    // no signature: all random
	}

	[Fact]
	public void SignatureLeavesRoomForEveryKind()
	{
		var (none, error) = RandomTools.Signed(Secure, All(6), "abc", "end");
		Assert.Null(none);
		Assert.Contains("at least 7", error);
		Assert.NotNull(RandomTools.Signed(Secure, All(7), "abc", "end").Password);
	}

	[Fact]
	public void PrefsRoundTripAndAreSanitized()
	{
		string path = Path.Combine(Path.GetTempPath(), "softcalc-random-" + Guid.NewGuid().ToString("N") + ".json");
		try
		{
			var settings = new AppSettings();
			settings.Random.Kind = "dice";
			settings.Random.Sides = 20;
			settings.Random.PasswordLength = 32;
			settings.Random.Tab = "password";
			SettingsStore.Save(path, settings);
			Assert.DoesNotContain("signature\":", File.ReadAllText(path));    // only signature_place
			var loaded = SettingsStore.Load(path).Settings.Random;
			Assert.Equal(("dice", 20, 32, "password"), (loaded.Kind, loaded.Sides, loaded.PasswordLength, loaded.Tab));

			File.WriteAllText(path, "{\"random\":{\"kind\":\"bogus\",\"sides\":1,\"password_length\":999,\"count\":0}}");
			var fixedUp = SettingsStore.Load(path).Settings.Random;
			Assert.Equal(("whole", 2, 128, 1), (fixedUp.Kind, fixedUp.Sides, fixedUp.PasswordLength, fixedUp.Count));
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void WindowOpensSavesSettingsAndHasItsHelpTopic()
	{
		var prefs = new RandomPrefs { Kind = "coin", HeadsPercent = 70 };
		string? inserted = null;
		using var window = new RandomWindow(prefs, text => inserted = text);
		Assert.Equal("random", HelpWindow.TopicFor(window));
		Assert.Equal("17-random", HelpTopics.Find("random").Id);
		window.SavePrefs();
		Assert.Equal(("coin", 70m), (prefs.Kind, prefs.HeadsPercent));
		Assert.Null(inserted);
	}
}
