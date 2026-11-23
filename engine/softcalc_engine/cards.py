"""softcalc_engine/cards.py - card("Deck", [count], [no repeat]): a draw from a deck in Input (owner 2026-11-24).

The UI hands the engine its decks (server `decks`): the user's own and
the ready-made ones, card names in the language chosen, each card's
weight and whether it was saved to start unticked (the jokers, left
out here). The engine keeps no file of its own. Cards are drawn by
weight from the engine's generator, so `rands(seed)` repeats them.
A card named like a number gives that number; several number cards give
a list `[3, 5, 1]` (matrix.py), and names are joined by ", " when any
card is not a number.
"""

from dataclasses import dataclass, field

import mpmath

from .dates import TextValue
from .errors import CalcError
from .lexer import tokenize
from .matrix import from_numbers
from .random_shapes import below

MAX_COUNT = 1000
MAX_DECKS = 2000
MAX_CARDS = 5000


@dataclass
class Deck:
    name: str
    cards: list = field(default_factory=list)    # (name, weight), the unticked ones left out


def decks_from(items):
    """A deck table from the UI's list: lower-case name (and alias, a ready-made
    deck's name in the other language) -> Deck. A deck found by its alias
    gives the cards' aliases (owner 2026-11-24: a Thai deck name, Thai cards).
    The first deck of a name wins, so the user's own (sent first) come before
    the ready-made ones. Raises ValueError on a malformed list.
    """
    if not isinstance(items, list) or len(items) > MAX_DECKS:
        raise ValueError(f"'decks' must be a list of at most {MAX_DECKS} decks")
    table = {}
    for item in items:
        if not isinstance(item, dict) or not isinstance(item.get("name"), str) or not isinstance(item.get("cards"), list):
            raise ValueError("a deck needs a name and cards")
        cards, aliases = [], []
        for card in item["cards"][:MAX_CARDS]:
            if not isinstance(card, dict) or not isinstance(card.get("name"), str):
                raise ValueError("a card needs a name")
            weight = card.get("weight", 1)
            if not isinstance(weight, int) or isinstance(weight, bool) or weight < 1:
                raise ValueError("a card's weight must be a whole number from 1")
            alias = card.get("alias")
            if card.get("off") is not True:
                cards.append((card["name"], weight))
                aliases.append((alias if isinstance(alias, str) and alias else card["name"], weight))
        names = [item["name"], item.get("alias")]
        for name, deck in zip(names, (Deck(item["name"], cards), Deck(str(names[1]), aliases))):
            if isinstance(name, str) and name.strip():
                table.setdefault(name.strip().lower(), deck)
    return table


def card_value(name):
    """A card named like a number ("10", "-2.5", "0xFF") is that number, else text."""
    try:
        tokens = tokenize(name)[:-1]
    except CalcError:
        return TextValue(name)
    sign = 1
    if len(tokens) == 2 and tokens[0].kind == "op" and tokens[0].text in "+-":
        sign = -1 if tokens[0].text == "-" else 1
        tokens = tokens[1:]
    if len(tokens) != 1 or tokens[0].kind != "number":
        return TextValue(name)
    token = tokens[0]
    value = mpmath.mpf(token.text) if token.base == 10 else mpmath.mpf(int(token.text, token.base))
    return sign * value


def draw(source, decks, name, count, unique, format_value):
    """count cards from the deck called name; unique: no card twice in this call.
    One card gives its value; several give a list (see the module notes).
    """
    deck = decks.get(name.strip().lower())
    if deck is None:
        raise CalcError(f"card: no deck named '{name}'")
    pool = list(deck.cards)
    if not pool:
        raise CalcError(f"card: '{deck.name}' has no cards to draw")
    if unique and count > len(pool):
        raise CalcError(f"card: '{deck.name}' has only {len(pool)} cards")
    picked = []
    for _ in range(count):
        at = below(source, sum(weight for _, weight in pool))
        for index, (card, weight) in enumerate(pool):
            if at < weight:
                picked.append(card)
                if unique:
                    del pool[index]
                break
            at -= weight
    values = [card_value(card) for card in picked]
    if count == 1:
        return values[0]
    if all(isinstance(value, mpmath.mpf) for value in values):
        return from_numbers(values)    # a list: it computes on (matrix_plan.md X1)
    return TextValue(", ".join(picked))
