# Planned extensions beyond the original

Requested 2026-10-27. The clone first matches the original (golden tests),
then these change behavior on purpose. Each item lists open decisions to
settle before implementation.

## E1. Number base prefixes
- Add `0b101` (binary) next to the original suffix form `101b`.
- Add `0o17` (octal): the common prefix set in C-family languages,
  Python, and JS is `0x`, `0b`, `0o`.
- `0d123` (explicit decimal): exists in a few languages, rarely used.
  Harmless to accept, so accept it.
- Other forms seen elsewhere, not planned unless wanted: suffix `h` as in
  assembly (`0FFh`), C-style leading-zero octal (`017`, ambiguous, avoid),
  `_` digit separators (`1_000_000`).
- Hex digits make `b` ambiguous: inside `0x1b`, `b` is a digit. Prefix
  forms are always unambiguous; the suffix `b` only applies to a digit
  run of 0s and 1s with no prefix.

## E2. C-style identifiers
- Names: `[A-Za-z_][A-Za-z0-9_]*`. Original allowed letters only.
- Decide: keep the original's case-insensitivity, or be case-sensitive
  like C. Case-insensitive keeps old `.pcf` files and habits working.
- Decide: what wins when a user name collides with a reserved word
  (`pi`, `e`, `i`, `j`, `ans`, `answer`, `mod`, unit tokens).

## E3. Implicit multiplication (feasibility)
Moderate effort; the parser is easy, the rules need care. Proposed scope:
- number followed by a name or `(`: `2x`, `2pi`, `3(x+1)`
- `)` followed by `(` or a name: `(a+1)(b+1)`, `(x)y`
- name followed by `(`: call if the name is a function, otherwise
  multiply (`a(b+1)` with variable `a`)
- Not between two names: with C-style names `ab` is one identifier.
Conflicts to settle:
- `2e3`: scientific notation wins (not `2*e*3`). `2e` alone, `2*e` or
  error?
- `101b`: binary literal or `101*b` when a variable `b` exists? Keep
  literal (original behavior); E1 prefixes give an unambiguous form.
- `2i` / `2j`: complex literal, already the original's behavior.
- Precedence: does `1/2x` mean `1/(2x)` or `(1/2)*x`? Many calculators
  bind implicit multiplication tighter than `/`. Pick one and document.
- `2^3x`: `2^(3x)` or `(2^3)x`?

## E4. Answer references
- Original: typing an operator into an empty Input after a result
  inserts `answer` automatically (confirmed, see
  `behavior/session.md`). Keep that.
- Accept `ans` and `answer` for the last result.
- Accept `$1`, `$2`, ... for earlier results. Decide numbering: `$1` as
  the first result in History (stable line-number style) or as the most
  recent (stack style). Also what Clear History does to the numbering.

## E5. New operators
- `!` factorial: integers only, or gamma for non-integers (`0.5!`)?
  Precedence: postfix, binds tighter than `^`?
- `mod`: infix keyword. Sign rule for negatives (C truncation vs
  mathematical floor)? Real operands allowed (`7.5 mod 2`)?
- `%`: modulo (C) or percent (`50%` = 0.5, calculator style)? Pick one
  meaning, possibly `%` as percent and `mod` as modulo.

## E6. `j` as imaginary unit
- Accept `j` as an alias of `i` (`2j`, `3+4j`).
- With E2 names, a user variable named `i` or `j` would shadow the unit.
  Decide: reserve both, or let a user definition win.
- Output keeps `i` unless a display option is added.

---
Second batch, requested 2026-10-27 after reviewing linecalc.com (a web
line-by-line calculator; only its public feature list was read).
Decisions on E1-E6 are settled in `SPEC.md`.

## E7. Date arithmetic
- Date literal `dd.mm.yyyy` (two dots, so no clash with decimals:
  `1.2.3` is already an error). `/` and `-` separators are not used,
  since they are operators.
- `date - date` = number of days; `date + n`, `date - n` = date.
- Result of date type prints as `dd.mm.yyyy`.
- Decided 2026-10-27:
  - Gregorian years only; no Buddhist-era (พ.ศ.) input.
  - `today` keyword: the current local date.
  - Epoch conversion: `epoch(date_or_datetime)` gives Unix seconds,
    `fromepoch(n)` gives a date/time. Local time zone. [names DEF]
  - Long format: `longdate(x)` gives for example
    `Tuesday, 27 October 2026`, `longtime(x)` gives `14:05:09`,
    `longdatetime(x)` both. [names DEF]
- Date-time literal `27.10.2026 14:05:09` (seconds optional), accepted
  2026-10-27.

