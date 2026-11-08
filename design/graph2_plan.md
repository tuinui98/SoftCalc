# Graph lines: colours, equations, nearest-line trace (owner 2026-11-08)

Decisions are in `design/TODO.md` section 7. Built 2026-11-09 on branch
`graph-lines`. Earlier graph work: `design/graph_plan.md` (G1-G3).

## G4 - line colours
- The Graph list's swatch is a 14 px square; clicking it opens the
  Windows colour dialog. Right-click on a line: Colour..., Default
  Colour, Edit..., Delete.
- `GraphSettings.Colours` (name -> ARGB), saved as `graph.colours`
  `{"g": "#FF00FF"}`. Lines without one keep the palette colour of
  their place (functions, then equations).
- Renaming a function in the editor moves its colour, tick and
  selection (`DefinitionCommands.Renamed` -> `GraphSettings.Rename`).

## G5 - equations in x and y
- Add > Equation... in the graph toolbar (sidebar and window); Edit and
  Delete work on the selected equation. Saved as `graph.equations`; a
  line's name is its text, which always holds `=`.
- Engine op `plot_equation`: lhs - rhs sampled on a coarse grid of
  about 10 px cells; only cells where it changes sign (or turns
  undefined) are split 6 x 6 and traced by marching squares. A sign
  change through a pole is dropped (the cell's middle is farther from
  zero than its corners). About 0.2 s for a circle on a 600 px plot.
- Limits: a curve that only touches zero (`(y-x)^2=0`) or is smaller
  than a coarse cell is not drawn.
- The sidebar fits Y to the functions only; equations are drawn in
  that rectangle.

## G6 - trace snaps to the nearest line
- The shown line nearest the mouse within 20 px (screen distance to
  its drawn segments; jumps over half the plot are ignored) is traced;
  the selected line wins within 1 px, the traced line stays until
  another is nearer by more than 4 px.
- Function: the nearest point's x (polar: angle) rounded to what the
  status line shows, value from `value_at` as before.
- Equation: where the line runs flatter x is fixed (rounded to a
  pixel), else y; engine op `equation_at` solves the other coordinate
  near the point (secant, full precision, within 6 px).
- Status line: `x^2+y^2=r^2:   x = 1.5   y = 2.598...`. Left click
  copies the solved coordinate; the right-click menu has Copy x / y /
  (x, y) and Insert x / y / (x, y) for equations.

## After G6 (owner 2026-11-09)
- Graph window placement kept in GraphSettings as it changes (the main
  window saves settings before this window closes).
- 1:1: `PlotMath.Square` widens the narrower axis around the centre;
  locked mode re-squares after zoom, pan and resize at the previous
  scale (the drawing width at the last paint), twice because the y
  numbers' width moves the left edge.

## Speed (owner 2026-11-09: lines lagged while zooming and dragging)
- Measured before: mpmath at 20 digits, ~36 us per value; one redraw
  of g, a circle and y^2=x^3-x took ~0.8 s, rounds were started every
  120 ms without waiting, so requests queued in the engine, and the
  sidebar graph recomputed too.
- UI: one round at a time, later changes folded into one more round
  with the newest range; each line shows as it arrives; the Graph
  window asks at most every 40 ms; the sidebar waits until changes
  stop for 400 ms and does nothing while hidden.
- Engine `fastplot.py`: expressions made only of numbers, real
  variables, + - * / ^ mod, the elementary functions and user
  functions of those compile to float closures for `plot` and
  `plot_equation`; anything else keeps mpmath. Circle 457 -> 24 ms,
  cubic 284 -> 19 ms, a 600-point function ~1 ms. Values for trace,
  Copy and results stay full precision. Parity tests compare both
  paths in rad, deg and grad.
