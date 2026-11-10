// HelpMarkdown.cs - The Markdown of help/*.md as HTML for the Help window (owner 2026-11-10)
//
// Only what the topics use: # headings, paragraphs, - and 1. lists with
// indented continuation lines, | tables |, ![pictures](img/x.light.png),
// ``` fences, and inline `code`, **bold** and *italic*. A ```calc or
// ```rpn fence becomes clickable example rows; `line => result` shows
// the result faint after the line. An *italic* text that is a topic's
// title becomes a link to that topic.

using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace SoftCalc.Logic;

internal static partial class HelpMarkdown
{
	// image: picture path to a URI (null: show the caption); topic: title to topic id (null: plain italic)
	public static string ToHtml(string markdown, Func<string, string?> image, Func<string, string?> topic)
	{
		var html = new StringBuilder();
		var lines = markdown.Replace("\r\n", "\n").Split('\n');
		int i = 0;
		while (i < lines.Length)
		{
			string line = lines[i];
			if (line.Trim().Length == 0)
			{
				i++;
			}
			else if (line.StartsWith("```"))
			{
				i = Fence(lines, i, html);
			}
			else if (HeadingRegex().Match(line) is { Success: true } heading)
			{
				int level = heading.Groups[1].Length;
				html.Append($"<h{level}>").Append(Format(heading.Groups[2].Value, topic)).Append($"</h{level}>\n");
				i++;
			}
			else if (PictureRegex().Match(line.Trim()) is { Success: true } picture)
			{
				string caption = Format(picture.Groups[1].Value, topic);
				string? uri = image(picture.Groups[2].Value);
				html.Append("<div class=\"picture\">");
				if (uri != null)
				{
					html.Append($"<img src=\"{Attribute(uri)}\" alt=\"{Attribute(picture.Groups[1].Value)}\"><br>");
				}
				html.Append($"<span class=\"caption\">{caption}</span></div>\n");
				i++;
			}
			else if (line.StartsWith('|'))
			{
				i = Table(lines, i, html, topic);
			}
			else if (ItemRegex().IsMatch(line))
			{
				i = List(lines, i, html, topic);
			}
			else
			{
				var text = new List<string>();
				while (i < lines.Length && lines[i].Trim().Length > 0 && !StartsBlock(lines[i]))
				{
					text.Add(lines[i].Trim());
					i++;
				}
				html.Append("<p>").Append(Format(string.Join(" ", text), topic)).Append("</p>\n");
			}
		}
		return html.ToString();
	}

	// One example line, split as the engine's help test reads it: `line => result`
	public static (string Line, string? Result) Example(string fenceLine)
	{
		int arrow = fenceLine.IndexOf("=>", StringComparison.Ordinal);
		return arrow < 0 ? (fenceLine.Trim(), null) : (fenceLine[..arrow].Trim(), fenceLine[(arrow + 2)..].Trim());
	}

	private static bool StartsBlock(string line)
	{
		return line.StartsWith("```") || line.StartsWith('|') || HeadingRegex().IsMatch(line)
			|| ItemRegex().IsMatch(line) || PictureRegex().IsMatch(line.Trim());
	}

	//==================================================================
	//  Blocks
	//==================================================================
	private static int Fence(string[] lines, int i, StringBuilder html)
	{
		string kind = lines[i][3..].Trim();
		bool examples = kind is "calc" or "rpn";
		i++;
		html.Append(examples ? $"<div class=\"examples\" data-kind=\"{kind}\">\n" : "<pre>");
		while (i < lines.Length && !lines[i].StartsWith("```"))
		{
			if (!examples)
			{
				html.Append(WebUtility.HtmlEncode(lines[i])).Append('\n');
			}
			else if (lines[i].Trim().Length > 0)
			{
				var (source, result) = Example(lines[i]);
				html.Append($"<div class=\"example\" title=\"Click to put this line in Input\" data-line=\"{Attribute(source)}\">")
					.Append("<code>").Append(WebUtility.HtmlEncode(source)).Append("</code>");
				if (result != null)
				{
					html.Append("<span class=\"result\">= ").Append(WebUtility.HtmlEncode(result)).Append("</span>");
				}
				html.Append("</div>\n");
			}
			i++;
		}
		html.Append(examples ? "</div>\n" : "</pre>\n");
		return i + 1;    // past the closing fence
	}

