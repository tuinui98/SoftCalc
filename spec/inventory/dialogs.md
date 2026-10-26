# Dialogs and secondary windows (observed)

Observed 2026-10-27 with `tools/probe_dialogs.ps1` and
`tools/probe_graph_window.ps1` (run via `tools/run_elevated.ps1`): each
toolbar button was triggered with its own command, the window that opened
was dumped and screenshotted, then cancelled. *Delete* was never pressed.
Raw dumps: `dialogs.txt`, `graph_window_menu.txt`. Screenshots:
`spec/raw/dlg_*.png` (not in git).

## Toolbars
Graph and Variables toolbars share command IDs (Add 92, Edit 96,
Delete 95); Graph adds Window 97. Variables *Edit*/*Delete* stay disabled
until a user-defined variable is selected (constants are not editable).

## Add Function / Add Variable
- Small fixed dialog (~400x100): one full-width text box, *OK* and
  *Cancel*.
- The whole definition is typed in the one box (e.g. `f(x)=x*x`,
  `a=5`); there are no separate name or parameter fields.

## Edit Function
- Same size. Left: read-only label with the function head (`g(x)=`).
  Right: text box holding only the body (`x`).
- So the name and parameter list cannot be changed in Edit, only the body.
  To verify whether Edit Variable behaves the same.

## Graph window (Graph toolbar > Window)
- Separate resizable top-level window titled *Graph* (~600x500), own menu:
  - File: *Save as bitmap*
  - View: *XY Graph* / *Polar Graph* (radio), *Show Grid* (check)
- Left: collapsible *Range* panel with six labelled fields and *Apply*.
  In XY mode: X Min, X Max, X Increment, Y Min, Y Max, Y Increment
  (defaults -10, 10, 1, -10, 10, 1, shown with 6 decimals in large type).
  Polar mode fields presumably map to `State\2DPolar` (minT, maxT, gridT,
  minR, maxR, gridR), to verify.
- Right: the same Graph toolbar (Add, Edit, Delete, Window), function
  drop-down, and a large plot with grid and axes.
- Confirms `State\iGraphMode = 32814` means XY Graph (32815 = Polar) and
  `fShowGrid` maps to *Show Grid*.

## Not probed yet
- File menu dialogs: Save History, Save/Load Functions & Variables
  (standard file dialogs; the file formats matter more, see Phase 2).
- Edit Variable (needs a user variable to exist first).
- Help (opens the CHM).