## E8. Currency conversion
- Same tag syntax as units: `100 [usd->thb]`, chainable, case-
  insensitive ISO 4217 codes. Also the Conversions menu gets a
  *Currency* category with common codes.
- Rates come from an online daily source and are cached locally with
  their date; offline use falls back to the cache, and the result line
  notes the rate date when it is old.
- This is the only network access in the program.
- Decided 2026-10-27: source is the ECB daily reference rates (free, no
  key, about 30 currencies including THB, cross rates via EUR). Rates
  are fetched **when a currency conversion is evaluated**, never in
  the background. To avoid refetching on every line, a fetch is skipped
  when the cache already holds the latest ECB publication (ECB updates
  once per working day). [DEF]

## E9. Display control, copy, always on top
- Display decimals: a setting separate from calculation precision.
  *Increase / Decrease decimal places* and *Show all decimal places*
  (pad with zeros). Calculation precision (32/64/128/512) unchanged.
- History context menu: *Copy result*, *Copy expression and result*,
  *Copy all*.
- View > *Always on top*.

## E10. History lines (owner's wording, interpreted)
- No `#N` line references.
- History shows each calculation with **numbers instead of variable
  names** (values at the time of calculation), so a line reads as a
  self-contained arithmetic record.
- Clicking a History line puts the **original input text** (with the
  variable names) back into the Input box for editing.
- Past lines are never recalculated.
- Interpretation confirmed by the owner 2026-10-27.
- Layout changed by the owner 2026-10-27: the first line shows the input
  as typed on the left; when variables, answer or $n were substituted,
  the numeric form is shown right-aligned on the same line in a fainter
  colour (nothing on the right otherwise). The result stays on the
  second line, indented. Entries are selectable items; Ctrl+C copies the
  selected entry (free text selection was given up for this layout).
