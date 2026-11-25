// HelpFind.cs - `?word` in Input: the Help topic and heading to open at (owner 2026-11-26)
//
// The same rules as the web app (web/app/src/lib/helpfind.ts). Each section
// scores, for the words as whole words:
//   HeadingScore when its heading holds them (not a "Note: ..." heading);
//   ExampleScore for each ```calc / ```rpn line using them (not one that
//   answers ERROR);
//   1 for each `inline` code span holding them (tables too).
// Error messages (ErrorsTopic) scores on its headings only: it names every
// function in passing. The best wins, the first in reading order on a tie;
// no score at all: nothing, and Help searches for the text.
// A heading's anchor is its slug (Slug), the same on both apps.

using System.Text;
using System.Text.RegularExpressions;

namespace SoftCalc.Logic;

internal sealed record HelpSection(string TopicId, string Anchor, string Heading, string Text);

internal sealed record HelpSpot(string TopicId, string Anchor);

internal static partial class HelpFind
{
	public const char Prefix = '?';
	public const int HeadingScore = 10;
	public const int ExampleScore = 3;
	public const string ErrorsTopic = "16-errors";

	// A help line: '?' first, spaces before it allowed
	public static bool IsHelpLine(string text)
	{
		return text.TrimStart().StartsWith(Prefix);
	}

	// The words after '?', lower case and single-spaced ("" for a bare ?)
	public static string Query(string text)
	{
		return string.Join(' ', text.TrimStart().TrimStart(Prefix).ToLowerInvariant()
			.Split(' ', StringSplitOptions.RemoveEmptyEntries));
	}

	// "Settings typed in Input" -> "settings-typed-in-input"
	public static string Slug(string heading)
	{
		var slug = new StringBuilder();
		foreach (char c in heading.ToLowerInvariant())
		{
			if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
			{
				slug.Append(c);
			}
			else if (slug.Length > 0 && slug[^1] != '-')
			{
				slug.Append('-');
			}
		}
		return slug.ToString().TrimEnd('-');
	}

	// A topic split at its # to ### headings; the text before the first ## belongs to the title (anchor "")
	public static List<HelpSection> Sections(string topicId, string markdown)
	{
		var sections = new List<HelpSection>();
		string anchor = "", heading = "";
		var text = new StringBuilder();
		bool fenced = false;
		foreach (string line in markdown.Replace("\r\n", "\n").Split('\n'))
		{
			if (line.StartsWith("```"))
			{
				fenced = !fenced;
			}
			var match = fenced ? Match.Empty : HeadingRegex().Match(line);
			if (match.Success && match.Groups[1].Length >= 2)
			{
				sections.Add(new HelpSection(topicId, anchor, heading, text.ToString()));
				(anchor, heading) = (Slug(match.Groups[2].Value), match.Groups[2].Value.Trim());
				text.Clear();
				continue;
			}
			if (match.Success)
			{
				heading = match.Groups[2].Value.Trim();
			}
			text.Append(line).Append('\n');
		}
		sections.Add(new HelpSection(topicId, anchor, heading, text.ToString()));
		return sections;
	}

	public static HelpSpot? Find(IEnumerable<HelpTopic> topics, string query)
	{
		if (query.Length == 0)
		{
			return null;
		}
		var sections = topics.SelectMany(topic => Sections(topic.Id, topic.Markdown)).ToList();
		var whole = new Regex($@"(?<![a-z0-9_]){Regex.Escape(query)}(?![a-z0-9_])", RegexOptions.IgnoreCase);
		HelpSection? best = null;
		int bestScore = 0;
		foreach (var section in sections)
		{
			int score = Score(section, whole);
			if (score > bestScore)
			{
				(best, bestScore) = (section, score);
			}
		}
		return best == null ? null : new HelpSpot(best.TopicId, best.Anchor);
	}

	private static int Score(HelpSection section, Regex whole)
	{
		bool heading = whole.IsMatch(section.Heading) && !section.Heading.StartsWith("Note", StringComparison.OrdinalIgnoreCase);
		int score = heading ? HeadingScore : 0;
		if (section.TopicId == ErrorsTopic)
		{
			return score;
		}
		bool fenced = false;
		foreach (string line in section.Text.Split('\n'))
		{
			if (line.StartsWith("```"))
			{
				fenced = !fenced;
			}
			else if (fenced)
			{
				string[] parts = line.Split("=>");
				bool error = parts.Length > 1 && parts[1].TrimStart().StartsWith("ERROR");
				score += !error && whole.IsMatch(parts[0]) ? ExampleScore : 0;
			}
			else
			{
				score += InlineCodeRegex().Matches(line).Count(span => whole.IsMatch(span.Groups[1].Value));
			}
		}
		return score;
	}

	[GeneratedRegex(@"^(#{1,3})\s+(.*)$")]
	private static partial Regex HeadingRegex();

	[GeneratedRegex(@"`([^`]+)`")]
	private static partial Regex InlineCodeRegex();
}
