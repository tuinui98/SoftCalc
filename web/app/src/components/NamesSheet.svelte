<!-- NamesSheet.svelte - The user's variables and functions (⋮ > Variables, owner 2026-11-18).
     A tap on a row puts the name in Input; ✕ asks once more ("Delete?") before the name goes, so a
     slip of the finger does not lose it. There is no edit window: typing `x=5` again redefines. -->
<script lang="ts">
	import type { Definitions } from "../lib/calc";
	import { closeOnTouch, tapOn } from "../lib/repeat";

	let { definitions, oninsert, ondelete, onclose }: {
		definitions: Definitions;
		oninsert: (name: string, isFunction: boolean) => void;
		ondelete: (name: string) => void;
		onclose: () => void;
	} = $props();
	let asking = $state("");    // the name whose ✕ was tapped once

	const outside = (box: HTMLElement) => ({ destroy: closeOnTouch(box, onclose) });
	const tap = (button: HTMLElement, act: () => void) => ({ destroy: tapOn(button, () => act()) });

	function remove(name: string)
	{
		if (asking === name)
		{
			asking = "";
			ondelete(name);
		}
		else
		{
			asking = name;
		}
	}
</script>

{#snippet row(name: string, shown: string, isFunction: boolean)}
	<div class="row">
		<button class="name" use:tap={() => oninsert(name, isFunction)}>{shown}</button>
		<button class="delete" class:asking={asking === name} aria-label="Delete {name}"
			use:tap={() => remove(name)}>{asking === name ? "Delete?" : "✕"}</button>
	</div>
{/snippet}

<div class="backdrop" role="presentation" use:outside></div>
<div class="sheet" role="dialog" aria-label="Variables">
	<div class="grip"></div>
	<div class="items">
		<div class="title">Variables</div>
		{#each definitions.variables as variable (variable.name)}
			{@render row(variable.name, `${variable.name} = ${variable.value}`, false)}
		{:else}
			<div class="empty">None yet: type rate=0.05 and press =</div>
		{/each}
		<div class="title">Your functions</div>
		{#each definitions.functions as fn (fn.name)}
			{@render row(fn.name, fn.source, true)}
		{:else}
			<div class="empty">None yet: type f(x)=x^2 and press =</div>
		{/each}
	</div>
	<button class="cancel" use:tap={onclose}>Close</button>
</div>

<style>
	.backdrop { position: fixed; inset: 0; background: rgb(0 0 0 / 0.35); z-index: 30; animation: fade 150ms; touch-action: none; }
	.sheet
	{
		position: fixed; left: 0; right: 0; bottom: 0; z-index: 31; max-height: 75vh; display: flex; flex-direction: column;
		background: var(--page); color: var(--ink); border-radius: 16px 16px 0 0;
		box-shadow: 0 -4px 20px rgb(0 0 0 / 0.25); animation: rise 180ms ease-out; max-width: 640px; margin: 0 auto;
		padding-bottom: max(28px, env(safe-area-inset-bottom));
	}
	.grip { width: 40px; height: 4px; border-radius: 2px; background: var(--line); margin: 8px auto 4px; }
	.title { padding: 10px 18px 6px; font-weight: 600; color: var(--faint); }
	.items { overflow-y: auto; }
	.empty { padding: 6px 18px 12px; color: var(--faint); font-style: italic; }
	.row { display: flex; align-items: stretch; }
	button
	{
		border: none; background: none; color: inherit; font: inherit; font-size: 17px; cursor: pointer;
		touch-action: manipulation; padding: 14px 18px;
	}
	button:active { background: var(--line); }
	.name { flex: 1; min-width: 0; text-align: left; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
	.delete { flex: none; min-width: 56px; color: var(--faint); }
	.delete.asking { color: var(--error); font-weight: 600; }
	.cancel { display: block; width: 100%; border-top: 1px solid var(--line); color: var(--select); text-align: center; }
	@keyframes rise { from { transform: translateY(100%); } }
	@keyframes fade { from { opacity: 0; } }
</style>
