"""tests/test_equations.py - Equations in x and y: implicit plot and trace solve (design/graph2_plan.md)."""

import math

import pytest

from softcalc_engine.engine import Engine
from softcalc_engine.errors import CalcError
from softcalc_engine.server import Dispatcher


def engine_with(*lines):
    engine = Engine()
    for line in lines:
        engine.evaluate(line)
    return engine


def test_circle_segments_lie_on_the_circle():
    engine = engine_with("r=3")
    segments = engine.plot_equation("x^2+y^2=r^2", -5, 5, -5, 5, 20, 20)
    assert len(segments) > 50
    for x1, y1, x2, y2 in segments:
        assert abs(math.hypot(x1, y1) - 3) < 0.02
        assert abs(math.hypot(x2, y2) - 3) < 0.02
    xs = [s[0] for s in segments]
    assert min(xs) < -2.9 and max(xs) > 2.9    # all the way round


def test_pole_is_not_drawn_as_a_line():
    segments = engine_with().plot_equation("y=1/x", -4, 4, -4, 4, 16, 16)
    assert segments
    for x1, y1, x2, y2 in segments:
        assert (x1 < 0) == (x2 < 0) or abs(x1) > 0.01 and abs(x2) > 0.01
        assert abs(y2 - y1) < 2    # no vertical jump across x = 0


def test_branches_and_lines_without_y():
    engine = engine_with()
    assert all(abs(s[0] - 2) < 1e-9 and abs(s[2] - 2) < 1e-9 for s in engine.plot_equation("x=2", -4, 4, -4, 4, 8, 8))
    hyperbola = engine.plot_equation("x^2-y^2=1", -4, 4, -4, 4, 16, 16)
    assert any(s[0] > 0 for s in hyperbola) and any(s[0] < 0 for s in hyperbola)


def test_undefined_parts_are_skipped():
    segments = engine_with().plot_equation("y=sqrt(x)", -4, 4, -4, 4, 16, 16)
    assert segments and all(s[0] >= -1e-9 and s[2] >= -1e-9 for s in segments)


@pytest.mark.parametrize("text, message", [
    ("x+1", "one '='"),
    ("x=y=1", "one '='"),
    ("1=2", "x or y"),
    ("x=", "both sides"),
    ("q=x", "q"),
])
def test_bad_equations(text, message):
    with pytest.raises(CalcError, match=message):
        engine_with().plot_equation(text, -1, 1, -1, 1, 4, 4)


def test_axes_win_over_user_variables():
    engine = engine_with("x=100", "y=100")
    segments = engine.plot_equation("y=x", -2, 2, -2, 2, 8, 8)
    assert segments and all(abs(s[0] - s[1]) < 1e-9 for s in segments)


def test_trace_solves_at_full_precision():
    engine = engine_with("r=3")
    x_text, y_text, x, y = engine.equation_at("x^2+y^2=r^2", "x", "1.5", 2.6, 0.5)
    assert (x_text, y_text) == ("1.5", "2.5980762113533159402911695122588")
    assert engine.equation_at("x^2+y^2=r^2", "y", "0", -2.9, 0.5)[0] == "-3"    # the left side
    assert engine.equation_at("x^2+y^2=r^2", "x", "5", 2.6, 0.5) is None    # no point there
    assert engine.equation_at("x^2+y^2=r^2", "x", "1.5", 1.0, 0.2) is None    # root too far from the guess
    with pytest.raises(CalcError):
        engine.equation_at("x^2+y^2=r^2", "x", "abc", 1.0, 0.2)


def test_protocol_ops():
    dispatcher = Dispatcher()
    dispatcher.handle({"op": "eval", "input": "r=2"})
    reply = dispatcher.handle({"op": "plot_equation", "equation": "x^2+y^2=r^2", "x_min": -3, "x_max": 3,
                               "y_min": -3, "y_max": 3, "cols": 12, "rows": 12})
    assert reply["ok"] and len(reply["segments"][0]) == 4
    bad = dispatcher.handle({"op": "plot_equation", "equation": "x+1", "x_min": -3, "x_max": 3, "y_min": -3, "y_max": 3})
    assert bad["ok"] is False and "'='" in bad["error"]
    at = dispatcher.handle({"op": "equation_at", "equation": "x^2+y^2=r^2", "axis": "y", "value": "0", "guess": 2.1, "window": 0.5})
    assert at["ok"] and at["x"] == "2" and at["y_value"] == 0.0
    none = dispatcher.handle({"op": "equation_at", "equation": "x^2+y^2=r^2", "axis": "x", "value": "9", "guess": 1, "window": 0.5})
    assert none["ok"] and none["x"] is None
    assert dispatcher.handle({"op": "equation_at", "equation": "x=1", "axis": "z", "value": "0", "guess": 0, "window": 1})["ok"] is False


def test_value_at_takes_the_shown_decimal_text():
    engine = engine_with("sq(x)=x^2")
    assert engine.value_at("sq", "1.43")[0] == "2.0449"
    assert engine.value_at("sq", 1.43)[0] != "2.0449"    # a float is the nearest double, as before
    dispatcher = Dispatcher()
    dispatcher.handle({"op": "eval", "input": "sq(x)=x^2"})
    assert dispatcher.handle({"op": "value_at", "function": "sq", "x": "1.43"})["text"] == "2.0449"
    assert dispatcher.handle({"op": "value_at", "function": "sq", "x": "abc"})["ok"] is False
