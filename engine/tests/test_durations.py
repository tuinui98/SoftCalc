"""tests/test_durations.py - Duration literals h:mm[:ss] and their overlaps.

Durations were added after M3; these tests also pin down that they do
not change earlier behavior: date-time literals, joined digit runs, RPN
spacing, conversions and the History display.
"""

import pytest

from softcalc_engine.engine import Engine


def run(*lines, **settings):
    engine = Engine()
    for key, value in settings.items():
        setattr(engine.settings, key, value)
    result = None
    for line in lines:
        result = engine.evaluate(line)
    return result


@pytest.mark.parametrize("line, want", [
    ("0:0:15", "0:00:15"),
    ("1:30", "1:30:00"),
    ("36:00:00", "36:00:00"),
    ("1:30 + 0:45", "2:15:00"),
    ("1:30 * 3", "4:30:00"),
    ("3*1:30", "4:30:00"),
    ("2(1:30)", "3:00:00"),
    ("1:30/4", "0:22:30"),
    ("1:30/0:45", "2"),
    ("-1:30", "-1:30:00"),
    ("1:30-2:00", "-0:30:00"),
    ("0:0:1.5", "0:00:01.5"),
    ("today+0:0:15", None),
    ("27.10.2026 14:05 - 1:30", "27.10.2026 12:35:00"),
    ("27.10.2026 + 25:00", "28.10.2026 01:00:00"),
    ("0:30 + 27.10.2026", "27.10.2026 00:30:00"),
])
def test_duration_values(line, want):
    result = run(line)
    assert result.kind == "result"
    if want is None:
        assert result.text.endswith(" 00:00:15")
    else:
        assert result.text == want


@pytest.mark.parametrize("line", [
    "1:75", "1:30:75", "1:30:", "1:30x", "1:30+1", "sin(1:30)", "1:30*1:30",
    "1:30 [min->s]", "2:00 - 27.10.2026", "1:30!", "1:30%", "2 1:30", "1:30 2",
])
def test_duration_errors(line):
    assert run(line).kind == "error"


#----------------------------------------------------------------------
#  Overlap with earlier features
#----------------------------------------------------------------------
@pytest.mark.parametrize("line, want", [
    ("27.10.2026 14:05", "27.10.2026 14:05:00"),          # still one date-time literal
    ("27.10.2026 14:05:09", "27.10.2026 14:05:09"),
    ("1 000 + 1", "1001"),                                 # digit runs still join
    ("28.11.2025 - 20.11.2025", "8"),                       # date - date still days
    ("1 [ft->in]", "12"),
])
def test_earlier_features_unchanged(line, want):
    assert run(line).text == want


def test_rpn_display_keeps_spaces():
    result = run("1 2 +", rpn=True)
    assert (result.text, result.display_input) == ("3", "1 2 +")


def test_rpn_display_substitutes_items():
    result = run("x=4", "x 2 *", rpn=True)
    assert (result.text, result.display_input) == ("8", "4 2 *")


@pytest.mark.parametrize("line, want", [
    ("1:30 2 *", "3:00:00"),
    ("27.10.2026 0:0:15 +", "27.10.2026 00:00:15"),
])
def test_rpn_durations(line, want):
    assert run(line, rpn=True).text == want


def test_duration_variable_display_and_pcf():
    engine = Engine(load_defaults=False)
    engine.evaluate("d=1:30")
    assert engine.evaluate("d*2").display_input == "1:30:00*2"
    text = engine.export_pcf()
    assert text == "d=1:30:00\r\n"
    other = Engine(load_defaults=False)
    assert other.import_pcf(text) == []
    assert other.evaluate("d+0:30").text == "2:00:00"
