// time_matrix.mjs - How long the big matrix functions take on the web engine (Pyodide in Node)
//
// Owner 2026-11-24: det/inv/solve take at most 50x50 and eig 20x20; this
// times them on the WebAssembly Python the browser uses, to see whether
// the web needs smaller limits. Same lines run on the desktop engine
// (engine/.venv) give the comparison.
//   node time_matrix.mjs

import { loadPyodide } from "pyodide";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const pyodide = await loadPyodide();
await pyodide.loadPackage(["mpmath"]);
pyodide.FS.mkdirTree("/repo");
pyodide.FS.mount(pyodide.FS.filesystems.NODEFS, { root: repo }, "/repo");
await pyodide.runPythonAsync(`
import sys, time
sys.dont_write_bytecode = True
sys.path.insert(0, "/repo/engine")
exec(open("/repo/web/time_matrix.py").read())
`);
