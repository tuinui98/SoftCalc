"""tests/test_signs.py - Two signs in a row (owner 2026-11-26).

`2+-3` reads as 2+(-3), `--2` as 2, `2*-3` as 2*(-3): two operators in a
row at most, so `2*--3` is an error too; the hint line says how a doubled
sign reads.
"""

import pytest

from softcalc_engine import hints
from softcalc_engine.engine import Engine


@pytest.mark.parametrize("line, want", [
    ("2++3", "5"), ("2+-3", "-1"), ("2-+3", "-1"), ("2--3", "5"),
    ("--2", "2"), ("+-2", "-2"), ("2- -3", "5"),
    ("2*-3", "-6"), ("2/+4", "0.5"), ("2^-1", "0.5"), ("5 mod -3", "-1"),
    ("2--3^2", "11"), ("-2^2", "-4"),
])
def test_two_signs_read_as_a_sign_on_the_next_value(line, want):
    assert Engine().evaluate(line).text == want


@pytest.mark.parametrize("line", ["2---3", "---2", "2+-+3", "2*--3", "2/-+4", "2^--2", "5 mod --3"])
def test_three_operators_are_an_error(line):
    """Owner 2026-11-26: a doubled sign after * / ^ makes no sense when writing a sum."""
    assert Engine().evaluate(line).text == "ERROR:Three operators in a row: two at most, as 2+-3 or 2*-3"


def test_a_variable_holding_a_negative_still_shows_in_brackets():
    engine = Engine()
    engine.evaluate("x=-3")
    result = engine.evaluate("5--x")
    assert (result.text, result.display_input) == ("2", "5--(-3)")


@pytest.mark.parametrize("text, warned", [
    ("2+-3", True), ("2--", True), ("2- -3", True), ("sqrt(2+-", True),
    ("2*-3", False), ("2e-3", False), ("2+3", False), ("2026-11-26", False),
])
def test_the_hint_line_warns_of_two_signs(text, warned):
    hint = hints.hint_for(text, len(text), lambda name: None) or ""
    assert hint.startswith(hints.TWO_SIGNS_HINT) == warned
