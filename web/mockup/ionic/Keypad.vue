<!-- Keypad.vue - One keypad block (Basic or Scientific) from shared/keys.ts -->
<script setup lang="ts">
import { keyClass, keyLabel, keyPlace, type Key } from "../shared/keys";

defineProps<{ keys: Key[]; sci?: boolean; second: boolean; armed: boolean; base: string; angle: string }>();
const emit = defineEmits<{ press: [key: Key, where: HTMLElement] }>();
</script>

<template>
	<div class="keypad" :class="{ sci }">
		<button v-for="key in keys" :key="key.id" :class="keyClass(key, second, armed)" :style="keyPlace(key)"
			@pointerdown.prevent @click="emit('press', key, $event.currentTarget as HTMLElement)">
			<span v-if="key.secondLabel" class="second">{{ key.secondLabel }}</span>
			<span class="label">{{ keyLabel(key, base, angle) }}</span>
		</button>
	</div>
</template>
