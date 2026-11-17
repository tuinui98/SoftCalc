// test_engine.mjs - Run the engine's pytest suite inside Pyodide (Node)
//
// The same tests as `pytest` in engine/, but on the WebAssembly Python
// the browser build uses, so a pass here means the web engine answers
// like the desktop one. The repository is mounted read-only in spirit:
// no bytecode or pytest cache is written. Tests that start a process
// (test_protocol's pipe tests) cannot run in Pyodide and are deselected.
//   node test_engine.mjs [extra pytest arguments]

import { loadPyodide } from "pyodide";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const started = performance.now();
const pyodide = await loadPyodide();
const loaded = performance.now();
await pyodide.loadPackage(["mpmath", "pytest"]);
const packages = performance.now();

pyodide.FS.mkdirTree("/repo");
pyodide.FS.mount(pyodide.FS.filesystems.NODEFS, { root: repo }, "/repo");
pyodide.globals.set("extra", process.argv.slice(2));
const code = await pyodide.runPythonAsync(`
import os, sys
sys.dont_write_bytecode = True
os.chdir("/repo/engine")
sys.path.insert(0, "/repo/engine")
import pytest
pytest.main(["-q", "-p", "no:cacheprovider", "--deselect", "tests/test_protocol.py::test_process_round_trip",
	"-k", "not frozen and not process and not subprocess", *extra.to_py()])
`);
const done = performance.now();
console.log(`pyodide ${((loaded - started) / 1000).toFixed(1)} s, packages ${((packages - loaded) / 1000).toFixed(1)} s, tests ${((done - packages) / 1000).toFixed(1)} s`);
process.exit(Number(code));
