# SoftCalc - project status (handoff)

Paused by the owner on 2026-10-27 after M6 plus follow-ups. Everything
below is committed on `main` and pushed to the private GitHub repo
https://github.com/tuinui98/softcalc (remote added 2026-11-05).

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
| **M7 persistence and files** | **next** | |
| M8 tools (compound interest window, ECB currency, copy menu, always on top) | todo | |
| M9 packaging (single folder or installer, icon, about) | todo | |

Done after milestones, on `main` (no tag): durations `h:mm:ss`;
`yr mo wk h` units (year = 365 d, month = 30 d) and `[utc->local]`;
`now` and format tags `[longdatetime]` etc.; Up/Down input recall with
draft restore; RPN hints and the line-based RPN keypad (owner's option A);
Enter on an empty Input repeats the last line (observed on the original,
golden `U_repeat`); History layout with the input on the left and the
substituted numeric form right-aligned and faint; History View and a
400x651 window as defaults.

Tests at pause: engine `pytest` 293 passed; UI `dotnet test` 67 passed.

## M7 scope (agreed plan, design/phase4_plan.md and spec/extensions.md E12)
- Settings file `%APPDATA%\SoftCalc\settings.json`: precision, angle,
  base, RPN, decimals, view, Show Samples, window rect and maximized,
  graph mode, grid, XY and Polar ranges, selected graph function.
  First run uses defaults, never the registry (SPEC 8).
- User variables and functions persist across runs (engine
  `pcf_export` / `pcf_import`).
- History file `%APPDATA%\SoftCalc\history.jsonl`: loaded at start,
  written at close, rolling cap (setting), merged on close when several
  windows ran: entry IDs = process start time + PID + sequence, short
  exclusive lock, union by ID, sort by time, keep newest N, atomic
  replace; Clear History writes a "cleared at" mark so cleared entries
  do not come back from another window.
- File menu: Save History (UTF-8 text as shown), Save / Load Functions
  & Variables (`.pcf`, UTF-16 LE no BOM on save; load accepts UTF-16 LE
  or UTF-8; skipped lines reported).
- UI tests for the history merge.

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
