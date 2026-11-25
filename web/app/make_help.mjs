// make_help.mjs - The Help topics for the web app, as HTML in src/generated/help.json (not in git)
//
// The topics are the desktop's help/NN-name.md (owner 2026-11-18), minus
// those about parts the web app does not have, and with the web's own
// version of a topic from app/help/ where there is one. In a shared topic
// the lines between `<!-- desktop -->` and `<!-- /desktop -->` are left
// out and the `<!-- web -->` blocks kept (the desktop does the opposite,
// HelpMarkdown.ForDesktop). Pictures are the desktop's windows, so they
// are left out. A ```calc example becomes a button that puts the line in
// Input; `line => result` shows the result faint. An *italic* text that
// is a topic's title links to that topic.
//   node app/make_help.mjs

import { mkdirSync, readFileSync, writeFileSync, existsSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { Marked } from "marked";
import { slug } from "./src/lib/helpfind.ts";

const here = path.dirname(fileURLToPath(import.meta.url));
const shared = path.resolve(here, "..", "..", "help");
const own = path.join(here, "help");

// Topics in the web app, in reading order (10 RPN, 12 Ribbon, 13 Views,
// 14 Data files and 15 Keyboard are desktop parts; 17 Random is the web's own)
const TOPICS = ["01-getting-started", "02-numbers-operators", "03-functions", "04-variables-functions",
	"05-display-precision", "06-conversions", "07-currency", "08-dates-time", "09-finance", "11-graph", "16-errors", "17-random",
	"18-cryptocurrency", "19-matrices"];

// The web's text: desktop blocks and pictures out, web blocks in
export function forWeb(markdown)
{
	const kept = [];
	let desktop = false;
	for (const line of markdown.replace(/\r\n/g, "\n").split("\n"))
	{
		const marker = line.trim();
		if (marker === "<!-- desktop -->")
		{
			desktop = true;
		}
		else if (marker === "<!-- /desktop -->")
		{
			desktop = false;
		}
		else if (marker !== "<!-- web -->" && marker !== "<!-- /web -->" && !desktop && !/^!\[[^\]]*\]\([^)]*\)$/.test(marker))
		{
			kept.push(line);
		}
	}
	return kept.join("\n");
}

// The reference sites a More: line may link to (owner 2026-11-26; desktop HelpMarkdown.WebHosts)
export const WEB_HOSTS = ["dlmf.nist.gov", "www.itl.nist.gov", "mathworld.wolfram.com", "oeis.org", "mpmath.org"];

export function isWebLink(href)
{
	try
	{
		const url = new URL(href);
		return url.protocol === "https:" && WEB_HOSTS.includes(url.hostname);
	}
	catch
	{
		return false;
	}
}

const escape = text => text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");

export function makeHelp(read)
{
	const sources = TOPICS.map(id =>
	{
		const markdown = forWeb(read(id));
		const title = markdown.match(/^# (.+)$/m)?.[1].trim() ?? id;
		return { id, title, markdown };
	});
	const byTitle = new Map(sources.map(topic => [topic.title.toLowerCase(), topic.id]));
	const marked = new Marked({
		renderer:
		{
			code({ text, lang })
			{
				if (lang !== "calc")
				{
					return `<pre><code>${escape(text)}</code></pre>\n`;
				}
				const rows = text.split("\n").filter(line => line.trim().length > 0).map(line =>
				{
					const [source, result] = line.split("=>").map(part => part.trim());
					const shown = result === undefined ? "" : `<span class="result">${escape(result)}</span>`;
					return `<button class="example" data-line="${escape(source)}"><span class="line">${escape(source)}</span>${shown}</button>`;
				});
				return `<div class="examples">${rows.join("")}</div>\n`;
			},
			// ## and ### carry their slug as id, for `?word` (helpfind.ts, owner 2026-11-26)
			heading({ tokens, depth })
			{
				const text = this.parser.parseInline(tokens);
				const raw = tokens.map(token => token.raw).join("");
				const id = depth === 2 || depth === 3 ? ` id="${slug(raw)}"` : "";
				return `<h${depth}${id}>${text}</h${depth}>\n`;
			},
			// A More: link opens the reference site in a new tab (the phone's browser in the APK); others are text
			link({ href, tokens })
			{
				const text = this.parser.parseInline(tokens);
				return isWebLink(href) ? `<a href="${escape(href)}" target="_blank" rel="noopener noreferrer">${text}</a>` : text;
			},
			em({ tokens })
			{
				const text = this.parser.parseInline(tokens);
				const target = byTitle.get(text.toLowerCase());
				return target ? `<a href="#" data-topic="${target}">${text}</a>` : `<em>${text}</em>`;
			},
		},
	});
	// markdown: the web's text of the topic, for ?word (helpfind.ts)
	return sources.map(({ id, title, markdown }) => ({ id, title, html: marked.parse(markdown), markdown }));
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url))
{
	const topics = makeHelp(id =>
	{
		const mine = path.join(own, `${id}.md`);
		return readFileSync(existsSync(mine) ? mine : path.join(shared, `${id}.md`), "utf8");
	});
	const target = path.join(here, "src", "generated");
	mkdirSync(target, { recursive: true });
	writeFileSync(path.join(target, "help.json"), JSON.stringify(topics));
	console.log(`help: ${topics.length} topics`);
}
