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
```
cd web
npm install
python make_engine.py
python -m http.server 8765 --bind 127.0.0.1    # open http://127.0.0.1:8765/
node test_engine.mjs                            # engine tests in Pyodide
```

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
