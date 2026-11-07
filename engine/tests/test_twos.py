"""tests/test_twos.py - twos() / signed() and the word size setting (owner 2026-11-07)."""

import pytest

from softcalc_engine.engine import Engine
from softcalc_engine.server import Dispatcher


def run(*lines, **settings):
    engine = Engine()
    for key, value in settings.items():
        setattr(engine.settings, key, value)
    result = None
    for line in lines:
        result = engine.evaluate(line)
    return result


@pytest.mark.parametrize("line, want", [
    ("twos(-1, 8)", "255"),
    ("twos(-2, 16)", "65534"),
    ("twos(5, 8)", "5"),
    ("twos(300, 8)", "44"),          # wraps like hardware
    ("signed(0xFF, 8)", "-1"),
    ("signed(0x80, 8)", "-128"),
    ("signed(0x7F, 8)", "127"),
    ("signed(0x1FF, 8)", "-1"),
    ("twos(-1, 12)", "4095"),        # any width 1-1024
    ("twos(-1, 1)", "1"),
    ("twos(-1)", "4294967295"),      # the word size setting, 32 by default
    ("signed(0xFFFFFFFF)", "-1"),
    ("twos(-1, 64)", "18446744073709551615"),
])
def test_values(line, want):
    assert run(line).text == want


@pytest.mark.parametrize("line", ["twos(1.5, 8)", "twos(-1, 0)", "twos(-1, 1025)", "twos(-1, 8.5)",
                                  "twos(1+2i, 8)", "twos(-1, 8, 1)", "signed(today, 8)"])
def test_errors(line):
    assert run(line).kind == "error"


def test_word_size_setting():
    assert run("twos(-1)", word_size=16).text == "65535"
    assert run("signed(0x8000)", word_size=16).text == "-32768"


@pytest.mark.parametrize("lines, settings, want", [
    (["twos(-1)"], {}, "twos(-1, 32) = 0xFFFFFFFF"),          # bits filled in
    (["twos(-1, 8)"], {}, "twos(-1, 8) = 0xFF"),
    (["twos(-1, 8) + 1"], {}, "twos(-1, 8) + 1 = 0x100"),     # the whole line in hex
    (["twos(-1)"], {"word_size": 16}, "twos(-1, 16) = 0xFFFF"),
    (["twos(signed(0xFF), 8)"], {}, "twos(signed(255, 32), 8) = 0xFF"),
    (["twos(-1)"], {"base": "hex"}, "twos(-0x1, 0x20)"),       # hex output: no second hex
    (["-1 twos"], {"rpn": True}, "-1 twos = 0xFFFFFFFF"),
])
def test_history_display(lines, settings, want):
    assert run(*lines, **settings).display_input == want


def test_word_size_over_protocol():
    dispatcher = Dispatcher()
    reply = dispatcher.handle({"op": "settings", "word_size": 8})
    assert reply["ok"] and reply["word_size"] == 8
    assert dispatcher.handle({"op": "eval", "input": "twos(-1)"})["text"] == "255"
    assert dispatcher.handle({"op": "settings", "word_size": 12})["ok"] is False    # the menu offers 8/16/32/64
    assert "twos" in dispatcher.handle({"op": "catalog"})["functions"]["Two's Complement"]
