// RandomSeedTests.cs - The Random window's Source row and Seed against the real engine (design/random_plan.md R4)

using SoftCalc.Engine;
using SoftCalc.Persistence;
using SoftCalc.UI;

namespace SoftCalc.Tests;

public sealed class RandomSeedTests
{
	private static SeedEngine Seeds(int precision = 32)
	{
		return new SeedEngine(EngineLocator.Locate(Path.Combine(Path.GetTempPath(), "softcalc-test-engine")), () => precision);
	}

	private static T Field<T>(RandomWindow window, string name)
	{
		return (T)typeof(RandomWindow).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
			.GetValue(window)!;
	}

	private static Task Call(RandomWindow window, string name)
	{
		return (Task)typeof(RandomWindow).GetMethod(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
			.Invoke(window, null)!;
	}

	[Fact]
	public async Task SeedGivesWhatTypingInInputGives()
	{
		using var seeds = Seeds();
		var (texts, error) = await seeds.DrawsAsync("42", "randbeta(2, 5, 1, 10)", 8, false);
		Assert.Null(error);
		using var session = new EngineSession(EngineLocator.Locate(Path.Combine(Path.GetTempPath(), "softcalc-test-engine")));
		await session.StartAsync();
		await session.EvalAsync("rands(42)");
		var typed = new List<string>();
		for (int i = 0; i < 8; i++)
		{
			typed.Add((await session.EvalAsync("randbeta(2, 5, 1, 10)")).Text);
		}
		Assert.Equal(typed, texts);
		Assert.Equal(texts, (await seeds.DrawsAsync("42", "randbeta(2, 5, 1, 10)", 8, false)).Texts);    // again: the same
		Assert.Contains("Division", (await seeds.DrawsAsync("1", "1/0", 1, false)).Error);
	}

	[Fact]
	public async Task SeedFollowsThePrecision()
	{
		using var seeds = Seeds(precision: 64);
		var (texts, error) = await seeds.DrawsAsync("3", "rand()", 1, false);
		Assert.Null(error);
		Assert.InRange(texts![0].Length, 50, 70);    // 32 would give about 34 characters
	}

	[Fact]
	public void PasswordTabTurnsSeedOffAndBack()
	{
		using var window = new RandomWindow(new RandomPrefs(), _ => { }, Seeds());
		var tabs = Field<TabControl>(window, "_tabs");
		_ = tabs.Handle;    // the tab events need a window handle
		var seeded = Field<RadioButton>(window, "_seeded");
		var system = Field<RadioButton>(window, "_system");
		Assert.True(system.Checked);    // never saved: System on opening
		Field<TextBox>(window, "_seed").Text = "42";
		Assert.True(seeded.Checked);    // typing a seed picks Seed
		tabs.SelectedIndex = 2;
		Assert.True(system.Checked);
		Assert.False(seeded.Enabled);
		tabs.SelectedIndex = 1;
		Assert.True(seeded.Enabled);
		Assert.True(seeded.Checked);
	}

	[Fact]
	public async Task SeededTabsRepeatTheirDraws()
	{
		var prefs = new RandomPrefs { Kind = "whole", From = 1, To = 1000, Count = 5, Shape = "bell", ShapeCount = 20 };
		using var window = new RandomWindow(prefs, _ => { }, Seeds());
		Field<TextBox>(window, "_seed").Text = "7";
		var numbers = Field<ListBox>(window, "_numbers");
		await Call(window, "DrawNumbers");
		var first = numbers.Items.Cast<string>().ToList();
		await Call(window, "DrawNumbers");
		Assert.Equal(5, first.Count);
		Assert.Equal(first, numbers.Items.Cast<string>());
		Assert.Contains("rands(7)", Field<Label>(window, "_numbersNote").Text);

		var shaped = Field<ListBox>(window, "_shapeList");
		await Call(window, "DrawShaped");
		var once = shaped.Items.Cast<string>().ToList();
		await Call(window, "DrawShaped");
		Assert.Equal(20, once.Count);
		Assert.Equal(once, shaped.Items.Cast<string>());

		Field<TextBox>(window, "_seed").Text = "x";
		await Call(window, "DrawNumbers");
		Assert.Contains("Seed must be a number", Field<Label>(window, "_numbersNote").Text);
	}

	[Fact]
	public void WithoutAnEngineSeedIsOff()
	{
		using var window = new RandomWindow(new RandomPrefs(), _ => { });
		Assert.False(Field<RadioButton>(window, "_seeded").Enabled);
	}
}
