# SoftCalc - project status (handoff)

Updated 2026-11-06 after M8. Everything below is committed on `main` and
pushed to the private GitHub repo https://github.com/tuinui98/softcalc.

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
| **M9 packaging (single folder or installer, icon, about)** | **next** | |

Done after milestones, on `main` (no tag): durations `h:mm:ss`;
`yr mo wk h` units (year = 365 d, month = 30 d) and `[utc->local]`;
`now` and format tags `[longdatetime]` etc.; Up/Down input recall with
draft restore; RPN hints and the line-based RPN keypad (owner's option A);
Enter on an empty Input repeats the last line (observed on the original,
golden `U_repeat`); History layout with the input on the left and the
substituted numeric form right-aligned and faint; History View and a
400x651 window as defaults.

Tests after M8: engine `pytest` 334 passed; UI `dotnet test` 135 passed.

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

## Open items to raise with the owner
- Graph interface improvements: noted in `spec/ideas.md`, to discuss.
- Dialog ideas (separate name/params fields, list in dialog):
  `spec/ideas.md`, after the clone.
- Error lines in History are red; the owner did not ask for that and
  may want it removed.

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
