"""tests/test_time_units.py - Extra time units and UTC/local conversion tags.

Year/month/week/hour units with long-name aliases, and `[utc->local]`,
`[local->utc]` on dates, without changing the original's unit tags.
"""

import pytest

from softcalc_engine.dates import local_offset
from softcalc_engine.engine import Engine


def run(*lines):
    engine = Engine()
    result = None
    for line in lines:
        result = engine.evaluate(line)
    return result


@pytest.mark.parametrize("line, want", [
    ("1 [yr->d]", "365"),
    ("1 [mo->d]", "30"),
    ("1 [yr->mo]", "12.166666666666666666666666666667"),
    ("1 [wk->d]", "7"),
    ("1 [wk->h]", "168"),
    ("90 [min->h]", "1.5"),
    ("1 [h->s]", "3600"),
    ("2 [hours->minutes]", "120"),
    ("1 [year->days]", "365"),
    ("3 [weeks->day]", "21"),
    ("1 [day->sec]", "86400"),
    ("1 [HOUR->MIN]", "60"),
    # original units unchanged
    ("1 [d->s]", "86400"),
    ("1 [d->min]", "1440"),
    ("1 [mi/h->km/h]", "1.609344"),
    ("1 [m->mm]", "1000"),
])
def test_time_units(line, want):
    assert run(line).text == want


@pytest.mark.parametrize("line", ["1 [h->m]", "1 [yr->kg]", "1 [days->]", "1 [mon->d]"])
def test_time_unit_errors(line):
    assert run(line).kind == "error"


def test_alias_names_stay_free_outside_tags():
    assert run("day=3", "hours=2", "day*hours").text == "6"


def offset_hours():
    return local_offset().total_seconds() / 3600


def test_utc_to_local_and_back():
    there = run("27.10.2026 12:00 [utc->local]")
    back = run("27.10.2026 12:00 [utc->local] [local->utc]")
    assert there.kind == "result"
    assert back.text == "27.10.2026 12:00:00"
    if offset_hours() == 7:
        assert there.text == "27.10.2026 19:00:00"


def test_zone_tag_on_epoch_result():
    assert run("fromepoch(0) [local->utc]").text == "01.01.1970 00:00:00"


@pytest.mark.parametrize("line", ["5 [utc->local]", "1:30 [utc->local]", "27.10.2026 [utc->d]", "27.10.2026 [gmt->local]"])
def test_zone_errors(line):
    assert run(line).kind == "error"
