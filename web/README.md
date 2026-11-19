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
Wide screens (landscape, at least 900 x 600) show Sci beside Basic in both.
The ⌨ button lets the system keyboard open on Input.

Owner 2026-11-18, after a first try on a tablet and a phone: the 2nd key
is yellow; the Sci panel covers only the keys (History and Input stay in
view), with "Sci ▸" / "◂ Basic" at the top left of the keys as on the
desktop; a phone on its side puts History left and the keys right
(the wide layout had made the keys flat); ⌫ beside Input deletes one
character, and repeats while held.

Owner 2026-11-18, second try: **Svelte chosen** (lighter; both looked the
same). The phones use gesture navigation (edge swipe = Back, swipe from
the bottom = app list), so mockup B no longer uses edge swipes or the
Back button: Basic and Sci are two pages, a swipe left or right on the
keys flips them and the keys follow the finger, a swipe up on the keys
opens the menu, taps press keys as before (`shared/swipe.ts`).
```
cd web
npm install
npm run mockup        # prepare, build, serve on the network: open http://<this PC>:8766/ on the tablet
```
Versions are pinned (exact, at least two weeks old): vite 8.3.5, vue
3.5.44, @ionic/vue 9.0.10, vue-router 5.4.0 (Ionic imports it),
ionicons 8.1.0, svelte 5.57.3, @sveltejs/vite-plugin-svelte 7.3.1.

## The app (step 1, owner 2026-11-18)
`app/` is the real web app, in Svelte, built from mockup B:
- `src/lib/calc.ts`: the calculator (keys, History walk, menus), with the
  engine and the store passed in; `tests/calc.test.ts` checks its rules
  (ported from the desktop KeypadRules, InputRules and InputRecall tests)
  and its saving, against a fake engine. `npm test` runs them.
- `src/lib/store.ts`: settings, definitions (`pcf_export` text) and the
  last 1000 History lines in IndexedDB, on the device only. On start, as
  on the desktop: settings, `pcf_import`, then `restore_results` so `$n`
  works again. A browser that refuses IndexedDB still works, without
  keeping anything (the status line says so).
- `public/sw.js`: a service worker, so the app works offline and keeps
  Pyodide (in `pyodide-<version>/`) and the build files; the page,
  `worker.js` and `engine.zip` come from the network first. It needs a
  secure page: `https`, or `localhost` / `127.0.0.1`. On a plain
  `http://192.168.x.x` address it does not start (everything else works).
- `public/manifest.webmanifest` and icons from `softcalc.ico`: the app can
  be installed ("Add to Home screen") from a secure page.
```
cd web
npm install
npm test             # app rules
npm run app          # prepare, build, serve on the network: http://<this PC>:8767/
```
Verified in desktop Chrome (2026-11-18): History, definitions and `$n`
come back after a reload; with the server stopped the app starts from
the cache and calculates. A cached file has to be matched with
`ignoreVary`: the server sends `Vary: Origin` and module scripts are
asked for with an Origin header.

### Steps 2 to 4 (owner 2026-11-18)
- Every key, ⌫, Sci / Basic, a History tap and a menu pick bring the
  focus back to Input.
- The hint line under Input comes from the engine (`hint`,
  `softcalc_engine/hints.py`), the same rules as the desktop. One line,
  cut with ...; a tap shows all of it. Settings > Input hints.
- ⋮ > Variables lists the user's variables and functions: a tap puts the
  name in Input, ✕ then "Delete?" removes it. Functions has "Your
  functions" first when there are any.
- A long press (or a right click) on a History line: Copy result, Copy
  line, Delete line (a result line also leaves `$n`, as on the desktop).
  Copy falls back to the old copy command on a page that is not secure.
- System Back closes the open menu, Variables or Settings first.
- Settings: decimals (all, or − / + from 2, 0 to 30), pad zeros, word
  size, text size (100 to 200%, History and Input).
- RPN is not in the web app (owner 2026-11-18).

### Currency and coin prices (owner 2026-11-18)
- `100 [THB->USD]`: on `rates_needed` the app fetches the ECB reference
  rates and evaluates again (`src/lib/rates.ts`, the desktop RatesSource
  and EcbCalendar). The desktop's eurofxref-daily.xml cannot be read by a
  web page (no CORS header); the ECB Data API
  (`data-api.ecb.europa.eu/service/data/EXR/D..EUR.SP00.A`, CSV, about
  5 KB) gives the same rates with `Access-Control-Allow-Origin: *`. It
  asks for the last 10 days (`startPeriod`) and uses the newest day: the
  API keeps answers per page origin for 30 s only, and for a new origin
  `lastNObservations=1` took 60 to 90 s, `startPeriod` 3 to 12 s.
