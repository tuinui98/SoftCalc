"""tests/test_matrix_functions.py - Arithmetic and functions on lists and matrices (design/matrix_plan.md X2)."""

import pytest

from softcalc_engine.cards import decks_from
from softcalc_engine.engine import Engine


def engine():
    e = Engine()
    e.settings.angle = "deg"
    e.evaluate("A=[[1, 2], [3, 4]]")
    e.evaluate("B=[[5, 6], [7, 8]]")
    return e


@pytest.mark.parametrize("line, text", [
    # operators
    ("2*[1, 2]", "[2, 4]"),
    ("[1, 2]*2", "[2, 4]"),
    ("2A", "[[2, 4], [6, 8]]"),
    ("[1, 2]+[3, 4]", "[4, 6]"),
    ("[1, 2]-[3, 4]", "[-2, -2]"),
    ("[1, 2]+1", "[2, 3]"),
    ("10-[1, 2]", "[9, 8]"),
    ("A/2", "[[0.5, 1], [1.5, 2]]"),
    ("1/[2, 4]", "[0.5, 0.25]"),
    ("[1, 2]^2", "[1, 4]"),
    ("2^[1, 2]", "[2, 4]"),
    ("[7, 8] mod 3", "[1, 2]"),
    ("-A", "[[-1, -2], [-3, -4]]"),
    ("[3, 4]!", "[6, 24]"),
    ("[50, 10]%", "[0.5, 0.1]"),
    ("A*B", "[[19, 22], [43, 50]]"),
    ("A*[1, 1]", "[3, 7]"),
    ("[1, 1]*A", "[4, 6]"),
    ("A^2", "[[7, 10], [15, 22]]"),
    ("A^-1", "[[-2, 1], [1.5, -0.5]]"),
    ("A^0", "[[1, 0], [0, 1]]"),
    ("A*A^-1", "[[1, 0], [0, 1]]"),
    # making
    ("identity(2)", "[[1, 0], [0, 1]]"),
    ("zeros(3)", "[0, 0, 0]"),
    ("zeros(2, 3)", "[[0, 0, 0], [0, 0, 0]]"),
    ("ones(2, 2)", "[[1, 1], [1, 1]]"),
    ("diag([1, 2])", "[[1, 0], [0, 2]]"),
    ("diag(A)", "[1, 4]"),
    ("seq(1, 5)", "[1, 2, 3, 4, 5]"),
    ("seq(0, 1, 0.25)", "[0, 0.25, 0.5, 0.75, 1]"),
    ("seq(5, 1)", "[5, 4, 3, 2, 1]"),
    ("seq(1, 2, 0.3)", "[1, 1.3, 1.6, 1.9]"),
    # size and cells
    ("rows(A)", "2"),
    ("cols([1, 2, 3])", "3"),
    ("len([1, 2, 3])", "3"),
    ("len(A)", "4"),
    ("at(A, 2, 1)", "3"),
    ("at([5, 6, 7], 3)", "7"),
    ("row(A, 2)", "[3, 4]"),
    ("col(A, 1)", "[1, 3]"),
    # linear algebra
    ("transpose(A)", "[[1, 3], [2, 4]]"),
    ("transpose([1, 2])", "[[1], [2]]"),
    ("det(A)", "-2"),
    ("inv(A)", "[[-2, 1], [1.5, -0.5]]"),
    ("trace(A)", "5"),
    ("solve([[2, 1], [1, -1]], [5, 1])", "[2, 1]"),
    ("solve(A, [[5], [6]])", "[[-4], [4.5]]"),
    ("dot([1, 2, 3], [4, 5, 6])", "32"),
    ("cross([1, 0, 0], [0, 1, 0])", "[0, 0, 1]"),
    # lists
    ("sum(A)", "10"),
    ("sum([1, 2], 3)", "6"),
    ("mean([1, 2, 3, 4])", "2.5"),
    ("prod([1, 2, 3, 4])", "24"),
    ("min([3, 1, 2])", "1"),
    ("max(3, 9, 2)", "9"),
    ("sort([3, 1, 2])", "[1, 2, 3]"),
    # one-number functions, cell by cell
    ("sin([0, 30, 90])", "[0, 0.5, 1]"),
    ("sqrt([4, 9])", "[2, 3]"),
    ("abs([-1, 2])", "[1, 2]"),
    ("round([1.234, 5.678], 1)", "[1.2, 5.7]"),
    ("floor([1.5, -1.5])", "[1, -2]"),
])
def test_values(line, text):
    assert engine().evaluate(line).text == text


