# PowerCalc clone - consolidated specification

Version 1, 2026-10-27. Built only from black-box observation of the
original (see `inventory/`, `behavior/`, `golden/`) plus the owner's
decisions. Detailed evidence lives in those files; this document is the
contract for the clone.

Tags used below:
- **[KEEP]** match the original.
- **[FIX]** original defect; the clone behaves differently on purpose.
- **[EXT]** requested extension (`extensions.md`).
- **[DEF]** minor choice made by default during consolidation; listed
  again in section 9 for review.

---

## 1. Scope
- Personal-use desktop calculator for Windows 10/11 reproducing the
  original's UI and behavior, with the fixes and extensions below.
- No original code, icons, bitmaps, help text, or names. New name and
  icon (to choose in Phase 4).
- Out of scope for v1: Help/CHM content (write new help later),
  clipboard beyond standard copy of History text.

## 2. Window and views [KEEP]
- One main window with menu bar *File, View, Functions, Conversions*.
- Panes have a titled header strip (light-blue gradient, bold blue
  caption). Three views (View menu, radio):
  - **Numeric Calc**: History, Input, keypad. Samples never shown.
  - **History View**: History, Samples, Input.
  - **Advanced View** (default): left sidebar with collapsible *Graph*
    and *Variables* sections; right column History, Samples, Input.
- **Keypad** (Numeric view), 4x5: `M+ 7 8 9 +` / `M- 4 5 6 -` /
  `MC 1 2 3 /` / `MR Clr 0 Ent *`.
- **Graph section**: toolbar *Add, Edit, Delete, Window*; drop-down of
  functions; plot with grid and axes.
- **Variables section**: toolbar *Add, Edit, Delete*; list with columns
  *Name, Definition*, grouped: *Constants* (`pi`, `e`, shown at current
  precision) then user variables. Edit/Delete enabled only for a
  selected user variable.
- **Samples pane**: short syntax examples; switches to RPN examples in
  RPN mode. Content written new [DEF].
- Window position, size, maximized state, view, and *Show Samples*
  persist between runs.

Reference screenshots of the original: `spec/raw/*.png` (local only).

## 3. Menus
Full original tree: `inventory/menu_tree.txt`.
- **File**: Help, Save History, Save Functions & Variables, Load
  Functions & Variables, Exit. [KEEP]
- **View**: Clear History; Numeric / History / Advanced; Hexadecimal /
  Decimal / Binary Output; Show Samples; Advanced Options > Degrees /
  Radians / Grads, RPN Mode, Low (32) / Medium (64) / High (128) /
  Extreme (512) precision. [KEEP]
  - [EXT] add *Octal Output* next to Hex/Dec/Bin, since octal input is
    added (E1). [DEF]
- **Functions**: Basic Operations (`+ - * / ^ sqrt abs`), Trig (12),
  Log (`exp log10 ln`), User Functions (dynamic list). [KEEP]
  - [EXT] Basic Operations also lists `mod`, `!`, `%`. [DEF]
- **Conversions**: Length, Mass, Time, Velocity, Temperature, each a
  from-unit > to-unit submenu (section 5.9). [KEEP]
- Menu insertion rules [KEEP] (`behavior/menu_insertion.md`):
  - operators are appended at the caret;
  - functions wrap the whole current Input: `1` becomes `sqrt(1)`,
    empty becomes `sqrt()`;
  - conversions append ` [from->to]`.

## 4. Dialogs [KEEP]
- **Add Function / Add Variable**: one text box with the whole
  definition, OK, Cancel.
- **Edit Function / Edit Variable**: read-only label with the head
  (`g(x)=` or `a=`), text box with the body or value only.
