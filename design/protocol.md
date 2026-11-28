# SoftCalc engine protocol (version 1)

The UI (`SoftCalc.exe`) starts one engine process per window
(`softcalc-engine.exe`, or `python -m softcalc_engine` in development)
and talks to it over the process's stdin and stdout.

## Transport
- UTF-8 text, one JSON object per line (`\n`), in both directions.
- The UI sends requests; the engine answers each one with exactly one
  response line, in order. Nothing else is written to stdout; the
  engine writes diagnostics to stderr only.
- On start the engine sends one unsolicited line:
  `{"id": 0, "op": "ready", "protocol": 1, "version": "<engine version>"}`.
- Closing stdin, or the `shutdown` request, ends the process.

## Envelope
Request: `{"id": <int>, "op": "<name>", ...arguments}`
Response, success: `{"id": <same>, "ok": true, ...results}`
Response, failure: `{"id": <same>, "ok": false, "error": "<message>"}`

`ok: false` means the request itself was wrong (unknown op, bad
argument type). A calculation that fails is still `ok: true`, with
`kind: "error"` (see `eval`).

Numbers shown to the user are always sent as display text, formatted
by the engine. Only `plot` and the `schedule` graph series send floats.

## Operations

### `eval`
`{"input": "2x+1"}` ->
`{"kind": "result"|"definition"|"error"|"empty"|"rates_needed", "input": "2x+1",
"display_input": "2*5+1", "text": "11", "note": ""}`
- `kind: "empty"`: add nothing to History.
- `kind: "rates_needed"`: the line converts a currency and the engine
  has no rates, or they are due (`set_rates`). Nothing was recorded;
  the UI fetches the rates, sends `set_rates` and evaluates the line
  again (M8).
- `note`: the rate date (`ECB 2026-11-05`, plus `, offline` when the
  last fetch failed) when a currency rate was used, else `""`.
- A line starting with `:` sets settings (owner 2026-11-26,
  `commands.py`): `:deg`, `:hex`, `:prec 64`, `:digits 2|all`,
  `:pad on|off`, `:rpn on|off`, `:word 16`, short or full names, several
  on a line, all or none. It answers `kind: "definition"` with the
  settings said back (`Degrees, Precision 64`) and `settings`, the full
  settings object as `settings` returns it, for the UI to keep; a bare `:`
  lists them as the line that sets them again (`:prec 32 :rad :dec
  :rpn off :digits all :pad off :word 32`) and sends no `settings`. A wrong one is an error and changes
  nothing. No `$n`, no answer.
- `:sw`, `:lap`, `:timer`, `:alarm` lines (owner 2026-11-28,
  `timers.py`) answer `kind: "definition"`, a message, and `timer`, the
  action for the UI, which keeps the clock:
  `{"kind": "stopwatch", "do": "toggle"|"start"|"stop"|"pause"|"go"|"reset"|"lap"}`,
  `{"kind": "timer"|"alarm", "do": "start", "seconds": 900.0,
  "at": "28.11.2026 05:07:29", "label": "15m", "name": "tea"|null}`,
  `{"kind": ..., "do": "show"|"off", "name": ...}`. The length or time
  may be a calculation (`:timer 0:05*3`, `:alarm now+0:15`); `seconds` is
  from the engine's now, so the UI adds it to its own clock. No `$n`, no
  answer; the stopwatch's message is a stand-in the UI replaces.
- `display_input` is the History expression line (extensions.md E10);
  `input` is what to put back in the Input box when the line is
  clicked; `text` is the result line (`ERROR:...` for errors).

### `record`
`{"label": "Lap 2", "value": "0:00:41.2", "note": "total 0:01:23.4"}` ->
the `eval` reply with `input` = the value (a click in History brings back
a line that calculates) and `display_input` = `Lap 2, total 0:01:23.4`.
The stopwatch's measure (owner 2026-11-28): `value` is evaluated and
kept as `answer` and `$n`, as a typed line is.

### `clear_history`
`{}` -> `{}`. Forgets `$n` results; `answer` survives.

### `settings`
Any subset of `{"precision": 32|64|128|512, "angle": "rad"|"deg"|"grad",
"base": "dec"|"hex"|"bin"|"oct", "rpn": bool, "decimals": int|null,
"pad_zeros": bool, "word_size": 8|16|32|64}` -> the full settings
object after the change. `{}` just reads them. `word_size` is the bits
`twos(x)` and `signed(x)` use without a second argument.

### `state`
`{}` ->
```
{"constants": [{"name": "pi", "value": "3.14159..."}, ...],
 "variables": [{"name": "Rate", "value": "0.05"}, ...],
 "functions": [{"name": "g", "params": ["x"], "body": "x",
                "head": "g(x)=", "source": "g(x)=x"}, ...]}
```
Values at the current precision and base. Feeds the Variables list,
the function drop-down, the User Functions menu, and the Edit dialogs
(`head` is the read-only label, `body` the editable text).

