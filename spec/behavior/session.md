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
- `L_precision.tsv` was re-run with verified settings; its 512 rows are
  now valid.

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
- **Observed problems in the saved file** (retested with
  `tools/probe_files2.ps1`):
  - Variables are always written with **empty values** (`zzsv=`,
    `zzsw=`; reproduced twice). Variable values are lost on save.
  - `zzpf(x)=x*zzpa` was once written as `x*answer`; the retest wrote
    `zzsf(x) = x*zzsw` correctly. Not reproduced, so not a spec item.
- **Load Functions & Variables hangs every time** (100% of one core,
  file kept open, killed by the watchdog after 15 s). Six hand-made
  files all hung, including a single-function file
  (`zzt(x) = x+1`), so the trigger is not empty values. Load is unusable
  on this Windows 11 machine under the XP compat layer. Whether it worked
  on real XP cannot be checked here.
- Clone consequence: no working original to match for load. Define the
  format from the save output above, keep variable values, and make
  load tolerant (skip bad lines, report them).

## Save History
- Produces a **0-byte file**, reproduced twice with a non-empty History.
  Same conclusion: broken here; the clone defines its own plain-text
  format (input line, then tab-indented result, as on screen).
