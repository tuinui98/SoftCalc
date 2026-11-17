# SoftCalc Web (spike)

The Python engine running in the browser on [Pyodide](https://pyodide.org)
(MPL-2.0), as a first step to a web app (PWA) and an Android build
(Capacitor). Owner 2026-11-17: way 1 (PWA + Pyodide), then way 2 (APK).

## Files
- `index.html`: Input and History only, plus a **Measure** button.
- `worker.js`: Pyodide in a module Web Worker; calls the engine's
  `Dispatcher.handle` with the same requests as `design/protocol.md`.
- `make_engine.py`: packs `engine/softcalc_engine/*.py` into
  `engine.zip` (not in git), which the worker unpacks.
- `test_engine.mjs`: runs the engine's pytest suite inside Pyodide on Node.
- `package.json`: pins `pyodide` 314.0.7 (2026-09-14; Python 3.14.2,
  mpmath 1.4.1, the same as the desktop engine). It is served from
  `node_modules/`, not a CDN, so the page can work offline later.

## Run
Once, in `web/`: `npm install` (gets the pinned Pyodide into `node_modules/`).
```
python web/serve.py          # packs the engine, serves web/, opens http://127.0.0.1:8765/
python web/serve.py --lan    # also prints an address for a phone on the same Wi-Fi
node web/test_engine.mjs     # engine tests in Pyodide
```
The page must come from a server: opened as a file (`file://`) the
browser does not start the worker, and the page says so. If Pyodide or
`engine.zip` cannot be loaded, the status line shows why.

## Results (2026-11-18, desktop Chrome, local server)
- Engine tests in Pyodide: **604 passed**; 3 left out because they start a
  process (pipes, the frozen exe). 40 s, against 22 s on desktop Python.
- The engine needed no change: `Dispatcher.handle` takes a dict.
- Local time is right in the browser: `now` gives +07:00 like Windows.
  `fromepoch(0) [utc->local]` gives 14:00 on both desktop and web
  (`fromepoch` already shows local time), so it is the same answer.
- Download: about 14 MB as files, about 7 MB compressed (wasm 3.5 MB
  gzipped, stdlib 2.5 MB, mpmath 0.4 MB, engine 45 KB).
- Start: 9.8 s first, 7.1 s again (Pyodide 5.2 s, mpmath 0.4 s, engine
  1.4 s). A phone will be slower; a service worker and HTTP compression
  are the next things to try.

| line | web | desktop |
|---|---|---|
| `1/3`, `2^(1/3)`, `sin(1)`, `fv(...)` (64 digits) | 1-3 ms | under 1 ms |
| `(1/3)!` at 64 digits, first time | 40 ms | 4 ms |
| `(1/3)!` at 512 digits | 904 ms | 633 ms |
| plot of `x^2/(1+x!)`, 400 points | 123 ms | 28 ms |
| plot of `sin(x)*x`, 400 points | 8 ms | |

Typing a line answers at once; only heavy lines and plots of
factorials are noticeably slower (1.4-4x).

## Mockups (owner 2026-11-18: try both before choosing)
`mockup/` has two versions of the same main screen, on the same engine,
keys (`shared/keys.ts`, from the desktop `Keypad.cs`) and rules
(`shared/calc.ts`: C, %, memory keys, STO, base cycling, `answer` before
an operator, tap-to-recall with the highlight, entry/Up walk):
- **A. Ionic + Vue** (`ionic.html`): Sci in an `ion-menu` (swipe from the
  left edge or the Sci button), func / units and the ⋮ menu as action
  sheets, Settings in a full-screen modal. JS 246 KB gzipped.
- **B. Svelte** (`svelte.html`): hand-made drawer that follows the
  finger (left edge, or the "Sci ▸" tab), bottom sheets, Settings as a
  sheet, Back closes the top layer. JS 19 KB gzipped.
Wide screens (landscape, 900 px and more) show Sci beside Basic in both.
The ⌨ button lets the system keyboard open on Input.
```
cd web
npm install
npm run mockup        # prepare, build, serve on the network: open http://<this PC>:8766/ on the tablet
```
Versions are pinned (exact, at least two weeks old): vite 8.3.5, vue
3.5.44, @ionic/vue 9.0.10, vue-router 5.4.0 (Ionic imports it),
ionicons 8.1.0, svelte 5.57.3, @sveltejs/vite-plugin-svelte 7.3.1.