- `1 [BTC->THB]`, `1000 [sat->USD]`: on `prices_needed` the app fetches
  CoinGecko (`src/lib/prices.ts`, the desktop PriceSource); good for 5
  minutes.
- Both are kept in IndexedDB; a failed fetch uses them, shown `, offline`.
  The History line shows where the rate came from (`ECB 2026-11-17`).
- Conversions > Currency lists the common codes, the coins, then the rest.

### Help (owner 2026-11-18)
- ⋮ > Help shows the desktop's topics (`help/*.md`) that fit the web
  app: 01 to 09, 16 and 18. `app/make_help.mjs` (run by `prepare.mjs`,
  `marked` at build time only) writes `app/src/generated/help.json`,
  loaded when Help opens.
- In a shared topic, `<!-- desktop -->` ... `<!-- /desktop -->` lines
  are the desktop's own text and `<!-- web -->` ... `<!-- /web -->` the
  web's; each side leaves out the other's (desktop:
  `HelpMarkdown.ForDesktop`). `app/help/` holds the web's own version of
  a topic (Getting started). Pictures are left out.
- A `calc` example is a button that puts its line in Input; the engine
  test `test_help_examples.py` checks the web's own topics too.

### Start time (owner 2026-11-19)
The owner timed 4-5 s on a desktop and 7-8 s on a phone. Pyodide ships
Python's library and mpmath as `.py` files and writes no `.pyc`, so each
start compiled them all again. `make_engine.py` now compiles them with
this machine's Python (it must be Pyodide's 3.x): `engine.zip` holds
`softcalc_engine/` and `mpmath/` as `.pyc`, `python_stdlib_pyc.zip` is
Pyodide's library as `.pyc` (passed as `stdLibURL`). The engine worker
starts before the store opens, engine.zip downloads while Python starts,
History shows at once, and = waits for the engine. A bar on top of Input
moves with the start steps; Settings > Start shows each step's time.

Desktop Chrome, files cached: Python 3.8 s to 0.7 s, mpmath + engine
1.3 s to 0.1-0.2 s; ready in 1.3-2.5 s instead of 6.1 s.

### Export and Import (owner 2026-11-19, moved into the first release)
⋮ > Export writes History as the desktop's `history.jsonl` and the
variables and functions as the desktop's `.pcf` (UTF-16 LE, no BOM, CRLF),
so files go between the web app, the Android app and the desktop
unchanged (`src/lib/transfer.ts`; the desktop test
`PersistenceTests.ParsesWebAppExport` reads the web's lines). Names carry
the local time, `SoftCalc-history-2026-11-19-1530.jsonl` and
`SoftCalc-definitions-2026-11-19-1530.pcf`, as the desktop's Export names
them; for the desktop's data folder History is renamed `history.jsonl`.
A browser downloads them to its Downloads, with no folder of its own: a
page cannot make one (both as octet-stream, the blob kept a minute: Chrome
on Android dropped the ndjson History). Its chooser lists the two types,
as Chrome on Android otherwise offers the camera too. The Android app saves them to Download/SoftCalc
with its own plugin (`DownloadsPlugin.java`: MediaStore, no permission on
Android 10 and later; the storage permission up to SDK 28), not the share
sheet: a device without a file manager app had nowhere to save, and the
system's file chooser lists Download for Import (owner 2026-11-19, after
trying the share sheet). ⋮ > Import takes several files at once (hold the
first in the chooser), each by what is in it:
- A History file merges by id, as the desktop merges windows (after the
  file's newest clear, without its deleted lines, by time, the newest
  1000), and `$n` then counts the merged results. History lines now carry
  the desktop's `id` and `time`; older lines get them on the next start.
- Any other file is a `.pcf` whose definitions are added: the current
  ones, then the file's, so a name in both takes the file's. The
  desktop's Load replaces them all (`import_pcf`).

### Graph (owner 2026-11-19)
⋮ > Graph opens a full page (Back closes it): the plot, a tool row (Fit,
1:1, Reset, Grid, Polar), a status line and the lines with tick boxes.
A phone shows the plot above the list, a tablet on its side beside it.
The engine's `plot`, `plot_equation`, `value_at` and `equation_at` do
the work, as on the desktop; `src/lib/graph.ts` is PlotMath (ranges,
ticks, runs, fit, 1:1, trace hits) and `src/lib/plotter.ts` the
requests, one round at a time with changes folded into the next.
Gestures (owner): **one finger traces** the nearest line; **two fingers
move and zoom**, along one axis when they lie along it (within 20
degrees); a double tap zooms in. A mouse traces by hovering, drags to
move and wheels to zoom. The equations are the ones a .pcf's [Graph]
carries (`graph` in the store); the range and the lines shown are
`graphView`. A first run draws `y=sin(x)` and `y=x/360` over -360..360,
as the desktop.
- G1 (done): the page, drawing, gestures, trace with Copy and Insert,
  Polar; the tools sit beside the title on a phone on its side; Pan lets
  one finger move instead of trace (kept, for one hand).
