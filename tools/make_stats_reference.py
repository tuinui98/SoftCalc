"""tools/make_stats_reference.py - Exact answers from SymPy for the statistics functions (owner 2026-11-26).

Writes engine/tests/data/stats_reference.json: lines the engine is asked
(random values from a fixed seed) with SymPy's answer, exact where it can
be (comb, perm, percentile, quartile) and worked out to DIGITS digits for
the normal distribution (normcdf, norminv), or the error expected.
engine/tests/test_stats_reference.py compares the engine with it.

Runs in the reference environment (see tools/make_matrix_reference.py):
    engine/.venv-ref/Scripts/python tools/make_stats_reference.py
"""

import json
import random
from pathlib import Path

import sympy
from sympy import Rational, erf, erfinv, sqrt

SEED = 20261126
OUT = Path(__file__).resolve().parent.parent / "engine" / "tests" / "data" / "stats_reference.json"
DIGITS = 45


def text(value):
    """A rational as the engine is typed it: 3, -2.5, 0.125 (denominators of 2s and 5s only)."""
    value = Rational(value)
    if value.q == 1:
        return str(value.p)
    return str(sympy.Float(value, 30)).rstrip("0").rstrip(".")


def exact(value):
    """An answer as p/q, or a decimal string at DIGITS digits."""
    if isinstance(value, Rational):
        return f"{value.p}/{value.q}"
    return str(sympy.N(value, DIGITS))


def percentile(values, p):
    values = sorted(values)
    rank = p * (len(values) - 1)
    below = int(sympy.floor(rank))
    if below + 1 >= len(values):
        return values[-1]
    return values[below] + (rank - below) * (values[below + 1] - values[below])


def cdf(x, mean, sd):
    return (1 + erf((x - mean) / (sd * sqrt(2)))) / 2


def cases():
    rng = random.Random(SEED)
    out = []

    def add(line, value=None, error=None, group=""):
        out.append({"line": line, "value": None if value is None else exact(value), "error": error, "group": group})

    def decimal(low, high, places):
        return Rational(rng.randint(low * 10 ** places, high * 10 ** places), 10 ** places)

    #==================================================================
    #  BLOCK 1 - comb and perm: exact whole numbers
    #==================================================================
    for _ in range(120):
        n = rng.randint(0, 60)
        k = rng.randint(0, n + 3)
        add(f"comb({n}, {k})", sympy.binomial(n, k) if k <= n else Rational(0), group="comb")
        add(f"perm({n}, {k})", sympy.ff(n, k) if k <= n else Rational(0), group="perm")
    for n, k in ((-1, 2), (5, -1)):
        add(f"comb({n}, {k})", error="comb:", group="comb")
    add("comb(5, 2.5)", error="comb:", group="comb")

    #==================================================================
    #  BLOCK 2 - percentile and quartile: exact, as Excel's .INC
    #==================================================================
    for _ in range(120):
        values = [decimal(-50, 50, rng.choice((0, 1, 2))) for _ in range(rng.randint(1, 12))]
        listed = "[" + ", ".join(text(v) for v in values) + "]"
        p = Rational(rng.randint(0, 100), 100)
        add(f"percentile({listed}, {text(p)})", percentile(values, p), group="percentile")
        q = rng.randint(0, 4)
        add(f"quartile({listed}, {q})", percentile(values, Rational(q, 4)), group="quartile")
    add("percentile([1, 2], 1.5)", error="percentile:", group="percentile")
    add("quartile([1, 2], 2.5)", error="quartile:", group="quartile")

    #==================================================================
    #  BLOCK 3 - normcdf and norminv, worked out to DIGITS digits
    #==================================================================
    for _ in range(80):
        x = decimal(-6, 6, 2)
        add(f"normcdf({text(x)})", cdf(x, 0, 1), group="normcdf")
        mean, sd = decimal(-100, 100, 1), decimal(1, 30, 1)
        y = mean + decimal(-4, 4, 2) * sd
        add(f"normcdf({text(y)}, {text(mean)}, {text(sd)})", cdf(y, mean, sd), group="normcdf")
        a, b = sorted((decimal(-4, 4, 2), decimal(-4, 4, 2)))
        add(f"normcdf({text(a)}, {text(b)})", cdf(b, 0, 1) - cdf(a, 0, 1), group="normcdf")
        add(f"normcdf({text(mean + a * sd)}, {text(mean + b * sd)}, {text(mean)}, {text(sd)})",
            cdf(mean + a * sd, mean, sd) * -1 + cdf(mean + b * sd, mean, sd), group="normcdf")
        p = Rational(rng.randint(1, 999), 1000)
        add(f"norminv({text(p)})", sqrt(2) * erfinv(2 * p - 1), group="norminv")
        add(f"norminv({text(p)}, {text(mean)}, {text(sd)})", mean + sd * sqrt(2) * erfinv(2 * p - 1), group="norminv")
    add("normcdf(1, 2, 3, 0)", error="normcdf:", group="normcdf")
    add("normcdf(2, 1)", error="normcdf(a, b)", group="normcdf")
    add("norminv(0)", error="norminv:", group="norminv")
    add("norminv(0.5, 1)", error="norminv expects", group="norminv")
    return out


if __name__ == "__main__":
    data = cases()
    OUT.write_text(json.dumps(data, indent=1), encoding="utf-8")
    print(f"{OUT}: {len(data)} cases")
