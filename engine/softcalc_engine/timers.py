"""timers.py - Stopwatch, timer and alarm lines typed in Input (owner 2026-11-27/28).

`:sw` / `:stopwatch` (start or stop; `pause`, `go`, `reset`, `lap`), `:lap`,
`:timer 5m`, `:timer 1h30m "tea"`, `:timer off`, `:alarm 14:30`,
`:alarm now+0:15:0`, `:alarm off "tea"`, and `*` after any of them (`:sw *`,
`:timer *`) to list the stopwatch, the timers and the alarms all together.
A name already in use sets that timer's time again (the UI's choice).
The engine only reads the line:
the UI keeps the clock (design/TODO.md, Stopwatch, timer and alarm), so a
line gives an action for the UI, sent with the eval reply as "timer".

A timer or alarm takes a calculation (owner 2026-11-28): `:timer 0:05*3`,
`:alarm today+1+7:00`. `:alarm now+0:15:0` is the same as `:timer 15m`:
both give the seconds from now. A timer takes `5m`, `90s`, `1h30m` too
(`5:00` would be five hours as a duration), a duration, or a date (the
time until it); a plain number is refused (days or seconds?). An alarm
takes a date with its time, or a duration under 24 hours as a time of day
(`:alarm 14:30`: the next 14:30). A name in quotes may follow.
"""

import re
from datetime import datetime, timedelta

import mpmath

from .dates import DateValue, DurationValue
from .errors import CalcError

STOPWATCH = ("sw", "stopwatch")
LAP = ("lap",)
TIMER = ("timer",)
ALARM = ("alarm",)
WORDS = STOPWATCH + LAP + TIMER + ALARM
STOPWATCH_DOES = ("start", "stop", "pause", "go", "reset", "lap", "last")    # last: the last run (owner 2026-11-28)
OFF = ("off", "cancel")
MAX_SECONDS = 366 * 86400    # a year and a day: longer is a slip
SHORT = re.compile(r"^\s*(?:(\d+(?:\.\d+)?)\s*h)?\s*(?:(\d+(?:\.\d+)?)\s*m(?:in)?)?\s*(?:(\d+(?:\.\d+)?)\s*s)?\s*$",
                   re.IGNORECASE)
NAMED = re.compile(r'^(.*?)\s*"([^"]*)"\s*$', re.DOTALL)
TIMER_USAGE = 'write :timer 5m, 90s, 1h30m, 0:05:00 or a sum giving one (0:05*3), a name in quotes after it'
ALARM_USAGE = 'write :alarm 14:30, 29.11.2026 7:00 or now+0:15, a name in quotes after it'
HINT = ':sw (stopwatch)  :lap  :timer 5m "name"  :alarm 14:30  (off cancels each)'


def first_word(text):
    """The word after ':' in lower case, or ''."""
    words = text.strip()[1:].split()
    return words[0].lower() if words else ""


def is_timer(text):
    """A stopwatch, timer or alarm line (otherwise a settings line)."""
    return first_word(text) in WORDS


