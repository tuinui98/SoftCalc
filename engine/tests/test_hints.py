"""tests/test_hints.py - The hint line under the Input box (protocol `hint`).

Ported from the desktop's InputHelpTests.cs when the rules moved into the
engine (owner 2026-11-18), plus the user names and literals the engine
now looks up itself.
"""

import pytest

from softcalc_engine import hints
from softcalc_engine.server import Dispatcher


def user(name):
    return {"f": "f(x)=x^2 - your function", "rate": "rate = 0.05 - your variable"}.get(name.lower())


def hint(text, caret=None):
    return hints.hint_for(text, len(text) if caret is None else caret, user)


def test_open_call_describes_its_function():
    assert hint("fv(0.05/12, 120, ").startswith("fv(rate, nper, pmt")
    assert hint("1+sqrt(2*").startswith("sqrt(x)")


def test_innermost_open_call_wins():
    assert hint("fv(ln(2").startswith("ln(x)")
    assert hint("fv(ln(2), ").startswith("fv(")


def test_typed_word_is_described_or_completed():
    assert hint("sqrt").startswith("sqrt(x)")
    assert hint("long") == "longdate  longdatetime  longtime"
    assert hint("fro").startswith("fromepoch(")
    assert hint("q") is None    # one letter: too early to list names


def test_user_names_come_first():
    assert hint("f(3") == "f(x)=x^2 - your function"
    assert hint("100*Rate") == "rate = 0.05 - your variable"


def test_tags_and_references():
    assert hint("100 [usd-").startswith("[from->to]")
    assert hint("100 [usd->thb] + 1") is None
    assert hint("2*$").startswith("$1")
    assert hint("$12").startswith("$1")


def test_caret_decides():
    assert hint("sqrt(2)+ln(3)", 5).startswith("sqrt(x)")
    assert hint("1+2", 3) is None
    assert hint("") is None
    assert hint("sqrt", 99).startswith("sqrt(x)")    # a caret past the end is clamped


@pytest.mark.parametrize("text, want", [
    ("0x1F", "0x1F"),
    ("2*101b", "101b"),
    ("sqrt(0.5", "0.5"),
    ("1e3", "1e3"),
    ("0o17", "0o17"),
    ("x12", None),               # a name
    ("$12", None),               # a result reference
    ("27.10.2026", None),        # a date
    ("1:30", None),              # a duration
    ("2+", None),
    ("4-1-50 [rnw", "4-1-50"),   # rai-ngan-wa group
    ("2*4-1-50 [rnw->m2]", "4-1-50"),
    ("10-4-1-50 [RNW]", "4-1-50"),    # the three before the tag, as the engine reads it
    ("4-1-50", "50"),            # no tag: just the last number
])
def test_literal_at_caret(text, want):
    assert hints.literal_at(text, len(text)) == want


#------------------------------------------------------------------
#  Through the protocol: the engine's own names and the output base
#------------------------------------------------------------------
def ask(dispatcher, text, caret=None):
    answer = dispatcher.handle({"id": 1, "op": "hint", "text": text, "caret": len(text) if caret is None else caret})
    assert answer["ok"], answer
    return answer["text"]


def test_hint_op_uses_user_definitions():
    dispatcher = Dispatcher()
    dispatcher.handle({"op": "eval", "input": "Rate=0.05"})
    dispatcher.handle({"op": "eval", "input": "g(x, y)=x*y"})
    assert ask(dispatcher, "100*rate") == "Rate = 0.05 - your variable"
    assert ask(dispatcher, "g(2, ") == "g(x, y)=x*y - your function"
    assert ask(dispatcher, "sq") == hints.BUILTINS["sqrt"]


def test_hint_op_shows_a_literal_in_the_output_base():
    dispatcher = Dispatcher()
    assert ask(dispatcher, "sqrt(0x1F") == "0x1F = 31"
    assert ask(dispatcher, "sqrt(12").startswith("sqrt(x)")    # already decimal: the plain hint
    assert ask(dispatcher, "4-1-50 [rnw") == "4-1-50 = 4.375 rai"
    assert ask(dispatcher, "1+2") is None


def test_hint_op_needs_text_and_caret():
    answer = Dispatcher().handle({"id": 1, "op": "hint", "text": "sq"})
    assert not answer["ok"]
