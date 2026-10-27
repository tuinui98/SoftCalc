# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is
SoftCalc: a personal-use, clean-room clone of the Windows XP
"Calculator PowerToy" (PowerCalc), plus owner-requested extensions.
- The behavior contract is `spec/SPEC.md`. Extensions and their
  decisions are in `spec/extensions.md`. Post-clone ideas are in
  `spec/ideas.md`.
- The build plan and milestones are in `design/phase4_plan.md`.
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
- `ui/` (C# WinForms, .NET 10): windows, settings and history files in
  `%APPDATA%\SoftCalc\`, graph drawing, ECB rate fetch, engine
  watchdog/restart.
Golden tests drive the engine directly, without the UI.

## Commands
Engine (run from `engine/`):
```
python -m venv .venv                      # once
.venv/Scripts/python -m pip install -e ".[dev]"
.venv/Scripts/python -m pytest -q         # all tests
.venv/Scripts/python -m pytest tests/test_golden_data.py::test_reference_rows -q   # one test
```
.NET SDK: `C:\Program Files\dotnet\dotnet.exe` (10.0.x). It may not be on
the PATH of an already-open shell.

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
- Local repository only. Suggest a private GitHub remote after M2.
