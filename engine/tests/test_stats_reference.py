"""tests/test_stats_reference.py - The statistics functions against SymPy (owner 2026-11-26).

data/stats_reference.json comes from tools/make_stats_reference.py (random
values from a fixed seed): comb, perm, percentile and quartile exact,
normcdf and norminv worked out to 45 digits. The engine shows 32 digits,
so each answer must match to 28 digits of its size (at least 1, since a
probability near 0 is still read against 1: normcdf(a, b) takes one value
from another).
"""

import json
from pathlib import Path

import mpmath
import pytest

from softcalc_engine.engine import Engine

DATA = json.loads((Path(__file__).parent / "data" / "stats_reference.json").read_text(encoding="utf-8"))
TOLERANCE = mpmath.mpf("1e-28")


def wanted(text):
    if "/" in text:
        p, q = text.split("/")
        return mpmath.mpf(p) / mpmath.mpf(q)
    return mpmath.mpf(text)


@pytest.fixture(scope="module")
def engine():
    return Engine()


@pytest.mark.parametrize("case", DATA, ids=[f"{case['group']}-{n}" for n, case in enumerate(DATA)])
def test_against_sympy(engine, case):
    result = engine.evaluate(case["line"])
    if case["error"]:
        assert result.text.startswith("ERROR:" + case["error"]), (case["line"], result.text)
        return
    with mpmath.workdps(60):
        got = mpmath.mpf(result.text)
        want = wanted(case["value"])
        assert abs(got - want) <= TOLERANCE * max(1, abs(want)), (case["line"], result.text, case["value"])


def test_the_reference_covers_every_function():
    assert {case["group"] for case in DATA} == {"comb", "perm", "percentile", "quartile", "normcdf", "norminv"}
