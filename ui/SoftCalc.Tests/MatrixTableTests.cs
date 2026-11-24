// MatrixTableTests.cs - A matrix result as a History table (matrix round 2 D)

using SoftCalc.Logic;

namespace SoftCalc.Tests;

public class MatrixTableTests
{
	[Fact]
	public void MatrixTextBecomesRowsOfCells()
	{
		var table = MatrixTable.Parse("[[1, -2.5], [(3-4i), 4]]")!;
		Assert.Equal("2x2", table.Size);
		Assert.Equal(new[] { "(3-4i)", "4" }, table.Cells[1]);
		Assert.Equal("1\t-2.5\r\n(3-4i)\t4", table.Tsv());
	}

	[Fact]
	public void PreviewLinesUpTheCells()
	{
		Assert.Equal("2x2\r\n  1       -2.5\r\n100   0.333333",
			MatrixTable.Parse("[[1, -2.5], [100, 0.33333333333333333]]")!.Preview());
	}

	[Theory]
	[InlineData("[1, 2, 3]")]    // a list stays one line
	[InlineData("5")]
	[InlineData("ERROR:Sizes do not fit")]
	[InlineData("[[1, 2], [3]]")]
	public void AnythingButAMatrixHasNoTable(string text)
	{
		Assert.Null(MatrixTable.Parse(text));
	}

	[Theory]
	[InlineData("2.1380899352993950774764278470380", "2.13809")]
	[InlineData("-1.3292049616611459904487618767859e68", "-1.3292e68")]
	[InlineData("6.9852941176470588235294117647059e-4", "0.000698529")]
	[InlineData("(-1.92-15.221077491426157245651276015896i)", "(-1.92-15.2211i)")]
	[InlineData("0.33333333333333333333333333333333i", "0.333333i")]
	[InlineData("123456", "123456")]
	[InlineData("0x1F", "0x1F")]
	public void CellsShortenToSixDigits(string cell, string shown)
	{
		Assert.Equal(shown, MatrixTable.Short(cell));
	}
}
