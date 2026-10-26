# Menu insertion behavior (observed)

Source: `spec/golden/menu_insert.tsv`, from `tools/probe_menu_insert.ps1`.
124 Functions/Conversions commands, each posted twice: into an empty Input
and into an Input holding `1` (caret at end). Nothing was evaluated.

## Functions menu
- Operators (`+ - * / ^`): appended at the caret. `1` becomes `1+`.
- Named functions (sqrt, abs, trig, exp, log10, ln): **wrap the whole
  current Input** as the argument. Empty gives `sqrt()`, `1` gives
  `sqrt(1)`. To check where the caret lands after an empty insert.
- User functions: same wrapping, using the function name only
  (`g(x)` gives `g()` / `g(1)`, `function(var)` gives `function(1)`).

## Conversions menu
- Appends ` [from->to]` (leading space) after the current Input:
  `1` becomes `1 [in->ft]`.
- Conversion is a postfix bracket tag, not a function call.
- Unit tokens used:
  - Length: `in ft yd mile mm cm m km`
  - Mass: `lb oz g kg`
  - Time: `d min s`
  - Velocity: `mi/h m/s ft/h km/h knot`
  - Temperature: `c f k`
- Conversions only exist within a category (no length-to-mass entries).

## Questions this raises for later groups
- Is the space before `[` required? Does `1[in->ft]` parse?
- Can the tag follow an expression, `(1+2) [ft->in]`, or a variable?
- Can tags chain, `1 [ft->in] [in->cm]`?
- `g` is both the gram unit and the default user function `g(x)`: does
  `[g->kg]` still work while `g(x)` exists? (likely fine, the tag is
  separate syntax, to verify)
- Is the bracket tag case-sensitive (`[IN->FT]`)?
