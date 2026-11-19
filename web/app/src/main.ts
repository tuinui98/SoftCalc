// main.ts - Start the SoftCalc web app: start the engine, open the store, show the screen, register the service worker

import { mount } from "svelte";
import App from "./App.svelte";
import { Engine } from "./lib/engine";
import { exitGuard } from "./lib/exitguard";
import { inApp as isApp } from "./lib/files";
import { openStore } from "./lib/store";
import "./lib/layout.css";
import "./lib/keypad.css";

// The engine takes longest, so it starts first, while the store opens
const engine = new Engine(new URL("./worker.js", document.baseURI).href);
const { store, kept } = await openStore();
mount(App, { target: document.getElementById("app")!, props: { engine, store, kept } });

// The start screen (index.html) stays SPLASH_MS from the page's start, then fades; the app
// under it is already working, so a slow engine shows on its own load bar (owner 2026-11-19)
const SPLASH_MS = 1500;
const splash = document.getElementById("splash");
setTimeout(() =>
{
	splash?.classList.add("gone");
	setTimeout(() => splash?.remove(), 400);
}, Math.max(0, SPLASH_MS - performance.now()));

// Offline and quick second starts. A service worker needs a secure page:
// https, or localhost; a plain http address on the network has none.
// The Android app has its files inside the APK, and a service worker
// there could keep serving an old version after an update.
const inApp = isApp();

// In the Android app the system Back closes the app unless it is handed
// to the page: there it goes back through the open layers (App.svelte),
// and leaves the app only from the main screen, on a second Back (exitguard.ts).
if (inApp)
{
	import("@capacitor/app").then(({ App: Native }) =>
	{
		void Native.addListener("backButton", ({ canGoBack }) =>
		{
			if (canGoBack)
				history.back();
			else if (exitGuard.press())
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
