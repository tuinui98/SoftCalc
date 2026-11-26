// LineTimeTests.cs - How long a line took, in the hint line (owner 2026-11-24)

using SoftCalc.Logic;

namespace SoftCalc.Tests;

public class LineTimeTests
{
	[Fact]
	public void TimesReadAsOnTheWeb()
	{
		Assert.Equal("Finished in 0.23 s", LineTime.Took(TimeSpan.FromMilliseconds(234)));
		Assert.Equal("Finished in 9.99 s", LineTime.Took(TimeSpan.FromMilliseconds(9994)));
		Assert.Equal("Finished in 12.5 s", LineTime.Took(TimeSpan.FromMilliseconds(12470)));
		Assert.Equal("Working... 1.4 s", LineTime.Working(TimeSpan.FromMilliseconds(1440)));
		Assert.Equal(TimeSpan.FromSeconds(1), LineTime.WorkingAfter);
	}
}
