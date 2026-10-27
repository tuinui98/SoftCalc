"""softcalc_engine/dates.py - Date, date-time and duration values (extensions.md E7).

Literals `dd.mm.yyyy` and `dd.mm.yyyy hh:mm[:ss]`, local time, Gregorian
only. `date - date` gives days, `date +/- n` shifts by n days. Duration
literals `h:mm[:ss]` (hours may exceed 24) shift dates and combine with
each other and with plain numbers as factors. Text results (long
formats) are TextValue and cannot be used in arithmetic.
"""

import re
from dataclasses import dataclass
from datetime import datetime, timedelta
from fractions import Fraction

import mpmath

from .errors import CalcError

DATE_LITERAL = re.compile(r"(\d{1,2})\.(\d{1,2})\.(\d{4})(?:\s+(\d{1,2}):(\d{2})(?::(\d{2}))?)?")
DURATION_LITERAL = re.compile(r"(\d+):(\d{1,2})(?::(\d{1,2}(?:\.\d+)?))?")
SECONDS_PER_DAY = 86400
EPOCH = datetime(1970, 1, 1)


@dataclass(frozen=True)
class DateValue:
    """A local date, with or without a time of day."""

    moment: datetime
    has_time: bool

    def text(self):
        if self.has_time:
            return self.moment.strftime("%d.%m.%Y %H:%M:%S")
        return self.moment.strftime("%d.%m.%Y")


@dataclass(frozen=True)
class DurationValue:
    """A length of time in seconds (an mpf, may be negative or fractional)."""

    seconds: object

    def text(self):
        """`h:mm:ss`, hours not wrapped at 24; fractional seconds kept."""
        total = self.seconds
        sign = "-" if total < 0 else ""
        total = abs(total)
        hours = int(mpmath.floor(total / 3600))
        minutes = int(mpmath.floor((total - hours * 3600) / 60))
        seconds = total - hours * 3600 - minutes * 60
        whole = int(mpmath.floor(seconds))
        text = f"{sign}{hours}:{minutes:02d}:{whole:02d}"
        fraction = seconds - whole
        if fraction:
            digits = mpmath.nstr(fraction, 9, min_fixed=-30, max_fixed=30)
            text += digits.lstrip("0").rstrip("0").rstrip(".")
        return text


@dataclass(frozen=True)
class TextValue:
    """A text result such as a long date; shown as is, not computable."""

    text: str


def parse_literal(match):
    """DateValue from a DATE_LITERAL match; invalid dates are errors."""
    day, month, year, hour, minute, second = match.groups()
    try:
        moment = datetime(int(year), int(month), int(day),
                          int(hour or 0), int(minute or 0), int(second or 0))
    except ValueError as exc:
        raise CalcError(f"Invalid date '{match.group(0)}'") from exc
    return DateValue(moment, hour is not None)


def parse_duration(match):
    """DurationValue from a DURATION_LITERAL match; mm and ss must be < 60."""
    hours, minutes, seconds = match.groups()
    seconds_value = mpmath.mpf(seconds or 0)
    if int(minutes) >= 60 or seconds_value >= 60:
        raise CalcError(f"Invalid duration '{match.group(0)}'")
    return DurationValue(int(hours) * 3600 + int(minutes) * 60 + seconds_value)


def now():
    """Current local date and time, to the second."""
    return DateValue(datetime.now().replace(microsecond=0), True)


def today():
    now = datetime.now()
    return DateValue(datetime(now.year, now.month, now.day), False)


def days_between(a, b):
    """a - b in days as an exact mpf."""
    delta = a.moment - b.moment
    micro = delta.days * SECONDS_PER_DAY * 10**6 + delta.seconds * 10**6 + delta.microseconds
    frac = Fraction(micro, SECONDS_PER_DAY * 10**6)
    return mpmath.mpf(frac.numerator) / frac.denominator


def shift_seconds(date, seconds):
    """date + seconds (rounded to whole seconds); adds a time of day if needed."""
    try:
        moment = date.moment + timedelta(seconds=int(mpmath.nint(seconds)))
    except OverflowError as exc:
        raise CalcError("Date out of range") from exc
    has_time = date.has_time or moment.time() != datetime.min.time()
    return DateValue(moment, has_time)


def shift(date, days):
    """date + days; a fractional day adds a time of day."""
    if isinstance(days, mpmath.mpc):
        raise CalcError("Cannot add a complex value to a date")
    return shift_seconds(date, days * SECONDS_PER_DAY)


def is_plain_real(value):
    return isinstance(value, mpmath.mpf)


