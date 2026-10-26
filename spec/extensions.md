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
  inserts `answer` automatically (reported by the user; to capture in
  Phase 2). Keep that.
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
