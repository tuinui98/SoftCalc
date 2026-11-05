"""tests/test_currency.py - Currency conversion tags with rates from the UI (E8, M8)."""

import time

import pytest

from softcalc_engine.engine import Engine
from softcalc_engine.server import Dispatcher

RATES = {"USD": "1.25", "THB": "40", "JPY": "160"}
LATER = time.time() + 3600


def engine_with_rates(stale=False, next_check=LATER):
    engine = Engine()
    engine.rates.load("2026-11-05", RATES, stale, next_check)
    return engine


@pytest.mark.parametrize("line, want", [
    ("100 [usd->thb]", "3200"),
    ("100 [USD->THB]", "3200"),
    ("100 [ Usd -> thb ]", "3200"),
    ("1 [eur->usd]", "1.25"),
    ("3200 [thb->eur]", "80"),
    ("100 [usd->thb] [thb->jpy]", "12800"),
    ("100 [usd->usd]", "100"),
    ("1 [usd->thb]+1", "33"),
])
def test_conversions(line, want):
    result = engine_with_rates().evaluate(line)
    assert (result.kind, result.text) == ("result", want)


def test_full_precision():
    # Owner 2026-11-06: no automatic rounding to cents
    result = engine_with_rates().evaluate("1 [thb->usd]")
    assert result.text == "0.03125"


def test_every_currency_line_has_a_note():
    engine = engine_with_rates()
    assert engine.evaluate("100 [usd->thb]").note == "ECB 2026-11-05"
    assert engine.evaluate("1+1").note == ""
    assert engine.evaluate("x = 5 [usd->thb]").note == "ECB 2026-11-05"


def test_stale_rates_are_marked_offline():
    result = engine_with_rates(stale=True).evaluate("1 [usd->thb]")
    assert result.note == "ECB 2026-11-05, offline"


def test_no_rates_asks_for_them():
    engine = Engine()
    result = engine.evaluate("100 [usd->thb]")
    assert result.kind == "rates_needed"
    assert engine.results == [] and engine.answer is None


def test_due_rates_ask_again():
    engine = engine_with_rates(next_check=time.time() - 1)
    assert engine.evaluate("100 [usd->thb]").kind == "rates_needed"


def test_no_rates_and_not_due_is_an_error():
    engine = Engine()
    engine.rates.load(None, {}, True, LATER)
    result = engine.evaluate("100 [usd->thb]")
    assert result.text == "ERROR:Currency rates unavailable"


def test_known_code_without_a_rate():
    result = engine_with_rates().evaluate("1 [usd->gbp]")
    assert result.text == "ERROR:No ECB rate for GBP"


def test_units_are_unchanged():
    engine = engine_with_rates()
    assert engine.evaluate("1 [ft->in]").text == "12"
    assert engine.evaluate("1 [ft->in]").note == ""
    assert engine.evaluate("1 [usd->kg]").text == "ERROR:Cannot convert usd to kg"
    assert engine.evaluate("1 [kg->usd]").text == "ERROR:Cannot convert kg to usd"


def test_unknown_code_beside_a_currency():
    assert engine_with_rates().evaluate("1 [usd->bgn]").text == "ERROR:No ECB rate for BGN"
    assert engine_with_rates().evaluate("1 [xyz->usd]").text == "ERROR:No ECB rate for XYZ"


def test_functions_see_the_rates():
    engine = engine_with_rates()
    engine.evaluate("thb(x) = x [usd->thb]")
    assert engine.evaluate("thb(2)").text == "64"


def test_complex_values_are_refused():
    assert engine_with_rates().evaluate("(1+i) [usd->thb]").kind == "error"


#----------------------------------------------------------------------
#  Protocol
#----------------------------------------------------------------------
def ask(dispatcher, op, **arguments):
    return dispatcher.handle({"id": 3, "op": op, **arguments})


def test_set_rates_then_eval():
    dispatcher = Dispatcher()
    first = ask(dispatcher, "eval", input="2 [usd->thb]")
    assert first["kind"] == "rates_needed"
    assert ask(dispatcher, "set_rates", date="2026-11-05", rates=RATES, next_check=LATER)["ok"]
    reply = ask(dispatcher, "eval", input="2 [usd->thb]")
    assert (reply["kind"], reply["text"], reply["note"]) == ("result", "64", "ECB 2026-11-05")


@pytest.mark.parametrize("arguments", [
    {"date": "2026-11-05", "rates": {"USD": "x"}, "next_check": 0},
    {"date": "2026-11-05", "rates": {"USD": -1}, "next_check": 0},
    {"date": "2026-11-05", "rates": {"USDX": 1}, "next_check": 0},
    {"rates": {"USD": 1}, "next_check": 0},
    {"date": "2026-11-05", "rates": {}},
])
def test_set_rates_rejects_bad_requests(arguments):
    assert ask(Dispatcher(), "set_rates", **arguments)["ok"] is False


def test_catalog_lists_currencies():
    reply = ask(Dispatcher(), "catalog")
    assert reply["conversions"]["Currency"][0] == "THB"
    assert len(reply["conversions"]["Currency"]) == 10
    assert "EUR" in reply["currencies"]


def test_menu_inserts_tag_halves():
    dispatcher = Dispatcher()
    first = ask(dispatcher, "menu_insert", kind="currency_from", code="THB", text="100")
    assert first["text"] == "100 [THB"
    second = ask(dispatcher, "menu_insert", kind="currency_to", code="USD", text=first["text"])
    assert second["text"] == "100 [THB->USD]"
