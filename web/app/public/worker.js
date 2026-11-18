// worker.js - The SoftCalc engine on Pyodide, in a module Web Worker
//
// Requests and responses are the desktop protocol (design/protocol.md);
// the first message is {id: 0, op: "ready"} or {id: 0, op: "failed"}; before
// it, {id: 0, op: "step", name, at} marks the end of each start step (at: ms
// since the epoch, so the page can line it up with its own clock).
// Pyodide (in a folder named after its version, so the service worker can
// keep it for good) and engine.zip (the engine and mpmath) are copied next
// to this file by prepare.mjs.

import { loadPyodide } from "./pyodide-314.0.7/pyodide.mjs";

let handle = null;

function step(name)
{
	postMessage({ id: 0, op: "step", name, at: performance.timeOrigin + performance.now() });
}

step("worker");

async function start()
{
	// engine.zip downloads while Python starts
	const engineZip = fetch(new URL("./engine.zip", import.meta.url)).then(answer =>
	{
		if (!answer.ok)
		{
			throw new Error(`engine.zip: HTTP ${answer.status}`);
		}
		return answer.arrayBuffer();
	});
	engineZip.catch(() => undefined);   // reported below, once Python is up
	const base = new URL("./pyodide-314.0.7/", import.meta.url).href;
	// Python's library compiled (make_engine.py); Pyodide's own has .py files only
	const pyodide = await loadPyodide({ indexURL: base, stdLibURL: base + "python_stdlib_pyc.zip" });
	step("python");
	// The engine and mpmath, compiled, at the front of sys.path
	pyodide.unpackArchive(await engineZip, "zip", { extractDir: "/home/pyodide" });
	step("unpack");
	await pyodide.runPythonAsync(`
import sys, json
sys.path.insert(0, "/home/pyodide")
from softcalc_engine.server import Dispatcher
from softcalc_engine import __version__
_dispatcher = Dispatcher()

def handle_json(text):
    return json.dumps(_dispatcher.handle(json.loads(text)), ensure_ascii=False)
`);
	step("import");
	handle = pyodide.globals.get("handle_json");
	postMessage({ id: 0, op: "ready", version: pyodide.globals.get("__version__") });
}

const ready = start().catch(error => postMessage({ id: 0, op: "failed", error: String(error) }));

onmessage = async event =>
{
	await ready;
	if (handle)
	{
		postMessage(JSON.parse(handle(JSON.stringify(event.data))));
	}
};
