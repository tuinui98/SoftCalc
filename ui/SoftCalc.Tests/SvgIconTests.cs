// SvgIconTests.cs - Own ribbon icons: the SVG renderer, its refusals, and saving the icon with the layout

using SoftCalc.Logic;
using SoftCalc.Persistence;
using SoftCalc.UI;

namespace SoftCalc.Tests;

public sealed class SvgIconTests
{
	private const string Head = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 16 16\"";

	// The outline trash icon as iconmcp.com's Copy SVG gives it
	private const string OutlineTrash = Head + " fill=\"none\" stroke=\"currentColor\" stroke-width=\"0.833\" stroke-linecap=\"round\" stroke-linejoin=\"round\">"
		+ "<g><path d=\"M3.5 4.25 L4.105 11.907 C4.187 12.948 5.055 13.75 6.098 13.75 L9.902 13.75 C10.945 13.75 11.813 12.948 11.895 11.907 L12.5 4.25\"/>"
		+ "<line x1=\"2.25\" y1=\"4.25\" x2=\"13.75\" y2=\"4.25\"/>"
		+ "<path d=\"M6 4.25 L6 3.25 C6 2.698 6.448 2.25 7 2.25 L9 2.25 C9.552 2.25 10 2.698 10 3.25 L10 4.25\"/>"
		+ "<line x1=\"6.75\" y1=\"7\" x2=\"6.75\" y2=\"11\"/><line x1=\"9.25\" y1=\"7\" x2=\"9.25\" y2=\"11\"/></g></svg>";

	private static Bitmap Draw(string body, string rootAttributes = " fill=\"none\" stroke=\"currentColor\"", int size = 16)
	{
		return SvgIcon.FromText(Head + rootAttributes + ">" + body + "</svg>", size, Color.Black);
	}

	private static bool Inked(Bitmap bitmap, int x, int y)
	{
		return bitmap.GetPixel(x, y).A > 128;
	}

	private static int InkCount(Bitmap bitmap)
	{
		int inked = 0;
		for (int y = 0; y < bitmap.Height; y++)
		{
			for (int x = 0; x < bitmap.Width; x++)
			{
				if (Inked(bitmap, x, y))
				{
					inked++;
				}
			}
		}
		return inked;
	}

	[Fact]
	public void OutlineIconFromTheSiteDraws()
	{
		Assert.Null(SvgIcon.Problem(OutlineTrash));
		Assert.InRange(InkCount(SvgIcon.FromText(OutlineTrash, 20, Color.Black)), 15, 300);
	}

	[Fact]
	public void EvenOddLeavesTheHoleOpen()
	{
		const string square = "<path d=\"M2 2 H14 V14 H2 Z M5 5 H11 V11 H5 Z\" fill=\"currentColor\" stroke=\"none\"";
		var evenOdd = Draw(square + " fill-rule=\"evenodd\"/>");
		var nonZero = Draw(square + "/>");
		Assert.True(Inked(evenOdd, 3, 8));
		Assert.False(Inked(evenOdd, 8, 8));
		Assert.True(Inked(nonZero, 8, 8));    // SVG's default rule fills it
	}

	[Fact]
	public void ArcIsTheUpperHalfCircle()
	{
		var arc = Draw("<path d=\"M2 8 A6 6 0 0 1 14 8\"/>", " fill=\"none\" stroke=\"currentColor\" stroke-width=\"1\"", 32);
		Assert.True(Inked(arc, 16, 4));      // top of a circle of radius 12 px around (16, 16)
		Assert.False(Inked(arc, 16, 28));    // not the lower half
		Assert.True(Inked(arc, 8, 7) || Inked(arc, 7, 7) || Inked(arc, 8, 8));    // 45 degrees: (16-8.5, 16-8.5)
		var lower = Draw("<path d=\"M2 8 A6 6 0 0 0 14 8\"/>", " fill=\"none\" stroke=\"currentColor\" stroke-width=\"1\"", 32);
		Assert.True(Inked(lower, 16, 28));
	}

