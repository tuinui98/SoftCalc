"""tests/test_commands.py - Settings typed in Input, ':' lines (owner 2026-11-26)."""

import pytest

from softcalc_engine import commands, hints
from softcalc_engine.server import Dispatcher


def run(dispatcher, line):
    return dispatcher.handle({"op": "eval", "input": line})


@pytest.fixture
def dispatcher():
    return Dispatcher()


def test_a_command_changes_settings_and_sends_them(dispatcher):
    reply = run(dispatcher, ":deg :prec 64")
    assert (reply["kind"], reply["text"]) == ("definition", "Degrees, Precision 64")
    assert reply["settings"]["angle"] == "deg" and reply["settings"]["precision"] == 64
    assert run(dispatcher, "sin(90)")["text"] == "1"
    assert dispatcher.handle({"op": "settings"})["precision"] == 64


@pytest.mark.parametrize("line, name, value", [
    (":rad", "angle", "rad"), (":radians", "angle", "rad"), (":grad", "angle", "grad"),
    (":angle deg", "angle", "deg"), (":DEGREES", "angle", "deg"),
    (":hex", "base", "hex"), (":binary", "base", "bin"), (":base oct", "base", "oct"),
    (":prec 128", "precision", 128), (":precision=512", "precision", 512),
    (":digits 2", "decimals", 2), (":decimals 0", "decimals", 0), (":places all", "decimals", None),
    (":pad", "pad_zeros", True), (":pad off", "pad_zeros", False), (":padzeros on", "pad_zeros", True),
    (":rpn", "rpn", True), (":rpn off", "rpn", False), (":infix", "rpn", False),
    (":word 16", "word_size", 16), (":wordsize 64", "word_size", 64), ("  :word_size 8", "word_size", 8),
])
def test_short_and_full_names(dispatcher, line, name, value):
    assert run(dispatcher, line)["settings"][name] == value


def test_a_bare_colon_shows_them_all_and_changes_nothing(dispatcher):
    reply = run(dispatcher, ":")
    assert reply["text"] == ":prec 32 :rad :dec :rpn off :digits all :pad off :word 32"
    assert "settings" not in reply


def test_a_bare_colon_gives_the_line_that_sets_them_again(dispatcher):
    run(dispatcher, ":grad :hex :prec 128 :digits 3 :pad on :word 8")
    shown = run(dispatcher, ":")["text"]
    other = Dispatcher()
    run(other, shown)
    assert run(other, ":")["text"] == shown


@pytest.mark.parametrize("line, error", [
    (":prec 100", "Precision is 32, 64, 128 or 512"),
    (":word 12", "The word size is 8, 16, 32 or 64"),
    (":digits 600", "Decimal places are 0 to 512, or all"),
    (":digits", "':digits' needs a value"),
    (":angle north", "The angle is deg, rad or grad"),
    (":foo", "Unknown setting 'foo'"),
])
def test_a_wrong_command_changes_nothing(dispatcher, line, error):
    reply = run(dispatcher, ":deg " + line)
    assert reply["kind"] == "error" and reply["text"].startswith("ERROR:" + error)
    assert "settings" not in reply
    assert dispatcher.handle({"op": "settings"})["angle"] == "rad"    # all or none


def test_a_command_is_no_result(dispatcher):
    run(dispatcher, "6*7")
    run(dispatcher, ":hex")
    assert run(dispatcher, "$1")["text"] == "0x2A"
    assert run(dispatcher, "ans")["text"] == "0x2A"


def test_rpn_mode_still_reads_commands(dispatcher):
    run(dispatcher, ":rpn")
    assert run(dispatcher, "2 3 +")["text"] == "5"
    assert run(dispatcher, ":rpn off")["settings"]["rpn"] is False
    assert run(dispatcher, "2+3")["text"] == "5"


def test_a_colon_inside_a_line_is_not_a_command(dispatcher):
    assert run(dispatcher, "1:30")["kind"] == "result"


def test_hints():
    def hint(text):
        return hints.hint_for(text, len(text), lambda name: None)
    assert hint(":") == commands.SHORT_HINT
    assert hint(":deg :prec ") == ":prec - 32 64 128 512"
    assert hint(":digits=").startswith(":digits - 0 to 512")
    assert hint(":rpn ") == ":rpn - on off"
