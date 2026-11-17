// vite.config.mjs - Build both mockups (Ionic + Vue, Svelte) as pages of one site
//
// index.html links to ionic.html and svelte.html; both use the same
// engine worker in public/. base "./" keeps the build relative, so it
// also works from a Capacitor app later.

import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";
import { svelte } from "@sveltejs/vite-plugin-svelte";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));

export default defineConfig({
	root: here,
	base: "./",
	plugins: [vue(), svelte()],
	build: {
		outDir: path.join(here, "dist"),
		emptyOutDir: true,
		rollupOptions: {
			input: {
				index: path.join(here, "index.html"),
				ionic: path.join(here, "ionic.html"),
				svelte: path.join(here, "svelte.html"),
			},
		},
	},
	preview: { port: 8766 },
});
