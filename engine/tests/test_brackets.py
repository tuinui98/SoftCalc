"""tests/test_brackets.py - What `[ ]` does before matrices come in (design/matrix_plan.md X0, owner 2026-11-24).

Matrices will be written `[1, 2]`. These lines pin every use of `[ ]` that
works today, so the list brackets cannot change any of them: conversions
and formats as typed (spaces, case), rai-ngan-wa, a tag in the middle of a
line, the Conversions menu's inserts, and the lines that are errors now.
"""

import pytest

from softcalc_engine.engine import Engine
from softcalc_engine.server import Dispatcher


@pytest.mark.parametrize("line, text", [
    ("1 [ft->in]", "12"),
    ("1[ft->in]", "12"),
    ("1 [ ft -> in ]", "12"),
    ("1 [FT->IN]", "12"),
    ("1 [ft->in]+1", "13"),
    ("2*3 [ft->in]", "72"),
    ("(1+2) [ft->in]", "36"),
    ("1 [ft->in] [in->ft]", "1"),
    ("100 [c->f]", "212"),
    ("1 [mi/h->km/h]", "1.609344"),
    ("1 [km/h -> m/s]*3.6", "1"),
    ("4-1-50 [rnw]", "4.375"),
    ("4-1-50 [RNW]", "4.375"),
    ("4-1-50 [rai-ngan-wa]", "4.375"),
    ("4-1-50[rnw]+0-2-400[rnw]", "5.875"),
    ("1 [rai->rnw]", "1"),
    ("1600 [m2->rnw]", "1"),
    ("x=2", "Variable Added"),
    ("x [ft->in]", "24"),
    ("f(t)=t [ft->in]", "Function Added"),
    ("f(1)", "12"),
    ("$1 [in->ft]", "1"),
    ("5! [s->min]", "2"),
])
def test_tags_that_work_today_keep_working(line, text):
    engine = Engine()
    engine.evaluate("x=2")
    engine.evaluate("f(t)=t [ft->in]")
    engine.evaluate("12")
    assert engine.evaluate(line).text == text


@pytest.mark.parametrize("line", ["now [epoch]", "now [EPOCH]", "0 [fromepoch]", "now [longdate]", "now [LongDateTime]", "now [longtime]"])
def test_format_tags_in_any_case(line):
    assert not Engine().evaluate(line).text.startswith("ERROR")


@pytest.mark.parametrize("line", [
    "5 [nope]", "1 [in-cm]", "1 []", "5 [m]", "1 [in->cm", "1 [in->s]", "[m->ft]", "[1,2]", "x=[1]",
    "5[ft->in]2", "1 [usd]",
])
def test_lines_that_are_errors_today(line):
    # [1,2] and x=[1] become lists with X1; the others must stay errors
    assert Engine().evaluate(line).text.startswith("ERROR:")


def test_conversions_menu_inserts_tags_after_the_value():
    d = Dispatcher()
    unit = d.handle({"op": "menu_insert", "kind": "conversion", "text": "5", "caret": 1, "source": "ft", "target": "in"})
    fmt = d.handle({"op": "menu_insert", "kind": "format", "text": "now", "caret": 3, "name": "longdate"})
    assert unit["ok"] and "[" in unit["text"] and unit["text"].startswith("5")
    assert fmt["ok"] and fmt["text"] == "now [longdate]"