- G2 (done): + Function and + Equation (checked by the engine, no
  History line); press and hold a line for Insert, Edit, Colour (12)
  and Delete. The plot keeps its size while the editor's keyboard is up.
- G3 (done): Examples (`ExamplesView.svelte`) from
  `data/graph-examples.json`, which the desktop embeds too.
- Help: `app/help/11-graph.md`, the web's own topic.
- PNG (done): the plot with a legend of the drawn lines, saved as
  `SoftCalc-graph-<local time>.png` (files.ts, as Export). Not planned: typed ranges, the
  Advanced View's small graph.

### Compound (owner 2026-11-20)
⋮ > Compound (`CompoundView.svelte`, `src/lib/compound.ts`) is the
desktop's Compound Interest window on the engine's `schedule`: inputs
(kept as `compound` in the store), totals that go to Input, a stacked
growth chart a finger reads, and a table that makes only the rows on
screen. Export: CSV as the desktop's (`SoftCalc-compound-<time>.csv`; the
desktop's Export CSV now suggests the same name), a PNG of the chart with
a summary line, and Copy of that line. Help: 09 (web block).

### Random (owner 2026-11-20)
⋮ > Random (`RandomView.svelte`, `CardsTab.svelte`) has the desktop's
Simple, Advanced and Password tabs and a plain Cards tab. Draws use
crypto.getRandomValues without bias (`random.ts`, `shapes.ts`, ports of
RandomTools.cs and RandomShapes.cs). Seed draws through the engine's
`draws` with `apart: true` (a second Engine in the same worker), so the
calculator's seed and `$n` are untouched. Cards: the ready-made decks
from `data/decks/*.txt` (the desktop embeds the same files), own decks as
`name | weight` lines, the desktop's CSV both ways, unticked cards and
No repeat piles kept (`cards` in the store). Not here (owner): Entropy,
Mix, draw tabs with steps and conditions, pictures, slot/dart/wheel.
A copied password is cleared after 30 s while the page is open; a
phone's clipboard history cannot be kept out on the web. Help 17 (web).

### On the web (owner 2026-11-19)
https://tuinui98.github.io/SoftCalc/ is built and published by
`.github/workflows/pages.yml` when main changes the web app, the engine,
the Help or the version (or by hand: Actions > Run workflow). It runs
`npm test` before the build; a failing test publishes nothing. On https
the service worker runs, so the app works offline and can be installed
from the browser. History is kept per address: another address starts
empty.

## The Android app (owner 2026-11-18)

Capacitor 8.6.0 puts the built web app (`app/dist`) into an Android app in
`android/`: package `io.github.tuinui98.softcalc`, Android 7 (SDK 24) and
later, built against SDK 36. Inside the app there is no service worker
(the files are in the APK), and the system Back goes back through the
open layers (`@capacitor/app`), leaving only from the main screen.

Tools, installed without admin rights:
- JDK 21: Microsoft's zip build in `%LOCALAPPDATA%\Programs\jdk\jdk-21.*`
  (the winget package needs a machine install).
- Android SDK in `%LOCALAPPDATA%\Android\Sdk`: the command-line tools in
  `cmdline-tools\latest`, then `android sdk install platform-tools
  platforms/android-36 build-tools/36.0.0` (`sdkmanager.bat` splits names
  at `;`).

The release key is never in git. `android/keystore.local.properties`
(gitignored; the form is in `android/keystore.example.properties`) names
the keystore, kept outside the repo, and its passwords. Keep a copy of
both somewhere safe: an installed SoftCalc takes an update only when it
is signed by the same key. The key's SHA-256 is what Android developer
verification asks for.

The version is SoftCalc's own (`<Version>` in `ui/SoftCalc/SoftCalc.csproj`).
versionCode is `(major*10000 + minor*100 + patch)*10 + androidBuild`;
raise `androidBuild` in `android/gradle.properties` for an APK-only fix,
and set it back to 0 with the next SoftCalc version.

Icons: `dotnet run --project tools/IconMaker -- --android
web/android/app/src/main/res` draws the launcher icons from the same
drawing as the desktop icon, on the web app's dark page colour.

```
cd web
npm run apk          # web build, Capacitor sync, Gradle release, signature check
```
It writes `dist-apk/SoftCalc-<version>.apk` (not in git) and prints the
APK's SHA-256 and the signing key's. To install it on a phone, serve it
with the type `application/vnd.android.package-archive`; without that
type Chrome on Android saves it as a .zip.
