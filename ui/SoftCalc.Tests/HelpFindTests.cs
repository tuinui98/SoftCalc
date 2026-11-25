// HelpFindTests.cs - `?word` in Input finds a Help heading (owner 2026-11-26)

using SoftCalc.Logic;

namespace SoftCalc.Tests;

public class HelpFindTests
{
	[Theory]
	[InlineData("Settings typed in Input", "settings-typed-in-input")]
	[InlineData("Operators, strongest first", "operators-strongest-first")]
	[InlineData("Two's Complement", "two-s-complement")]
	[InlineData("  Lists & matrices ", "lists-matrices")]
	public void SlugIsLowerCaseWordsJoinedByDashes(string heading, string slug)
	{
		Assert.Equal(slug, HelpFind.Slug(heading));
	}

	[Theory]
	[InlineData("?det", true, "det")]
	[InlineData("  ?  Compound   Interest ", true, "compound interest")]
	[InlineData("?", true, "")]
	[InlineData("2?3", false, "")]
	public void AHelpLineStartsWithAQuestionMark(string text, bool help, string query)
	{
		Assert.Equal(help, HelpFind.IsHelpLine(text));
		if (help)
		{
			Assert.Equal(query, HelpFind.Query(text));
		}
	}

	[Fact]
	public void AHeadingWinsThenTheMostCode()
	{
		var topics = new[]
		{
			new HelpTopic("01-a", "A", "# A\n\n`det(m)` once\n\n## Seeds\n\n```calc\nrands(42)\n```\n"),
			new HelpTopic("02-b", "B", "# B\n\n## Matrix work\n\n`det` `det` and `det(m)`\n\n### Random seeds\n\ntext\n"),
			new HelpTopic("16-errors", "E", "# E\n\n## Odd\n\n`det` `det` `det` `det` `det` `rands`\n\n## Note: when det is 0\n\n```calc\ndet(m) => 0\n```\n"),
		};
		Assert.Equal(new HelpSpot("01-a", "seeds"), HelpFind.Find(topics, "seeds"));
		Assert.Equal(new HelpSpot("02-b", "matrix-work"), HelpFind.Find(topics, "det"));
		Assert.Equal(new HelpSpot("01-a", "seeds"), HelpFind.Find(topics, "rands"));
		Assert.Equal(new HelpSpot("02-b", ""), HelpFind.Find(topics, "b"));
		Assert.Null(HelpFind.Find(topics, "nothing"));
		Assert.Null(HelpFind.Find(topics, "de"));    // whole words only
		Assert.Equal(new HelpSpot("16-errors", "odd"), HelpFind.Find(topics, "odd"));    // Error messages: headings only
	}

	[Theory]
	[InlineData("det", "19-matrices")]
	[InlineData("rands", "17-random")]
	[InlineData("fv", "09-finance")]
	[InlineData("precision", "02-numbers-operators")]
	[InlineData("rpn", "10-rpn")]
	[InlineData("ribbon", "12-ribbon")]
	[InlineData("inv", "19-matrices")]
	[InlineData("seeds", "17-random")]
	[InlineData("compound interest", "09-finance")]
	public void TheRealHelpFindsTheTopic(string query, string topic)
	{
		var spot = HelpFind.Find(HelpTopics.All, query);
		Assert.NotNull(spot);
		Assert.Equal(topic, spot.TopicId);
	}

	[Fact]
	public void EveryHeadingHasAnAnchorOfItsOwnInItsTopic()
	{
		foreach (var topic in HelpTopics.All)
		{
			var anchors = HelpFind.Sections(topic.Id, topic.Markdown).Select(section => section.Anchor).ToList();
			Assert.Equal(anchors.Count, anchors.Distinct().Count());
			Assert.All(anchors.Skip(1), anchor => Assert.NotEqual("", anchor));
		}
	}

	[Fact]
	public void TheAnchorIsTheHeadingsId()
	{
		string html = HelpMarkdown.ToHtml("# T\n\n## Settings typed in Input\n\ntext", _ => null, _ => null);
		Assert.Contains("<h2 id=\"settings-typed-in-input\">", html);
		Assert.Contains("<h1>", html);
	}
}
