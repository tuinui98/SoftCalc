# SoftCalc - project status (handoff)

Updated 2026-11-24: version 1.8.0 released (ribbon rows; card decks exported and imported as one zip; card() draws from a deck in Input). Everything below is committed on `main` and
pushed to the GitHub repo https://github.com/tuinui98/SoftCalc (public: see TODO section 10).

Open work is listed in `design/TODO.md`.

## Where things stand
| milestone | state | tag |
|---|---|---|
| M0 toolchain | done | milestone/20261027-m0-toolchain |
| M1 engine core | done | milestone/20261027-m1-engine-core |
| M2 engine state, conversions, bases, RPN | done | milestone/20261027-m2-engine-state |
| M3 engine extensions (dates, display, decimals, finance, pcf) | done | milestone/20261027-m3-engine-ext |
| M4 protocol server + engine exe | done | milestone/20261027-m4-protocol |
| M5 UI shell | done | milestone/20261027-m5-ui-shell |
| M6 sidebar graph, variables, dialogs, Graph window | done | milestone/20261027-m6-sidebar-graph |
| M7 persistence and files | done | milestone/20261105-m7-persistence |
| M8 tools (compound interest window, ECB currency, copy menu, always on top) | done | milestone/20261106-m8-tools |
| M9 packaging (single portable exe, icon, about) | done | milestone/20261107-m9-portable |