	private static int Table(string[] lines, int i, StringBuilder html, Func<string, string?> topic)
	{
		html.Append("<table>\n");
		bool head = true;
		while (i < lines.Length && lines[i].StartsWith('|'))
		{
			var cells = lines[i].Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToList();
			i++;
			if (cells.All(cell => cell.Length > 0 && cell.All(c => c is '-' or ':')))
			{
				continue;    // the |---| row under the header
			}
			string tag = head ? "th" : "td";
			html.Append("<tr>");
			foreach (string cell in cells)
			{
				html.Append($"<{tag}>").Append(Format(cell, topic)).Append($"</{tag}>");
			}
			html.Append("</tr>\n");
			head = false;
		}
		html.Append("</table>\n");
		return i;
	}

	private static int List(string[] lines, int i, StringBuilder html, Func<string, string?> topic)
	{
		bool ordered = char.IsDigit(lines[i][0]);
		html.Append(ordered ? "<ol>\n" : "<ul>\n");
		while (i < lines.Length && ItemRegex().Match(lines[i]) is { Success: true } item)
		{
			var text = new List<string> { item.Groups[1].Value };
			i++;
			while (i < lines.Length && lines[i].StartsWith("  ") && lines[i].Trim().Length > 0)
			{
				text.Add(lines[i].Trim());
				i++;
			}
			html.Append("<li>").Append(Format(string.Join(" ", text), topic)).Append("</li>\n");
		}
		html.Append(ordered ? "</ol>\n" : "</ul>\n");
		return i;
	}

	//==================================================================
	//  Inline: `code` first, then **bold** and *italic* in the rest
	//==================================================================
	public static string Format(string text, Func<string, string?> topic)
	{
		var html = new StringBuilder();
		var parts = text.Split('`');
		for (int n = 0; n < parts.Length; n++)
		{
			if (n % 2 == 1 && n < parts.Length - 1)
			{
				html.Append("<code>").Append(WebUtility.HtmlEncode(parts[n])).Append("</code>");
				continue;
			}
			string plain = n % 2 == 1 ? "`" + parts[n] : parts[n];    // an unclosed backtick stays as text
			plain = WebUtility.HtmlEncode(plain);
			plain = BoldRegex().Replace(plain, "<b>$1</b>");
			plain = ItalicRegex().Replace(plain, match =>
			{
				string title = WebUtility.HtmlDecode(match.Groups[1].Value);
				string? id = topic(title);
				return id == null
					? $"<i>{match.Groups[1].Value}</i>"
					: $"<a href=\"#\" class=\"topic\" data-topic=\"{Attribute(id)}\"><i>{match.Groups[1].Value}</i></a>";
			});
			html.Append(plain);
		}
		return html.ToString();
	}

	private static string Attribute(string text)
	{
		return WebUtility.HtmlEncode(text);
	}

	[GeneratedRegex(@"^(#{1,3})\s+(.*)$")]
	private static partial Regex HeadingRegex();

	[GeneratedRegex(@"^!\[([^\]]*)\]\(([^)]+)\)$")]
	private static partial Regex PictureRegex();

	[GeneratedRegex(@"^(?:-|\d+\.)\s+(.*)$")]
	private static partial Regex ItemRegex();

	[GeneratedRegex(@"\*\*(.+?)\*\*")]
	private static partial Regex BoldRegex();

	[GeneratedRegex(@"\*([^*\s][^*]*?)\*")]
	private static partial Regex ItalicRegex();
}