- **Graph window** (toolbar *Window*): own window, menu *File > Save as
  bitmap*, *View > XY Graph / Polar Graph / Show Grid*; collapsible
  *Range* panel and *Apply*; same graph toolbar and drop-down.
  - XY range: X Min, X Max, X Increment, Y Min, Y Max, Y Increment;
    defaults -10, 10, 1, -10, 10, 1.
  - Polar range: Theta Min, Theta Max, Theta Increment, R Min, R Max,
    R Increment; defaults -pi, pi, 1, 0, 10, 1. Theta Increment and
    R Min are read-only.
  - Range values display with 6 decimals.
  - Only one-parameter functions can be graphed.
  - Save as bitmap writes PNG (BMP optional). [DEF]
- Post-clone ideas for these dialogs: `ideas.md`.

## 5. Input language

### 5.1 Lexing
- Names, keywords, and unit tokens are case-insensitive. A user name is
  displayed as first defined. [KEEP]
- Whitespace: ignored between tokens; **a space inside a digit run
  joins it** (`1 000` = 1000) [KEEP]. Two adjacent names separated by
  space are an error (`a b`). In RPN mode spaces separate tokens.
- Names [EXT E2]: `[A-Za-z_][A-Za-z0-9_]*`.
- Reserved, cannot be defined by the user: `pi e i j answer ans mod`,
  and every built-in function name. Defining one is an error
  (`sin(x)=1` was accepted and ignored by the original) [FIX].

### 5.2 Numbers
- Decimal: `12`, `.5`, `5.`, `1e3`, `1.5e-3`, `1E+2`, leading zeros.
- Scientific `e` needs at least one digit after it (optional sign).
  Otherwise `e` is the constant: `2e` = 2*e via implicit
  multiplication. The original read `1e` as 1. [DEF]
- Hex `0x1F` (any case) [KEEP]; binary suffix `101b` [KEEP]; prefixes
  `0b101`, `0o17`, `0d12` [EXT E1].
  - A `b` suffix applies only to a run of 0s and 1s that is not followed
    by a name character; `2b` is then `2*b`, and `10bar` is `10*bar`.
  - `0x`, `0b`, `0o`, `0d` with no digits are an error (the original
    crashed on `0x`) [FIX].
- No thousands separator; `1,000`, `1.2.3`, `2..5` are errors [KEEP].

### 5.3 Constants and results
- `pi`, `e` built in, at the current precision [KEEP].
- `i` and `j` are the imaginary unit; complex literals `2i`, `2j`,
  `3+4i` [KEEP i, EXT j].
- `answer` = last successful result [KEEP]; `ans` is an alias [EXT].
  It survives Clear History [KEEP].
- `$1` = latest result (same as `answer`), `$2` = the one before, and so
  on, counting only result lines that are not errors or definitions
  [EXT]. After Clear History, `$n` refers to nothing and is an error,
  while `answer` keeps its value. [DEF]
- Auto-insert: typing an operator into an empty Input right after a
  result inserts `answer` first (`+` gives `answer+`), also from the
  keypad [KEEP].

### 5.4 Operators and precedence (highest first)
| level | forms | assoc. | note |
|---|---|---|---|
| 1 | number, name, call `f(a,b)`, `( )`, `answer`, `$n` | | |
| 2 | postfix `!` (gamma for non-integers), `%` (percent, /100) | left | [EXT] |
| 3 | `^` | **right** | `2^3^2` = 512 [FIX]; right side may start with a sign: `2^-1` |
| 4 | implicit multiplication | left | `2x`, `2(x+1)`, `(a)(b)`, `2^3x` = `2^(3x)`, `2x^2` = `2*(x^2)` [EXT] |
| 5 | unary `-` `+` | | `-2^2` = **-4** [FIX], `-2x` = `-(2x)` |
| 6 | `*` `/` `mod` | left | `1/2x` = `1/(2x)`; `mod` floors: `-7 mod 3` = 2, reals allowed [EXT] |
| 7 | `+` `-` | left | `--2`, `1++2` stay errors [KEEP] |
| 8 | conversion tag `[a->b]` | left | applies to everything left of it (5.9) |

- The right operand of `^` is a signed implicit product, so
  `2^3x` = `2^(3x)` and `2^-3x` = `2^(-(3x))`, while `2x^2` =
  `2*(x^2)` because the left side of `^` is a single primary.
