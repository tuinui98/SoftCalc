// worker.js - The SoftCalc engine on Pyodide, in a Web Worker
//
// The page posts protocol requests ({id, op, ...}, design/protocol.md)
// and gets one response per request, as the desktop UI does over pipes.
// Pyodide comes from the local npm copy (node_modules/pyodide, pinned in
// package.json), so nothing is fetched from a CDN and it can work offline.
// A module worker: new Worker("worker.js", { type: "module" }).

import { loadPyodide } from "./node_modules/pyodide/pyodide.mjs";

const PYODIDE = new URL("./node_modules/pyodide/", import.meta.url).href;

let handle = null;

async function start()
{
	const t0 = performance.now();
	const pyodide = await loadPyodide({ indexURL: PYODIDE });
	const t1 = performance.now();
	await pyodide.loadPackage("mpmath");
	const t2 = performance.now();
	const zip = await (await fetch("engine.zip", { cache: "no-store" })).arrayBuffer();
	pyodide.unpackArchive(zip, "zip", { extractDir: "/home/pyodide" });
	await pyodide.runPythonAsync(`
import sys, json
sys.path.insert(0, "/home/pyodide")
from softcalc_engine.server import Dispatcher
from softcalc_engine import __version__
_dispatcher = Dispatcher()

def handle_json(text):
    return json.dumps(_dispatcher.handle(json.loads(text)), ensure_ascii=False)

def spike_clock():
    from datetime import datetime
    now = datetime.now().astimezone()
    return json.dumps({"python_now": now.isoformat(timespec="seconds"), "python_offset": str(now.utcoffset()),
        "python_tzname": now.tzname()})
`);
	const t3 = performance.now();
	handle = pyodide.globals.get("handle_json");
	const clock = JSON.parse(pyodide.globals.get("spike_clock")());
	postMessage({
		id: 0, op: "ready", version: pyodide.globals.get("__version__"),
		timings: { pyodide: t1 - t0, mpmath: t2 - t1, engine: t3 - t2 },
		clock: { ...clock, js_now: new Date().toString(), js_offset_minutes: -new Date().getTimezoneOffset() },
	});
}

const ready = start().catch(error => postMessage({ id: 0, op: "failed", error: String(error) }));

onmessage = async event =>
{
	await ready;
	const request = event.data;
	const t0 = performance.now();
	const response = JSON.parse(handle(JSON.stringify(request)));
	response.ms = performance.now() - t0;
	postMessage(response);
};