- Owner 2026-11-06: click recalls the expression; Shift+click inserts the
  result at the Input caret (replacing a selection; spaced as its own
  item in RPN); the context menu has Use Expression and Insert Result
  too. Only results insert. (A balloon tip was tried and removed at the
  owner's request: the context menu is enough.)
- Owner 2026-11-06: Input context menu = the usual edit commands plus
  Clear Input.

## E12. Persistent history
- Decided 2026-10-27: History is saved to a file (differs from the
  original, which starts empty).
  - Loaded when the program starts; written when it closes, with the
    History as it is at that moment.
  - Rolling: a setting caps the number of entries kept; oldest entries
    drop first.
  - Two windows (two running instances) may load the same history
    file at the same time; no exclusive lock.
- Decided 2026-10-27: merge on close, so no instance loses entries and
  no instance needs to know whether others are running:
  - every entry has a unique ID: process start time + PID + sequence
    number (PID alone is reused by Windows);
  - on close: take a short exclusive lock on the file, re-read it, union
    with this instance's entries (dedupe by ID), sort by timestamp, keep
    the newest N, write atomically (temp file + replace), unlock;
  - Clear History records a "cleared at" timestamp in the file; the
    merge drops every entry older than the newest such mark, so cleared
    entries do not come back from another instance.
- Settings and definitions with several windows: last writer wins
  (owner, 2026-11-05); only History merges. Definitions are also saved
  after every definition or delete. History cap 1000. `answer` and `$n`
  are restored from the loaded History. Details: design/m7_plan.md.
- History file format: UTF-8, one entry per record holding the
  original input, the numeric display form, the result, and a
  timestamp (needed for E10 click-to-recall and for merging).

## E11. Compound interest tool and functions
- Tools menu > *Compound Interest*: own window with
  - inputs: principal, annual rate %, years, compounding frequency
    (yearly, quarterly, monthly, daily, continuous), periodic
    contribution amount, contribution frequency, at start or end of
    period;
  - outputs: future value, total contributions, total interest;
  - growth graph: balance over time split into contributions and
    interest;
  - period table: period, start balance, interest, contribution, end
    balance; copy and export CSV.
- Input-box functions for the same maths, Excel-style names:
  `fv(rate, n, pmt, pv)`, `pv(...)`, `pmt(...)`, `nper(...)`.
- Decided 2026-10-27: signed cash flows as in Excel (money paid out
  negative, money received positive), e.g. `fv(0.05/12, 120, -100,
  -1000)` is positive. Confirmed 2026-10-27; to revisit after the
  owner tries it.
- Open: rate per period (Excel) or annual rate plus frequency in the
  functions? Default: rate per period, as Excel. [DEF]

---
## Implementation notes (M3, 2026-10-27)
Choices made while implementing E7-E11 in the engine; all [DEF].
- E7: `epoch` / `fromepoch` use the current UTC offset of the local zone
  for every date (Windows rejects `timestamp()` near 1970; Thailand has
  no daylight saving). `longdate`/`longtime`/`longdatetime` also accept
  a number, taken as Unix seconds. English day and month names. In RPN
  mode only date literals without a time work, since spaces separate
  RPN items. `date + n` with a fractional n adds a time of day.
- E9: `decimals` rounds half away from zero; it applies to decimal
  output only (hex/bin/oct unchanged) and never to the stored value.
  `pad_zeros` pads fixed notation only; scientific results stay as is.
- E10: substituted tokens are variables, `answer`/`ans` and `$n`.
  Constants (`pi`, `e`) and function names stay. Negative values are
  wrapped in parentheses, and `*` is inserted where a value replaces a
  name inside an implicit product (`2x` shows `2*5`). Digit runs joined
  by spaces show joined (`1 000` shows `1000`).
- E11: in RPN the finance functions take their 3 required operands.
  The schedule rows follow the contribution frequency, or the
  compounding frequency when there is no contribution (daily for
  continuous); contributions use the effective rate per row.

## E7b. Durations (added 2026-10-27, after M3)
- Literal `h:mm[:ss[.fraction]]`; hours may exceed 24 (`36:00:00`),
  minutes and seconds must be below 60. Shown as `h:mm:ss`, negative as
  `-0:30:00`, fractional seconds kept (`0:00:01.5`).
- `date +/- duration` = date, `duration + date` = date;
  `duration +/- duration` = duration; `duration * n`, `n * duration`,
  `duration / n` = duration; `duration / duration` = number.
- Not allowed: `duration + number` (unit unclear), `duration * duration`,
  functions, `!`, `%`, unit conversion tags.
- `date - date` still gives days (decided: keep LineCalc behavior).
- Overlaps checked (engine/tests/test_durations.py): a time after a date
  with a space is still part of the date-time literal; `2 1:30` and
  `1:30 2` are not joined into one duration; RPN items keep their
  spaces. The RPN History display used to join digit runs
  (`1 2 +` showed `12 +`), fixed with this change.

## E13. More time units and UTC/local (added 2026-10-27)
- Time units now: `yr mo wk d h min s` (original had `d min s`).
  1 yr = 365 days, 1 mo = 30 days, 1 wk = 7 days (owner's choice
  2026-10-27, replacing the mean Gregorian year); so 1 yr = 12.1666... mo.
- Long-name aliases inside tags only: `year(s) month(s) week(s) day(s)
  hour(s) hr minute(s) sec second(s)`. Outside `[ ]` these stay free
  names for variables.
- Conversions menu > Time lists the new units too.
- `[utc->local]` and `[local->utc]` on dates and date-times, using the
  current local UTC offset (same limitation as epoch). Numbers,
  durations and other units are errors in these tags.

## E7c. `now` and format tags (added 2026-10-27)
- `now`: current local date and time to the second (reserved name, like
  `today`, which stays the date at 00:00).
- Format tags without an arrow apply a date function as a conversion:
  `now [longdatetime]` = `longdatetime(now)`. Tags: `[longdatetime]`,
  `[longdate]`, `[longtime]`, `[epoch]`, `[fromepoch]`; they chain with
  unit and zone tags (`x [utc->local] [longdatetime]`). Text results
  cannot be computed further. A number before a long-format tag is read
  as Unix seconds, as in the functions.
- Menu: protocol `catalog.formats` lists them; `menu_insert` kind
  `format` appends ` [name]`.

## Implementation notes (M8, 2026-11-06)
- E8: owner chose full precision for currency results and a rate note
  on every currency line (`ECB 2026-11-05`, `, offline` after a failed
  fetch). A tag is a currency conversion when either side is a known
  ECB code; a code the ECB does not publish (BGN since the euro, 2026)
  gives `No ECB rate for BGN`; `[usd->kg]` gives `Cannot convert`.
  Menu: 10 common codes as pairs, then From / To halves (owner).
- E9: History context menu and View > Always on Top as planned.
- E9 decimals (2026-11-06): View > Decimal Places (More, Fewer, All
  Digits, Pad with Zeros) and the ribbon's Decimals group. From all
  digits, More or Fewer starts at 2 places [DEF]; range 0-30.

## Ribbon (owner 2026-11-06, layout B3)
- Groups under the menu bar: Angle, Output, Decimals (caption shows a
  sample such as `0.00` or `all`), Mode (RPN, precision), History
  (Clear without confirmation, Save), Tools (Graph, Interest, On top,
  Conversions drop-down with the same items as the menu).
- All group frames have the same height (owner).
- History and Input font (owner 2026-11-06): Font... sets the family and
  the base size, which becomes 100%; View > Text Size (100-200%) scales
  that base. Default 9 pt at 150% (13.5 pt). Saved as text_font_base and
  text_scale; an older text_font_size is read as base at 100%.
- Input hint line (owner 2026-11-06): a second line in the Input box,
  italic and faint, same size as the Input font. It describes the
  function whose call is open at the caret (`fv(0.05/12, 120, ` shows
  `fv(rate, nper, pmt, [pv], [type]) - ...`), the word being typed (or
  the names it can become, from 2 letters), tag examples inside `[`,
  `$n` after `$`, and user functions (definition) and variables
  (value). View > Show Input Hints turns it off. SoftCalc's own text.
- Theme (owner 2026-11-06): View > Theme > System / Light / Dark, saved
  as `theme` (default `system`, follows Windows). Uses WinForms'
  colour mode (`Application.SetColorMode`, experimental in .NET 10), set
  before the first window, so choosing another theme saves and restarts
  SoftCalc. Fixed colours that would be unreadable on dark (error red,
  chart grid) come from `UI/Theme.cs`; the Compound table draws flat
  headers in dark mode.
- The menu bar stays complete; View > Show Ribbon hides it; the Numeric
  view never shows it. Icons from IconMCP (MIT, ui/SoftCalc/Icons/
  LICENSE.txt), drawn from SVG by our own small renderer.
- Program icon: IconMCP "Calculator" with a filled display, owner's
  draft 2 (dark body, teal screen, orange key); tools/IconMaker writes
  ui/SoftCalc/softcalc.ico.
- E11: the window recalculates 300 ms after a change (owner); amounts
  follow the main window's precision, base and decimals.

## E14. Graph window navigation (owner 2026-11-06, design/graph_plan.md G1)
- Graph window only; the sidebar graph is unchanged.
- Numbers on the axes at round steps (1, 2, 5 x 10^n) from the pixel
  space; polar labels the rings along the positive x axis.
- Status line: coordinates under the mouse (polar adds r and theta),
  otherwise a short key help.
- Wheel zooms around the cursor (Shift: X only, Ctrl: Y only); drag
  pans; plot keys: arrows pan 10%, + / - zoom, Home resets the range,
  F fits Y. View menu: Zoom In / Out, Fit Y (Ctrl+F), Reset Range.
  The Range panel follows every change; a zoom resets the grid
  increments to round steps.
- Fit Y: 2nd-98th percentile of the plotted values plus 5% margin, so
  poles (tan) do not flatten the view. Polar: no panning; zoom and fit
  change R Max only (owner).
- Trace (G2): a dot on the curve at the mouse's x (polar: its angle)
  with a short value label; the status line shows `g(2.35) = <full
  precision>`. The x is rounded to what is shown, and the engine's
  `value_at` computes the value there (does not touch answer/$n).
  Click copies the full value; right-click: Copy Value, Insert Value in
  Input. View > Trace turns it off.
- Several functions (G3): the function drop-down is a checklist in the
  sidebar and the Graph window (shared). Ticked functions are drawn, each
  in its colour (swatch in the list, legend in the Graph window when more
  than one); the selected one is drawn thicker and is what Edit, Delete
  and the trace use. Functions without exactly one parameter are listed
  greyed out. The sidebar keeps the X range and fits Y to the lines
  (polar: R) so their shape shows (owner). Saved as graph.shown; older
  files show their one saved function.

## Owner tweaks 2026-11-07
- Graph window toolbar under the menu: XY, Polar, Grid, Trace, Zoom +,
  Zoom -, Fit, Reset (same commands as View). Range panel 150 px wide.
- Graph grid and axes a step stronger in both themes (Theme.PlotGrid /
  PlotAxis). Variables in dark mode: bold heading rows instead of the
  native group headers (dark blue on grey).
- History and Numeric views share one window size; Advanced View grows
  the window to the left by the sidebar width, so History and Input
  stay put on screen; leaving it shrinks back. settings.json keeps the
  History-view size.
- Double-click on a graph function edits it and no longer also toggles
  its tick (the native list toggles on double-click).
- Functions / Variables editor (owner 2026-11-07): Add and Edit in the
  Graph and Variables toolbars (sidebar and Graph window) open one
  window per kind: the definitions listed on the left, Name, Parameters
  (functions) and Definition / Value fields on the right, the composed
  line as a preview, a note when the name replaces or renames, the
  field at fault marked on error. Apply defines without closing (an
  open graph redraws); renaming defines the new name and deletes the
  old one; leaving unsaved changes asks first. A function added after
  start-up is ticked in the graph at once. Replaces the original's
  one-line dialogs (SPEC 4) on purpose.
- Trace on/off is saved (graph.trace). Dragging the graph repaints at
  once and fetches new points at most every 120 ms during the drag.

## After v1.0.0 (owner 2026-11-07)
- About box: a short note (version, data folder); the third-party
  licences open from a link under it.
- Literals in another base than the output (E10 extended): History's
  faint right-hand form shows them in the output base at calculation
  time, also on lines without variables, and in RPN. Only when the
  output base shows the literal exactly (`0x1F+1` shows `31+1`;
  in hex `0.5` shows `0x0.8`, `0.1` stays `0.1`). A `-` stays an
  operator (`-0x1F` shows `-31`); an RPN negative literal keeps its
  sign. Imaginary literals (`2i`) are decimal only and stay. The
  Input hint line shows the literal at the caret the same way
  (`255 = 0xFF`), through the engine's `literal` op.
- History context menu: Delete (the selected line) and Clear History,
  both without asking. Deleting a result line removes its `$n` and
  later references renumber; `answer` stays `$1`, and keeps its value
  when the last result goes (as Clear History does). history.jsonl
  keeps a `{"deleted": id, "time": ...}` mark so the line does not
  come back from the file or another window; a later clear drops the
  marks it covers. After a restart, text-only results (long dates)
  keep their `$n` place, so numbering matches the running session.
- Two's complement: `twos(x, [bits])` gives the unsigned bit pattern of
  a signed value (`twos(-1, 8)` = 255), `signed(x, [bits])` reads one
  back (`signed(0xFF, 8)` = -1). Outside the word they wrap like
  hardware (`twos(300, 8)` = 44); a fraction or complex x is an error;
  bits 1-1024. Without bits, View > Word Size (8/16/32/64, also on the
  ribbon's Output group, saved as `word_size`, default 32) applies at
  calculation time. History's right-hand form fills the bits in and
  adds the whole line's result in hex: `twos(-1)` shows
  `twos(-1, 32) = 0xFFFFFFFF` (nothing added when the output is hex).
  Functions menu: Two's Complement. In RPN only the one-argument form
  works (RPN takes each function's fixed argument count).
  Results are exact while they fit the precision (about 128 bits at
  32 digits); wider words need a higher precision.

## Customize Ribbon (owner 2026-11-08, design/ribbon_plan.md)
- Every menu command can go on the ribbon. A menu item without a
  submenu becomes a button that clicks it (check marks follow the menu
  item); a submenu becomes a drop-down copied from the menu when it
  opens (Theme, Word Size, the whole Conversions menu). Each function,
  conversion pair and user function is its own command. One extra
  command: Precision (`Prec 32`), because its menu items sit inside
  Advanced Options. New menu item Tools > Graph... so Graph is a menu
  command too.
- A command id is its menu path without `&` and `...`
  (`View/Hexadecimal Output`, `Conversions/Length/ft/in`); a user
  function uses its name (`Functions/User Functions/g`), so new
  parameters keep its button. If a menu text changes later, an alias
  table will map the old id.
- View > Customize Ribbon... or the ribbon's right-click menu
  (Customize Ribbon..., Hide Ribbon). Left: Available commands in menu
  order, a menu filter and a search box. Right: groups (caption,
  columns) and their buttons. Add (also double-click or Enter) appends
  after the selected button or at the end of the selected group;
  Remove (also Delete). The selected row shows Up / Down / Edit /
  Remove (Ctrl+Up / Ctrl+Down / F2 / Delete); a button at the edge of
  its group moves into the next group, a group swaps with its
  neighbour. New group; Reset to default (B3).
- Edit group: caption and columns (1-12); `{decimals}` in a caption
  shows the decimal places sample. Edit button: label (empty = the
  command's name) and Show icon and text / icon only / text only
  (commands without an icon show text). No icon picker yet.
- OK and Apply rebuild the ribbon and write the `ribbon` key of
  settings.json at once (null = default); Cancel after Apply goes back.
  A closing window writes `ribbon` only if it changed it itself, so a
  window opened earlier does not overwrite a newer layout; other open
  windows show it the next time they open.
- A button whose command is gone (deleted user function) is kept in
  the layout but hidden, and comes back with the command; the window
  shows it greyed as "(missing)". A group with no shown buttons is
  hidden.
- Later (TODO): drag and drop in the tree, an icon picker.
