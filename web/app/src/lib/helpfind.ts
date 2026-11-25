// helpfind.ts - `?word` in Input: the Help topic and heading to open at (owner 2026-11-26)
//
// The desktop's rules (ui/SoftCalc/Logic/HelpFind.cs), so both apps open at
// the same place. Each section scores, for the words as whole words:
//   HEADING_SCORE when its heading holds them (not a "Note: ..." heading);
//   EXAMPLE_SCORE for each ```calc / ```rpn line using them (not one that
//   answers ERROR);
//   1 for each `inline` code span holding them (tables too).
// Error messages (ERRORS_TOPIC) scores on its headings only: it names every
// function in passing. The best wins, the first in reading order on a tie;
// no score at all: null.

export const PREFIX = "?";
export const HEADING_SCORE = 10;
export const EXAMPLE_SCORE = 3;
export const ERRORS_TOPIC = "16-errors";

export type HelpSection = { topic: string; anchor: string; heading: string; text: string };
export type HelpSpot = { topic: string; anchor: string };

// A help line: '?' first, spaces before it allowed
export function isHelpLine(text: string): boolean
{
	return text.trimStart().startsWith(PREFIX);
}

// The words after '?', lower case and single-spaced ("" for a bare ?)
export function helpQuery(text: string): string
{
	return text.trimStart().replace(/^\?+/, "").toLowerCase().split(" ").filter(word => word.length > 0).join(" ");
}

// "Settings typed in Input" -> "settings-typed-in-input"
export function slug(heading: string): string
{
	return heading.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "");
}

// A topic split at its ## and ### headings; the text before the first belongs to the title (anchor "")
export function sections(topic: string, markdown: string): HelpSection[]
{
	const found: HelpSection[] = [];
	let anchor = "", heading = "", text: string[] = [], fenced = false;
	for (const line of markdown.replace(/\r\n/g, "\n").split("\n"))
	{
		if (line.startsWith("```"))
		{
			fenced = !fenced;
		}
		const match = fenced ? null : /^(#{1,3})\s+(.*)$/.exec(line);
		if (match && match[1].length >= 2)
		{
			found.push({ topic, anchor, heading, text: text.join("\n") });
			anchor = slug(match[2]);
			heading = match[2].trim();
			text = [];
			continue;
		}
		if (match)
		{
			heading = match[2].trim();
		}
		text.push(line);
	}
	found.push({ topic, anchor, heading, text: text.join("\n") });
	return found;
}

export function findHelp(topics: { id: string; markdown: string }[], query: string): HelpSpot | null
{
	if (query.length === 0)
	{
		return null;
	}
	const escaped = query.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
	const whole = new RegExp(`(?<![a-z0-9_])${escaped}(?![a-z0-9_])`, "i");
	let best: HelpSection | null = null, bestScore = 0;
	for (const section of topics.flatMap(topic => sections(topic.id, topic.markdown)))
	{
		const score = scoreOf(section, whole);
		if (score > bestScore)
		{
			best = section;
			bestScore = score;
		}
	}
	return best ? { topic: best.topic, anchor: best.anchor } : null;
}

function scoreOf(section: HelpSection, whole: RegExp): number
{
	const heading = whole.test(section.heading) && !/^note/i.test(section.heading);
	let score = heading ? HEADING_SCORE : 0;
	if (section.topic === ERRORS_TOPIC)
	{
		return score;
	}
	let fenced = false;
	for (const line of section.text.split("\n"))
	{
		if (line.startsWith("```"))
		{
			fenced = !fenced;
		}
		else if (fenced)
		{
			const [source, result] = line.split("=>");
			const error = result !== undefined && result.trimStart().startsWith("ERROR");
			score += !error && whole.test(source) ? EXAMPLE_SCORE : 0;
		}
		else
		{
			score += [...line.matchAll(/`([^`]+)`/g)].filter(span => whole.test(span[1])).length;
		}
	}
	return score;
}

//==================================================================
//  Search, as the desktop's Help (HelpTopics.Search, the page's marks)
//==================================================================
export function searchWords(text: string): string[]
{
	return text.toLowerCase().split(" ").filter(word => word.length > 0);
}

// Topics holding every word of the search, any letter case; an empty search keeps all
export function searchTopics<T extends { markdown: string }>(topics: T[], text: string): T[]
{
	const words = searchWords(text);
	return topics.filter(topic =>
	{
		const markdown = topic.markdown.toLowerCase();
		return words.every(word => markdown.includes(word));
	});
}

// Wraps each word found in the text under root in <mark>; returns the first mark
export function markWords(root: HTMLElement, words: string[]): HTMLElement | null
{
	if (words.length === 0)
	{
		return null;
	}
	const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
	const texts: Text[] = [];
	for (let node = walker.nextNode(); node; node = walker.nextNode())
	{
		texts.push(node as Text);
	}
	let first: HTMLElement | null = null;
	for (let text of texts)
	{
		for (;;)
		{
			const low = text.data.toLowerCase();
			let at = -1, size = 0;
			for (const word of words)
			{
				const found = low.indexOf(word);
				if (found >= 0 && (at < 0 || found < at))
				{
					at = found;
					size = word.length;
				}
			}
			if (at < 0)
			{
				break;
			}
			const hit = text.splitText(at);
			text = hit.splitText(size);
			const mark = document.createElement("mark");
			hit.replaceWith(mark);
			mark.append(hit);
			first ??= mark;
		}
	}
	return first;
}
