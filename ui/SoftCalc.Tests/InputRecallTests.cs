// InputRecallTests.cs - Arrow Up/Down history walk with draft restore

using SoftCalc.Logic;

namespace SoftCalc.Tests;

public class InputRecallTests
{
	private readonly List<string> _inputs = new() { "1+1", "x=5", "2x" };

	private InputRecall Make()
	{
		return new InputRecall(() => _inputs);
	}

	[Fact]
	public void UpWalksBackFromNewest()
	{
		var recall = Make();
		Assert.Equal("2x", recall.Up("draft"));
		Assert.Equal("x=5", recall.Up("2x"));
		Assert.Equal("1+1", recall.Up("x=5"));
		Assert.Null(recall.Up("1+1"));    // oldest reached
	}

	[Fact]
	public void DownPastNewestRestoresDraft()
	{
		var recall = Make();
		recall.Up("typing 12");
		recall.Up("2x");
		Assert.Equal("2x", recall.Down());
		Assert.Equal("typing 12", recall.Down());
		Assert.Null(recall.Down());       // back at the draft, no walk
	}

	[Fact]
	public void DownWithoutWalkingDoesNothing()
	{
		Assert.Null(Make().Down());
	}

	[Fact]
	public void ResetStartsANewWalkFromNewest()
	{
		var recall = Make();
		recall.Up("");
		recall.Up("2x");
		recall.Reset();
		_inputs.Add("3*3");
		Assert.Equal("3*3", recall.Up("next draft"));
	}

	[Fact]
	public void ClickedLineIsWhereUpAndDownGoOn()
	{
		var recall = Make();
		Assert.Equal("x=5", recall.Jump(1, "typing"));
		Assert.Equal(1, recall.Position);
		Assert.Equal("1+1", recall.Up("x=5"));
		Assert.Equal("x=5", recall.Down());
		Assert.Equal("2x", recall.Down());
		Assert.Equal("typing", recall.Down());    // past the newest: what was typed before the click
		Assert.Equal(-1, recall.Position);
	}

	[Fact]
	public void EscapeBringsBackTheDraft()
	{
		var recall = Make();
		recall.Up("draft");
		recall.Jump(0, "2x");    // a click while walking keeps the first draft
		Assert.Equal("draft", recall.Cancel());
		Assert.Equal(-1, recall.Position);
		Assert.Null(recall.Cancel());    // not walking
	}

	[Fact]
	public void TypingEndsTheWalk()
	{
		var recall = Make();
		recall.Up("");
		recall.Edited();
		Assert.Equal(-1, recall.Position);
		Assert.Equal("2x", recall.Up("2x and more"));    // a new walk from the newest
		Assert.Equal("2x and more", recall.Cancel());
	}

	[Fact]
	public void EmptyHistoryGivesNothing()
	{
		_inputs.Clear();
		Assert.Null(Make().Up("x"));
	}
}
