"""tests/test_matrix.py - Lists and matrices, round 1 core (design/matrix_plan.md X1, owner 2026-11-24)."""

import pytest

from softcalc_engine.cards import decks_from
from softcalc_engine.engine import Engine
from softcalc_engine.matrix import MAX_CELLS, Matrix
from softcalc_engine.server import Dispatcher


def run(*lines):
    engine = Engine()
    results = [engine.evaluate(line) for line in lines]
    return results[-1], engine


@pytest.mark.parametrize("line, text", [
    ("[1, 2, 3]", "[1, 2, 3]"),
    ("[1,2,3]", "[1, 2, 3]"),
    ("[[1, 2], [3, 4]]", "[[1, 2], [3, 4]]"),
    ("[[1, 2, 3]]", "[[1, 2, 3]]"),    # a one-row matrix stays a matrix
    ("[1/3, 2^10, -5]", "[0.33333333333333333333333333333333, 1024, -5]"),
    ("[1+2i, 3]", "[(1+2i), 3]"),
    ("[1 [ft->in], 2]", "[12, 2]"),
    ("[sqrt(16), (1+2)*3]", "[4, 9]"),
    ("[ 1 000 , 2 ]", "[1000, 2]"),
])
def test_lists_and_matrices(line, text):
    assert run(line)[0].text == text


def test_items_can_be_variables_answer_and_results():
    result, _ = run("x=5", "7", "[x, 2x, answer, $1]")
    assert result.text == "[5, 10, 7, 7]"
    assert result.display_input == "[5, 2*5, 7, 7]"


def test_variables_and_results_hold_them():
    result, engine = run("A=[[1, 2], [3, 4]]", "A")
    assert result.text == "[[1, 2], [3, 4]]"
    assert isinstance(engine.variables["a"][1], Matrix)
    assert engine.evaluate("$1").text == "[[1, 2], [3, 4]]"
    assert engine.state()["variables"][0] == {"name": "A", "value": "[[1, 2], [3, 4]]"}


def test_the_text_parses_back_in_history_and_the_pcf():
    engine = Engine()
    engine.restore_results(["[1, 2]", "[[1, 0.5], [3, 4]]", "[(1+2i), 3]"])
    assert engine.evaluate("$2").text == "[[1, 0.5], [3, 4]]"
    assert isinstance(engine.results[0], Matrix)
    engine.evaluate("B=[[1, 2], [3, 4]]")
    assert "B=[[1, 2], [3, 4]]" in engine.export_pcf()
    fresh = Engine()
    assert fresh.import_pcf(engine.export_pcf()) == []
    assert fresh.evaluate("B").text == "[[1, 2], [3, 4]]"


def test_output_base_applies_to_every_cell():
    engine = Engine()
    engine.settings.base = "hex"
    assert engine.evaluate("[255, 16]").text == "[0xFF, 0x10]"
    engine.settings.base = "dec"
    engine.restore_results(["[0xFF, 0x10]"])
    assert engine.evaluate("$1").text == "[255, 16]"


@pytest.mark.parametrize("line, error", [
    ("[]", "ERROR:Empty list: write numbers between [ and ]"),
    ("[1, 2", "ERROR:Missing ']' after a list"),
    ("[1, [2]]", "ERROR:A list holds numbers, or lists of numbers of the same length"),
    ("[[1, 2], [3]]", "ERROR:The rows of a matrix must be the same length"),
    ("[[[1]]]", "ERROR:A matrix holds lists of numbers, not matrices"),
    ("[now]", "ERROR:A list holds numbers, or lists of numbers of the same length"),
    ("2[1, 2]", "ERROR:Write * before a list (2*[1, 2]); at(A, 1) takes a cell"),
])
def test_errors(line, error):
    assert run(line)[0].text == error


def test_a_name_then_a_bracket_is_a_tag_not_a_cell():
    assert run("A=[1, 2]", "A[1]")[0].text == "ERROR:Write * before a list (2*[1, 2]); at(A, 1) takes a cell"


def test_at_most_max_cells():
    engine = Engine()
    row = "[" + ", ".join(["1"] * 100) + "]"
    assert engine.evaluate("[" + ", ".join([row] * 100) + "]").kind == "result"
    assert engine.evaluate("[" + ", ".join([row] * 101) + "]").text == f"ERROR:A matrix may have at most {MAX_CELLS} cells"


def test_rpn_has_no_lists():
    engine = Engine()
    engine.settings.rpn = True
    assert engine.evaluate("[1, 2]").text == "ERROR:Lists and matrices are not available in RPN"
    assert engine.evaluate("1 2 +").text == "3"


def test_card_numbers_come_as_a_list():
    engine = Engine()
    engine.decks = decks_from([{"name": "Dice", "cards": [{"name": str(n)} for n in range(1, 7)]}])
    result = engine.evaluate('card("Dice", 4)')
    assert result.text.startswith("[") and isinstance(engine.answer, Matrix)
    assert result.display_input == result.text
    assert engine.evaluate("$1").text == result.text


def test_protocol_eval_and_state():
    d = Dispatcher()
    reply = d.handle({"op": "eval", "input": "M=[[1, 2], [3, 4]]"})
    assert reply["kind"] == "definition"
    assert d.handle({"op": "eval", "input": "M"})["text"] == "[[1, 2], [3, 4]]"
    assert d.handle({"op": "restore_results", "texts": ["[1, 2]"]})["restored"] == 1
