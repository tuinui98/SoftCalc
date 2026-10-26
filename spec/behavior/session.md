# Session behavior: answer, RPN, keypad, memory, files (observed)

Sources: `spec/golden/Q_answer.tsv`, `M_rpn.tsv`, `R_retest.tsv`,
`O_keypad.tsv`, and `spec/raw/saved_funcs.pcf` (not in git). Run
2026-10-27 through `tools/phase2_rest.ps1`.

## Input box after Enter
- Success: Input is cleared.
- Error: Input **keeps the text**, so the user can fix it. Typing after
  an error appends to the old text.

## `answer` (last result)
- Keyword `answer` holds the last successful result (`5+5`, then
  `answer*3` = 30, `answer+answer` = 60 after `answer` became 30).
- Auto-insert: typing an operator into an **empty** Input right after a
  result inserts `answer` first: typing `+` gives `answer+`. Same in the
  keypad (`2 Ent *` gives `answer*`).
- Survives Clear History: after clearing, `answer` still returned 60.
- `ans` and `$1` do not exist.

## RPN mode
- One line per calculation, tokens separated by spaces, postfix
  operators and functions: `64 53 +` = 117, `1 2 3 + *` = 5,
  `5 sqrt`, `pi 2 *`.
- Negative literals work (`-2 3 *` = -6). No `neg` operator.
- Infix input is rejected while RPN is on (`1+2` is an error).
- No stack carried across lines: entering `5`, `3`, `+` separately gives
  5, 3, then an error. Leftover operands (`2 3`) are an error.

## Precision 512 (retest)
- With the setting verified, 512 works: `pi` and `1/3` print about 500
  digits, `2^1000` prints all 302 digits exactly; `2^1000` took ~630 ms.
- The earlier 512 rows in `L_precision.tsv` matched 128 and are not
  trustworthy (setting not confirmed then). Re-run L before relying on
  them.

## Defining a function with a built-in name
- `sin(x)=1` and `cos(x)=x` print `Function Added`, but `sin(1)` and
  `cos(5)` still use the built-ins. The user definition is stored (it
  appears in the saved `.pcf`) and shadowed. The clone should reject the
  definition instead.

## Keypad (Numeric view)
- Digit and operator keys append to Input; `Ent` evaluates; `Clr`
  clears Input.
- Memory keys are text macros on a variable named `memory`:
  - `M+` on Input `5` with no memory yet submits `memory = 5`
    (`Variable Added`).
  - `MR` inserts the text `memory`.
  - `MC` puts `memory = ` into Input and submits it, which is an error
    (empty assignment).
  - After `MC`, the error leaves `memory = ` in Input (see above), and
    the next `M+` builds `memory = memory + memory = 5`, which fails.
  - So M+/M-/MC only behave in the simplest sequence. Treat as an
    original bug: the clone should give real memory semantics
    (MC = 0, M+ adds the current value, M- subtracts, MR recalls).

## Save Functions & Variables (`.pcf`)
- UTF-16 LE text without BOM, CRLF line endings, one definition per line,
  variables first, then functions:
  ```
  memory=
  zzpa=
  g(x) = x
  function(var) = var*cos(var)
  sin(x) = 1
  cos(x) = x
  zzpf(x) = x*answer
  zzpg(a, b) = a^b
  ```
- Functions are written as `name(params) = body`, with `, ` between
  params.
- **Observed problems in the saved file**:
  - Variables are written with **empty values** (`zzpa=` although
    zzpa was 5).
  - `zzpf(x)=x*zzpa` was written as `x*answer`. Cause unknown; possibly
    the auto-insert or an internal rewrite. To isolate.
- **Loading that file hung the program** (100% of one CPU core for
  10+ minutes, file kept open); the process had to be killed. Likely
  trigger: the empty variable values. To isolate with small hand-made
  files and a watchdog.

## Save History
- The dialog produced a **0-byte file** in this run. Unclear whether
  saving failed or the run order was off. To retest.

## Not done yet
- Polar range panel (the run stopped at the hang before reaching it).
- Edit Variable dialog.
