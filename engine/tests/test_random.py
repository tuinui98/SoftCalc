"""tests/test_random.py - rand(), rands() and round/ceil/floor (owner 2026-11-12, design/random_plan.md R0)."""

import mpmath
import pytest

from softcalc_engine.engine import Engine
from softcalc_engine.server import Dispatcher


def results(engine, line, count):
    return [engine.evaluate(line) for _ in range(count)]


def test_whole_number_arguments_give_whole_numbers_with_both_ends():
    engine = Engine()
    seen = {r.text for r in results(engine, "rand(1, 6)", 400)}
    assert seen == {"1", "2", "3", "4", "5", "6"}
    assert {r.text for r in results(engine, "rand(2)", 200)} == {"0", "1", "2"}
    assert {r.text for r in results(engine, "rand(-2)", 200)} == {"-2", "-1", "0"}
    assert {r.text for r in results(engine, "rand(6, 1)", 200)} <= {"1", "2", "3", "4", "5", "6"}


def test_a_point_or_a_fraction_gives_a_decimal():
    engine = Engine()
    for line in ("rand(1.0, 10)", "rand(0.5, 2)", "rand(1e1)"):
        values = [r.value for r in results(engine, line, 50)]
        assert any(v != mpmath.floor(v) for v in values), line
    assert all(1 <= r.value <= 10 for r in results(engine, "rand(1.0, 10)", 200))


def test_rand_without_arguments_is_a_fraction_at_full_precision():
    engine = Engine()
    values = [r.value for r in results(engine, "rand()", 200)]
    assert all(0 <= v < 1 for v in values)
    assert len(set(values)) == 200
    assert len(engine.evaluate("rand()").text) > 20    # many digits, not a float


def test_seed_repeats_the_draws_and_no_seed_does_not():
    engine = Engine()
    assert engine.evaluate("rands(42)").text == "Seed 42"
    first = [r.text for r in results(engine, "rand(1, 1000000)", 5)]
    engine.evaluate("rands(42)")
    assert [r.text for r in results(engine, "rand(1, 1000000)", 5)] == first
    other = Engine()
    other.evaluate("rands(42)")
    assert [r.text for r in results(other, "rand(1, 1000000)", 5)] == first    # same seed, same draws in any window
    assert engine.evaluate("rands()").text == "Random seed"


def test_history_shows_the_drawn_values():
    engine = Engine()
    engine.evaluate("rands(7)")
    line = engine.evaluate("rand(1, 6)+rand(10, 20)")
    first, second = line.display_input.split("+")
    assert int(first) + int(second) == int(line.text)
    assert line.input == "rand(1, 6)+rand(10, 20)"    # Use Expression draws again
    assert engine.evaluate("2rand(1, 6)").display_input.startswith("2*")
    nested = engine.evaluate("rand(1, rand(5, 9))")
    assert nested.display_input == nested.text    # the inner draw goes with the outer one
    assert engine.evaluate("rand(-5, -1)").display_input.startswith("(-")


def test_coin_lines_from_insert_command_give_one_or_zero():
    engine = Engine()
    fair = {r.text for r in results(engine, "round(rand())", 200)}
    assert fair == {"0", "1"}
    biased = [r.text for r in results(engine, "floor(rand()+0.7)", 4000)]
    assert set(biased) == {"0", "1"}
    assert 2600 < biased.count("1") < 3000    # 70% give or take
    assert {r.text for r in results(engine, "floor(rand()+1)", 50)} == {"1"}
    assert {r.text for r in results(engine, "floor(rand()+0)", 50)} == {"0"}
    decimals = [r.value for r in results(engine, "round(rand(1.0, 2), 2)", 200)]
    assert all(1 <= v <= 2 and v * 100 == mpmath.floor(v * 100 + mpmath.mpf("0.5")) for v in decimals)


def test_bad_arguments():
    engine = Engine()
    assert engine.evaluate("rand(i)").text.startswith("ERROR:")
    assert engine.evaluate("rand(1, 2, 3)").text.startswith("ERROR:")
    assert engine.evaluate("rands(1, 2)").text.startswith("ERROR:")
    assert engine.evaluate("sqrt()").text.startswith("ERROR:")    # only rand and rands may be empty


def test_rand_in_a_user_function_and_rpn():
    engine = Engine()
    engine.evaluate("roll(n)=rand(1, n)")
    assert {r.text for r in results(engine, "roll(3)", 200)} == {"1", "2", "3"}
    engine.settings.rpn = True
    assert engine.evaluate("1 6 rand").value in [mpmath.mpf(n) for n in range(1, 7)]


@pytest.mark.parametrize("line, want", [
    ("round(2.5)", "3"),
    ("round(-2.5)", "-3"),
    ("round(2.4)", "2"),
    ("round(2.675, 2)", "2.68"),
    ("round(3.14159, 2)", "3.14"),
    ("round(1234, -2)", "1200"),
    ("round(1250, -2)", "1300"),
    ("floor(0.1*30)", "3"),
    ("floor(-1.2)", "-2"),
    ("ceil(-1.2)", "-1"),
    ("ceil(1.2)", "2"),
    ("ceil(0.7+0.1+0.2)", "1"),
    ("floor(2.999)", "2"),
])
def test_rounding(line, want):
    assert Engine().evaluate(line).text == want


def test_rounding_errors():
    engine = Engine()
    for line in ("round(i)", "round(1, 0.5)", "floor(1, 2)", "round(1, 5000)", "ceil(today)"):
        assert engine.evaluate(line).text.startswith("ERROR:"), line


def test_catalog_lists_the_new_groups():
    catalog = Dispatcher().handle({"op": "catalog"})
    groups = catalog["functions"]
    assert groups["Rounding"] == ["round", "ceil", "floor"]
    assert groups["Random"] == ["rand", "rands"]
