# SoftCalc - Phase 4 plan (design and build)

Status: draft for owner review, 2026-10-27. No code is written until the
owner approves this plan.

## 1. Architecture
```
+-------------------------+   JSON lines over stdin/stdout   +----------------------+
| SoftCalc.exe            | <------------------------------> | softcalc-engine.exe  |
| C# WinForms UI          |   one engine process per window  | Python + mpmath      |
| owns: windows, settings |                                  | owns: parsing, maths,|
| file, history file,     |                                  | variables, functions,|
| graph drawing, ECB fetch|                                  | answer/$n, formats,  |
+-------------------------+                                  | .pcf text, finance   |
                                                             +----------------------+
```
- The UI starts its own engine process and restarts it if it crashes or
  stops answering (watchdog, same idea as the probes).
- The engine has no window and no file or network access except what a
  request hands it. The UI does all file and network I/O, so the engine
  stays testable from the command line.
- Golden tests run against the engine directly (pytest), with no UI.

## 2. Repository layout
```
powercalc/                  (repo; folder name stays, product is SoftCalc)
  spec/                     behavior spec, golden data (exists)
  tools/                    probes of the original (exists)
  design/                   this plan, protocol spec
  engine/
    pyproject.toml
    softcalc_engine/        lexer, parser, evaluator, numbers, units,
                            dates, finance, formatting, protocol server
    tests/                  pytest: golden runner + unit tests
  ui/
    SoftCalc.sln
    SoftCalc/               WinForms app (net10.0-windows)
    SoftCalc.Tests/         UI-side unit tests (history merge, settings)
  build/                    packaging scripts (PyInstaller, dotnet publish)
```

## 3. Engine design (Python)
- Pratt (precedence-climbing) parser driven by the table in SPEC 5.4.
  Infix and RPN front ends produce the same AST.
- Numbers: mpmath `mpf`/`mpc` with working precision = selected digits
  + 10 guard digits; results rounded to the selected digits only for
  display. This reproduces the original's decimal-looking output
  (`0.1+0.2` = 0.3).
- Values: number (real or complex), date, date-time; unit tags and
  currency tags convert numbers.
- State per engine process: variables (by value), functions (AST,
  dynamic variable lookup), `answer`, result stack for `$n`, settings.
- Formatting module implements SPEC 6.2 exactly (significant digits,
  trimming, scientific switch, hex/bin/oct, complex), the part most
  likely to differ from the golden data.

## 4. Protocol (to be written in `design/protocol.md` in M4)
One JSON object per line, request `{"id", "op", ...}`, response
`{"id", "ok", ...}`. Planned ops:
- `eval` {input} -> {kind: result|definition|error, display_input,
  result, message}
- `settings` {precision, angle, base, rpn, display_decimals}
- `state` -> variables, functions, constants (for the sidebar)
- `define` / `delete` / `edit` for the Add/Edit/Delete dialogs
- `plot` {function, mode xy|polar, range, samples} -> list of points
  (low precision, one call per redraw)
- `menu_insert` {command, input, caret} -> new input text and caret
- `pcf_export` -> text; `pcf_import` {text} -> report of skipped lines
- `finance` {principal, rate, years, ...} -> totals and period table
- `rates` {ecb_xml} hands fetched currency data to the engine

## 5. UI design (C# WinForms, .NET 10)
- Native controls where the original used them: menu, RichTextBox for
  History, TextBox for Input, ListView for Variables, ToolStrip
  toolbars, ComboBox for functions.
- Owner-drawn pieces: pane header strips (gradient + caption),
  collapsible sidebar sections, the plot area.
- Graph drawn with GDI+ from engine `plot` points; Save as bitmap to
  PNG.
- Settings: `%APPDATA%\SoftCalc\settings.json`. History:
  `%APPDATA%\SoftCalc\history.jsonl`, merged on close (extensions.md
  E12).
- Currency: fetch ECB XML only when an `eval` reports that it needs
  rates, then retry the line.

## 6. Milestones (each ends with tests passing, a commit, and a tag)
| # | content | done when |
|---|---|---|
| M0 | toolchain: install .NET 10 SDK, Python venv, pytest, PyInstaller check on Python 3.14 | `dotnet --version`, `pytest` run empty |
| M1 | engine core: lexer, parser, arithmetic, precision, formatting, functions, complex, errors | golden A, B, D, E, F, G, L, N pass (with expected diffs) |
| M2 | engine: variables, functions, answer/$n, conversions, bases, RPN, menu insertion | golden C, H, I, J, M, Q, R, menu_insert pass |
| M3 | engine extensions: dates/epoch (E7), display decimals (E9), finance functions (E11), .pcf text | unit tests |
| M4 | protocol server + `design/protocol.md` + CLI | pytest drives the server over a pipe |
| M5 | UI shell: main window, 3 views, menus, History, Input, Samples, keypad, memory keys, engine watchdog | manual check against screenshots |
| M6 | UI sidebar: Variables, Graph section, dialogs, Graph window XY/Polar | manual check |
| M7 | persistence: settings, history file with merge, .pcf save/load, history save | UI tests for merge |
| M8 | tools: Compound Interest window (graph + table + CSV), currency via ECB, always on top, copy menu | manual check |
| M9 | packaging: single folder or installer, icon, about box | runs on a clean user profile |

Golden tests that depend on keypad (O) are checked in M5 against the
fixed memory semantics, not the original output.

## 7. Conventions (owner rules + choices to confirm)
- Python (engine): PEP8, 4 spaces, snake_case, max 3 words per name,
  docstrings, file header docstring (Python addendum).
- C# (UI): there is no C# addendum yet. Proposal:
  - braces on the next line (already the C# default) and tabs (4);
  - .NET casing: PascalCase types, methods, properties; camelCase
    locals and parameters; `_camelCase` private fields; max 3 words
    per name;
  - file header: `// FileName.cs - purpose` comment block;
  - block banners for long methods as in the global rules.
- Git: one branch per milestone (`m1-engine-core`, ...), merged to
  `main` when its tests pass; tag `milestone/<yyyymmdd>-m1-engine-core`.
- Remote: local only for now. Suggest a private GitHub repo after M2
  (first end-to-end engine), owner decides.

## 8. Decisions needed before M0
1. Approve this plan (or change milestones/scope).
2. Install the .NET 10 SDK (system install, about 200 MB; winget or the
   Microsoft installer).
3. C# conventions in section 7 (adds a C# addendum to the owner's
   global rules file, or keep them project-local in `CLAUDE.md`).