### `delete`
`{"name": "Rate"}` -> `{"deleted": "variable"|"function"}`; `ok: false`
if there is no such user variable or function (constants cannot be
deleted).

### `catalog`
`{}` -> the menu contents:
```
{"functions": {"Basic Operations": ["+", "-", "*", "/", "^", "sqrt", "abs", "mod", "!", "%"],
               "Trig Functions": [...], "Log Functions": [...],
               "Date Functions": [...], "Finance Functions": [...]},
 "conversions": {"Length": ["in", "ft", ...], "Time": [...], ...,
                 "Time zone": ["utc", "local"],
                 "Currency": ["THB", "USD", ...]},
 "subgroups": {"Matrix": {"Build": ["identity", ...], "Size and pick": [...], ...}},
 "currencies": ["AUD", "BRL", ...],
 "formats": ["longdatetime", "longdate", "longtime", "epoch", "fromepoch"]}
```

`subgroups` (owner 2026-11-25): a function group's submenus, shown after
its own items and a separator. Functions > Matrix holds the ten used most,
then Build, Size and pick, Linear algebra and Arrange; the top ten are in
their submenus too, sum and mean in Functions > Statistics (owner 2026-11-26).

### `menu_insert`
`{"kind": "operator", "symbol": "+", "text": "1", "caret": 1}`
`{"kind": "function", "name": "sqrt", "text": "1", "caret": 1}`
`{"kind": "conversion", "source": "in", "target": "cm", "text": "1", "caret": 1}`
`{"kind": "format", "name": "longdatetime", "text": "now", "caret": 3}`
`{"kind": "currency_from", "code": "THB", "text": "100"}` (appends ` [THB`)
`{"kind": "currency_to", "code": "USD", "text": "100 [THB"}` (appends `->USD]`)
-> `{"text": "...", "caret": <int>}` (SPEC 3 insertion rules).

### `plot`
`{"function": "g", "mode": "xy"|"polar", "start": -10, "end": 10,
"samples": 400}` ->
`{"points": [[x, y], ...]}` with `null` in place of a point where the
function is undefined or complex (the UI breaks the line there).
- xy: x runs from start to end; y = f(x).
- polar: theta runs from start to end; r = f(theta);
  point = (r cos theta, r sin theta), theta in radians.
- Only one-parameter user functions; computed at low precision
  (plain floats when the expression allows, `fastplot.py`).

### `value_at`
`{"function": "g", "x": "1.5"}` -> `{"text": "...", "value": 1.23}`:
the function's value at x (polar: at theta) at full precision,
formatted like a result, plus a float for drawing; both `null` where
it is undefined or not real. Graph trace (G2); does not change
`answer` or `$n`. `x` is decimal text as the status line shows it, so
`"1.43"` is exactly 1.43 (`g(x)=x^2` gives `2.0449`); a number is still
accepted and means its exact binary value.

### `plot_equation`
`{"equation": "x^2+y^2=r^2", "x_min": -10, "x_max": 10, "y_min": -10,
"y_max": 10, "cols": 60, "rows": 45}` -> `{"segments": [[x1, y1, x2, y2], ...]}`:
line segments where lhs = rhs (x and y are the axes, other names are
variables). lhs - rhs is sampled on a cols x rows grid; cells where it
changes sign are split 6 x 6 and traced by marching squares; a sign
change through a pole is left out. At most 40000 cells. A malformed
equation answers `ok: false` (`An equation needs one '='`, `An
equation needs x or y`, `Unknown name 'q'`). Low precision (graph2_plan G5).
Optional `"angle": "rad"|"deg"|"grad"` draws in that angle mode instead
of the setting, without changing it (the Graph Examples pictures).

### `equation_at`
`{"equation": "x^2+y^2=r^2", "axis": "x"|"y", "value": "1.5", "guess": 2.6,
"window": 0.1}` -> `{"x": "1.5", "y": "2.598...", "x_value": 1.5, "y_value": 2.598}`:
the point on the curve with `axis` fixed at `value` (decimal text, so
it is exact), the other coordinate solved near `guess` at full
precision; all `null` when no root lies within `window`. Graph trace
(G6); does not change `answer` or `$n`.

### `literal`
`{"text": "0x1F"}` -> `{"text": "31"}`: one number literal shown in
the output base for the Input hint line; `null` when the text is not a
single literal, is already in the output base, or the output base
cannot show it exactly (`0.1` in hex). A rai-ngan-wa group answers in
rai: `{"text": "4-1-50"}` -> `{"text": "4.375 rai"}`. History lines use the same rule
in `display_input`.

### `hint`
`{"text": "fv(0.05, ", "caret": 9}` -> `{"text": "fv(rate, nper, pmt, [pv], [type]) - ..."}`:
the hint line under Input for the text left of `caret` (a character
index, clamped), or `null`. In order: a number literal at the caret that
the output base shows differently (`0x1F = 31`, `4-1-50 = 4.375 rai`,
as `literal`); an open `[` (tag examples); `$` or `$12` (result
references); the word being typed (a user function or variable first,
then a built-in name, else up to 6 built-in names it can become, from 2
letters on); the innermost unclosed call. Rules and wording are in
`softcalc_engine/hints.py`, shared by the desktop and the web app
(owner 2026-11-18).