def parse(text, value_of, clock=datetime.now):
    """(action, said) for a line; value_of(expression) evaluates a calculation.

    action is the dict sent to the UI; said is the History message. Raises
    CalcError naming what is wrong.
    """
    word = first_word(text)
    rest = text.strip()[1:].strip()[len(word):].strip()
    if rest == "*":    # owner 2026-11-28: any of them with * lists all three
        kind = "stopwatch" if word in STOPWATCH + LAP else word
        return {"kind": kind, "do": "list"}, "Stopwatch, timers and alarms"
    if word in STOPWATCH + LAP:
        does = rest.lower() if rest else ("lap" if word in LAP else "toggle")
        if word in LAP and rest:
            raise CalcError(":lap takes nothing after it")
        if does not in STOPWATCH_DOES + ("toggle",):
            raise CalcError(f"Unknown ':sw {rest}'; :sw alone starts or stops, or :sw {', '.join(STOPWATCH_DOES)}")
        return {"kind": "stopwatch", "do": does}, "Stopwatch"
    kind = "timer" if word in TIMER else "alarm"
    rest, name = named(rest)
    if rest == "":
        return {"kind": kind, "do": "show", "name": name}, kind.capitalize() + "s"
    if rest.lower() in OFF:
        return {"kind": kind, "do": "off", "name": name}, (f'{kind.capitalize()} "{name}" off' if name
                                                          else f"{kind.capitalize()}s off")
    now = clock().replace(microsecond=0)    # as `now` reads it, so now+0:15 is 900 s
    if kind == "timer":
        seconds, label = timer_seconds(rest, value_of, now)
        at = now + timedelta(seconds=seconds)
    else:
        at, label = alarm_time(rest, value_of, now)
        seconds = (at - now).total_seconds()
    if seconds <= 0:
        raise CalcError(f"The {kind} is already past ({label})")
    if seconds > MAX_SECONDS:
        raise CalcError(f"The {kind} is more than a year away ({label})")
    action = {"kind": kind, "do": "start", "seconds": seconds, "at": at.strftime("%d.%m.%Y %H:%M:%S"),
              "label": label, "name": name}
    quoted = f' "{name}"' if name else ""
    if kind == "timer":
        said = f"Timer {label}{quoted}, done at {at.strftime('%H:%M:%S')}"
    else:
        said = f"Alarm {at.strftime('%d.%m.%Y %H:%M:%S')}{quoted}, in {span(seconds)}"
    return action, said


def span(seconds):
    """0:15:00 for 900 seconds."""
    return DurationValue(mpmath.mpf(seconds)).text()


def named(rest):
    """(rest, name): a name in quotes at the end comes off."""
    match = NAMED.match(rest)
    if match is None:
        return rest, None
    name = match.group(2).strip()
    if name == "":
        raise CalcError("The name in quotes is empty")
    return match.group(1).strip(), name


def timer_seconds(rest, value_of, now):
    """(seconds, label) for a timer's length."""
    short = SHORT.match(rest)
    if short and any(short.groups()):
        hours, minutes, seconds = (float(part) if part else 0.0 for part in short.groups())
        return hours * 3600 + minutes * 60 + seconds, re.sub(r"\s+", "", rest.lower())
    value = calculated(rest, value_of, TIMER_USAGE)
    if isinstance(value, DurationValue):
        return float(value.seconds), value.text()
    if isinstance(value, DateValue):
        seconds = (value.moment - now).total_seconds()
        return seconds, span(seconds)    # now+0:15 is a 0:15:00 timer
    raise CalcError(f"A timer needs a length, not a number; {TIMER_USAGE}")


def alarm_time(rest, value_of, now):
    """(moment, label) for an alarm: a date, or a time of day (the next one)."""
    value = calculated(rest, value_of, ALARM_USAGE)
    if isinstance(value, DateValue):
        return value.moment, value.text()
    if isinstance(value, DurationValue):
        seconds = float(value.seconds)
        if not 0 <= seconds < 86400:
            raise CalcError(f"A time of day is from 0:00 to 23:59:59; {ALARM_USAGE}")
        at = now.replace(hour=0, minute=0, second=0) + timedelta(seconds=seconds)
        if at <= now:
            at += timedelta(days=1)    # 14:30 after half past two: tomorrow's
        return at, value.text()
    raise CalcError(f"An alarm needs a time or a date, not a number; {ALARM_USAGE}")


def calculated(rest, value_of, usage):
    try:
        return value_of(rest)
    except CalcError as exc:
        raise CalcError(f"{exc}; {usage}") from None


def hint(left):
    """The hint line for a stopwatch, timer or alarm line."""
    word = first_word(left)
    if word in TIMER:
        return 'timer: :timer 5m, 90s, 0:05*3, now+0:15; "name" sets it again; off; * all'
    if word in ALARM:
        return 'alarm: :alarm 14:30, 29.11.2026 7:00, now+0:15; "name" sets it again; off; * all'
    if word in STOPWATCH + LAP:
        return "stopwatch: :sw starts or stops; :sw pause, go, lap, reset, last; * lists all; F8"
    return HINT
