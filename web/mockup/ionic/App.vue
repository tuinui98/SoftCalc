<!-- App.vue - Mockup A: Ionic + Vue. Sci in an ion-menu over the keys only (swipe from the edge, the Sci
     button or "Sci ▸"), Functions / Conversions as action sheets, Settings in a modal. Wide screens show
     Sci beside Basic; a phone on its side puts History left and the keys right. -->
<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, shallowRef } from "vue";
import {
	IonApp, IonButton, IonButtons, IonContent, IonHeader, IonIcon, IonItem, IonLabel, IonList, IonMenu, IonModal,
	IonSegment, IonSegmentButton, IonSelect, IonSelectOption, IonTitle, IonToolbar, actionSheetController, menuController,
} from "@ionic/vue";
import { ellipsisVertical, keypadOutline } from "ionicons/icons";
import Keypad from "./Keypad.vue";
import { Calculator } from "../shared/calc";
import { BASIC, SCIENTIFIC, type Key } from "../shared/keys";
import { applyTheme, loadTheme, saveTheme, SIDE, trackKeys, WIDE } from "../shared/page";
import { holdToRepeat } from "../shared/repeat";

const calc = new Calculator(new URL("./worker.js", document.baseURI).href);
const snap = () => ({
	text: calc.text, entries: [...calc.entries], highlight: calc.highlight, settings: { ...calc.settings },
	second: calc.second, armed: calc.storeArmed, status: calc.status,
});
const view = shallowRef(snap());
const input = ref<HTMLInputElement | null>(null);
const systemKeys = ref(false);
const settingsOpen = ref(false);
const theme = ref(loadTheme());
const wide = ref(matchMedia(WIDE).matches);
const side = ref(matchMedia(SIDE).matches);
const historyBox = ref<HTMLElement | null>(null);
const keysBox = ref<HTMLElement | null>(null);
const backBox = ref<HTMLElement | null>(null);
const cleanups: (() => void)[] = [];

const unsubscribe = calc.subscribe(async () =>
{
	const before = view.value.entries.length;
	view.value = snap();
	await nextTick();
	if (input.value)
	{
		input.value.value = calc.text;
		input.value.setSelectionRange(calc.caret, calc.caret);
	}
	if (view.value.entries.length !== before)
	{
		historyBox.value?.scrollTo({ top: historyBox.value.scrollHeight });
	}
});
const media = matchMedia(WIDE);
const sideMedia = matchMedia(SIDE);
const onWide = () => { wide.value = media.matches; side.value = sideMedia.matches; };
onMounted(() =>
{
	media.addEventListener("change", onWide);
	sideMedia.addEventListener("change", onWide);
	applyTheme(theme.value);
	cleanups.push(trackKeys(keysBox.value!), holdToRepeat(backBox.value!, () => calc.backspace()));
});
onUnmounted(() =>
{
	media.removeEventListener("change", onWide);
	sideMedia.removeEventListener("change", onWide);
	unsubscribe();
	cleanups.forEach(clean => clean());
});

// Android back closes the menu and overlays by itself once in Capacitor; in a browser, Back leaves the page
const status = computed(() => view.value.armed ? "STO: press x or y" : view.value.second ? "2nd" : view.value.status);

async function press(key: Key, where: HTMLElement)
{
	const menu = await calc.press(calc.resolve(key));
	if (menu === "func")
	{
		await openFunctions();
	}
	else if (menu === "units")
	{
		await openConversions();
	}
}

async function sheet(header: string, items: string[]): Promise<string | null>
{
	const actions = await actionSheetController.create({
		header,
		buttons: [...items.map(text => ({ text, data: text })), { text: "Cancel", role: "cancel" }],
	});
	await actions.present();
	const { data, role } = await actions.onDidDismiss();
	return role === "cancel" || data == null ? null : String(data);
}

async function openFunctions()
{
	const group = await sheet("Functions", Object.keys(calc.catalog.functions));
	const name = group && await sheet(group, calc.catalog.functions[group]);
	if (name)
	{
		await calc.insertFunction(name);
	}
}

async function openConversions()
{
	const groups = Object.keys(calc.catalog.conversions).filter(group => group !== "Currency");
	const group = await sheet("Conversions", groups);
	const units = group ? calc.catalog.conversions[group] : [];
	const source = group && await sheet(`${group}: from`, units);
	const target = source && await sheet(`${source} to`, units.filter(unit => unit !== source));
	if (source && target)
	{
		await calc.insertConversion(source, target);
	}
}

async function openMore()
{
	const choice = await sheet("SoftCalc", ["Functions", "Conversions", "Settings", "Clear History", "Svelte mockup"]);
	if (choice === "Functions") await openFunctions();
	else if (choice === "Conversions") await openConversions();
	else if (choice === "Settings") settingsOpen.value = true;
	else if (choice === "Clear History") calc.clearHistory();
	else if (choice === "Svelte mockup") location.href = "svelte.html";
}

function onInput(event: Event)
{
	const box = event.target as HTMLInputElement;
	calc.typed(box.value, box.selectionStart ?? box.value.length);
}

function onKey(event: KeyboardEvent)
{
	if (event.key === "Enter") { event.preventDefault(); calc.submit(); }
	else if (event.key === "ArrowUp") { event.preventDefault(); calc.walk(true); }
	else if (event.key === "ArrowDown") { event.preventDefault(); calc.walk(false); }
	else if (event.key === "Escape") { calc.cancelWalk(); }
}

async function toggleKeyboard()
{
	systemKeys.value = !systemKeys.value;
	await nextTick();
	input.value?.blur();
	input.value?.focus();
}