def date_binary(op, a, b):
    """Arithmetic where at least one side is a DateValue or DurationValue."""
    a_date, b_date = isinstance(a, DateValue), isinstance(b, DateValue)
    a_dur, b_dur = isinstance(a, DurationValue), isinstance(b, DurationValue)

    #==================================================================
    #  BLOCK 1 - Dates : date-date, date+-number, date+-duration
    #==================================================================
    if a_date or b_date:
        if op == "-" and a_date and b_date:
            return days_between(a, b)
        if op in "+-" and a_date and b_dur:
            return shift_seconds(a, b.seconds if op == "+" else -b.seconds)
        if op == "+" and a_dur and b_date:
            return shift_seconds(b, a.seconds)
        if op == "+" and a_date and is_plain_real(b):
            return shift(a, b)
        if op == "+" and b_date and is_plain_real(a):
            return shift(b, a)
        if op == "-" and a_date and is_plain_real(b):
            return shift(a, -b)
        raise CalcError(f"'{op}' is not defined for these dates")
    # end BLOCK 1

    #==================================================================
    #  BLOCK 2 - Durations : with durations, scaled by real numbers
    #==================================================================
    if a_dur and b_dur:
        if op == "+":
            return DurationValue(a.seconds + b.seconds)
        if op == "-":
            return DurationValue(a.seconds - b.seconds)
        if op == "/":
            if b.seconds == 0:
                raise CalcError("Division by zero")
            return a.seconds / b.seconds
        raise CalcError(f"'{op}' is not defined for two durations")
    if op == "*" and a_dur and is_plain_real(b):
        return DurationValue(a.seconds * b)
    if op == "*" and b_dur and is_plain_real(a):
        return DurationValue(a * b.seconds)
    if op == "/" and a_dur and is_plain_real(b):
        if b == 0:
            raise CalcError("Division by zero")
        return DurationValue(a.seconds / b)
    raise CalcError(f"'{op}' is not defined for a duration and this value")
    # end BLOCK 2


#----------------------------------------------------------------------
#  Built-in date functions
#----------------------------------------------------------------------
def as_date(value, name):
    """Accept a DateValue, or a number taken as Unix seconds."""
    if isinstance(value, DateValue):
        return value
    if isinstance(value, (TextValue, mpmath.mpc)):
        raise CalcError(f"{name} needs a date")
    return from_epoch(value)


def local_offset():
    """Current UTC offset of the local zone.

    Computed once from 'now' instead of per date: Windows refuses
    datetime.timestamp() for dates near or before 1970, and the zones
    this is built for have no daylight saving. [DEF]
    """
    return datetime.now().astimezone().utcoffset()


ZONES = ("utc", "local")


def convert_zone(value, source, target):
    """`[utc->local]` / `[local->utc]` on a date or date-time [EXT].

    Uses the current local UTC offset, like epoch().
    """
    if not isinstance(value, DateValue):
        raise CalcError(f"[{source}->{target}] needs a date")
    if source not in ZONES or target not in ZONES:
        raise CalcError(f"Cannot convert {source} to {target}")
    if source == target:
        return value
    offset = local_offset()
    moment = value.moment + offset if source == "utc" else value.moment - offset
    return DateValue(moment, True)


def to_epoch(value):
    if not isinstance(value, DateValue):
        raise CalcError("epoch needs a date")
    delta = value.moment - local_offset() - EPOCH
    return mpmath.mpf(delta.days * SECONDS_PER_DAY + delta.seconds)


def from_epoch(value):
    if isinstance(value, (DateValue, TextValue, mpmath.mpc)):
        raise CalcError("fromepoch needs a number of seconds")
    try:
        moment = EPOCH + local_offset() + timedelta(seconds=int(mpmath.nint(value)))
    except (OverflowError, ValueError) as exc:
        raise CalcError("Date out of range") from exc
    return DateValue(moment, True)


def long_date(value):
    moment = as_date(value, "longdate").moment
    return TextValue(f"{moment:%A}, {moment.day} {moment:%B %Y}")


def long_time(value):
    return TextValue(f"{as_date(value, 'longtime').moment:%H:%M:%S}")


def long_datetime(value):
    moment = as_date(value, "longdatetime").moment
    return TextValue(f"{moment:%A}, {moment.day} {moment:%B %Y %H:%M:%S}")


FUNCTIONS = {
    "epoch": to_epoch,
    "fromepoch": from_epoch,
    "longdate": long_date,
    "longtime": long_time,
    "longdatetime": long_datetime,
}
