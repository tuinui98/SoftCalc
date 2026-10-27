# Golden cases whose expected result changes in the clone

The golden files record the original. For these cases the clone must
produce the result below instead, because of a [FIX], [EXT] or [DEF]
item in `SPEC.md`. "error" means any `ERROR:` line. Settings are the
golden row's settings. Everything not listed must match the golden file.

| file | input | original | clone | reason |
|---|---|---|---|---|
| A_arith | `2^3^2` | 64 | 512 | FIX right-assoc `^` |
| A_arith | `-2^2` | 4 | -4 | FIX unary minus below `^` |
| A_arith | `3!` | error | 6 | EXT factorial |
| A_arith | `7 mod 2` | error | 1 | EXT mod |
| A_arith | `7%2` | error | error | `7%` then `2`: no implicit multiply after a number |
| B_literals | `0b101` | error | 5 | EXT prefix |
| B_literals | `1e` | 1 | 2.7182818284590452353602874713527 | DEF `e` needs digits; else constant |
| B_literals | `0x` | crash | error | FIX |
| B_literals | `2b` | error | error (b undefined) | EXT rule `2b` = `2*b` |
| D_complex | `j` | error | 1i | EXT `j` |
| F_logs | `exp(-1000)` | 1.017932519480705747411831723934e395 | 5.0759588975494567652918094795743e-435 | FIX exponent sign |
| G_consts | `ans`, `ans*2` | error | value of `answer` (depends on the previous case) | EXT alias |
| H_vars | `2zza` (zza=6) | error | 12 | EXT implicit multiplication |
| H_vars | `zzlong_name_1=1` | error | Variable Added | EXT C-style names |
| H_vars | `zzlong_name_1` | error | 1 | EXT |
| H_vars | `zz1=3` | error | Variable Added | EXT |
| I_funcs | `sin(x)=1` | Function Added | error | FIX reserved names |
| R_retest | `sin(x)=1`, `cos(x)=x` | Function Added | error | FIX |
| R_retest | `cos(5)` after `cos(x)=x` | 0.2836... | 0.2836... (unchanged) | built-in kept |
| N_errors | `=5` | 0 | error | FIX |
| J_conv | `1 [kg->lb]` | 2.2046224760379583079434311509628 | 2.2046226218487758072297380134503 | FIX exact pound |
| Q_answer | `ans` | error | 30 | EXT alias (answer is 30 there) |
| Q_answer | `$1` | error | 30 | EXT `$1` = latest result |
| O_keypad | all `MC` / `M+` / `M-` sequences | broken macros | per SPEC 7 | FIX memory keys |

Notes
- Golden runs clear History before every case unless the case file says
  `@keep on`; `$n` cases there see an empty History and stay errors.
- Mass factors derived from the exact pound also change `lb`/`oz`
  conversions beyond the one row listed; recompute expected values for
  every mass row from 1 lb = 0.45359237 kg, 1 oz = 1/16 lb.
