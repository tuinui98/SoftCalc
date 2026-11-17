// vite.config.mjs - Build and test the SoftCalc web app (Svelte)
//
// base "./" keeps the build relative, so it works from any folder and
// later inside a Capacitor app. Tests: vitest (tests/*.test.ts).

import { defineConfig } from "vite";
import { svelte } from "@sveltejs/vite-plugin-svelte";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));

export default defineConfig({
	root: here,
	base: "./",
	plugins: [svelte()],
	build: { outDir: path.join(here, "dist"), emptyOutDir: true, target: "es2022" },
	preview: { port: 8767 },
	test: { include: ["tests/**/*.test.ts"], environment: "node" },
});
