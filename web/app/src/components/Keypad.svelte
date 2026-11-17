<!-- Keypad.svelte - One keypad block (Basic or Scientific) from shared/keys.ts -->
<script lang="ts">
	import { keyClass, keyLabel, keyPlace, type Key } from "../lib/keys";

	let { keys, sci = false, second, armed, base, angle, onpress }: {
		keys: Key[]; sci?: boolean; second: boolean; armed: boolean; base: string; angle: string;
		onpress: (key: Key) => void;
	} = $props();
</script>

<div class="keypad" class:sci>
	{#each keys as key (key.id)}
		<button class={keyClass(key, second, armed)} style={keyPlace(key)}
			onpointerdown={event => event.preventDefault()} onclick={() => onpress(key)}>
			{#if key.secondLabel}<span class="second">{key.secondLabel}</span>{/if}
			<span class="label">{keyLabel(key, base, angle)}</span>
		</button>
	{/each}
</div>
