// main.ts - Start the SoftCalc web app: start the engine, open the store, show the screen, register the service worker

import { mount } from "svelte";
import App from "./App.svelte";
import { Engine } from "./lib/engine";
import { openStore } from "./lib/store";
import "./lib/layout.css";
import "./lib/keypad.css";

// The engine takes longest, so it starts first, while the store opens
const engine = new Engine(new URL("./worker.js", document.baseURI).href);
const { store, kept } = await openStore();
mount(App, { target: document.getElementById("app")!, props: { engine, store, kept } });

// Offline and quick second starts. A service worker needs a secure page:
// https, or localhost; a plain http address on the network has none.
// The Android app has its files inside the APK, and a service worker
// there could keep serving an old version after an update.
const inApp = (window as { Capacitor?: { isNativePlatform?: () => boolean } }).Capacitor?.isNativePlatform?.() === true;

// In the Android app the system Back closes the app unless it is handed
// to the page: there it goes back through the open layers (App.svelte),
// and leaves the app only from the main screen.
if (inApp)
{
	import("@capacitor/app").then(({ App: Native }) =>
	{
		void Native.addListener("backButton", ({ canGoBack }) =>
		{
			if (canGoBack)
				history.back();
			else
				void Native.exitApp();
		});
	});
}

if ("serviceWorker" in navigator && isSecureContext && !inApp)
{
	navigator.serviceWorker.register("./sw.js").catch(error => console.warn("service worker:", error));
	// The files this first visit loaded before the service worker was in charge
	navigator.serviceWorker.ready.then(registration =>
	{
		const urls = performance.getEntriesByType("resource").map(entry => entry.name)
			.filter(url => url.startsWith(location.origin));
		registration.active?.postMessage({ type: "cache", urls: [location.href, ...urls] });
	});
}
