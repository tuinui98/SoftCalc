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
