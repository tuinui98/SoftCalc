// RibbonTests.cs - Ribbon pieces: decimal steps and sample, embedded icons, the ShowRibbon setting

using SoftCalc.Logic;
using SoftCalc.Persistence;
using SoftCalc.UI;

namespace SoftCalc.Tests;

public sealed class RibbonTests
{
	[Theory]
	[InlineData(null, 1, 2)]      // from all digits, both buttons start at 2
	[InlineData(null, -1, 2)]
	[InlineData(2, 1, 3)]
	[InlineData(2, -1, 1)]
	[InlineData(0, -1, 0)]
	[InlineData(30, 1, 30)]
	public void DecimalsStep(int? current, int delta, int want)
	{
		Assert.Equal(want, DecimalSteps.Next(current, delta));
	}

	[Theory]
	[InlineData(null, "all")]
	[InlineData(0, "0")]
	[InlineData(2, "0.00")]
	[InlineData(6, "0.000000")]
	[InlineData(12, "0.000000 (12)")]
	public void DecimalsSample(int? decimals, string want)
	{
		Assert.Equal(want, DecimalSteps.Sample(decimals));
	}

	[Theory]
	[InlineData("trash")]
	[InlineData("save")]
	[InlineData("line-chart")]
	[InlineData("compound-interest")]
	[InlineData("pushpin")]
	[InlineData("decimal-increase")]
	[InlineData("decimal-decrease")]
	[InlineData("swap-horizontal")]
	[InlineData("calculator")]
	public void EmbeddedIconsDraw(string name)
	{
		var bitmap = SvgIcon.Get(name, 20, Color.Black);
		Assert.Equal(new Size(20, 20), bitmap.Size);
		int inked = 0;
		for (int y = 0; y < 20; y++)
		{
			for (int x = 0; x < 20; x++)
			{
				if (bitmap.GetPixel(x, y).A > 128)
				{
					inked++;
				}
			}
		}
		Assert.InRange(inked, 15, 300);    // something drawn, not a filled square
	}

	[Fact]
	public void ProgramIconLoads()
	{
		using var icon = SvgIcon.ProgramIcon();
		Assert.True(icon.Width > 0);
	}

	[Fact]
	public void ShowRibbonRoundTrips()
	{
		string path = Path.Combine(Path.GetTempPath(), "softcalc-ribbon-" + Guid.NewGuid().ToString("N") + ".json");
		try
		{
			SettingsStore.Save(path, new AppSettings { ShowRibbon = false });
			Assert.False(SettingsStore.Load(path).Settings.ShowRibbon);
			File.WriteAllText(path, "{}");
			Assert.True(SettingsStore.Load(path).Settings.ShowRibbon);    // older files: shown
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void TextSizeIsBaseTimesScale()
	{
		string path = Path.Combine(Path.GetTempPath(), "softcalc-text-" + Guid.NewGuid().ToString("N") + ".json");
		try
		{
			File.WriteAllText(path, "{}");
			var fresh = SettingsStore.Load(path).Settings;
			Assert.Equal((9f, 150), (fresh.TextFontBase, fresh.TextScale));    // 13.5 pt as before
			File.WriteAllText(path, "{\"text_font_size\":14}");
			var legacy = SettingsStore.Load(path).Settings;
			Assert.Equal((14f, 100), (legacy.TextFontBase, legacy.TextScale));    // old absolute size kept as 100%
			SettingsStore.Save(path, legacy);
			Assert.DoesNotContain("text_font_size", File.ReadAllText(path));
			File.WriteAllText(path, "{\"text_font_base\":12,\"text_scale\":130}");
			Assert.Equal(150, SettingsStore.Load(path).Settings.TextScale);    // only the menu's steps
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void ThemeRoundTripsAndFallsBack()
	{
		string path = Path.Combine(Path.GetTempPath(), "softcalc-theme-" + Guid.NewGuid().ToString("N") + ".json");
		try
		{
			SettingsStore.Save(path, new AppSettings { Theme = "dark" });
			Assert.Equal("dark", SettingsStore.Load(path).Settings.Theme);
			File.WriteAllText(path, "{}");
			Assert.Equal("system", SettingsStore.Load(path).Settings.Theme);    // older files: follow Windows
			File.WriteAllText(path, "{\"theme\":\"purple\"}");
			Assert.Equal("system", SettingsStore.Load(path).Settings.Theme);
		}
		finally
		{
			File.Delete(path);
		}
	}
}
