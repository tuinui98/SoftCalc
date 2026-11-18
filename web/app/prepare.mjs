// prepare.mjs - Copy Pyodide, pack the compiled engine and library into app/public and make the Help (not in git)
//
// Vite serves public/ as it is, so the worker finds ./pyodide-<version>/
// and ./engine.zip next to it in development and in the build.
//   node app/prepare.mjs

import { copyFileSync, mkdirSync, readFileSync } from "node:fs";
import { execFileSync } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const web = path.resolve(here, "..");
const source = path.join(web, "node_modules", "pyodide");
const version = JSON.parse(readFileSync(path.join(source, "package.json"), "utf8")).version;
const target = path.join(here, "public", `pyodide-${version}`);
mkdirSync(target, { recursive: true });
// Python's library and mpmath come compiled from make_engine.py, not as Pyodide ships them
for (const name of ["pyodide.mjs", "pyodide.asm.mjs", "pyodide.asm.wasm", "pyodide-lock.json"])
{
	copyFileSync(path.join(source, name), path.join(target, name));
}
execFileSync("python", [path.join(web, "make_engine.py")], { stdio: "inherit" });
copyFileSync(path.join(web, "engine.zip"), path.join(here, "public", "engine.zip"));
copyFileSync(path.join(web, "python_stdlib_pyc.zip"), path.join(target, "python_stdlib_pyc.zip"));
execFileSync("node", [path.join(here, "make_help.mjs")], { stdio: "inherit" });
console.log("prepared", path.join(here, "public"));
