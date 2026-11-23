# Matrices - plan (owner 2026-11-24, proposed; not started)

Owner decisions 2026-11-24:
- Written with `[ ]`: a list `[1, 2, 3]`, a matrix `[[1, 2], [3, 4]]`.
  Before building, check for certain that it cannot clash with what `[ ]`
  does now (step X0).
- History shows a matrix on one line; a table view comes later (it has
  many decisions of its own).
- A list works as a row or a column, whichever a function needs:
  `solve(A, [5, 1])`.
- Limits: at most 10000 cells; `inv`, `det`, `solve` at most 50x50.
- Variables, `$n`, `answer` and the .pcf hold matrices (the original
  PowerCalc cannot read such a .pcf; that is fine).
- No RPN (a matrix line in RPN mode is an error).

## What `[ ]` does now (to keep working)
- `value [from->to]`: a conversion; the body always has `->` (units,
  currencies, coins, `utc`/`local`).
- `value [name]`: a format, one of `longdatetime longdate longtime epoch
  fromepoch`.
- Both are read by `lexer.read_tag`, which drops spaces and lower-cases
  the body, and both stand after a value (`parser` takes tags after an
  additive expression).
- `1-2-3 [rnw]`: rai-ngan-wa input is spotted by looking ahead for
  `[rnw` (lexer `RNW_GROUP`).
- `hints.py`: an open `[` before the caret shows the conversion hint.
- Keypad (desktop and web): 2nd `(` `)` types `[` `]`.
- The Random window's Extra cards `[1,100]` are the UI's own text, never
  sent to the engine: no clash.

## X0 - Clash check (first, before any code; done, see X0 result)
Rule first proposed (replaced by the position rule in X0 result): a `[` starts a conversion or format when its body (up to
the matching `]`) holds `->`, or is exactly one of the five format names;
anything else is a list or matrix. Check, with tests written first:
1. Every golden row, help example and saved History line in the test
   data still parses to the same tree (`spec/golden/*.tsv`,
   `help/*.md`, engine tests).
2. Spaces and case inside tags as people type them: `[ M -> FT ]`,
   `[LongDate]`, `[rai-ngan-wa]`, `[ km/h -> mi/h ]`.
3. Tags after a list: `[1, 2][m->ft]` converts every cell;
   `[1, 2] [longdate]` an error.
4. A list after a value: `2[1, 2]` is `2*[1, 2]` (implicit product,
   as `2(1+2)`); `x[1, 2]` too. `5[m->ft]` stays a conversion.
5. Names that are also format names: `[epoch]` stays the format; a
   one-cell list of a variable called `epoch` needs `[epoch+0]` or
   `[(epoch)]` (documented). Same for unit-like variable names?
   (`[m]` has no `->`, so it is a list of the variable m: check that no
   old line relied on `[m]` meaning anything else.)
6. rnw: `1-2-3 [rnw]` still the rai-ngan-wa reading, `[1-2-3]` a list
   holding 1-2-3 = -4.
7. Old .pcf files and saved History (`restore_results`) that hold `[`
   in a definition or result text.
8. The hint for an open `[`: conversion hint while it could still be a
   tag, list hint once a `,` or a digit shows it is not.
9. The Graph: equations with `[ ]`, Conversions menu inserts, the
   ribbon's conversion buttons.
Outcome: a short report to the owner; build only after it is clean.

## X0 result (2026-11-24)
Scanned every `[ ... ]` in the golden data, help (examples and inline
code), engine tests, data/graph-examples.json, the UI and web sources,
and the owner's own history.jsonl and definitions.pcf (read only):
- Golden data, help examples, the owner's files: only conversions
  (`->`), formats, and `[rnw]` / `[rai-ngan-wa]`. The owner's History
  and .pcf hold no `[` at all.
- **The body rule above is not safe on its own**: `[rnw]`,
  `[rai-ngan-wa]` and `[RNW]` are tags without `->`; and `5 [m]` with a
  variable m would quietly become 5 times a list instead of the error
  it is now (same for typos such as `1 [in-cm]` when names exist).
- **Every line that works today has its `[` right after a value** (a
  number, a name, `)`, `$n`, `!`, `%`, a date, a tag). A `[` where an
  operand starts (start of line, after an operator, `(`, `,` or `=`) is
  always an error today (`[1,2]` gives "Unexpected '1,2'").
