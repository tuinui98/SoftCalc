// helpfind.test.ts - ?word finds a Help heading, as the desktop's HelpFindTests (owner 2026-11-26)

import { describe, expect, it } from "vitest";
import { findHelp, helpQuery, isHelpLine, sections, slug } from "../src/lib/helpfind";
import { forWeb, makeHelp } from "../make_help.mjs";
import { readFileSync, existsSync } from "node:fs";
import path from "node:path";

describe("?word", () =>
{
	it("makes the desktop's slugs", () =>
	{
		expect(slug("Settings typed in Input")).toBe("settings-typed-in-input");
		expect(slug("Operators, strongest first")).toBe("operators-strongest-first");
		expect(slug("Two's Complement")).toBe("two-s-complement");
		expect(slug("  Lists & matrices ")).toBe("lists-matrices");
	});

	it("reads a help line", () =>
	{
		expect(isHelpLine("?det")).toBe(true);
		expect(helpQuery("  ?  Compound   Interest ")).toBe("compound interest");
		expect(helpQuery("?")).toBe("");
		expect(isHelpLine("2?3")).toBe(false);
	});

	it("scores a heading, examples and code, and Error messages on headings only", () =>
	{
		const topics = [
			{ id: "01-a", markdown: "# A\n\n`det(m)` once\n\n## Seeds\n\n```calc\nrands(42)\n```\n" },
			{ id: "02-b", markdown: "# B\n\n## Matrix work\n\n`det` `det` and `det(m)`\n\n### Random seeds\n\ntext\n" },
			{ id: "16-errors", markdown: "# E\n\n## Odd\n\n`det` `det` `det` `det` `det` `rands`\n\n## Note: when det is 0\n\n```calc\ndet(m) => 0\n```\n" },
		];
		expect(findHelp(topics, "seeds")).toEqual({ topic: "01-a", anchor: "seeds" });
		expect(findHelp(topics, "det")).toEqual({ topic: "02-b", anchor: "matrix-work" });
		expect(findHelp(topics, "rands")).toEqual({ topic: "01-a", anchor: "seeds" });
		expect(findHelp(topics, "b")).toEqual({ topic: "02-b", anchor: "" });
		expect(findHelp(topics, "nothing")).toBeNull();
		expect(findHelp(topics, "de")).toBeNull();
		expect(findHelp(topics, "odd")).toEqual({ topic: "16-errors", anchor: "odd" });
	});

	it("finds the real topics, and every heading has an anchor of its own", () =>
	{
		const here = path.resolve(__dirname, "..");
		const topics = makeHelp((id: string) =>
		{
			const mine = path.join(here, "help", `${id}.md`);
			return readFileSync(existsSync(mine) ? mine : path.join(here, "..", "..", "help", `${id}.md`), "utf8");
		});
		const spot = (query: string) => findHelp(topics, query)?.topic;
		expect(spot("det")).toBe("19-matrices");
		expect(spot("inv")).toBe("19-matrices");
		expect(spot("fv")).toBe("09-finance");
		expect(spot("precision")).toBe("02-numbers-operators");
		expect(spot("seed")).toBe("17-random");    // the web's own Random topic has ## Seed
		for (const topic of topics)
		{
			const anchors = sections(topic.id, topic.markdown).map(section => section.anchor);
			expect(new Set(anchors).size).toBe(anchors.length);
			expect(topic.html).not.toContain('id=""');
		}
		expect(forWeb("a\n<!-- desktop -->\nb\n<!-- /desktop -->\nc")).toBe("a\nc");
	});
});