function setTheme(value: string)
{
	theme.value = value;
	saveTheme(value);
	applyTheme(value);
}
</script>

<template>
	<ion-app>
		<ion-menu menu-id="sci" content-id="main" :side="side ? 'end' : 'start'" type="overlay" :disabled="wide" class="sci-menu">
			<ion-content :scroll-y="false">
				<div class="sci-box">
					<button class="sci-tab" @click="menuController.close('sci')">◂ Basic</button>
					<Keypad :keys="SCIENTIFIC" sci :second="view.second" :armed="view.armed" :base="view.settings.base"
						:angle="view.settings.angle" @press="press" />
				</div>
			</ion-content>
		</ion-menu>

		<div id="main" class="ion-page">
			<ion-header>
				<ion-toolbar>
					<ion-buttons slot="start">
						<ion-button v-if="!wide" @click="menuController.open('sci')">Sci</ion-button>
					</ion-buttons>
					<ion-title>SoftCalc</ion-title>
					<ion-buttons slot="end">
						<ion-button :color="systemKeys ? 'primary' : undefined" @click="toggleKeyboard">
							<ion-icon slot="icon-only" :icon="keypadOutline" />
						</ion-button>
						<ion-button @click="openMore"><ion-icon slot="icon-only" :icon="ellipsisVertical" /></ion-button>
					</ion-buttons>
				</ion-toolbar>
			</ion-header>
			<ion-content :scroll-y="false">
				<div class="calc">
					<div ref="historyBox" class="history">
						<div v-if="view.entries.length === 0" class="empty-history">Type with the keys; tap a line to use it again.</div>
						<div v-for="(entry, index) in view.entries" :key="index" class="entry ion-activatable"
							:class="[entry.kind, { selected: index === view.highlight }]" @click="calc.tapEntry(index)">
							<div class="top"><span>{{ entry.input }}</span>
								<span v-if="entry.display !== entry.input" class="sub">{{ entry.display }}</span></div>
							<div class="result">{{ entry.text }}</div>
						</div>
					</div>
					<div class="hint">{{ status }}</div>
					<div class="input-row">
						<input ref="input" :inputmode="systemKeys ? 'text' : 'none'" autocomplete="off" autocapitalize="off"
							spellcheck="false" placeholder="0" @input="onInput" @keydown="onKey"
							@click="calc.moveCaret(($event.target as HTMLInputElement).selectionStart ?? 0)" />
						<button ref="backBox" class="backspace" aria-label="Delete one character">⌫</button>
					</div>
					<div ref="keysBox" class="keys">
						<button v-if="!wide" class="sci-tab" @click="menuController.open('sci')">Sci ▸</button>
						<Keypad v-if="wide" :keys="SCIENTIFIC" sci :second="view.second" :armed="view.armed"
							:base="view.settings.base" :angle="view.settings.angle" @press="press" />
						<Keypad :keys="BASIC" :second="view.second" :armed="view.armed" :base="view.settings.base"
							:angle="view.settings.angle" @press="press" />
					</div>
				</div>
			</ion-content>
		</div>

		<ion-modal :is-open="settingsOpen" @did-dismiss="settingsOpen = false">
			<ion-header>
				<ion-toolbar>
					<ion-title>Settings</ion-title>
					<ion-buttons slot="end"><ion-button @click="settingsOpen = false">Done</ion-button></ion-buttons>
				</ion-toolbar>
			</ion-header>
			<ion-content>
				<ion-list>
					<ion-item>
						<ion-select label="Precision" :value="view.settings.precision" interface="popover"
							@ion-change="calc.setSetting('precision', $event.detail.value)">
							<ion-select-option v-for="digits in [32, 64, 128, 512]" :key="digits" :value="digits">{{ digits }} digits</ion-select-option>
						</ion-select>
					</ion-item>
					<ion-item>
						<ion-label>Angle</ion-label>
						<ion-segment :value="view.settings.angle" @ion-change="calc.setSetting('angle', $event.detail.value)">
							<ion-segment-button value="deg">deg</ion-segment-button>
							<ion-segment-button value="rad">rad</ion-segment-button>
							<ion-segment-button value="grad">grad</ion-segment-button>
						</ion-segment>
					</ion-item>
					<ion-item>
						<ion-select label="Output base" :value="view.settings.base" interface="popover"
							@ion-change="calc.setSetting('base', $event.detail.value)">
							<ion-select-option v-for="base in ['dec', 'hex', 'oct', 'bin']" :key="base" :value="base">{{ base }}</ion-select-option>
						</ion-select>
					</ion-item>
					<ion-item>
						<ion-label>Theme</ion-label>
						<ion-segment :value="theme" @ion-change="setTheme(String($event.detail.value))">
							<ion-segment-button value="system">system</ion-segment-button>
							<ion-segment-button value="light">light</ion-segment-button>
							<ion-segment-button value="dark">dark</ion-segment-button>
						</ion-segment>
					</ion-item>
				</ion-list>
			</ion-content>
		</ion-modal>
	</ion-app>
</template>

<style>
/* The Sci panel lies over the keys only; History and Input stay in view (owner 2026-11-18) */
.sci-menu { --width: var(--keys-width, 100vw); --ion-backdrop-opacity: 0.12; }
.sci-menu::part(container) { top: var(--keys-top, 50vh); height: var(--keys-h, 50vh); bottom: auto; }
.sci-box { position: absolute; inset: 0; display: flex; padding-top: 26px; background: var(--key-bg); }
.sci-box > .keypad { flex: 1; }
.calc { position: absolute; inset: 0; }
</style>