	[Fact]
	public void RelativeAndShortFormsMatchTheLongForms()
	{
		const string fill = " fill=\"currentColor\" stroke=\"none\"";
		var absolute = Draw("<path d=\"M2 2 L14 2 L14 14 L2 14 Z\"/>", fill);
		var relative = Draw("<path d=\"m2 2 h12 v12 h-12 z\"/>", fill);
		var polygon = Draw("<polygon points=\"2,2 14,2 14,14 2,14\"/>", fill);
		Assert.Equal(InkCount(absolute), InkCount(relative));
		Assert.Equal(InkCount(absolute), InkCount(polygon));
		Assert.InRange(InkCount(Draw("<path d=\"M2 8 Q8 0 14 8 T26 8\"/>")), 10, 200);
		Assert.InRange(InkCount(Draw("<path d=\"M2 8 C4 2 6 2 8 8 S12 14 14 8\"/>")), 10, 200);
		Assert.Null(SvgIcon.Problem(Head + "><path d=\"M0 0a1 1 0 011 1\"/></svg>"));    // touching arc flags
	}

	[Fact]
	public void GroupsPassTheirPaintAndTransformDown()
	{
		var moved = Draw("<g transform=\"translate(8 0)\" fill=\"currentColor\" stroke=\"none\"><rect x=\"0\" y=\"0\" width=\"8\" height=\"16\"/></g>");
		Assert.False(Inked(moved, 3, 8));
		Assert.True(Inked(moved, 12, 8));
		var styled = Draw("<rect x=\"2\" y=\"2\" width=\"12\" height=\"12\" style=\"fill:currentColor; stroke:none\"/>");
		Assert.True(Inked(styled, 8, 8));
	}

	[Fact]
	public void NumbersAreReadTheWaySvgWritesThem()
	{
		Assert.Equal(new[] { 1.5f, 0.5f, -20f, 3f }, SvgIcon.Numbers("1.5.5-2e1,3"));
	}

	[Theory]
	[InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><text>A</text></svg>", "can't draw: text")]
	[InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><image href=\"x.png\"/></svg>", "can't draw: image")]
	[InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M0 0 Z 4 4\"/></svg>", "can't draw: path data")]
	[InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><g transform=\"perspective(2)\"><path d=\"M0 0 L1 1\"/></g></svg>", "can't draw: transform")]
	[InlineData("<!DOCTYPE svg [<!ENTITY a \"b\">]><svg>&a;</svg>", "not SVG")]
	[InlineData("<html><body/></html>", "no <svg>")]
	[InlineData("copied text", "not SVG")]
	public void WhatCannotBeDrawnIsRefused(string svg, string message)
	{
		Assert.Contains(message, SvgIcon.Problem(svg));
	}

	[Fact]
	public void TooBigIsRefused()
	{
		string big = Head + "><desc>" + new string('x', SvgIcon.MaxLength) + "</desc></svg>";
		Assert.Contains("too big", SvgIcon.Problem(big));
		Assert.False(IconPicker.LooksLikeSvg(big));
		Assert.True(IconPicker.LooksLikeSvg(OutlineTrash));
	}

	[Fact]
	public void BuiltInIconsAreListedAndTheirTextDraws()
	{
		var names = SvgIcon.BuiltIn();
		Assert.Contains("trash", names);
		Assert.Equal(14, names.Count);
		Assert.All(names, n => Assert.Null(SvgIcon.Problem(SvgIcon.EmbeddedText(n))));
	}

	[Fact]
	public void OwnIconIsSavedWithTheButton()
	{
		string path = Path.Combine(Path.GetTempPath(), "softcalc-icon-" + Guid.NewGuid().ToString("N") + ".json");
		try
		{
			var layout = RibbonLayout.Default();
			layout[0].Items[0].IconSvg = OutlineTrash;
			SettingsStore.SaveRibbon(path, RibbonLayout.ToSaved(layout));
			Assert.Contains("\"icon_svg\"", File.ReadAllText(path));
			var loaded = SettingsStore.Load(path).Settings.Ribbon!;
			Assert.Equal(OutlineTrash, loaded[0].Items[0].IconSvg);
			Assert.Null(loaded[0].Items[1].IconSvg);
			Assert.Equal(OutlineTrash, RibbonLayout.Clone(loaded)[0].Items[0].IconSvg);
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void SanitizeDropsAnEmptyIcon()
	{
		var layout = RibbonLayout.Default();
		layout[0].Items[0].IconSvg = "  ";
		RibbonLayout.Sanitize(layout);
		Assert.Null(layout[0].Items[0].IconSvg);
		Assert.True(RibbonLayout.SameAs(layout, RibbonLayout.Default()));
	}
}
