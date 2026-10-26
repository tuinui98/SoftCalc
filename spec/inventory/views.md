# Views and panes (observed)

Observed 2026-10-27 by switching views through the View menu commands and
dumping the window tree (`tools/probe_views.ps1`, run via
`tools/run_elevated.ps1`). Raw trees: `view_*.txt`. Screenshots for
reference only, kept out of git: `spec/raw/view_*.png`.

## Common chrome
- Menu bar: File, View, Functions, Conversions (see `menu_tree.txt`).
- Every pane has a titled header strip (light blue gradient, bold blue
  caption): *History*, *Samples*, *Input*, *Graph*, *Variables*.
- History is a read-only rich-text area; Input is a single-line edit box
  at the bottom.

## Numeric Calc view
- Panes: History (top, fills width), Input, then a keypad below.
- Samples pane hidden in this view even when *Show Samples* is checked.
- Keypad, 4 rows x 5 columns, centered:

  | | | | | |
  |---|---|---|---|---|
  | M+ | 7 | 8 | 9 | + |
  | M- | 4 | 5 | 6 | - |
  | MC | 1 | 2 | 3 | / |
  | MR | Clr | 0 | Ent | * |

  Memory keys M+, M-, MC, MR exist only here; no menu equivalent.
- Help says this view cannot be resized; window kept its saved size in the
  test, to verify.

## History View
- Panes: History, Samples, Input. No keypad (keypad window height 0), no
  sidebar.

## Advanced View
- Left sidebar (blue gradient background), right main column.
- Sidebar, top to bottom, each section collapsible via a chevron button in
  its header:
  - **Graph**: toolbar *Add, Edit, Delete, Window*; drop-down of defined
    functions (shows `g(x)=x`); plot area with grid and axes, plotting the
    selected function.
  - **Variables**: toolbar *Add, Edit, Delete* (Edit/Delete disabled when
    nothing editable is selected); list with columns *Name*, *Definition*;
    grouped rows, first group *Constants* holding `pi` and `e` shown to the
    current precision (32 digits).
- Main column: History, Samples, Input.

## Samples pane content (paraphrased)
Four one-line examples with explanations:
1. adding two numbers;
2. assigning a value to a variable (`x=5`);
3. multiplying a variable by a number, noting that implicit multiplication
   such as `56x` is **not** supported, `*` must be written;
4. defining a one-argument function `f(x)=...` and calling it as `f(5)`.

The help says the samples switch to RPN form when RPN mode is on, to verify
in Phase 2.