- Implicit multiplication happens only:
  - after a number or `)`, before a name or `(`;
  - after a variable name, before `(` (a function name followed by
    `(` is a call).
  Never between two names, and never directly between two numbers.
- `!`: `n!` exact for non-negative integers; other reals use
  `gamma(x+1)`; negative integers are an error. `%`: `50%` = 0.5,
  `200*10%` = 20 (plain divide by 100).

### 5.5 Functions [KEEP]
- `sqrt abs exp ln log10`, `sin cos tan asin acos atan`,
  `sinh cosh tanh asinh acosh atanh`. No `log`, `log2` [KEEP] (can be
  added later).
- Circular trig input and inverse trig output follow the angle mode
  (Degrees, Radians, Grads); hyperbolic functions ignore it.
- Poles and singularities are errors: `tan(pi/2)` rad, `tan(90)` deg,
  `atanh(1)`, `ln(0)`, `log10(0)`, `x/0`, `0/0`.
- All functions accept complex arguments and use principal branches:
  `sqrt(-1)` = `1i`, `ln(-1)` = `πi`, `asin(2)` complex,
  `(-8)^(1/3)` = `(1+1.732...i)`.
- `exp(-1000)` must give about `5.08e-435` (original printed
  `1.0179...e395`) [FIX].

### 5.6 Variables [KEEP unless noted]
- `name=expr` defines or updates; prints `Variable Added`.
- The value is computed at assignment, not re-evaluated later.
- Undefined names in the expression are an error. `name=` with no value
  is an error. `=5` is an error (original returned 0) [FIX].

### 5.7 User functions [KEEP unless noted]
- `name(p1, p2, ...)=body` defines or replaces; prints
  `Function Added`. Any number of parameters.
- The body sees variables' **current** values at call time.
- Recursion (direct) is rejected at definition. Calls must match the
  parameter count. Calling without `()` is an error.
- Built-in names cannot be redefined [FIX] (5.1).

### 5.8 RPN mode [KEEP]
- One complete postfix expression per line, tokens separated by spaces:
  `1 2 3 + *` = 5, `5 sqrt`, `pi 2 *`, negative literals allowed.
- No stack across lines; leftover operands or a lone operator are an
  error. Infix input is rejected while RPN is on.
- New operators (`!`, `%`, `mod`) work as postfix tokens too. [DEF]

### 5.9 Conversions
- Syntax `expr [from->to]`, space before `[` optional,
  case-insensitive, chainable (`1 [ft->in] [in->cm]` = 30.48), and an
  expression may continue after a tag (`1 [ft->in]+1` = 13) [KEEP].
- The tag converts everything to its left: `2*3 [ft->in]` = 72
  [KEEP]. `1+1 [ft->in]` is inferred to give 24, unverified.
- Units [KEEP]:
  - Length `in ft yd mile mm cm m km`
  - Mass `lb oz g kg`
  - Time `d min s`
  - Velocity `mi/h m/s ft/h km/h knot`
  - Temperature `c f k` (affine conversions)
- Factors: exact international definitions: 1 in = 2.54 cm,
  1 mile = 1609.344 m, 1 lb = 0.45359237 kg, 1 knot = 1852/3600 m/s.
  The original uses 1 kg = 2.2046224760... lb, i.e. 1 lb = 453.59243 g,
  which is not the international pound [FIX].
- Cross-category, empty, or unknown units are errors [KEEP].

## 6. Evaluation and output

### 6.1 Precision [KEEP]
- Decimal arbitrary precision, not binary floating point
  (`0.1+0.2` = 0.3, `1e20+1-1e20` = 1).
- Levels 32 / 64 / 128 / 512 = significant digits shown. Exponent
  range is effectively unbounded (`10^1000` works).
- Results that need more digits than the level switch to scientific
  form (`2^200` at 32 gives `1.6069380442589902755419620923412e60`; at 64
  all 61 digits).

