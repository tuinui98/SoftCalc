# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is
SoftCalc: a personal-use, clean-room clone of the Windows XP
"Calculator PowerToy" (PowerCalc), plus owner-requested extensions.
- The behavior contract is `spec/SPEC.md`. Extensions and their
  decisions are in `spec/extensions.md`. Post-clone ideas are in
  `spec/ideas.md`.
- The build plan and milestones are in `design/phase4_plan.md`; the current
  state and the next step are in `design/STATUS.md` (read it first).
- Golden data recorded from the original is in `spec/golden/*.tsv`.
  Cases the clone must answer differently on purpose are in
  `spec/golden_expected_diffs.md`.

## Clean-room rule
The original's EULA forbids reverse engineering. Learn about the original
only by observing it as a black box: its UI, window messages, and the
files it writes. Never disassemble or decompile `PowerCalc.exe`, never
open its crash dumps, and never copy its icons, bitmaps, help text, or
names into SoftCalc. Original binaries (`extracted/`, `*.exe`, `*.chm`)
and raw captures (`spec/raw/`) are gitignored and must stay out of git.

## Architecture
Two processes, one engine per UI window, talking JSON lines over
stdin/stdout:
- `engine/softcalc_engine/` (Python + mpmath): parsing (Pratt parser for
  SPEC 5.4, infix and RPN), evaluation at selected precision + guard
  digits, variables/functions/answer/`$n` state, number formatting
  (SPEC 6.2), units, dates, finance. No file or network I/O of its own.
- `ui/` (C# WinForms, .NET 10, standard controls: the owner wants the
  original's UX, not its look): windows, settings and history files
  (portable `SoftCalc-data\` or `%APPDATA%\SoftCalc\`), graph drawing, ECB rate fetch, engine
  watchdog/restart.
Golden tests drive the engine directly, without the UI.
Toolbar icons are IconMCP SVGs (MIT) in `ui/SoftCalc/Icons/` with their
licence; `tools/IconMaker` regenerates `ui/SoftCalc/softcalc.ico`
(`dotnet run --project tools/IconMaker -- ui/SoftCalc/softcalc.ico`).

## Commands
Engine (run from `engine/`):
```
python -m venv .venv                      # once
.venv/Scripts/python -m pip install -e ".[dev]"
.venv/Scripts/python -m pytest -q         # all tests
.venv/Scripts/python -m pytest tests/test_golden_data.py::test_reference_rows -q   # one test
.venv/Scripts/python -m softcalc_engine   # protocol server on stdin/stdout (design/protocol.md)
```
Frozen engine: `powershell -ExecutionPolicy Bypass -File build\build_engine.ps1`
writes the one-folder build `engine\dist\softcalc-engine\` (exe +
`_internal\`); `test_frozen_exe_answers` runs against it when it exists.
Release: `powershell -ExecutionPolicy Bypass -File build\publish.ps1` builds
and tests the engine, zips it, refreshes `ui/SoftCalc/THIRD-PARTY-NOTICES.txt`
(`tools/make_notices.py`), publishes two single files with the engine zip
embedded, `dist\SoftCalc.exe` (self-contained) and `dist\SoftCalc-net10.exe`
(needs the .NET 10 Desktop Runtime), and smoke-runs each from an empty
folder (design/m9_plan.md). It ends with the SHA-256 of both exes;
they go into the GitHub release notes (owner 2026-11-11).
UI (run from `ui/`; .NET SDK at `C:\Program Files\dotnet\dotnet.exe`, 10.0.x,
may not be on the PATH of an already-open shell):
```
dotnet build                     # SoftCalc + SoftCalc.Tests
dotnet test                      # unit tests + engine session tests over real pipes
dotnet test --filter "FullyQualifiedName~InputRulesTests"   # one class
dotnet run --project SoftCalc    # start the app
```
The app finds the engine via `SOFTCALC_ENGINE`, then the zip embedded in the
published exe (unpacked to `<data>\engine\<hash>\`), then
`softcalc-engine.exe` next to `SoftCalc.exe`, then `engine/.venv`
(development). Data: `SOFTCALC_DATA_DIR`, else `SoftCalc-data\` beside the
published exe when writable, else `%APPDATA%\SoftCalc` (also for dev builds).
`tools/drive_softcalc.ps1` types lines into a running SoftCalc through
window messages and saves a screenshot to `spec/raw/` (own app only).
It takes `-DataDir <folder>` so scripted runs never touch the owner's
`%APPDATA%\SoftCalc` (the app honours `SOFTCALC_DATA_DIR`), `-OpenGraph`
and `-OpenCompound` for those windows. Run it with `&` from PowerShell:
through `powershell -File`, `-Lines @(...)` keeps only the first line.

## Probing the original (only if behavior needs re-checking)
- `extracted/PowerCalc.exe` runs only under an XP compat layer, and every
  XP layer forces elevation, so probes run elevated through
  `tools/run_elevated.ps1 -Probe <script> [-Group a,b]` (one UAC
  consent per run, and it launches PowerCalc itself). UIPI blocks
  messages from a non-elevated shell.
- A UAC prompt started from the background only flashes on the
  taskbar. Tell the owner before each elevated run.
- Hazards in the original: input `0x` crashes it, and Load of any
  `.pcf` hangs it at 100% CPU. Use a watchdog (`tools/probe_files2.ps1`
  shows the pattern) and never a plain blocking `SendMessage` to a
  possibly hung window.
- Undo test definitions with `tools/run_elevated.ps1 -Probe restore_state.ps1`
  (restores `spec/raw/registry_backup_HKCU.reg`).

## C# conventions (project-local; the owner's global rules have no C# addendum)
- Braces on the next line; literal tabs, size 4.
- .NET casing: PascalCase types, methods, properties; camelCase locals
  and parameters; `_camelCase` private fields. The 3-word naming limit
  still applies.
- File header: a `// FileName.cs - purpose` comment block at the top.

## Git
- One branch per milestone (`m1-engine-core`, ...), merged to `main`
  when its tests pass; tag `milestone/<yyyymmdd>-<name>`.
- Remote: https://github.com/tuinui98/softcalc (created 2026-11-05;
  made public by the owner after 2026-11-11). Commits use the GitHub
  noreply address `tuinui98@users.noreply.github.com` as author and
  committer (owner 2026-11-11), never a personal e-mail.
  Push every commit to `main` and every tag; keep local and GitHub in sync.
  Merged feature branches stay local.
