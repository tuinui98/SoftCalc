<!-- HelpView.svelte - ⋮ > Help: the topics list, then one topic (owner 2026-11-18).
     The topics come from make_help.mjs (the desktop's help/*.md for the web app), loaded only
     when Help opens. A tap on an example puts its line in Input; a topic title in italics opens
     that topic. The phone's Back goes from a topic to the list, then closes Help. -->
<script lang="ts">
	import { tick } from "svelte";
	import { findHelp } from "../lib/helpfind";
	import { tapOn } from "../lib/repeat";

	type Topic = { id: string; title: string; html: string; markdown: string };

	// query: `?word` typed in Input (owner 2026-11-26), opened at its best heading (helpfind.ts)
	let { topic = $bindable(), query = $bindable(null), onexample, onclose }: {
		topic: string | null;
		query?: string | null;
		onexample: (line: string) => void;
		onclose: () => void;
	} = $props();

	let topics: Topic[] = $state([]);
	let failed = $state("");
	let page: HTMLElement | undefined = $state();
	const shown = $derived(topics.find(item => item.id === topic) ?? null);

	import("../generated/help.json").then(module => { topics = module.default as Topic[]; },
		error => { failed = error instanceof Error ? error.message : String(error); });

	// A ?word once the topics are in: its topic, scrolled to its heading; none found: the list
	$effect(() =>
	{
		if (query === null || topics.length === 0)
		{
			return;
		}
		const spot = findHelp(topics, query);
		query = null;
		topic = spot?.topic ?? null;
		void tick().then(() =>
		{
			const heading = spot && spot.anchor ? page?.querySelector(`#${CSS.escape(spot.anchor)}`) : null;
			if (heading)
				heading.scrollIntoView({ block: "start" });
			else
				page?.scrollTo({ top: 0 });
		});
	});

	const tap = (button: HTMLElement, act: () => void) => ({ destroy: tapOn(button, () => act()) });

	// Examples and topic links inside the topic's HTML
	function onPage(event: MouseEvent)
	{
		const target = (event.target as HTMLElement).closest<HTMLElement>("[data-line], [data-topic]");
		if (!target)
		{
			return;
		}
		event.preventDefault();
		if (target.dataset.line !== undefined)
		{
			onexample(target.dataset.line);
		}
		else if (target.dataset.topic)
		{
			topic = target.dataset.topic;
			page?.scrollTo({ top: 0 });
		}
	}
</script>

<div class="help" role="dialog" aria-label="Help">
	<div class="head">
		{#if shown}
			<button class="bar" use:tap={() => { topic = null; }} aria-label="All topics">◂ Topics</button>
		{:else}
			<span class="name">Help</span>
		{/if}
		<button class="bar" use:tap={onclose}>Done</button>
	</div>
	<div class="page" bind:this={page}>
		{#if failed}
			<p class="failed">Help could not be loaded: {failed}</p>
		{:else if shown}
			<!-- The HTML is made at build time from SoftCalc's own help files (make_help.mjs), not from user input -->
			<!-- svelte-ignore a11y_no_noninteractive_element_interactions, a11y_click_events_have_key_events -->
			<article onclick={onPage}>{@html shown.html}</article>
		{:else}
			{#each topics as item (item.id)}
				<button class="topic" use:tap={() => { topic = item.id; }}>{item.title}</button>
			{/each}
		{/if}
	</div>
</div>

<style>
	.help
	{
		position: fixed; inset: 0; z-index: 40; display: flex; flex-direction: column;
		background: var(--page); color: var(--ink); height: 100dvh;
	}
	.head
	{
		display: flex; align-items: center; justify-content: space-between; gap: 8px;
		padding: 4px 6px; padding-top: max(4px, env(safe-area-inset-top)); border-bottom: 1px solid var(--line);
	}
	.name { margin: 0 10px; font-size: 20px; font-weight: 500; }
	.bar
	{
		min-width: 44px; height: 40px; border: none; border-radius: 20px; background: none; color: var(--select);
		font: inherit; font-size: 16px; padding: 0 12px; cursor: pointer; touch-action: manipulation;
	}
	.bar:active { background: var(--line); }
	.page
	{
		flex: 1; overflow-y: auto; overscroll-behavior: contain; padding: 0 16px max(28px, env(safe-area-inset-bottom));
		font-size: calc(16px * var(--text-scale, 1)); line-height: 1.45;
	}
	.topic
	{
		display: block; width: 100%; text-align: left; padding: 14px 4px; border: none; border-bottom: 1px solid var(--line);
		background: none; color: inherit; font: inherit; font-size: 17px; cursor: pointer;
	}
	.topic:active { background: var(--line); }
	.failed { color: var(--error); }
	article { max-width: 720px; margin: 0 auto; }
	article :global(h1) { font-size: 1.5em; font-weight: 500; margin: 0.8em 0 0.4em; }
	article :global(h2) { font-size: 1.15em; margin: 1.3em 0 0.4em; }
	article :global(code) { font-family: ui-monospace, Consolas, monospace; font-size: 0.92em; background: var(--line); padding: 0 3px; border-radius: 4px; }
	article :global(pre) { overflow-x: auto; background: var(--line); padding: 8px; border-radius: 6px; }
	article :global(table) { border-collapse: collapse; display: block; overflow-x: auto; font-size: 0.92em; }
	article :global(th), article :global(td) { border: 1px solid var(--line); padding: 4px 8px; text-align: left; vertical-align: top; }
	article :global(a) { color: var(--select); }
	article :global(.examples) { display: flex; flex-direction: column; gap: 2px; margin: 8px 0; }
	article :global(.example)
	{
		display: flex; justify-content: space-between; gap: 16px; text-align: left; padding: 8px 10px;
		border: 1px solid var(--line); border-radius: 8px; background: none; color: var(--ink);
		font-family: ui-monospace, Consolas, monospace; font-size: 0.95em; cursor: pointer; touch-action: manipulation;
		overflow-wrap: anywhere;
	}
	article :global(.example:active) { background: var(--line); }
	article :global(.example) { flex-wrap: wrap; column-gap: 16px; row-gap: 2px; }
	/* The line stays whole; a long result goes below it, on the right */
	article :global(.example .line) { flex: none; max-width: 100%; white-space: pre-wrap; }
	article :global(.example .result) { flex: 1 1 12ch; min-width: 0; text-align: right; color: var(--faint); }
</style>