@pytest.mark.parametrize("line, error", [
    ("[1, 2]*[3, 4]", "Two lists: use dot(a, b), or write one as a matrix"),
    ("A+[1, 2]", "'+' needs the same size: 2x2 and a list of 2"),
    ("A*[1, 2, 3]", "Sizes do not fit: 2x2 times a list of 3"),
    ("A/B", "Divide by a matrix: multiply by inv() instead"),
    ("2/A", "'/' with a matrix on the right is not defined"),
    ("A^0.5", "A matrix to a power needs a whole number from -1000 to 1000"),
    ("[[1, 2, 3], [4, 5, 6]]^2", "'^' needs a square matrix"),
    ("inv([[1, 2], [2, 4]])", "The matrix has no inverse (its det is 0)"),
    ("solve([[1, 2], [2, 4]], [1, 2])", "solve: the matrix has no inverse (its det is 0), so no single answer"),
    ("solve(A, [1, 2, 3])", "solve: A has 2 rows, b has 3"),
    ("det([1, 2])", "det needs a square matrix"),
    ("det(identity(51))", "det takes at most 50x50"),
    ("at(A, 3, 1)", "at: row 3 is past the end (2)"),
    ("at(A, 1)", "at(matrix, row, column) takes a row and a column"),
    ("at([1, 2], 0)", "at needs a whole number from 1"),
    ("dot([1, 2], [1, 2, 3])", "dot needs two lists of the same length (2 and 3)"),
    ("cross([1, 2], [3, 4])", "cross needs two lists of 3 numbers"),
    ("sort(A)", "sort takes a list"),
    ("det(5)", "det needs a list or a matrix"),
    ("seq(1, 5, -1)", "seq: the step must go from the start toward the end"),
    ("seq(1, 100000)", "A list may have at most 10000 numbers"),
    ("zeros(200, 200)", "A matrix may have at most 10000 cells"),
    ("min([1+2i])", "min needs real numbers"),
    ("sum()", "sum() needs an argument"),
    ("randm(2, 2, 1)", "randm takes rows, cols and both ends: randm(3, 3, 1, 6)"),
])
def test_errors(line, error):
    assert engine().evaluate(line).text == "ERROR:" + error


def test_randm_follows_the_seed_and_rand_rules():
    e = engine()
    e.evaluate("rands(3)")
    first = e.evaluate("randm(2, 3, 1, 6)")
    e.evaluate("rands(3)")
    again = e.evaluate("randm(2, 3, 1, 6)")
    assert first.text == again.text
    assert first.display_input == first.text    # History shows what was drawn
    cells = first.text.replace("[", "").replace("]", "").split(", ")
    assert len(cells) == 6 and all(c in {"1", "2", "3", "4", "5", "6"} for c in cells)
    assert "." in e.evaluate("randm(1, 2, 1.0, 6)").text


def test_card_lists_compute():
    e = engine()
    e.decks = decks_from([{"name": "Dice", "cards": [{"name": str(n)} for n in range(1, 7)]}])
    assert int(e.evaluate('sum(card("Dice", 3))').text) in range(3, 19)
    assert e.evaluate('len(card("Dice", 6, 1))').text == "6"
    assert e.evaluate('sort(card("Dice", 6, 1))').text == "[1, 2, 3, 4, 5, 6]"


def test_matrix_names_are_reserved():
    assert engine().evaluate("det=5").text == "ERROR:'det' is a reserved name"


#------------------------------------------------------------------
#  Round 2 (owner 2026-11-24)
#------------------------------------------------------------------
@pytest.mark.parametrize("line, text", [
    ("[1, 2] [m->cm]", "[100, 200]"),
    ("[[0, 100], [37, -40]] [c->f]", "[[32, 212], [98.6, -40]]"),
    ("median([3, 1, 2, 10])", "2.5"),
    ("median(5, [1, 9])", "5"),
    ("stdevp([2, 4, 4, 4, 5, 5, 7, 9])", "2"),
    ("variance(1, 2, 3, 4)", "1.6666666666666666666666666666667"),
    ("variancep([1, 2, 3, 4])", "1.25"),
    ("norm([3, 4])", "5"),
    ("rank([[1, 2], [2, 4]])", "1"),
    ("rank([[1, 2, 3], [4, 5, 6], [7, 8, 9]])", "2"),
    ("rank([[0.316, 1.2], [0.632, 2.4]])", "1"),
    ("rank(zeros(2, 3))", "0"),
    ("rank([1, 2])", "1"),
    ("eig([[4, 1], [2, 3]])", "[5, 2]"),
    ("eig([[0, -1], [1, 0]])", "[1i, -1i]"),
])
def test_round_two_values(line, text):
    assert engine().evaluate(line).text == text


@pytest.mark.parametrize("line, error", [
    ("[1, 2] [longdate]", "[longdate] takes one date, not a list"),
    ("[1, 2] [utc->local]", "A time zone tag takes one date, not a list"),
    ("[1, 2] [m->kg]", "Cannot convert m to kg"),
    ("stdev(5)", "stdev needs at least 2 numbers"),
    ("median([1+2i])", "median needs real numbers"),
    ("eig(identity(21))", "eig takes at most 20x20"),
    ("eig([1, 2])", "eig needs a square matrix"),
])
def test_round_two_errors(line, error):
    assert engine().evaluate(line).text == "ERROR:" + error


def test_a_matrix_near_det_0_shows_every_digit_with_a_note():
    e = engine()
    result = e.evaluate("inv([[1, 1], [1, 1.00000000000000000001]])")
    assert result.text.startswith("[[100000000000000000001.0000")
    assert result.note == "about 10 of 32 digits may be off"
    assert e.evaluate("inv(A)").note == ""    # a well-made matrix: no note, and the note does not linger
    assert e.evaluate("det([[1, 1], [1, 1.0000000001]])").note == ""


def test_var_stays_free_for_the_default_function():
    # `var` would clash with the default function(var)=var*cos(var); variance is the name
    assert not engine().evaluate("function(3)").text.startswith("ERROR")