### 6.2 Number format
- Trailing zeros trimmed [KEEP]. A mantissa with no fractional digits
  prints without a dot: `1e100`, `1e-40` (original `1.e100`) [FIX].
- Hex output: `0xFF`, `-0x1`, fractions `0x3.8`,
  `0x0.1999...9A` [KEEP].
- Binary output: `11111111b`, fractions `11.1b`, large or small values
  `1b*10b^101000b`, `1.1001...1101b*10b^-100b` (original put a dot
  before `b` even without fraction, `11111111.b`, `1.b*10b^...`)
  [FIX].
- Octal output, new: `0o17`, same layout rules as hex [EXT, DEF].
- Complex: `1i`, `2i`, `(3+4i)`, `(11+2i)`; results with a zero
  imaginary part print as real; hex `(0x3+0x4i)`.

### 6.3 History format [KEEP]
- Each entry: the input line, then the result on the next line,
  indented with a tab. Definitions print `Variable Added` or
  `Function Added`. Errors print `ERROR:` plus a message.
- Empty input adds nothing.
- On success the Input is cleared; on error the Input keeps its text.

### 6.4 Error messages [DEF]
- Keep the `ERROR:` prefix and the line placement.
- Messages become specific instead of the original's mostly
  `Unknown characters occurred [...]`, for example
  `ERROR:Unknown name 'zzx'`, `ERROR:Expected ')'`,
  `ERROR:zzg expects 2 arguments`, `ERROR:Division by zero`,
  `ERROR:Cannot convert in to kg`.
- Golden tests compare "is an error", not the message text.

## 7. Memory keys [FIX]
- The original's M keys are broken text macros. The clone uses a
  visible user variable `memory` (shown in Variables):
  - `M+`: evaluate the Input (empty: use `answer`), add it to `memory`
    (missing counts as 0);
  - `M-`: same, subtract;
  - `MC`: set `memory` to 0;
  - `MR`: insert the text `memory` at the caret.

## 8. Persistence and files
- Settings (precision, base, angle, RPN, view, samples, window rect,
  graph mode, grid, XY and Polar ranges) and user variables/functions
  persist between runs. Stored in the clone's own file under
  `%APPDATA%\<app>\`. The clone never reads or writes the registry.
- First run starts from defaults: precision 32, Radians, decimal
  output, RPN off, Advanced View, Show Samples on, XY graph with grid,
  default ranges (4), and the two sample functions `g(x)=x` and
  `function(var)=var*cos(var)`. No import from the original.
- **`.pcf` Save** (original's format, with values fixed):
  UTF-16 LE without BOM, CRLF, variables first as `name=value`, then
  functions as `name(p1, p2) = body`. The original wrote variables with
  empty values [FIX].
- **`.pcf` Load**: the original hung on every file [FIX]. The clone
  reads UTF-16 LE or UTF-8 (BOM or not), replaces the current user
  variables and functions, skips invalid lines, and reports them.
- **Save History**: the original wrote 0 bytes [FIX]. The clone writes
  UTF-8 text exactly as shown in the History pane.
- Graph *Save as bitmap*: PNG (4).

## 9. Minor default decisions
Marked [DEF] above; reviewed and accepted by the owner 2026-10-27.
1. Octal output menu item and Basic Operations entries for `mod ! %`.
2. `2e` = 2*e; scientific notation needs digits after `e`.
3. `$n` errors after Clear History; `answer` survives it.
4. RPN also accepts `!`, `%`, `mod`.
5. Owner changed: drop the dangling dot (`1e100`, `11111111b`), see 6.2.
6. Specific error messages with the same `ERROR:` prefix.
7. Owner changed: own settings file only, no registry, no import, see 8.
8. Graph bitmap saved as PNG.
9. Samples pane content written new.

## 10. Acceptance
- The clone must pass the golden cases in `golden/*.tsv`, except for
  the cases listed in `golden_expected_diffs.md`, which document the
  new expected result for each [FIX]/[EXT]/[DEF] change.
- Error cases match on "is an error".
- UI is checked against the reference screenshots by eye.