### `pcf_export`
`{}` -> `{"text": "<file content>"}`. The UI writes it as UTF-16 LE
without BOM (SPEC 8).

### `pcf_import`
`{"text": "<file content>"}` -> `{"skipped": [{"line": 2, "text": "zzv=",
"reason": "..."}]}`. The UI decodes the file (UTF-16 LE or UTF-8).

### `restore_results`
`{"texts": ["5", "0xFF", "(3+4i)", ...]}` -> `{"restored": <int>}`.
Result texts from a saved History, oldest first, are parsed back into
values for `answer` and `$n` (M7). Text-only results (long dates) and
lines that no longer parse come back as text, so `$n` numbers the same
lines as before the restart; `restored` counts the ones that parsed
back as values. Values keep only the digits that were shown.

### `delete_result`
`{"n": 2}` -> `{}`: removes `$n` (1 = newest) when its History line is
deleted; later references renumber. `answer` stays `$1`; when the last
result goes it keeps its value, as after `clear_history`. An `n`
outside the stack is an error.

### `schedule`
`{"principal": "1000", "annual_rate": "5", "years": "10",
"compounding": "yearly"|"quarterly"|"monthly"|"daily"|"continuous",
"contribution": "100", "contribution_freq": "monthly",
"at_start": false, "rate_unit": "yearly"|"monthly"|"weekly"|"daily",
"length_unit": "years"|"months"|"weeks"|"days"}` ->
```
{"future_value": "...", "total_contributions": "...", "total_interest": "...",
 "rows": [{"period": 1, "start": "...", "interest": "...",
           "contribution": "...", "end": "..."}, ...],
 "periods_per_year": 12, "periods_per_unit": 12.0, "length_unit": "years",
 "series": {"principal": float, "contributions": [float, ...], "interest": [float, ...]}}
```
Inputs are text (any engine expression, e.g. `"5"` or `"10000/12"`);
`annual_rate` is in percent. Amounts are display text at the current
settings; `series` holds cumulative contributions (principal included)
and cumulative interest per row, for the growth graph. An input that
fails answers `ok: false` with an error starting with the field name
(`"years: must be positive"`); at most 100000 rows.
`annual_rate` is per `rate_unit` and `years` is the length in
`length_unit` (owner 2026-11-28; the field names stay, both default to
yearly / years). A year is 365 days, a month 365/12, a week 7. Compounded
more often than the rate's unit, the rate is split evenly (12% a year
monthly: 1% a month); less often, it compounds over the longer period
(0.1% a day yearly: 1.001^365 - 1). A part period is cut, and a length
shorter than one period is an error (`"years: shorter than one month"`).
`periods_per_unit` is rows per length unit, the graph's x axis.

### `set_rates`
`{"date": "2026-11-05", "rates": {"USD": "1.0812", "THB": "37.95", ...},
"stale": false, "next_check": 1762444800}` -> `{}`.
ECB reference rates, units per 1 EUR (EUR itself is implied). The
engine does no network I/O; the UI fetches and caches the rates (M8).
`next_check` is Unix seconds: a conversion after it answers
`rates_needed`. `stale: true` marks rates kept after a failed fetch.
`rates: {}` with a future `next_check` means none are available yet
(conversions then fail with `Currency rates unavailable`).

### `draws`
`{"seed": "42", "line": "rand(1, 6)", "count": 10, "unique": false}` ->
`{"texts": ["4", "4", ...]}`. The Random window's Seed (owner
2026-11-12): evaluates `rands(seed)`, then `line` `count` times
(1 to 10000), so the texts are what typing those lines in Input would
give at the same settings. `seed` is a plain number (`42`, `-7.5`).
`unique: true` draws again when a text came up before (No repeats)
and fails after count x 50 + 1000 tries. A line that gives an error
fails the request with that error. The draws are not kept as `$n`.
`apart: true` (the web app, owner 2026-11-20) draws in a second engine
at this one's settings, so the calculator's own seed, `$n` and answer
are left as they were.

### `decks`
`{"decks": [{"name": "Months", "alias": "เดือน", "cards": [{"name": "January", "alias": "มกราคม", "weight": 2, "off": true}, ...]}, ...]}`
-> `{"decks": <int>}`. The decks `card("Deck", [count], [no repeat])`
draws from (owner 2026-11-24): the user's own first, then the ready-made
ones, in English with the Thai names as `alias`. A deck found by its
alias gives its cards' aliases (a Thai deck name, Thai cards). A name matches in any case; the first deck
of a name wins. Cards with `off` (saved to start unticked) are left out
of draws. Replaces the decks sent before; the reply counts the decks.

### `shutdown`
`{}` -> `{}`, then the process exits.
