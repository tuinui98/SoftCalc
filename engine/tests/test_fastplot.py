"""tests/test_fastplot.py - The float drawing path matches the mpmath one (owner 2026-11-09)."""

import math

import pytest

from softcalc_engine.engine import Engine
from softcalc_engine.functions import Functions

BODIES = [
    "x^2", "sin(x)*x", "cos(x)", "tan(x)", "sqrt(x)", "ln(x)", "log10(x)", "exp(x/3)", "abs(x)-2",
    "asin(x/5)", "acos(x/5)", "atan(x)", "sinh(x/2)", "cosh(x/2)", "tanh(x)", "asinh(x)", "acosh(x)",
    "atanh(x/5)", "1/x", "x mod 3", "(-8)^x", "x^0.5", "2^x", "0x10*x+0b11", "pi*x+e", "k*x", "q(x)+1",
]


def engine(angle="rad"):
    e = Engine()
    e.settings.angle = angle
    for line in ("k=2.5", "q(t)=t^3-t", "g(x)=x"):
        e.evaluate(line)
    return e


def slow_points(e, name, start, end, samples):
    """The mpmath path, with the float one switched off."""
    keep = e.fast_function
    e.fast_function = lambda *args: None
    try:
        return e.plot(name, "xy", start, end, samples)
    finally:
        e.fast_function = keep


def same(a, b):
    if a is None or b is None:
        return a is None and b is None
    return math.isclose(a[1], b[1], rel_tol=1e-9, abs_tol=1e-9) and math.isclose(a[0], b[0], rel_tol=1e-12, abs_tol=1e-12)


@pytest.mark.parametrize("angle", ["rad", "deg", "grad"])
@pytest.mark.parametrize("body", BODIES)
def test_float_path_matches_mpmath(angle, body):
    e = engine(angle)
    e.evaluate(f"f(x)={body}")
    builtins = Functions(e.settings.angle, e.settings.word_size)
    assert e.fast_function(e.functions["f"].body, ["x"], builtins) is not None    # really the float path
    fast = e.plot("f", "xy", -5.03, 5.07, 301)
    slow = slow_points(e, "f", -5.03, 5.07, 301)
    mismatches = [(f, s) for f, s in zip(fast, slow) if not same(f, s)]
    assert not mismatches, mismatches[:3]


@pytest.mark.parametrize("body", ["x!", "x%", "fv(5, 10, x)", "today+x", "(1+2i)*x", "twos(x, 8)", "x [ft->in]"])
def test_unsupported_keeps_mpmath(body):
    e = engine()
    e.evaluate(f"f(x)={body}")
    builtins = Functions(e.settings.angle, e.settings.word_size)
    assert e.fast_function(e.functions["f"].body, ["x"], builtins) is None


def test_polar_points_match():
    e = engine()
    e.evaluate("f(x)=1+cos(x)")
    fast = e.plot("f", "polar", -3, 3, 101)
    slow_keep = e.fast_function
    e.fast_function = lambda *args: None
    slow = e.plot("f", "polar", -3, 3, 101)
    e.fast_function = slow_keep
    assert all(math.isclose(a[0], b[0], abs_tol=1e-9) and math.isclose(a[1], b[1], abs_tol=1e-9) for a, b in zip(fast, slow))


def test_equations_match():
    e = engine()
    e.evaluate("r=3")
    for text in ("x^2+y^2=r^2", "y^2=x^3-x", "y=1/x", "sin(x)=y"):
        fast = e.plot_equation(text, -5, 5, -5, 5, 20, 20)
        keep = e.fast_function
        e.fast_function = lambda *args: None
        slow = e.plot_equation(text, -5, 5, -5, 5, 20, 20)
        e.fast_function = keep
        assert len(fast) == len(slow), text
        assert all(math.isclose(a, b, abs_tol=1e-9) for f, s in zip(fast, slow) for a, b in zip(f, s)), text
