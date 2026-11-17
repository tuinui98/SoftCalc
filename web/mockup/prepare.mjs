// prepare.mjs - Copy Pyodide and pack the engine into mockup/public (not in git)
//
// Vite serves public/ as it is, so the worker finds ./pyodide/ and
// ./engine.zip next to it in development and in the build.
//   node mockup/prepare.mjs

import { copyFileSync, mkdirSync } from "node:fs";
import { execFileSync } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const web = path.resolve(here, "..");
const source = path.join(web, "node_modules", "pyodide");
const target = path.join(here, "public", "pyodide");
mkdirSync(target, { recursive: true });
for (const name of ["pyodide.mjs", "pyodide.asm.mjs", "pyodide.asm.wasm", "python_stdlib.zip", "pyodide-lock.json",
	"mpmath-1.4.1-py3-none-any.whl"])
{
	copyFileSync(path.join(source, name), path.join(target, name));
}
execFileSync("python", [path.join(web, "make_engine.py")], { stdio: "inherit" });
copyFileSync(path.join(web, "engine.zip"), path.join(here, "public", "engine.zip"));
console.log("prepared", path.join(here, "public"));