- So the rule becomes **by position**: a `[` right after a value is a
  tag, exactly as now (body unchanged: spaces dropped, any case); a `[`
  where an operand starts opens a list. No line that works today can
  change, and typos in tags stay errors. Inside a list, `[1 [m->ft], 2]`
  works: the inner `[` follows a value.
- Costs of the position rule: no implicit product with a list
  (`2[1, 2]` is a tag error; write `2*[1, 2]`), and `A[1]` is never
  indexing (use `at(A, 1)`). Error messages for these say what to write.
- Other places that use `[`: the .pcf's `[Graph]` header line (only a
  line that is exactly `[Graph]`; a definition line never is), the
  Random window's Extra cards `[1,100]` (the UI's own text, never sent
  to the engine), the keypads' 2nd `(` `)` keys (type `[` `]`: fine for
  both), the currency menu's half tag `100 [THB` (stays a tag: it follows
  a value), the hint for an open `[` (X3: a list hint when the `[` opens
  an operand), help text writing optional arguments as `[pv]` (wording
  only; the Matrices help says how lists are written).
- Pinned by `engine/tests/test_brackets.py` (41 cases): conversions in
  any spacing and case, chains, rnw, formats, tags after names, `$n`,
  `!`, the Conversions menu inserts, and the lines that are errors today.

## X1 - Engine core (round 1)
- Lexer/parser: `[` `]` as list brackets by the X0 position rule; items are
  full expressions (`[a, 2x, sin(30)]`); nested lists of equal length
  make a matrix; ragged ones an error. Parsed into a `("list", items)`
  node; read_tag stays for tags.
- Value: a matrix type over mpmath (`mpmath.matrix`), kept with its
  shape (a list is 1xn but also fits as nx1 where needed). Real cells in
  round 1; complex cells allowed where mpmath gives them (`eig` later).
- Format: `[1, 2, 3]`, `[[1, 2], [3, 4]]`, each cell formatted like a
  number now (precision, base, decimals); the text parses back to the
  same value (History, `$n`, .pcf, Use Expression / Insert Result).
- Limits: 10000 cells; 50x50 for inv/det/solve; clear errors.
- card(): several number cards come out as a list value `[3, 5, 1]`
  instead of the `{3, 5, 1}` text.

Built 2026-11-24 (matrix.py; lexer `ends_value`, parser `list_literal`):
lists and matrices parse, show and parse back (History, `$n`, variables,
the .pcf, every output base), at most 10000 cells, RPN refuses them,
card() gives a list value. `2[1, 2]` and `A[1]` say to write `2*[1, 2]`
or use `at(A, 1)`. No arithmetic on them yet (X2): `[1, 2]+1` is
"'+' needs a number".

## X2 - Functions (round 1)
| group | functions |
|---|---|
| make | `[ ... ]`, `identity(n)`, `zeros(r, c)`, `ones(r, c)`, `diag(list)`, `seq(a, b, [step])`, `randm(r, c, [a, b])` (follows `rands`) |
| shape, cells | `rows(m)`, `cols(m)`, `len(list)`, `at(m, i, [j])`, `row(m, i)`, `col(m, j)` (counting from 1) |
| arithmetic | `+ -` cell by cell (same shape), `*` `/` with a number, `A*B` matrix product, `A^n` (whole n; `A^-1` the inverse), `dot(a, b)`, `cross(a, b)` |
| linear algebra | `transpose(m)`, `det(m)`, `inv(m)`, `trace(m)`, `solve(A, b)` |
| lists | `sum`, `mean`, `min`, `max`, `prod`, `sort` |
| cell by cell | the one-number functions take a matrix and work on each cell: `sin([0, 30, 90])`, `round(m, 2)`, `abs(m)`, `sqrt(m)` |
Names to check against existing functions and user names (min/max may
exist already; a user function called `len` or `at`).

Built 2026-11-24 (matrix.py FUNCTIONS, functions.binary/negate/postfix,
engine.random_matrix): every function above. Choices made while
building: a list and a number go cell by cell for + - * / ^ mod (either
side); a matrix with a number for + - * and /number, `^` is the matrix
power; number / matrix is an error; list * list asks for dot(); a list
times a matrix fits as a row (left) or a column (right) and gives a list;
zeros(n)/ones(n) make a list, zeros(r, c) a matrix; diag(list) makes a
matrix, diag(matrix) gives its diagonal; len counts every cell; sum,
mean, prod, min, max take any mix of numbers and lists; min(3, 9) works
too; randm(r, c, [a, b]) draws each cell as rand() would and follows
rands(seed). The names are reserved, so a .pcf variable called `sum` or
`det` is now skipped on Load.

