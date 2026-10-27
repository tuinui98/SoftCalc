"""tests/test_engine_golden.py - Run the engine against the golden data.

Each golden group runs in its own Engine, in file order. The recording
cleared History before every case, except in KEEP_GROUPS, so the result
stack is cleared the same way. Groups listed in GROUPS are the ones the
current milestone must pass.
"""

import pytest

from expected_diffs import ERROR, OVERRIDES, normalize_original
from golden import load_group
from softcalc_engine.engine import Engine

GROUPS = [
    "A_arith", "B_literals", "C_base", "D_complex", "E_trig", "F_logs",
    "G_consts", "H_vars", "I_funcs", "J_conv", "L_precision", "M_rpn",
    "N_errors", "Q_answer", "R_retest", "S_format", "T_bases",
]
KEEP_GROUPS = {"Q_answer"}
# U_repeat (Enter on empty Input repeats the last line) is a UI rule,
# tested in ui/SoftCalc.Tests/InputRulesTests.cs, so it is not listed in GROUPS.


def run_group(group):
    """Yield (case, EvalResult) for every case of the group."""
    engine = Engine()
    for case in load_group(group):
        engine.settings.precision = int(case.prec)
        engine.settings.angle = case.angle
        engine.settings.base = case.base
        engine.settings.rpn = case.rpn == "on"
        if group not in KEEP_GROUPS:
            engine.clear_history()
        yield case, engine.evaluate(case.input)


def expected_for(case):
    key = (case.group, case.input, case.angle)
    if key in OVERRIDES:
        return OVERRIDES[key]
    if case.is_error:
        return ERROR
    if case.result.startswith("<no history change"):
        return None
    return normalize_original(case.result)


@pytest.mark.parametrize("group", GROUPS)
def test_group(group):
    failures = []
    for case, got in run_group(group):
        want = expected_for(case)
        if want is ERROR:
            ok = got.kind == "error"
        elif want is None:
            ok = got.kind == "empty"
        else:
            ok = got.kind in ("result", "definition") and got.text == want
        if not ok:
            want_text = "ERROR" if want is ERROR else want
            failures.append(f"line {case.line_no}: {case.input!r} [{case.prec} {case.angle} {case.base}]"
                            f"\n    want {want_text!r}\n    got  {got.kind} {got.text!r}")
    assert not failures, f"{len(failures)} mismatches:\n" + "\n".join(failures)
