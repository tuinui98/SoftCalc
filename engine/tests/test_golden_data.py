"""tests/test_golden_data.py - Sanity checks on the recorded golden data.

Runs before any engine code exists (M0): makes sure every golden group
loads and that known reference rows read back as expected, so later
engine tests can trust the loader.
"""

import pytest

from golden import EVAL_GROUPS, load_group


@pytest.mark.parametrize("group", EVAL_GROUPS)
def test_group_loads(group):
    cases = load_group(group)
    assert cases, f"{group} has no cases"


def test_reference_rows():
    rows = {c.input: c for c in load_group("A_arith")}
    assert rows["1+2*3"].result == "7"
    assert rows["2^3^2"].result == "64"
    assert rows["7/0"].is_error


def test_crash_row_is_error():
    rows = {c.input: c for c in load_group("B_literals")}
    assert rows["0x"].is_error
