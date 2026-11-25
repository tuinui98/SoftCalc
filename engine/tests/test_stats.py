"""tests/test_stats.py - Statistics round 1 (owner 2026-11-26): what SymPy cannot answer.

The values themselves are checked against SymPy in test_stats_reference.py;
here: mode and its ties, randnorm, the menu, precision and arguments.
"""

import mpmath
import pytest

from softcalc_engine.engine import Engine
from softcalc_engine.server import Dispatcher


@pytest.fixture
def engine():
    return Engine()


def test_mode_gives_the_first_of_a_tie_and_shows_them_all(engine):
    result = engine.evaluate("mode([3, 1, 1, 3, 2])")
    assert result.text == "3"
    assert result.display_input == "[3, 1]"    # only the ties, faint on the right (owner 2026-11-26)
    plain = engine.evaluate("mode([1, 2, 2, 3])")
    assert (plain.text, plain.display_input) == ("2", "mode([1, 2, 2, 3])")


def test_mode_takes_numbers_and_lists_and_wants_a_repeat(engine):
    assert engine.evaluate("mode(4, [4, 5], 5, 5)").text == "5"
    assert engine.evaluate("mode([1, 2, 3])").text == "ERROR:mode: no value comes more than once"
    assert engine.evaluate("mode([[1, 1], [2, 3]])").text == "1"


def test_comb_and_perm_stay_whole_and_exact(engine):
    assert engine.evaluate("comb(100, 50)").text == "100891344545564193334812497256"
    assert engine.evaluate("perm(10, 0)").text == "1"
    assert engine.evaluate("comb(0, 0)").text == "1"
    assert engine.evaluate("comb(7, 9)").text == "0"


def test_the_normal_distribution_at_a_higher_precision():
    dispatcher = Dispatcher()
    dispatcher.handle({"op": "settings", "precision": 64})
    text = dispatcher.handle({"op": "eval", "input": "norminv(0.975)"})["text"]
    with mpmath.workdps(80):
        want = mpmath.sqrt(2) * mpmath.erfinv(mpmath.mpf("0.95"))
        assert abs(mpmath.mpf(text) - want) < mpmath.mpf("1e-60")
    assert len(text.replace(".", "")) >= 60


@pytest.mark.parametrize("line, error", [
    ("normcdf()", "normcdf() needs an argument"),
    ("normcdf(1, 2, 3, 4, 5)", "normcdf expects 1 to 4 argument(s)"),
    ("normcdf(0, 0, -1)", "normcdf: sd must be above 0"),
    ("normcdf(1i)", "normcdf needs real numbers"),
    ("percentile([1, 2], 50)", "percentile: p goes from 0 to 1"),
    ("quartile([1, 2], 1.5)", "quartile: q is 0, 1, 2, 3 or 4"),
    ("perm(3)", "perm expects 2 argument(s)"),
])
def test_arguments_are_checked(engine, line, error):
    assert engine.evaluate(line).text.startswith("ERROR:" + error), line


def test_randnorm_repeats_with_a_seed_and_has_its_shape(engine):
    engine.evaluate("rands(5)")
    first = [engine.evaluate("randnorm(10, 2)").text for _ in range(3)]
    engine.evaluate("rands(5)")
    assert [engine.evaluate("randnorm(10, 2)").text for _ in range(3)] == first
    draws = [mpmath.mpf(engine.evaluate("randnorm()").text) for _ in range(2000)]
    mean = sum(draws) / len(draws)
    sd = mpmath.sqrt(sum((x - mean) ** 2 for x in draws) / (len(draws) - 1))
    assert abs(mean) < 0.1 and abs(sd - 1) < 0.1


def test_randnorm_is_a_decimal_and_draws_in_every_cell(engine):
    engine.evaluate("rands(2)")
    assert "." in engine.evaluate("randnorm(0, 1)").text    # whole numbers in, still a decimal
    cells = engine.evaluate("fill(1, 4, randnorm())").text.strip("[]").split(", ")
    assert len(set(cells)) == 4
    assert engine.evaluate("randnorm(0, 0)").text == "ERROR:randnorm: sd must be above 0"
    assert engine.evaluate("randnorm(1)").text.startswith("ERROR:randnorm expects")


def test_the_statistics_menu():
    catalog = Dispatcher().handle({"op": "catalog"})
    top = catalog["functions"]["Statistics"]
    groups = catalog["subgroups"]["Statistics"]
    assert top == ["sum", "mean", "median", "mode", "stdev", "variance", "percentile", "comb", "normcdf", "norminv"]
    assert list(groups) == ["Descriptive", "Two variables", "Counting", "Distributions"]
    assert set(top) <= {name for names in groups.values() for name in names}
    assert "randnorm" in catalog["functions"]["Random"] and "randnorm" in groups["Distributions"]


#======================================================================
#  Round 2 (owner 2026-11-26)
#======================================================================
def test_zscore_of_a_list_keeps_its_shape(engine):
    assert engine.evaluate("zscore([1, 2, 3])").text == "[-1, 0, 1]"
    assert engine.evaluate("zscore(1, 2)").text.startswith("ERROR:zscore expects")


def test_randbinom_and_randpois_are_whole_repeat_and_have_their_mean(engine):
    engine.evaluate("rands(9)")
    first = [engine.evaluate("randbinom(20, 0.3)").text for _ in range(5)] + [engine.evaluate("randpois(4)").text]
    engine.evaluate("rands(9)")
    again = [engine.evaluate("randbinom(20, 0.3)").text for _ in range(5)] + [engine.evaluate("randpois(4)").text]
    assert first == again and all(text.isdigit() for text in first)
    binom = [int(engine.evaluate("randbinom(20, 0.3)").text) for _ in range(1500)]
    pois = [int(engine.evaluate("randpois(4)").text) for _ in range(1500)]
    assert abs(sum(binom) / 1500 - 6) < 0.3 and abs(sum(pois) / 1500 - 4) < 0.3
    assert engine.evaluate("randbinom(10, 1.5)").text == "ERROR:randbinom: p goes from 0 to 1"
    assert engine.evaluate("randpois(0)").text.startswith("ERROR:randpois: the mean must be above 0")


def test_special_functions_work_cell_by_cell(engine):
    assert engine.evaluate("gamma([1, 2, 3, 4])").text == "[1, 1, 2, 6]"
    assert engine.evaluate("erf(0)").text == "0"


def test_the_round_2_menu():
    catalog = Dispatcher().handle({"op": "catalog"})
    assert catalog["functions"]["Special Functions"] == ["gamma", "erf", "erfc"]
    groups = catalog["subgroups"]["Statistics"]
    assert groups["Two variables"] == ["covar", "covarp", "correl", "slope", "intercept", "rsq", "forecast"]
    assert {"range", "iqr", "geomean", "harmean", "zscore"} <= set(groups["Descriptive"])
    assert {"normpdf", "binompdf", "binomcdf", "poisspdf", "poisscdf", "randbinom", "randpois"} <= set(groups["Distributions"])
