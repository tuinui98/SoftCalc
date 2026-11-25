// help.test.ts - The Help topics for the web app (make_help.mjs, owner 2026-11-18)

import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
// @ts-expect-error: a plain build script without types
import { forWeb, isWebLink, makeHelp } from "../make_help.mjs";

const SAMPLE = [
	"# Functions",
	"",
	"<!-- desktop -->",
	"View > Word Size is used.",
	"<!-- /desktop -->",
	"",
	"<!-- web -->",
	"Settings > Word size is used.",
	"<!-- /web -->",
	"",
	"![The ribbon](img/ribbon.light.png)",
	"",
	"See *Currency* and *Random*.",
	"",
	"```calc",
	"2^10                  => 1024",
	"100 [usd->thb]",
	"```",
].join("\n");

describe("help for the web app", () =>
{
	it("keeps web blocks, drops desktop blocks, markers and pictures", () =>
	{
		const text = forWeb(SAMPLE);
		expect(text).toContain("Settings > Word size is used.");
		expect(text).not.toContain("View > Word Size");
		expect(text).not.toContain("<!--");
		expect(text).not.toContain("img/");
	});

	it("examples become buttons; a topic title links to it, other italics stay", () =>
	{
		const topics = makeHelp((id: string) => id === "03-functions" ? SAMPLE : `# ${id === "07-currency" ? "Currency" : id}`);
		const html: string = topics.find((topic: { id: string }) => topic.id === "03-functions").html;
		expect(html).toContain('<button class="example" data-line="2^10"><span class="line">2^10</span><span class="result">1024</span></button>');
		expect(html).toContain('<button class="example" data-line="100 [usd-&gt;thb]"><span class="line">100 [usd-&gt;thb]</span></button>');
		expect(html).toContain('<a href="#" data-topic="07-currency">Currency</a>');
		expect(html).toContain("<em>Random</em>");
	});

	it("a More: link opens a reference site in a new tab; any other link is text (owner 2026-11-26)", () =>
	{
		const [topic] = makeHelp(() => "# T\n\nMore: [DLMF §4.2](https://dlmf.nist.gov/4.2), [x](https://example.com/), [y](http://oeis.org/)\n");
		expect(topic.html).toContain('<a href="https://dlmf.nist.gov/4.2" target="_blank" rel="noopener noreferrer">DLMF §4.2</a>');
		expect(topic.html).toContain(", x, y");
		expect(isWebLink("https://oeis.org/A000142")).toBe(true);
		expect(isWebLink("javascript:alert(1)")).toBe(false);
	});

	it("no desktop-only text reaches the real topics", () =>
	{
		const root = path.resolve(__dirname, "..", "..", "..");
		const topics = makeHelp((id: string) =>
		{
			try
			{
				return readFileSync(path.join(root, "web", "app", "help", `${id}.md`), "utf8");
			}
			catch
			{
				return readFileSync(path.join(root, "help", `${id}.md`), "utf8");
			}
		});
		expect(topics.length).toBe(15);    // 19 Lists and matrices (owner 2026-11-24)
		for (const topic of topics)
		{
			expect(topic.html, topic.id).not.toMatch(/View &gt;|ribbon|Ribbon|img\/|&lt;!--|press Enter/);
		}
	});
});
