"""tests/test_area.py - Area conversions and rai-ngan-wa (owner 2026-11-08, TODO section 7)."""

import pytest

from softcalc_engine.engine import Engine
from softcalc_engine.server import Dispatcher
from softcalc_engine.units import MENU


def run(*lines, **settings):
    engine = Engine()
    for key, value in settings.items():
        setattr(engine.settings, key, value)
    result = None
    for line in lines:
        result = engine.evaluate(line)
    return result


@pytest.mark.parametrize("line, want", [
    ("1 [m2->cm2]", "10000"),
    ("1 [km2->ha]", "100"),
    ("1 [acre->m2]", "4046.8564224"),
    ("1 [mi2->acre]", "640"),
    ("1 [yd2->ft2]", "9"),
    ("1 [ft2->in2]", "144"),
    ("3 [rai->m2]", "4800"),
    ("1 [rai->ngan]", "4"),
    ("1 [ngan->wa2]", "100"),
    ("1 [acre->rai]", "2.529285264"),
    ("1 [sqft->m^2]", "0.09290304"),     # aliases
    ("1 [hectare->acres]", "2.4710538146716534224824392919881"),
    ("1 [sqwa->m2]", "4"),
    ("1 [rai-ngan-wa->m2]", "1600"),
])
def test_area_values(line, want):
    assert run(line).text == want


def test_area_does_not_mix_with_length():
    assert run("7 [m2->ft]").text == "ERROR:Cannot convert m2 to ft"


@pytest.mark.parametrize("line, text, display", [
    ("6800 [m2->rnw]", "4.25", "6800 [m2->rnw] = 4-1-0"),
    ("6801 [m2->rnw]", "4.250625", "6801 [m2->rnw] = 4-1-0.25"),
    ("4.25 [rnw->m2]", "6800", "4.25 [rnw->m2]"),          # leaving rnw: no rnw text
    ("4-1-50 [rnw]", "4.375", "4-1-50 [rnw] = 4-1-50"),
    ("4-1-50[rnw]+0-2-400[rnw]", "5.875", "4-1-50[rnw]+0-2-400[rnw] = 5-3-50"),
    ("4-1-50 [rnw->m2]", "7000", "4-1-50 [rnw->m2]"),
    ("4-1-50[rnw] / 0-2-0[rnw]", "8.75", "4-1-50[rnw] / 0-2-0[rnw] = 8-3-0"),    # simple rule: a ratio too
    ("-4-1-0 [rnw]", "-4.25", "-4-1-0 [rnw] = -4-1-0"),
    ("4-1-50 [rai-ngan-wa]", "4.375", "4-1-50 [rai-ngan-wa] = 4-1-50"),
])
def test_rai_ngan_wa(line, text, display):
    result = run(line)
    assert (result.text, result.display_input) == (text, display)


def test_group_needs_its_tag_right_after():
    assert run("4-1-50 + 0-2-400 [rnw]").text == "-45.5"    # the first group is subtraction
    assert run("10-4-1-50 [rnw]").text == "5.625"            # 10 minus 4-1-50
    assert run("4-1-50").text == "-47"


def test_wa_rounds_by_decimals_and_carries():
    assert run("6799.99 [m2->rnw]", decimals=2).display_input.endswith("= 4-1-0")    # 99.9975 wa -> 100: a ngan
    assert run("6802 [m2->rnw]", decimals=2, pad_zeros=True).display_input.endswith("= 4-1-0.50")


def test_rnw_text_is_decimal_in_hex_output():
    result = run("6800 [m2->rnw]", base="hex")
    assert result.display_input.endswith("= 4-1-0")


def test_hint_and_menu():
    assert Engine().literal_hint("4-1-50") == "4.375 rai"
    assert MENU["Area"] == ["mm2", "cm2", "m2", "ha", "km2", "in2", "ft2", "yd2", "acre", "mi2", "wa2", "ngan", "rai", "rnw"]
    catalog = Dispatcher().handle({"op": "catalog"})
    assert "rai" in catalog["conversions"]["Area"]
