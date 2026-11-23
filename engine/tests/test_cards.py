"""tests/test_cards.py - card("Deck", [count], [no repeat]): draws from the UI's decks in Input (owner 2026-11-24)."""

import pytest

from softcalc_engine.cards import decks_from
from softcalc_engine.engine import Engine
from softcalc_engine.server import Dispatcher

DECKS = [
    {"name": "Food", "cards": [{"name": "Rice"}, {"name": "Soup", "weight": 3}, {"name": "Joker", "off": True}]},
    {"name": "Dice", "alias": "ลูกเต๋า", "cards": [{"name": str(n)} for n in range(1, 7)]},
    {"name": "Odd", "cards": [{"name": "x"}, {"name": "ข้าว"}, {"name": "-2.5"}, {"name": "0xFF"}]},
    {"name": "Dice", "cards": [{"name": "never"}]},    # a later deck of the same name: the first wins
]


def engine():
    e = Engine()
    e.decks = decks_from(DECKS)
    return e


def texts(e, line, count):
    return [e.evaluate(line).text for _ in range(count)]


def test_draws_by_weight_and_leaves_out_the_unticked():
    e = engine()
    drawn = texts(e, 'card("Food")', 400)
    assert set(drawn) == {"Rice", "Soup"}
    assert 200 < drawn.count("Soup") < 400    # weight 3 of 4
    assert set(texts(e, 'card("food")', 50)) <= {"Rice", "Soup"}    # any case
    assert set(texts(e, 'card(" ลูกเต๋า ")', 100)) == {str(n) for n in range(1, 7)}    # the alias


def test_a_number_card_computes_and_shows_in_history():
    e = engine()
    e.evaluate("x=5")
    result = e.evaluate('card("Dice")*x')
    assert result.kind == "result"
    drawn = int(result.display_input.split("*")[0])
    assert result.text == str(drawn * 5)
    assert result.display_input == f"{drawn}*5"
    assert e.evaluate('2card("Dice")').display_input.startswith("2*")
    # a card named like a variable is shown as the card, not the variable's value
    shown = {e.evaluate('card("Odd")').display_input for _ in range(200)}
    assert shown == {"x", "ข้าว", "(-2.5)", "255"}    # a negative in brackets, as rand()
    assert e.evaluate('card("Food")+1').text == "ERROR:'+' needs a number"


def test_several_cards_make_a_list_and_no_repeat_holds_in_the_call():
    e = engine()
    numbers = e.evaluate('card("Dice", 6, 1)').text
    assert numbers.startswith("{") and numbers.endswith("}")
    assert sorted(numbers[1:-1].split(", ")) == ["1", "2", "3", "4", "5", "6"]
    names = e.evaluate('card("Food", 2, 1)').text
    assert sorted(names.split(", ")) == ["Rice", "Soup"]
    assert len(e.evaluate('card("Food", 5)').text.split(", ")) == 5
    assert e.evaluate('card("Food", 3, 1)').text == "ERROR:card: 'Food' has only 2 cards"
    assert e.evaluate('card("Dice", 1)').text in {str(n) for n in range(1, 7)}


def test_a_seed_repeats_the_cards():
    e = engine()
    e.evaluate("rands(7)")
    first = texts(e, 'card("Dice", 5)', 3)
    e.evaluate("rands(7)")
    assert texts(e, 'card("Dice", 5)', 3) == first


@pytest.mark.parametrize("line, error", [
    ('card("Nope")', "ERROR:card: no deck named 'Nope'"),
    ("card(Food)", 'ERROR:card: the deck name goes in quotes: card("Food")'),
    ('card("Dice", 0)', "ERROR:card: the count must be a whole number from 1 to 1000"),
    ('card("Dice", 2.5)', "ERROR:card: the count must be a whole number from 1 to 1000"),
    ('card("Dice", 2, 1, 1)', "ERROR:card takes at most 3 arguments"),
    ('"Food"+1', 'ERROR:A name in quotes goes only in card("deck")'),
    ('card("Food', "ERROR:Missing closing '\"'"),
])
def test_errors(line, error):
    assert engine().evaluate(line).text == error


def test_curly_quotes_from_a_phone():
    assert engine().evaluate("card(“Food”)").text in {"Rice", "Soup"}


def test_a_quoted_name_keeps_its_spaces():
    e = Engine()
    e.decks = decks_from([{"name": "Deck 1  2", "cards": [{"name": "a"}]}])
    assert e.evaluate('card("Deck 1  2")').text == "a"


def test_protocol_sets_the_decks():
    d = Dispatcher()
    assert d.handle({"op": "eval", "input": 'card("Dice")'})["text"] == "ERROR:card: no deck named 'Dice'"
    assert d.handle({"op": "decks", "decks": DECKS}) == {"id": None, "ok": True, "decks": 3}
    assert d.handle({"op": "eval", "input": 'card("Dice")'})["text"] in {str(n) for n in range(1, 7)}
    assert not d.handle({"op": "decks", "decks": [{"name": "A", "cards": [{"name": "b", "weight": 0}]}]})["ok"]
    assert not d.handle({"op": "decks", "decks": "nope"})["ok"]
    assert "card" in d.handle({"op": "catalog"})["functions"]["Random"]
