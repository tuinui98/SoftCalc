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

    #==================================================================
    #  BLOCK 4 - round 2: more description (owner 2026-11-26)
    #==================================================================
    def some(least=1, most=12, places=(0, 1, 2), low=-50, high=50):
        return [decimal(low, high, rng.choice(places)) for _ in range(rng.randint(least, most))]

    def listed(values):
        return "[" + ", ".join(text(v) for v in values) + "]"

    for _ in range(60):
        values = some()
        add(f"range({listed(values)})", max(values) - min(values), group="range")
        add(f"iqr({listed(values)})", percentile(values, Rational(3, 4)) - percentile(values, Rational(1, 4)), group="iqr")
        good = some(low=1, high=100)
        add(f"geomean({listed(good)})", sympy.prod(good) ** Rational(1, len(good)), group="geomean")
        add(f"harmean({listed(good)})", len(good) / sum(1 / v for v in good), group="harmean")
        x, mean, sd = decimal(-100, 100, 1), decimal(-50, 50, 1), decimal(1, 20, 1)
        add(f"zscore({text(x)}, {text(mean)}, {text(sd)})", (x - mean) / sd, group="zscore")
    add("geomean([1, 0, 2])", error="geomean needs numbers above 0", group="geomean")

    #==================================================================
    #  BLOCK 5 - round 2: two variables, xs first
    #==================================================================
    for _ in range(60):
        n = rng.randint(3, 10)
        xs = some(n, n, low=-20, high=20)
        ys = some(n, n, low=-20, high=20)
        if len(set(xs)) < 2 or len(set(ys)) < 2:
            continue
        mx, my = sum(xs) / n, sum(ys) / n
        sxx = sum((x - mx) ** 2 for x in xs)
        syy = sum((y - my) ** 2 for y in ys)
        sxy = sum((x - mx) * (y - my) for x, y in zip(xs, ys))
        pair = f"{listed(xs)}, {listed(ys)}"
        add(f"covar({pair})", sxy / (n - 1), group="covar")
        add(f"covarp({pair})", sxy / n, group="covar")
        add(f"correl({pair})", sxy / sqrt(sxx * syy), group="correl")
        add(f"slope({pair})", sxy / sxx, group="line")
        add(f"intercept({pair})", my - sxy / sxx * mx, group="line")
        add(f"rsq({pair})", sxy ** 2 / (sxx * syy), group="line")
        at = decimal(-30, 30, 1)
        add(f"forecast({text(at)}, {pair})", my - sxy / sxx * mx + sxy / sxx * at, group="line")
    add("slope([1, 2], [1, 2, 3])", error="slope: xs and ys", group="line")
    add("correl([1, 1, 1], [1, 2, 3])", error="correl:", group="correl")

    #==================================================================
    #  BLOCK 6 - round 2: normpdf, binomial, Poisson, gamma, erf
    #==================================================================
    for _ in range(50):
        x, mean, sd = decimal(-6, 6, 2), decimal(-10, 10, 1), decimal(1, 5, 1)
        add(f"normpdf({text(x)})", sympy.exp(-x ** 2 / 2) / sqrt(2 * sympy.pi), group="normpdf")
        add(f"normpdf({text(x)}, {text(mean)}, {text(sd)})",
            sympy.exp(-((x - mean) / sd) ** 2 / 2) / (sd * sqrt(2 * sympy.pi)), group="normpdf")
        n, p = rng.randint(1, 40), Rational(rng.randint(0, 100), 100)
        k = rng.randint(0, n)
        pmf = [sympy.binomial(n, i) * p ** i * (1 - p) ** (n - i) for i in range(n + 1)]
        add(f"binompdf({n}, {text(p)}, {k})", pmf[k], group="binomial")
        add(f"binomcdf({n}, {text(p)}, {k})", sum(pmf[:k + 1]), group="binomial")
        a, b = sorted((rng.randint(0, n), rng.randint(0, n)))
        add(f"binomcdf({n}, {text(p)}, {a}, {b})", sum(pmf[a:b + 1]), group="binomial")
        lam, k = decimal(1, 30, 1), rng.randint(0, 40)
        term = lambda i: sympy.exp(-lam) * lam ** i / sympy.factorial(i)
        add(f"poisspdf({text(lam)}, {k})", term(k), group="poisson")
        add(f"poisscdf({text(lam)}, {k})", sum(term(i) for i in range(k + 1)), group="poisson")
        a, b = sorted((rng.randint(0, 40), rng.randint(0, 40)))
        add(f"poisscdf({text(lam)}, {a}, {b})", sum(term(i) for i in range(a, b + 1)), group="poisson")
        g = decimal(-5, 12, 2)
        if g != sympy.floor(g) or g > 0:
            add(f"gamma({text(g)})", sympy.gamma(g), group="special")
        e = decimal(-4, 4, 2)
        add(f"erf({text(e)})", sympy.erf(e), group="special")
        add(f"erfc({text(e)})", sympy.erfc(e), group="special")
    add("gamma(-2)", error="gamma is undefined here", group="special")
    add("binompdf(5, 1.5, 2)", error="binompdf: p goes from 0 to 1", group="binomial")
    add("poisscdf(0, 2)", error="poisscdf: the mean must be above 0", group="poisson")
    return out


if __name__ == "__main__":
    data = cases()
    OUT.write_text(json.dumps(data, indent=1), encoding="utf-8")
    print(f"{OUT}: {len(data)} cases")
