"""tests/test_random_shapes.py - randbeta, randtri, randexp, randpeaks (owner 2026-11-12, design/random_plan.md R3)."""

from collections import Counter

import mpmath
import pytest

from softcalc_engine.engine import Engine


def values(engine, line, count):
    return [engine.evaluate(line).value for _ in range(count)]


def texts(engine, line, count):
    return Counter(engine.evaluate(line).text for _ in range(count))


def mean(xs):
    return sum(xs) / len(xs)


def variance(xs):
    m = mean(xs)
    return sum((x - m) ** 2 for x in xs) / len(xs)


def test_a_seed_gives_the_same_draws_on_any_python():
    """Pinned values: if a Python upgrade changes them, the seeds people
    saved no longer repeat, and this test says so first."""
    engine = Engine()
    engine.evaluate("rands(42)")
    got = [engine.evaluate(line).text for line in ("rand(1, 6)", "rand(1, 1000000)", "rand()", "rand(1.0, 10)", "rand(-5, 5)")]
    assert got == ["4", "477947", "0.15709464272787460811191865601749", "2.945524197803265375102122317124", "-2"]
    engine.evaluate("rands(42)")
    got = [engine.evaluate(line).text for line in ("randbeta(2, 5)", "randbeta(4, 4, 1, 10)", "randtri(3, 1, 10)",
                                                   "randexp(4, 0, 100)", "randpeaks(0.45, 1.0, 10)", "randbeta(0.5, 0.5)")]
    assert got == ["0.4617529646121530128127823177182", "4", "5", "5", "4.1049467553898496295965639002892",
                   "0.019180095314501750866731912336797"]


def test_beta_has_the_right_mean_and_spread():
    engine = Engine()
    for a, b in ((2, 5), (5, 2), (4, 4), (0.5, 0.5), (1, 1)):
        xs = values(engine, f"randbeta({a}, {b})", 3000)
        assert all(0 < x < 1 for x in xs)
        want_mean = a / (a + b)
        want_var = a * b / ((a + b) ** 2 * (a + b + 1))
        assert abs(mean(xs) - want_mean) < 0.02, (a, b)
        assert abs(variance(xs) - want_var) < 0.25 * want_var, (a, b)


def test_draws_have_the_full_precision():
    engine = Engine()
    for line in ("randbeta(2, 5)", "randtri(0.3)", "randexp(4)", "randpeaks(0.5)"):
        assert len(engine.evaluate(line).text) > 25, line


def test_whole_range_gives_whole_numbers_with_both_ends():
    engine = Engine()
    for line in ("randbeta(1, 1, 1, 6)", "randtri(3, 1, 6)", "randexp(0.5, 1, 6)", "randpeaks(0.5, 1, 6)"):
        seen = texts(engine, line, 1500)
        assert set(seen) == {"1", "2", "3", "4", "5", "6"}, line
    assert set(texts(engine, "randbeta(2, 2, 10, 1)", 300)) <= {str(n) for n in range(1, 11)}    # reversed range


def test_a_point_or_a_fraction_in_the_range_gives_decimals():
    engine = Engine()
    for line in ("randbeta(4, 4, 1.0, 10)", "randtri(2, 1, 10.5)", "randexp(4, 0, 1e2)"):
        xs = values(engine, line, 100)
        assert any(x != mpmath.floor(x) for x in xs), line
    assert all(1 <= x <= 10 for x in values(engine, "randbeta(4, 4, 1.0, 10)", 300))


def test_shapes_lean_the_right_way():
    engine = Engine()
    right = texts(engine, "randbeta(2, 5, 1, 10)", 3000)
    assert right["2"] + right["3"] > 3 * (right["8"] + right["9"])
    tri = texts(engine, "randtri(3, 1, 5)", 3000)
    assert tri["3"] == max(tri.values())
    assert abs(tri["1"] - tri["5"]) < 120    # the peak in the middle: both sides alike
    decay = mean(values(engine, "randexp(4)", 2000))
    assert abs(decay - (1 / 4 - mpmath.exp(-4) / (1 - mpmath.exp(-4)))) < 0.02
    assert abs(mean(values(engine, "randexp(-4)", 2000)) - (1 - decay)) < 0.03    # growth mirrors decay
    two = texts(engine, "randpeaks(0.6, 1, 9)", 3000)
    assert two["5"] < two["2"] / 3 and two["5"] < two["8"] / 3    # the middle dips


def test_history_shows_the_drawn_values():
    engine = Engine()
    line = engine.evaluate("randbeta(2, 5, 1, 10)+randtri(5, 1, 10)")
    first, second = line.display_input.split("+")
    assert int(first) + int(second) == int(line.text)
    assert line.input == "randbeta(2, 5, 1, 10)+randtri(5, 1, 10)"


@pytest.mark.parametrize("line", [
    "randbeta(1, 2, 3)", "randbeta(2)", "randbeta(0, 1)", "randbeta(-1, 2)", "randbeta(i, 1)",
    "randtri()", "randtri(11, 1, 10)", "randtri(0.5, 1)", "randpeaks(2)", "randpeaks(-0.1)", "randexp(today)",
])
def test_bad_arguments(line):
    assert Engine().evaluate(line).text.startswith("ERROR:"), line


def test_in_rpn_they_take_the_range():
    engine = Engine()
    engine.settings.rpn = True
    assert engine.evaluate("2 5 1 10 randbeta").value in [mpmath.mpf(n) for n in range(1, 11)]


def test_draws_op_matches_lines_typed_after_rands():
    """The Random window's Seed: same values as rands(seed) and the line typed count times."""
    from softcalc_engine.server import Dispatcher
    dispatcher = Dispatcher()
    for line in ("rand(1, 6)", "round(randbeta(4, 4, 1.0, 10), 2)", "floor(rand()+0.7)"):
        reply = dispatcher.handle({"op": "draws", "seed": "42", "line": line, "count": 20})
        engine = Engine()
        engine.evaluate("rands(42)")
        assert reply["texts"] == [engine.evaluate(line).text for _ in range(20)], line
    assert dispatcher.engine.results == []    # draws are not $n results
    unique = dispatcher.handle({"op": "draws", "seed": "7", "line": "rand(1, 6)", "count": 6, "unique": True})["texts"]
    assert sorted(unique) == ["1", "2", "3", "4", "5", "6"]


@pytest.mark.parametrize("request_, error", [
    ({"seed": "4)+1", "line": "rand()", "count": 1}, "'seed'"),
    ({"seed": "1", "line": "rand()", "count": 0}, "'count'"),
    ({"seed": "1", "line": "1/0", "count": 1}, "Division"),
    ({"seed": "1", "line": "rand(1, 3)", "count": 4, "unique": True}, "different"),
])
def test_draws_op_refuses(request_, error):
    from softcalc_engine.server import Dispatcher
    reply = Dispatcher().handle({"op": "draws", **request_})
    assert not reply["ok"] and error in reply["error"]
