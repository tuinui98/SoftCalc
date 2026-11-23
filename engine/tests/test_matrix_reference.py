"""tests/test_matrix_reference.py - The matrix functions against SymPy's exact answers (owner 2026-11-24).

data/matrix_reference.json comes from tools/make_matrix_reference.py
(random matrices from a fixed seed, answered by SymPy). Each cell the
engine shows must match to 28 significant digits of the answer's size;
the engine works at 32 digits plus guard digits, so this leaves room
only for the last shown digit and rounding inside long sums.

An ill-conditioned case carries its condition number `cond`: the input
itself is rounded to the working digits (1.00000000000000000001 is not
exact in binary), and the matrix grows that rounding up to cond times,
so there the answer may be off by cond x 1e-40 of its size. That much,
and no more, is the price of the input, not of the engine's working.
"""

import json
import re
from pathlib import Path

import mpmath
import pytest

from softcalc_engine.engine import Engine

DATA = json.loads((Path(__file__).parent / "data" / "matrix_reference.json").read_text(encoding="utf-8"))
TOLERANCE = mpmath.mpf("1e-28")
CELL = re.compile(r"\([^()]*\)|[^\[\],\s]+")


def number(text):
    """One cell as shown: `3`, `-2.5`, `1.5e-7`, `(16-4i)`, `2i`."""
    text = text.strip("()")
    if not text.endswith("i"):
        return mpmath.mpc(mpmath.mpf(text))
    body = text[:-1]
    split = max((k for k in range(1, len(body)) if body[k] in "+-" and body[k - 1] not in "eE"), default=0)
    real, imag = body[:split] or "0", body[split:]
    imag = {"": "1", "+": "1", "-": "-1"}.get(imag, imag)
    return mpmath.mpc(mpmath.mpf(real), mpmath.mpf(imag))


def shown(text):
    """The engine's text as rows of numbers, and whether it is a list."""
    if not text.startswith("["):
        return [[number(text)]], None
    flat = not text.startswith("[[")
    rows = [[number(cell) for cell in CELL.findall(row)] for row in re.findall(r"\[([^\[\]]*)\]", text)]
    return rows, flat


def wanted(cell):
    def part(text):
        if "/" in text:
            p, q = text.split("/")
            return mpmath.mpf(p) / mpmath.mpf(q)
        return mpmath.mpf(text)
    return mpmath.mpc(part(cell[0]), part(cell[1]))


@pytest.fixture(scope="module")
def engine():
    e = Engine()
    e.settings.angle = DATA["angle"]
    return e


@pytest.mark.parametrize("case", DATA["cases"], ids=[f"{i}-{c['group']}" for i, c in enumerate(DATA["cases"])])
def test_engine_matches_sympy(engine, case):
    text = engine.evaluate(case["line"]).text
    if "error" in case:
        assert text.startswith("ERROR:") and case["error"] in text, (case["line"], text)
        return
    assert not text.startswith("ERROR:"), (case["line"], text)
    with mpmath.workdps(60):
        got, flat = shown(text)
        want = [[wanted(case["value"])]] if "value" in case else [[wanted(c) for c in row] for row in case["rows"]]
        if "rows" in case:
            assert flat == case["flat"], (case["line"], text)
        assert [len(row) for row in got] == [len(row) for row in want], (case["line"], text)
        scale = max([mpmath.mpf(1)] + [abs(w) for row in want for w in row])
        worst = max(abs(g - w) for gr, wr in zip(got, want) for g, w in zip(gr, wr))
        allowed = max(TOLERANCE, mpmath.mpf(case.get("cond", 0)) * mpmath.mpf("1e-40"))
        assert worst <= allowed * scale, (case["line"], text, mpmath.nstr(worst / scale, 5))
