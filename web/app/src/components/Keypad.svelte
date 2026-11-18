<!-- Keypad.svelte - One keypad block (Basic or Scientific) from shared/keys.ts.
     A touch or mouse press is taken by keytap.ts on the keys' box (data-key names the key);
     the click here acts only for Enter or Space (detail 0). -->
<script lang="ts">
	import { keyClass, keyLabel, keyPlace, type Key } from "../lib/keys";

	let { keys, sci = false, second, armed, base, angle, onpress }: {
		keys: Key[]; sci?: boolean; second: boolean; armed: boolean; base: string; angle: string;
		onpress: (key: Key) => void;
	} = $props();
</script>

<div class="keypad" class:sci>
	{#each keys as key (key.id)}
		<!-- --chars: a one-letter label may grow wider than M+ or sin (Settings > Text size) -->
		<button class={keyClass(key, second, armed)} style="{keyPlace(key)}; --chars: {[...keyLabel(key, base, angle)].length}"
			data-key={key.id} onpointerdown={event => event.preventDefault()}
			onclick={event => { if (event.detail === 0) onpress(key); }}>
			{#if key.secondLabel}<span class="second">{key.secondLabel}</span>{/if}
			<span class="label">{keyLabel(key, base, angle)}</span>
		</button>
	{/each}
</div>