## X3 - Around the engine (round 1)
- Variables window (desktop) and Variables (web): a matrix variable shows
  its one-line text; Edit takes the same text.
- Hints for every new function; Functions menu group **Matrix** (desktop
  menu and ribbon command ids, web func sheet).
- Keypad: `[` `]` are already on 2nd `(` `)`; `,` is there? (check the
  Basic and Sci layouts on desktop and web.)
- Help: a Matrices topic (new number), examples checked by the test.
- Protocol: no new op; values travel as text as now.

Built 2026-11-24: catalog group **Matrix** (25 names; the desktop
Functions menu, the ribbon's fn: ids and the web Functions picker all
come from it), a hint for every new name (test: every menu function has
one), the open-`[` hint by the X0 position rule (a list hint where an
operand starts, the tag hint after a value; a call inside a list still
describes the call). Variables (desktop and web) needed no change: the
engine sends the one-line text and Edit sends it back. Keypads: Sci has
`,` and 2nd `[` `]` on desktop and web; Basic has neither (Sci sits
beside it). Help waits until the owner finishes the matrix topic
(owner rule: help after a topic is done).

SymPy check (owner 2026-11-24): tools/make_matrix_reference.py (own
venv `engine/.venv-ref`, SymPy 1.14.0 pins mpmath < 1.4) writes 1388
random cases with exact answers to engine/tests/data/matrix_reference.json;
test_matrix_reference.py compares to 28 digits. It found: a matrix with
det 0 on paper but decimal cells (0.316 is not exact in binary) gave an
"inverse" of 1e43 instead of the error. Fixed: det, inv, solve and `^`
work at twice the working digits, and a det below the shown precision
against Hadamard's bound counts as 0 (det shows 0, inv/solve say there is
no inverse). Left as is: an ill-conditioned matrix (cond 1e20) loses that
many of its shown digits to the rounding of its own input; the test
allows cond x 1e-40. 50x50 inv takes 2.3 s (was 1.5 s) on the desktop.

## Round 2 (owner 2026-11-24: A, B, C first; D the table later)
Decided: A unit, currency and coin tags convert a list cell by cell; date
formats and time zones refuse a list. B median, stdev/stdevp (sample,
population), rank, norm, eig (values only); `var` became `variance` /
`variancep` because `var` is the parameter of the default
function(var)=var*cos(var) and builtin names are reserved. `.*` not done;
`**` is free today (an error), but in Python and JavaScript `**` is the
power, so `2**3` meaning 6 would mislead: not used. B4 (reverse, cumsum,
count) after this set. C all digits shown, with a note under the result
(`about k of 32 digits may be off`), k from Hadamard's bound / |det| minus
the guard digits. D table: cells shortened, at most 10x10 shown, lists
stay one line.

Built 2026-11-24: all of A, B, C. eig at most 20x20 (20x20 takes 1.1 s on
the desktop), sorted by real part, a pair a+bi / a-bi with +bi first.
SymPy cases for median, variance, stdev, norm, rank and eig (1653 in all).

Filling and B4 (owner 2026-11-24): fill(n, v), fill(r, c, v), fill(r, c, list)
(the list as every row); reshape(list, r, c) row by row; seq's step goes
toward the end whatever its sign, like a deck's [10,1,3]; reverse, cumsum
(lists), count(...) (numbers in numbers and lists). A deck's [1,100] range
cannot be written in the calculator: [1, 100] is a list of two, and
[1..100] / [1:100] clash with decimals and durations; seq is the way.

## Round 2 notes (before the owner's decisions)
- History and Variables as a table (the decisions: alignment, wide
  matrices, copy as text or tab-separated, click to insert).
- `eig`, `rank`, `norm`, `median`, `stdev`, `var`, element-wise `.*`?
- Conversions of a whole list (`[1, 2][m->ft]`) if not done in X1.

## Tests
Engine first (parser, values, every function, limits, errors, display
and parse-back), then UI session tests over pipes, web tests, help
examples; golden data must stay unchanged.
