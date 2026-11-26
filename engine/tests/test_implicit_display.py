"""test_implicit_display.py - History brackets an implicit product holding a name
(owner 2026-11-26), so `1/2x` does not read as `1/2*3` once x shows its value.
"""

import pytest

from softcalc_engine.engine import Engine


def shown(line):
    engine = Engine()
    for setup in ("x=3", "y=-2", "f(t)=t+1"):
        engine.evaluate(setup)
    result = engine.evaluate(line)
    assert result.kind == "result", result.text
    return result.display_input


@pytest.mark.parametrize("line, want", [
    ("2/3x", "2/(3*3)"),
    ("2^3x", "2^(3*3)"),
    ("1/2x^2", "1/(2*3^2)"),
    ("2 mod 3x", "2 mod (3*3)"),
    ("2x+1", "(2*3)+1"),
    ("2/3x*2", "2/(3*3)*2"),
    ("2/3f(x)", "2/(3f(3))"),
    ("6/2(x)", "6/(2(3))"),
    ("1/2pi", "1/(2pi)"),
    ("2x^2y", "2*3^(2*(-2))"),
    ("1/2x [ft->in]", "1/(2*3) [ft->in]"),
])
def test_an_implicit_product_gets_brackets(line, want):
    assert shown(line) == want


@pytest.mark.parametrize("line, want", [
    ("2x", "2*3"),                  # the whole line
    ("-2x^2", "-2*3^2"),            # the whole line after a sign
    ("2x [ft->in]", "2*3 [ft->in]"),
    ("sin(2x)", "sin(2*3)"),        # brackets already hold it
    ("max(2x, 1)", "max(2*3, 1)"),
    ("[2x, 1]", "[2*3, 1]"),
    ("2*-3x", "2*(-3*3)"),          # a sign's brackets hold it
    ("2^-3x", "2^(-3*3)"),
    ("2(3)+1", "2(3)+1"),           # no name, no value goes in
    ("2/3*x", "2/3*3"),             # not an implicit product
])
def test_no_brackets_when_nothing_could_misread(line, want):
    assert shown(line) == want


def test_a_draw_in_a_product_is_bracketed_with_its_value():
    engine = Engine()
    engine.evaluate("rands(7)")
    result = engine.evaluate("3+2rand(1, 6)")
    drawn = int(result.text) - 3
    assert result.display_input == f"3+(2*{drawn // 2})"
