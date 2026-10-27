"""tests/expected_diffs.py - Golden rows the clone answers differently on purpose.

Machine-readable form of spec/golden_expected_diffs.md. Keys are
(group, input, angle); ERROR means any error line. Rows not listed must
match the original, after normalize_original() applies the global
format fix (no dangling dot: `1.e100` -> `1e100`, `11111111.b` ->
`11111111b`, hex `0x1.*0x10^0x20` ->
`0x1*0x10^0x20`).
"""

import re

ERROR = object()

OVERRIDES = {
    ("A_arith", "2^3^2", "rad"): "512",
    ("A_arith", "-2^2", "rad"): "-4",
    ("A_arith", "3!", "rad"): "6",
    ("A_arith", "7 mod 2", "rad"): "1",
    ("A_arith", "7%2", "rad"): ERROR,
    ("B_literals", "0b101", "rad"): "5",
    ("B_literals", "1e", "rad"): "2.7182818284590452353602874713527",
    ("B_literals", "0x", "rad"): ERROR,
    ("B_literals", "2b", "rad"): ERROR,
    ("D_complex", "j", "rad"): "1i",
    ("E_trig", "sin(90)", "rad"): "0.89399666360055789051826949840421",
    ("F_logs", "exp(-1000)", "rad"): "5.0759588975494567652918094795743e-435",
    ("G_consts", "ans", "rad"): "3.1415926535897932384626433832795",
    ("G_consts", "ans*2", "rad"): "6.283185307179586476925286766559",
    ("N_errors", "=5", "rad"): ERROR,
    ("S_format", "-2^200", "rad"): "-1.6069380442589902755419620923412e60",
    ("H_vars", "2zza", "rad"): "12",
    ("H_vars", "zzlong_name_1=1", "rad"): "Variable Added",
    ("H_vars", "zzlong_name_1", "rad"): "1",
    ("H_vars", "zz1=3", "rad"): "Variable Added",
    ("I_funcs", "sin(x)=1", "rad"): ERROR,
    ("R_retest", "sin(x)=1", "rad"): ERROR,
    ("R_retest", "cos(x)=x", "rad"): ERROR,
    ("Q_answer", "ans", "rad"): "30",
    ("Q_answer", "$1", "rad"): "30",
    ("J_conv", "1 [kg->lb]", "rad"): "2.2046226218487758072297380134503",
    ("T_bases", "-2^200", "rad"): "-0x1*0x10^0x32",
}

DANGLING_DOT = re.compile(r"([0-9A-F])\.(?=[eb*])")


def normalize_original(text):
    """Apply SPEC 6.2 [FIX]: drop a dot that has no fraction digits."""
    return DANGLING_DOT.sub(r"\1", text)
