// HelpTests.cs - Help topics embedded in the exe and their Markdown as HTML (owner 2026-11-10)

using System.Text.RegularExpressions;
using SoftCalc.Logic;

namespace SoftCalc.Tests;

public sealed class HelpTests
{
	private static string Html(string markdown)
	{
		return HelpMarkdown.ToHtml(markdown, path => "uri:" + path, title => title == "Graph" ? "11-graph" : null);
	}

	[Fact]
	public void EveryTopicIsEmbeddedInOrder()
	{
		var topics = HelpTopics.All;
		Assert.True(topics.Count >= 16);
		Assert.Equal("01-getting-started", topics[0].Id);
		Assert.Equal("Getting started", topics[0].Title);
		Assert.Equal(topics.OrderBy(t => t.Id, StringComparer.Ordinal).Select(t => t.Id), topics.Select(t => t.Id));
		Assert.DoesNotContain(topics, t => t.Id.Contains("README"));
	}

	[Fact]
	public void EveryPictureHasBothThemes()
	{
		foreach (var topic in HelpTopics.All)
		{
			foreach (Match picture in Regex.Matches(topic.Markdown, @"!\[[^\]]*\]\(([^)]+)\)"))
			{
				string path = picture.Groups[1].Value;
				Assert.EndsWith(".light.png", path);
				string? light = HelpTopics.Picture(path, dark: false);
				string? dark = HelpTopics.Picture(path, dark: true);
				Assert.NotNull(light);
				Assert.NotNull(dark);
				Assert.NotEqual(light, dark);
			}
		}
	}

	[Fact]
	public void EverySectionReferenceNamesATopic()
	{
		foreach (var topic in HelpTopics.All)
		{
			foreach (Match reference in Regex.Matches(topic.Markdown, @"section \*([^*]+)\*"))
			{
				Assert.NotNull(HelpTopics.IdOfTitle(reference.Groups[1].Value));
			}
		}
	}

	[Fact]
	public void FindAndSearch()
	{
		Assert.Equal("11-graph", HelpTopics.Find("graph").Id);
		Assert.Equal("06-conversions", HelpTopics.Find("06-conversions").Id);
		Assert.Equal("01-getting-started", HelpTopics.Find("no-such-topic").Id);
		Assert.Equal(HelpTopics.All.Count, HelpTopics.Search("  ").Count);
		Assert.Contains(HelpTopics.Search("RAI ngan"), t => t.Id == "06-conversions");
		Assert.Empty(HelpTopics.Search("zzqqxx"));
	}

	[Fact]
	public void ExampleFenceBecomesClickableRows()
	{
		string html = Html("```calc\n1 [ft->in]   => 12\nnow [longdatetime]\n```\n");
		Assert.Contains("data-line=\"1 [ft-&gt;in]\"", html);
		Assert.Contains("<span class=\"result\">= 12</span>", html);
		Assert.Contains("<code>now [longdatetime]</code></div>", html);
		Assert.Equal(("2^3", "8"), HelpMarkdown.Example(" 2^3 => 8 "));
		Assert.Equal(("now", (string?)null), HelpMarkdown.Example("now"));
	}

	[Fact]
	public void ListsJoinTheirContinuationLines()
	{
		string html = Html("- one\n  more\n- two\n\n1. first\n2. second\n");
		Assert.Contains("<ul>\n<li>one more</li>\n<li>two</li>\n</ul>", html);
		Assert.Contains("<ol>\n<li>first</li>\n<li>second</li>\n</ol>", html);
	}

	[Fact]
	public void TableSkipsTheRuleRow()
	{
		string html = Html("| key | does |\n|---|---|\n| Enter | calculate |\n");
		Assert.Contains("<tr><th>key</th><th>does</th></tr>", html);
		Assert.Contains("<tr><td>Enter</td><td>calculate</td></tr>", html);
		Assert.DoesNotContain("---", html);
	}

	[Fact]
	public void InlineCodeBoldItalicAndTopicLinks()
	{
		Assert.Equal("<code>a*b*c</code> and <b>Enter</b>", HelpMarkdown.Format("`a*b*c` and **Enter**", _ => null));
		Assert.Equal("<code>&lt;x&gt;</code> &amp;", HelpMarkdown.Format("`<x>` &", _ => null));
		string html = Html("see *Graph* and *other*\n");
		Assert.Contains("data-topic=\"11-graph\"><i>Graph</i></a>", html);
		Assert.Contains("<i>other</i>", html);
	}

	[Fact]
	public void HeadingsParagraphsAndPictures()
	{
		string html = Html("# Title\n\nA line\nwrapped.\n\n![Main window](img/main.light.png)\n## Part\n");
		Assert.Contains("<h1>Title</h1>", html);
		Assert.Contains("<p>A line wrapped.</p>", html);
		Assert.Contains("<img src=\"uri:img/main.light.png\" alt=\"Main window\">", html);
		Assert.Contains("<h2>Part</h2>", html);
	}
}
