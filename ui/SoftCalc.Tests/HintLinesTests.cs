// HintLinesTests.cs - One or two hint lines (owner 2026-11-26)

using SoftCalc.Logic;

namespace SoftCalc.Tests;

public class HintLinesTests
{
	[Theory]
	[InlineData(400, 500, 1)]    // all of it fits
	[InlineData(400, 300, 1)]    // three quarters fit: one line, cut at the end
	[InlineData(400, 299, 2)]    // less than three quarters: two lines
	[InlineData(0, 100, 1)]    // no hint
	public void TwoLinesWhenOneShowsLessThanThreeQuarters(int textWidth, int room, int lines)
	{
		Assert.Equal(lines, HintLines.For(textWidth, room));
	}
}
