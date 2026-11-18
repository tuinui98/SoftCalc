// vite.config.mjs - Build and test the SoftCalc web app (Svelte)
//
// base "./" keeps the build relative, so it works from any folder and
// later inside a Capacitor app. Tests: vitest (tests/*.test.ts).

import { defineConfig } from "vite";
import { svelte } from "@sveltejs/vite-plugin-svelte";
import { readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));

// SoftCalc's version (ui/SoftCalc/SoftCalc.csproj, as the desktop and the APK) for the start
// screen in index.html, which shows before any script has loaded
const csproj = readFileSync(path.join(here, "..", "..", "ui", "SoftCalc", "SoftCalc.csproj"), "utf8");
const version = /<Version>([^<]+)<\/Version>/.exec(csproj)[1];
const withVersion = { name: "softcalc-version", transformIndexHtml: html => html.replaceAll("%SOFTCALC_VERSION%", version) };

export default defineConfig({
	root: here,
	base: "./",
	plugins: [svelte(), withVersion],
	build: { outDir: path.join(here, "dist"), emptyOutDir: true, target: "es2022" },
	preview: { port: 8767 },
	test: { include: ["tests/**/*.test.ts"], environment: "node" },
});
