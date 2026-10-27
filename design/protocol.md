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
`{"kind": "result"|"definition"|"error"|"empty", "input": "2x+1",
"display_input": "2*5+1", "text": "11"}`
- `kind: "empty"`: add nothing to History.
- `display_input` is the History expression line (extensions.md E10);
  `input` is what to put back in the Input box when the line is
  clicked; `text` is the result line (`ERROR:...` for errors).

### `clear_history`
`{}` -> `{}`. Forgets `$n` results; `answer` survives.

### `settings`
Any subset of `{"precision": 32|64|128|512, "angle": "rad"|"deg"|"grad",
"base": "dec"|"hex"|"bin"|"oct", "rpn": bool, "decimals": int|null,
"pad_zeros": bool}` -> the full settings object after the change.
`{}` just reads them.

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
                 "Time zone": ["utc", "local"]}}
```

### `menu_insert`
`{"kind": "operator", "symbol": "+", "text": "1", "caret": 1}`
`{"kind": "function", "name": "sqrt", "text": "1", "caret": 1}`
`{"kind": "conversion", "source": "in", "target": "cm", "text": "1", "caret": 1}`
-> `{"text": "...", "caret": <int>}` (SPEC 3 insertion rules).

### `plot`
`{"function": "g", "mode": "xy"|"polar", "start": -10, "end": 10,
"samples": 400}` ->
`{"points": [[x, y], ...]}` with `null` in place of a point where the
function is undefined or complex (the UI breaks the line there).
- xy: x runs from start to end; y = f(x).
- polar: theta runs from start to end; r = f(theta);
  point = (r cos theta, r sin theta), theta in radians.
- Only one-parameter user functions; computed at low precision.

### `pcf_export`
`{}` -> `{"text": "<file content>"}`. The UI writes it as UTF-16 LE
without BOM (SPEC 8).

### `pcf_import`
`{"text": "<file content>"}` -> `{"skipped": [{"line": 2, "text": "zzv=",
"reason": "..."}]}`. The UI decodes the file (UTF-16 LE or UTF-8).

### `schedule`
`{"principal": "1000", "annual_rate": "5", "years": "10",
"compounding": "yearly"|"quarterly"|"monthly"|"daily"|"continuous",
"contribution": "100", "contribution_freq": "monthly",
"at_start": false}` ->
```
{"future_value": "...", "total_contributions": "...", "total_interest": "...",
 "rows": [{"period": 1, "start": "...", "interest": "...",
           "contribution": "...", "end": "..."}, ...],
 "series": {"contributions": [float, ...], "interest": [float, ...]}}
```
Inputs are text (any engine expression, e.g. `"5"` or `"10000/12"`);
`annual_rate` is in percent. Amounts are display text at the current
settings; `series` holds cumulative contributions (principal included)
and cumulative interest per row, for the growth graph.

### `shutdown`
`{}` -> `{}`, then the process exits.
