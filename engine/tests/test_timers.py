"""test_timers.py - Stopwatch, timer and alarm lines (owner 2026-11-27/28; timers.py)."""

from datetime import datetime

import pytest

from softcalc_engine import timers
from softcalc_engine.engine import Engine
from softcalc_engine.errors import CalcError
from softcalc_engine.server import Dispatcher

NOW = datetime(2026, 11, 28, 14, 0, 0)


def action(line, now=NOW):
    engine = Engine()
    return timers.parse(line, engine.value_of, clock=lambda: now)


@pytest.mark.parametrize("line, does", [
    (":sw", "toggle"), (":stopwatch", "toggle"), (":SW", "toggle"), (":sw pause", "pause"), (":sw go", "go"),
    (":sw start", "start"), (":sw stop", "stop"), (":sw reset", "reset"), (":sw lap", "lap"), (":lap", "lap"),
    (":sw last", "last"),
])
def test_stopwatch_lines(line, does):
    assert action(line)[0] == {"kind": "stopwatch", "do": does}


@pytest.mark.parametrize("line, seconds, label", [
    (":timer 5m", 300, "5m"), (":timer 90s", 90, "90s"), (":timer 1h30m", 5400, "1h30m"),
    (":timer 1.5m", 90, "1.5m"), (":timer 2min", 120, "2min"), (":timer 1h 5s", 3605, "1h5s"),
    (":timer 0:05:00", 300, "0:05:00"), (":timer 0:05*3", 900, "0:15:00"),
])
def test_timer_lengths(line, seconds, label):
    got, said = action(line)
    assert (got["kind"], got["do"], got["seconds"], got["label"]) == ("timer", "start", seconds, label)
    assert said.startswith(f"Timer {label}, done at ")


def test_an_alarm_15_minutes_on_is_a_15_minute_timer():
    """Owner 2026-11-28: `:alarm now+0:15:0` is the same as `:timer 15m`."""
    alarm, timer = action(":alarm now+0:15:0", datetime.now()), action(":timer 15m", datetime.now())
    assert abs(alarm[0]["seconds"] - timer[0]["seconds"]) <= 1
    assert abs(alarm[0]["seconds"] - 900) <= 1
    date_timer = action(":timer now+0:15:0", datetime.now())[0]    # a date: the time until it
    assert abs(date_timer["seconds"] - 900) <= 1 and date_timer["label"] in ("0:15:00", "0:14:59", "0:15:01")


@pytest.mark.parametrize("line, at", [
    (":alarm 14:30", "28.11.2026 14:30:00"),        # later today
    (":alarm 9:00", "29.11.2026 09:00:00"),         # passed today: tomorrow's
    (":alarm 30.11.2026 7:00", "30.11.2026 07:00:00"),
    (":alarm today+1+7:00", "29.11.2026 07:00:00"),
])
def test_alarm_times(line, at):
    engine = Engine()
    got, said = timers.parse(line, lambda text: engine.value_of(text.replace("today", "28.11.2026")),
                             clock=lambda: NOW)
    assert (got["kind"], got["at"]) == ("alarm", at)
    assert said.startswith(f"Alarm {at}, in ")


def test_names_show_and_off():
    got, said = action(':timer 5m "tea"')
    assert (got["name"], said.startswith('Timer 5m "tea"')) == ("tea", True)
    assert action(":timer")[0] == {"kind": "timer", "do": "show", "name": None}
    assert action(":alarm off")[0] == {"kind": "alarm", "do": "off", "name": None}
    assert action(':alarm off "wake"') == ({"kind": "alarm", "do": "off", "name": "wake"}, 'Alarm "wake" off')


@pytest.mark.parametrize("line, error", [
    (":timer 90", "A timer needs a length, not a number"),
    (":alarm 90", "An alarm needs a time or a date, not a number"),
    (":timer -0:05", "The timer is already past"),
    (":alarm 27.11.2026 7:00", "The alarm is already past"),
    (":alarm 25:00", "A time of day is from 0:00 to 23:59:59"),
    (":timer 400*24:00", "more than a year away"),
    (":timer x", "Unknown name 'x'"),
    (':timer 5m ""', "The name in quotes is empty"),
    (":sw bogus", "Unknown ':sw bogus'"),
    (":lap 2", ":lap takes nothing after it"),
    (":timer y=2", "not a definition"),
])
def test_errors(line, error):
    with pytest.raises(CalcError, match=error.replace("(", r"\(")):
        action(line)


def test_eval_sends_the_action_and_keeps_no_value():
    dispatcher = Dispatcher()
    dispatcher.handle({"op": "eval", "input": "7"})
    reply = dispatcher.handle({"op": "eval", "input": ":timer 0:01*2"})
    assert (reply["kind"], reply["timer"]["seconds"]) == ("definition", 120)
    assert dispatcher.handle({"op": "eval", "input": "answer"})["text"] == "7"    # not the timer's 0:02:00
    assert "timer" not in dispatcher.handle({"op": "eval", "input": ":deg"})


def test_record_keeps_answer_and_shows_the_label():
    dispatcher = Dispatcher()
    reply = dispatcher.handle({"op": "record", "label": "Lap 2", "value": "0:00:41.2", "note": "total 0:01:23.4"})
    assert (reply["kind"], reply["input"], reply["text"], reply["display_input"]) == \
        ("result", "0:00:41.2", "0:00:41.2", "Lap 2, total 0:01:23.4")
    assert dispatcher.handle({"op": "eval", "input": "answer*2"})["text"] == "0:01:22.4"
    assert dispatcher.handle({"op": "eval", "input": "$1/2"})["text"] == "0:00:41.2"


def test_hints():
    dispatcher = Dispatcher()
    for text, starts in ((":timer 5", "timer:"), (":alarm ", "alarm:"), (":sw ", "stopwatch:"), (":deg", ":deg")):
        hint = dispatcher.handle({"op": "hint", "text": text, "caret": len(text)})["text"]
        assert hint.startswith(starts) and (starts == ":deg" or len(hint) <= 82), hint


@pytest.mark.parametrize("line, kind", [(":sw *", "stopwatch"), (":stopwatch *", "stopwatch"), (":lap *", "stopwatch"),
                                        (":timer *", "timer"), (":alarm  * ", "alarm")])
def test_star_lists_all_three(line, kind):
    """Owner 2026-11-28: * after any of them lists the stopwatch, timers and alarms together."""
    assert action(line)[0] == {"kind": kind, "do": "list"}
