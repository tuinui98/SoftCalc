// HintLinesTests.cs - One or two hint lines (owner 2026-11-26)

using SoftCalc.Logic;

namespace SoftCalc.Tests;

public class HintLinesTests
{
	[Theory]
	[InlineData(500, 600, 1)]
	[InlineData(500, 500, 1)]
	[InlineData(500, 499, 2)]
	public void TwoLinesWhenTheLineHoldsFewerThanMinChars(int sampleWidth, int room, int lines)
	{
		Assert.Equal(lines, HintLines.For(sampleWidth, room));
	}

	[Fact]
	public void TheSampleIsMinCharsLong()
	{
		Assert.Equal(HintLines.MinChars, HintLines.Sample.Length);
	}
}
