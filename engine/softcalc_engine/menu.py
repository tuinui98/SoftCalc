"""softcalc_engine/menu.py - Text the Functions and Conversions menus insert.

SPEC 3 (observed in spec/golden/menu_insert.tsv): operators are inserted
at the caret, functions wrap the whole current input, conversions append
` [from->to]`. Returns the new text and caret position.
"""


def insert_operator(text, caret, symbol):
    new_text = text[:caret] + symbol + text[caret:]
    return new_text, caret + len(symbol)


def insert_function(text, name):
    """`1` becomes `sqrt(1)`; empty becomes `sqrt()` with the caret inside."""
    new_text = f"{name}({text})"
    caret = len(new_text) - 1 if text == "" else len(new_text)
    return new_text, caret


def insert_conversion(text, source, target):
    new_text = f"{text} [{source}->{target}]"
    return new_text, len(new_text)
