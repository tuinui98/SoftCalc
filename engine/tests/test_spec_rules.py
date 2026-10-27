"""tests/test_spec_rules.py - SPEC rules that the golden data does not cover.

Extensions and fixes from spec/SPEC.md sections 5 and 6: precedence,
implicit multiplication, new literals and operators, formatting edges.
"""

import pytest

from softcalc_engine.engine import Engine


def calc(text, **settings):
    engine = Engine()
    for key, value in settings.items():
        setattr(engine.settings, key, value)
    return engine.evaluate(text)


@pytest.mark.parametrize("text, want", [
    # precedence (SPEC 5.4)
    ("2^3^2", "512"),
    ("-2^2", "-4"),
    ("(-2)^2", "4"),
    ("2^-1", "0.5"),
    ("2*-3", "-6"),
    ("2^3!", "64"),
    ("-3!", "-6"),
    # implicit multiplication [EXT E3]
    ("2pi", "6.283185307179586476925286766559"),
    ("2(3+1)", "8"),
    ("(1+1)(2+2)", "8"),
    ("1/2pi", "0.15915494309189533576888376337251"),
    ("2^3e", "285.00540816072728080589222192791"),    # 2^(3e)
    ("pi(2)", "6.283185307179586476925286766559"),
    ("2e", "5.4365636569180904707205749427053"),
    ("2e3", "2000"),
    ("2i*2j", "-4"),
    # literals [EXT E1]
    ("0o17", "15"),
    ("0d12", "12"),
    ("0b101+101b", "10"),
    ("0x1b", "27"),
    # operators [EXT E5]
    ("-7 mod 3", "2"),
    ("7.5 mod 2", "1.5"),
    ("50%", "0.5"),
    ("200*10%", "20"),
    ("0.5!", "0.88622692545275801364908374167057"),
    ("0!", "1"),
    # trig edges
    ("sin(1e-40)", "1e-40"),
    ("sin(pi)", "0"),
    ("cos(90)", "0"),
])
def test_values(text, want):
    angle = "deg" if text == "cos(90)" else "rad"
    got = calc(text, angle=angle)
    assert (got.kind, got.text) == ("result", want)


@pytest.mark.parametrize("text", [
    "--2", "1++2", "0x", "0b", "0o9", "2x", "a b", "sin 1",
    "(-3)!", "1 mod 0", "i mod 2", "7%2", "1.2.3", "tan(pi/2)",
    "   ", "$1", "ln(0)",
])
def test_errors(text):
    assert calc(text).kind == "error"


def test_empty_input_adds_nothing():
    assert calc("").kind == "empty"


def test_answer_survives_clear_history():
    engine = Engine()
    engine.evaluate("5+5")
    engine.clear_history()
    assert engine.evaluate("ans*3").text == "30"
    assert engine.evaluate("answer").text == "30"


def test_answer_before_any_result_is_error():
    assert calc("answer").kind == "error"
