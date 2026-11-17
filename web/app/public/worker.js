// worker.js - The SoftCalc engine on Pyodide, in a module Web Worker
//
// Requests and responses are the desktop protocol (design/protocol.md);
// the first message is {id: 0, op: "ready"} or {id: 0, op: "failed"}.
// Pyodide (in a folder named after its version, so the service worker can
// keep it for good) and engine.zip are copied next to this file by
// prepare.mjs.

import { loadPyodide } from "./pyodide-314.0.7/pyodide.mjs";

let handle = null;

async function start()
{
	const pyodide = await loadPyodide({ indexURL: new URL("./pyodide-314.0.7/", import.meta.url).href });
	await pyodide.loadPackage("mpmath");
	const answer = await fetch(new URL("./engine.zip", import.meta.url));
	if (!answer.ok)
	{
		throw new Error(`engine.zip: HTTP ${answer.status}`);
	}
	pyodide.unpackArchive(await answer.arrayBuffer(), "zip", { extractDir: "/home/pyodide" });
	await pyodide.runPythonAsync(`
import sys, json
sys.path.insert(0, "/home/pyodide")
from softcalc_engine.server import Dispatcher
from softcalc_engine import __version__
_dispatcher = Dispatcher()

def handle_json(text):
    return json.dumps(_dispatcher.handle(json.loads(text)), ensure_ascii=False)
`);
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
