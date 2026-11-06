# Graph window improvements - plan (owner 2026-11-06)

Graph window only; the sidebar graph stays as it is (except G3). Own
drawing code in PlotView/PlotMath, no charting library (owner).

## G1 - axis numbers, cursor coordinates, mouse and keys, fit
- Numbers on the axes, left and bottom, at "nice" steps (1, 2, 5 x 10^n)
  chosen from the pixel space, independent of the grid increment.
- Status line under the plot: `x = 2.35   y = -1.2` (polar adds
  `r` and `theta`).
- Mouse wheel zooms around the cursor (Shift: X only, Ctrl: Y only);
  dragging pans. The Range panel follows at once, no Apply needed.
  After a mouse or key zoom the grid increments are reset to nice
  steps so the grid stays readable.
- Keys (plot focused): arrows pan 10%, + / - zoom, Home resets the
  range to the defaults, F fits Y.
- View > Fit Y (Ctrl+F) and View > Reset Range (Home): Y Min/Max from
  the plotted points with outliers cut (2nd-98th percentile, then 5%
  margin), so tan's poles do not flatten the view.
- Polar (owner): no panning; wheel and + / - change R Max; Fit sets
  R Max from the largest |r|.

## G2 - trace
- A dot follows the curve at the mouse's x with its value. Click
  copies the value; a context item sends it to the Input box.
- New engine op `eval_at` `{function, x}` -> full-precision text, so
  the value sent to Input is exact (owner: ok).

## G3 - several functions
- The function drop-down becomes a checklist; each line gets its own
  colour and a legend. Settings keep a list (the single saved function
  becomes a one-item list).
- Sidebar graph draws all checked lines over the X range, with Y
  auto-fitted only to show their shape (owner).