Done after milestones, on `main` (no tag): durations `h:mm:ss`;
`yr mo wk h` units (year = 365 d, month = 30 d) and `[utc->local]`;
`now` and format tags `[longdatetime]` etc.; Up/Down input recall with
draft restore; RPN hints and the line-based RPN keypad (owner's option A);
Enter on an empty Input repeats the last line (observed on the original,
golden `U_repeat`); History layout with the input on the left and the
substituted numeric form right-aligned and faint; History View and a
400x651 window as defaults.

After M8, on `main` (no tag): Shift+click on History inserts the result
at the caret, Use Expression / Insert Result in its context menu; Input
context menu with Clear Input; ribbon (B3) with decimals, Clear History
and a Conversions drop-down; program icon.

2026-11-06/07, on `main` (no tag), details in `spec/extensions.md`:
Input hint line; View > Theme System/Light/Dark (WinForms colour mode,
restart on change); Text Size scales the Font dialog's size; Graph
window G1-G3 (`design/graph_plan.md`): axis numbers, coordinates, wheel
zoom, drag pan, keys, Fit, trace with full-precision `value_at`,
several functions with colours and legend, sidebar Y fitted; Graph
toolbar, narrower Range panel, stronger grid; Advanced View grows the
window to the left; Functions / Variables editor (list + Name /
Parameters / Definition fields, Apply, rename) replacing the one-line
Add/Edit dialogs; Trace setting saved; Graph status line fitted to one
row (a long full-precision value is cut with "...", Copy keeps all
digits). The owner checked drag repaint, rename, trace and the status
line with a real mouse (2026-11-07).

After the v1.0.0 release (2026-11-07, design/TODO.md section 5, details
in `spec/extensions.md` "After v1.0.0"): short About with the licences
behind a link; literals typed in another base shown in the output base
in History and the hint line (only when exact); History context menu
Delete / Clear History with `$n` renumbering and deleted marks in
history.jsonl (restore now keeps text results' `$n` places);
`twos()` / `signed()` with View > Word Size; `publish.ps1` builds
`SoftCalc.exe` (58.4 MB) and `SoftCalc-net10.exe` (9.3 MB, needs the
.NET 10 Desktop Runtime) and smoke-runs both. 1.1.0 was never released;
its changes went out in 1.2.0.

Customize Ribbon (2026-11-08, `design/ribbon_plan.md` R1-R3, details in
`spec/extensions.md` "Customize Ribbon"): the ribbon is built from a
layout of menu command ids; View > Customize Ribbon... (also the
ribbon's right-click menu) adds any menu command, moves, renames and
regroups buttons; saved in settings.json `ribbon`; new Tools > Graph...
menu item.

Graph lines (2026-11-09, `design/graph2_plan.md` G4-G6): line colours
picked from the Windows colour dialog; equations in x and y drawn by
the engine's `plot_equation` (adaptive marching squares); trace snaps
to the nearest shown line and solves equations with `equation_at`.

Graph window (2026-11-09): size, place and Range width remembered; 1:1
scale button with a lock that survives zoom, pan and resize.

Graph speed (2026-11-09): float drawing path in the engine (10-20x),
non-overlapping refresh rounds, sidebar waits while hidden or dragging.

Graph trace sends x as the shown decimal text (`g(1.43)` = 2.0449, not 2.0448999...).

Customize Ribbon drag and drop (2026-11-09): move buttons and groups,
drag commands in from the left, drag rows out to remove.

Area conversions and rai-ngan-wa (2026-11-09): Conversions > Area with
Thai units, `rnw` values typed as `4-1-50 [rnw]`, History `= 4-1-0`,
hint `4-1-50 = 4.375 rai`; History's faint text drops a repeated line.

Version 1.3.0 (2026-11-10/11, TODO sections 8 and 9): Help window
(File > Help, F1, topics in `help/`, pictures from `tools/HelpShots`);
version in the caption; build-day ECB rates embedded by `publish.ps1`;
Graph Add as Function... / Equation... / Examples... buttons in the
Graph window (the sidebar keeps the split button); Graph Examples
gallery with constants, notes and a hint after Use, thumbnails drawn in
radians (engine `plot_equation` takes an optional `angle`); Customize
Ribbon own icons (SVG pasted from iconmcp.com, a file or built-in,
kept as `icon_svg`; `SvgIcon` draws arcs, relative paths, fill-rule,
transforms), Space and Separator items; the sidebar graph draws the
Graph window's range at the window's scale (no more fitted Y).
`publish.ps1`'s rates-seed path had a stray line break from
2026-11-10; fixed in the 1.3.0 release.

Version 1.4.0 (2026-11-12/15, TODO section 11, design/random_plan.md):
`rand`/`rands` and shaped draws in the engine; the Random window
(Simple, Advanced, Password, Entropy, Seed source); card decks and draw
tabs with weights, conditions (`>>`), number runs (`[1,100]`), colours,
and List / Slot machine / Dartboard (rings past 36 cards) / Spin wheel
themes; new-user defaults (owner's ribbon, degrees, Advanced view, a
Spin tab). Help 17-random rewritten, all pictures regenerated; the help
renderer reads `####` and `\|` in tables.

Tests: engine `pytest` 586 passed; UI `dotnet test` 414 passed.

Version 1.5.0 (2026-11-15/16, design/crypto_plan.md, TODO section 11):
Tools > Cryptocurrency (Address, Balance, Watch, Fees; look only, no
secrets), coin units and CoinGecko prices in lines, polite use of
public services (1 s apart, waits when busy), a watch list with tray
notifications and address-poisoning warnings; the Numeric keypad
redrawn with Basic and Scientific (TI-89-like, 2nd) modes and a drag
bar. Help 18-cryptocurrency and the keypad part of 13-views.

Tests: engine `pytest` 607 passed; UI `dotnet test` 459 passed.

Version 1.5.1 (2026-11-16): History highlight follows Up/Down and a
click (Up/Down go on from the clicked line; a second click takes the
highlight off, Esc brings back the draft); Random Mix into System adds
a mix row whose seed (SHA-256("seed" + key), 128 bits) works with Use
as Seed. Help 01, 15 and 17. Release files renamed (owner 2026-11-16):
`SoftCalc-standalone.exe` has .NET inside, `SoftCalc.exe` needs the
.NET 10 Desktop Runtime (was `SoftCalc.exe` / `SoftCalc-net10.exe`).

Tests: engine `pytest` 607 passed; UI `dotnet test` 462 passed.

Version 1.5.2 (2026-11-19): File > Export History... and Import
History... (history.jsonl lines, merged by id; Import drops the clear
mark); Save/Export names carry the local time; a .pcf carries the
Graph's equations below a [Graph] line (Load adds them). The web app
(web/, GitHub Pages https://tuinui98.github.io/SoftCalc/) and the
Android APK read and write the same files. Help 04, 14; web 01. UI
hint rules moved into the engine earlier, so UI tests went down.

Tests: engine `pytest` 631 passed; UI `dotnet test` 447 passed; web 71.

Version 1.6.0 (2026-11-19): Scientific keypad (desktop and web): a
function key, and Functions menu picks, type `name(` at the caret instead
of wrapping the line; 2nd ( ) type [ ], 2nd ans types ->, so a conversion
like 5[m->ft] can be typed on the keys; STO and entry are gone. Graph
examples are data/graph-examples.json, shared with the web app. Web and
APK: the Graph (trace, two-finger move and zoom, Pan, Fit, 1:1, Polar,
add/edit/delete/colour lines, Examples, PNG), Back twice to exit on the
main screen (fully in the APK; a browser only after a tap), func and units
open on a quick tap. Help 03, 04, 13; web 01, 11.

Tests: engine `pytest` 632 passed; UI `dotnet test` 445 passed; web 93.

Version 1.7.0 (2026-11-20): every view has a smallest window (the menu
bar and the widest ribbon group across; the menu, the whole ribbon,
Samples, Input and one History entry down; Numeric its keypad, Advanced
its Graph and Variables boxes; narrowing Advanced narrows History first,
then both; MainForm.Sizes.cs). Numeric shows the ribbon like the other
views. Graph's Save as bitmap and Compound's Export CSV suggest names with
the local time. The ready-made decks moved to data/decks, shared with the
web app. Engine: `draws` takes apart (a second engine, for the web) and
parses the line once. Web and APK: Compound (inputs, totals, chart, table,
CSV/PNG/Copy) and Random (Simple, Advanced, Seed, Password, Cards). Help
09, 12; web 01, 09, 17.

Tests: engine `pytest` 634 passed; UI `dotnet test` 445 passed; web 119.

Version 1.8.0 (2026-11-24): a ribbon group can start a new row (Edit
Group > Start a new row; RibbonLayout.Rows). All own card decks go out
and come in as one zip, decks.json plus pictures/ (File > Export Decks /
Import Decks, the ... tab; web and APK: ⋮ Export / Import and the Cards
tab; DeckTransfer.cs, web decks.ts with its own zip.ts). card("Deck",
[count], [no repeat]) draws from a deck in Input (engine cards.py,
protocol `decks`; a Thai deck name gives Thai cards; several numbers come
out as {a, b, c}); Insert Command on the draw tab, the ... tab and the web
Cards tab. Help 03, 12, 14, 17; web 17.

Tests: engine `pytest` 649 passed; UI `dotnet test` 464 passed; web 129.

## M8 result (design/m8_plan.md)
- Currency tags `100 [usd->thb]` with ECB rates. The engine asks for
  rates (`rates_needed`); the UI fetches eurofxref-daily.xml only then,
  caches it as rates.xml and skips fetching while the cache holds the
  latest publication (TARGET calendar, 1 h retry). Every currency line
  shows `ECB <date>` faint on its result line (`, offline` after a
  failed fetch); the note is saved with History.
- Conversions > Currency: 10 common codes as pairs, then From / To
  groups with all codes inserting ` [THB` and `->USD]`.
- Tools > Compound Interest: auto recalculation (300 ms), errors next
  to their field, stacked growth graph, virtual period table, Ctrl+C,
  Copy all, Export CSV; inputs remembered.
- History context menu: Copy Result, Copy Expression and Result, Copy
  All. View > Always on Top (remembered).
- Checked end to end with a scratch data folder, including one real ECB
  fetch (2026-11-05 rates).
- Note: `powershell -File tools\drive_softcalc.ps1 -Lines @(...)` passes
  only the first line; run the script with `&` from PowerShell.

## M7 result (design/m7_plan.md)
- `%APPDATA%\SoftCalc\` (override: `SOFTCALC_DATA_DIR`): settings.json,
  definitions.pcf, history.jsonl; window, view, graph and calculation
  settings restored; answer/$n restored from History (`restore_results`).
- File menu: Save History, Save/Load Functions & Variables work.
- Checked end to end: two runs keep history and $1; two windows at once
  merge their History on close.
- Fixed on the way: input or a plot refresh before the engine finished
  starting used to trigger a needless restart (and, briefly, an
  unhandled exception that blocked closing).

## M9 result (design/m9_plan.md, owner's choices 2026-11-07)
- `build\publish.ps1` -> `dist\SoftCalc.exe`, one file, 58.4 MB:
  self-contained .NET 10 (win-x64, compressed) plus the PyInstaller
  one-folder engine as an embedded 8.7 MB zip. Version 1.0.0.
- First start unpacks the engine to `SoftCalc-data\engine\<hash>\`
  (temp folder, `.complete` marker, rename; old hashes removed).
  Measured from an empty folder: engine ready 2.6-2.8 s on the first
  run, 1.1-1.3 s after.
- Data beside the exe in `SoftCalc-data\` when writable, else
  %APPDATA%\SoftCalc; the portable copy starts empty (owner: no copy).
- File > About SoftCalc: version, data folder with Open Folder,
  THIRD-PARTY-NOTICES (IconMCP, mpmath, Python and its Windows-build
  libraries, PyInstaller bootloader exception, .NET), generated by
  `tools/make_notices.py`.
- Not tried: a read-only folder (Program Files) falling back to
  %APPDATA% (unit-tested only); antivirus behaviour on other PCs; an
  older Windows than 11.

## Open items
- Owner decisions 2026-11-07: red error lines stay; Samples box colour and the light Font dialog in dark
  mode are fine.
- The one report (2026-11-07) of graph lines vanishing on view switches
  was dropped 2026-11-09 (owner: likely an accidental double-click).
- UI automation note: UIA calls into a SoftCalc modal dialog can hang;
  drive dialogs with window messages (see the editor check pattern:
  WM_SETTEXT on EDIT children, BM_CLICK on buttons).

## How to resume
- Read `CLAUDE.md` (commands, architecture, clean-room rule, C#
  conventions), then `spec/SPEC.md` and `spec/extensions.md`.
- Engine: `cd engine; .venv/Scripts/python -m pytest -q`.
- UI: `cd ui; & "C:\Program Files\dotnet\dotnet.exe" test` (PowerShell
  needs the `&`), run with `dotnet run --project SoftCalc` or the Debug
  exe in `ui/SoftCalc/bin/Debug/net10.0-windows/`.
- A running SoftCalc locks the Debug build; the owner allows closing it
  (and relaunching after the build).
- Commit messages: use bash heredoc with `git commit -F -`; a PowerShell
  here-string piped that way does not reach git (it broke one M6 commit).
- The engine exe `engine/dist/softcalc-engine.exe` is rebuilt with
  `build/build_engine.ps1`; it is only needed for packaging and for
  tests run from outside the repo folder.
- Next: nothing ordered. Open in `design/TODO.md`: the other-PC test
  (on hold); section 8 (ribbon icons). Help is built (help/*.md, UI/HelpWindow.cs). The owner batches requests:
  note them in TODO, build when told, release only when asked.
- Driving UI handlers without the real mouse (graph trace, drops in
  Customize Ribbon): a small reflection harness that creates MainForm
  and calls the protected handlers. Run it from
  `ui/SoftCalc/bin/Debug/net10.0-windows/` so it finds the dev engine;
  its windows show on the owner's screen; restore the clipboard.
- Shell edits: Git Bash heredocs collapse doubled backslashes and turn
  \n / \r in Python strings into real line breaks; edit files with
  the Edit/Write tools, or Python using chr(92) for a backslash.
