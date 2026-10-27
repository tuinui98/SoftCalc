// HistoryViewTests.cs - When the numeric form appears on the right, and what Ctrl+C copies

using SoftCalc.UI;

namespace SoftCalc.Tests;

public class HistoryViewTests
{
	[Theory]
	[InlineData("result", "x*2", "5*2", true)]      // variable substituted
	[InlineData("result", "1+2", "1+2", false)]     // nothing substituted: no right side
	[InlineData("definition", "x=5", "x=5", false)]
	[InlineData("error", "y*2", "y*2", false)]
	public void RightSideOnlyWhenSubstituted(string kind, string input, string display, bool expected)
	{
		Assert.Equal(expected, new HistoryEntry(kind, input, display, "10").HasSubstitution);
	}

	[Fact]
	public void CopyTakesInputAndResult()
	{
		var entry = new HistoryEntry("result", "x*2", "5*2", "10");
		Assert.Equal("x*2\r\n\t10", HistoryView.CopyText(entry));
	}
}
