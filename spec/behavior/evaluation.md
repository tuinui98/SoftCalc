# Expression evaluation behavior (observed)

Source: `spec/golden/<group>.tsv` from `tools/probe_eval.ps1`, groups A-L
and N of `spec/phase2_test_plan.md`. History is cleared before every case,
so each case runs alone. Defaults unless noted: precision 32, Radians,
decimal output, RPN off.

## History format
Each evaluation appends two lines: the input as typed, then the result
(indented with a tab). Definitions print `Variable Added` /
`Function Added`. Errors print `ERROR:<message>`.

## Lexing
- Whitespace is removed before parsing (`1 000` = 1000; errors echo the
  input without spaces, e.g. `[7mod2]`).
- Names are case-insensitive (`PI`, `ZZA` work).
- Names: letters only. Digits or `_` in a name give
  `ERROR:Garbage prior to the =` on assignment, and are unknown otherwise.
- No implicit multiplication: `2zza`, `zza zza` are errors. Exception:
  `2i` is accepted as a complex literal.

## Numbers
- Decimal: `1e3`, `1.5e-3`, `1E+2`, `.5`, `5.`, leading zeros all fine.
  `1e` evaluates to 1 (dangling exponent ignored).
- Hex: `0x1F`, `0XFF` (any case). Binary: suffix `b` (`101b` = 5);
  `0b101` is an error.
- No thousands separator (`1,000` error). `1.2.3`, `2..5` errors.
- **`0x` alone crashes the original program** (access violation). The
  clone should report an error instead.

## Operators and precedence
- `+ - * / ^`, parentheses. No `!`, `%`, `mod`.
- `^` is **left-associative**: `2^3^2` = 64.
- Unary minus binds tighter than `^`: `-2^2` = 4. `2^-1` = 0.5,
  `2*-3` = -6. Leading `+5` = 5. `--2` and `1++2` are errors.
- Division by zero and `0/0`: `ERROR:Evaluation failed`.

## Output bases
- Hex output: `0xFF`, negatives `-0x1`, fractions in hex (`0x3.8`,
  `0x0.1999...9A`), complex `(0x3+0x4i)`.
- Binary output: trailing `b`, large/small values in binary scientific
  form, e.g. `2^40` gives `1.b*10b^101000b`, `0.1` gives
  `1.1001...1101b*10b^-100b`; `255` gives `11111111.b` (note trailing `.`).
- Inputs in other bases still work in decimal output (`0xff+101b` = 260).

## Complex numbers
- `i` is the imaginary unit (`j` is not). Results print as `1i`, `2i`,
  `(3+4i)`, `(11+2i)`; real-valued results print plain (`i^2` = -1,
  `exp(i*pi)` = -1).
- Principal branches: `sqrt(-1)` = `1i`, `ln(-1)` = `πi`,
  `(-8)^(1/3)` = `(1+1.732...i)`, `asin(2)` complex, `acosh(0)` =
  `1.5707...i`, `log10(-1)` = `1.3643...i`. `abs(3+4*i)` = 5.

## Functions
- Trig: `sin cos tan asin acos atan`, hyperbolic and inverse hyperbolic.
  Angle mode applies to circular trig input and inverse trig output;
  hyperbolic ignores it (`sinh(1)` same in deg).
- `tan(pi/2)` (rad) and `tan(90)` (deg) give `ERROR:Evaluation failed`,
  so the poles are detected rather than returning a huge number.
  `atanh(1)`, `ln(0)`, `log10(0)` also `Evaluation failed`.
- Logs: `ln`, `log10`, `exp`. `log` and `log2` do not exist.
- **Original bug: `exp(-1000)` prints `1.0179...e395`** (exponent sign
  lost; true value is about 5.08e-435). `exp(1000)` = `1.97...e434` is
  correct. Decide in the clone whether to keep or fix (recommend fix).

## Constants and history references
- `pi`, `e` built in, cannot be reassigned (`pi=3` gives
  `Evaluation failed`).
- No `ans`, `$1`, or similar history references (all unknown tokens).
  The original uses the keyword `answer` instead; see `session.md`.

## Variables
- `name=expr` prints `Variable Added`; also used to update.
- The value is computed **at assignment**: after `zzc=2*zza` with zza=6,
  changing zza leaves zzc at 12.
- Referencing an undefined name is an error; `zzb=` with no value is an
  error. `=5` evaluates to 0 (odd; treat as error in the clone?).

## User functions
- `name(params)=body` prints `Function Added`; redefining replaces it.
- Multiple parameters work (`zzg(x,y)=x+y`); calls must match arity,
  otherwise an error with empty brackets `ERROR:Unknown characters
  occurred []`.
- Bodies see **current** variable values at call time (`zzk(t)=t*zzv`
  follows changes to zzv), unlike variables.
- Recursion is rejected at definition.
- `sin(x)=1` is accepted as `Function Added`, but the built-in still
  wins when called (see `session.md`).
- Calling a function without parentheses or with `()` is an error.

## Conversions
- Postfix tag `value [from->to]`; the space before `[` is optional and
  tags are case-insensitive. They apply to the whole preceding
  expression (`2*3 [ft->in]` = 72, `(1+1) [ft->in]` = 24) and to
  variables.
- Tags chain left to right (`1 [ft->in] [in->cm]` = 30.48), and an
  expression may continue after a tag (`1 [ft->in]+1` = 13).
- Cross-category, empty, or unknown units are errors. Writing
  `1 in->cm` without brackets is an error.
- Factors seen: 1 in = 2.54 cm, 1 mile = 1.609344 km,
  1 kg = 2.2046224760379583... lb, 1 knot = 0.514444... m/s (1852/3600),
  0 C = 32 F, 0 K = -273.15 C.

## Precision
- 32 / 64 / 128 digits behave as total significant digits. Example: at 64,
  `2^200` prints all 61 integer digits; at 32 it prints
  `1.6069...e60`.
- Exact-integer-looking results drop trailing zeros: `10^100` gives
  `1.e100`, `1e-40` gives `1.e-40`. Note the `1.` before `e`.
- `0.1+0.2` = 0.3 and `1e20+1-1e20` = 1 at 32 digits, so arithmetic is
  decimal or big-precision, not binary double.
- Overflow handling: `10^1000` = `1.e1000`; a 500-digit integer of 9s
  rounds to `1.e500`.
- 512 works (about 500 digits); `L_precision.tsv` was re-run with
  verified settings and now holds valid 512 rows.

## Errors and edge input
- Empty input: nothing is added to History.
- Spaces only, dangling operators, empty argument lists, unknown
  functions: `ERROR:Unknown characters occurred []`.
- Unbalanced parentheses echo the input: `[(1]`.
- Non-ASCII names (`ก=1`): `ERROR:Garbage prior to the =`.
