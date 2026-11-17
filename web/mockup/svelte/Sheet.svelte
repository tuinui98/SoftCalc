<!-- Sheet.svelte - A bottom sheet with a title and a list to pick from (menus of mockup B) -->
<script lang="ts">
	import { closeOnTouch, tapOn } from "../shared/repeat";

	let { title, items, onpick }: { title: string; items: string[]; onpick: (item: string | null) => void } = $props();
	const outside = (box: HTMLElement) => ({ destroy: closeOnTouch(box, () => onpick(null)) });
	const tap = (button: HTMLElement, item: string | null) => ({ destroy: tapOn(button, () => onpick(item)) });
</script>

<div class="backdrop" role="presentation" use:outside></div>
<div class="sheet" role="dialog" aria-label={title}>
	<div class="grip"></div>
	<div class="title">{title}</div>
	<div class="items">
		{#each items as item}
			<button use:tap={item}>{item}</button>
		{/each}
	</div>
	<button class="cancel" use:tap={null}>Cancel</button>
</div>

<style>
	.backdrop { position: fixed; inset: 0; background: rgb(0 0 0 / 0.35); z-index: 30; animation: fade 150ms; touch-action: none; }
	.sheet
	{
		position: fixed; left: 0; right: 0; bottom: 0; z-index: 31; max-height: 70vh; display: flex; flex-direction: column;
		background: var(--page); color: var(--ink); border-radius: 16px 16px 0 0;
		box-shadow: 0 -4px 20px rgb(0 0 0 / 0.25); animation: rise 180ms ease-out; max-width: 640px; margin: 0 auto;
		/* off the bottom edge, where gesture navigation takes the first touch */
		padding-bottom: max(28px, env(safe-area-inset-bottom));
	}
	.grip { width: 40px; height: 4px; border-radius: 2px; background: var(--line); margin: 8px auto 4px; }
	.title { padding: 6px 18px 10px; font-weight: 600; color: var(--faint); }
	.items { overflow-y: auto; }
	button
	{
		display: block; width: 100%; text-align: left; padding: 14px 18px; border: none; background: none;
		color: inherit; font: inherit; font-size: 17px; cursor: pointer; touch-action: manipulation;
	}
	button:active { background: var(--line); }
	.cancel { border-top: 1px solid var(--line); color: var(--select); text-align: center; }
	@keyframes rise { from { transform: translateY(100%); } }
	@keyframes fade { from { opacity: 0; } }
</style>
