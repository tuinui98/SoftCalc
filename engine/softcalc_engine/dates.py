"""softcalc_engine/dates.py - Date and date-time values (extensions.md E7).

Literals `dd.mm.yyyy` and `dd.mm.yyyy hh:mm[:ss]`, local time, Gregorian
only. `date - date` gives days, `date +/- n` shifts by n days. Text
results (long formats) are TextValue and cannot be used in arithmetic.
"""

import re
from dataclasses import dataclass
from datetime import datetime, timedelta
from fractions import Fraction

import mpmath

from .errors import CalcError

DATE_LITERAL = re.compile(r"(\d{1,2})\.(\d{1,2})\.(\d{4})(?:\s+(\d{1,2}):(\d{2})(?::(\d{2}))?)?")
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


def today():
    now = datetime.now()
    return DateValue(datetime(now.year, now.month, now.day), False)


def days_between(a, b):
    """a - b in days as an exact mpf."""
    delta = a.moment - b.moment
    micro = delta.days * SECONDS_PER_DAY * 10**6 + delta.seconds * 10**6 + delta.microseconds
    frac = Fraction(micro, SECONDS_PER_DAY * 10**6)
    return mpmath.mpf(frac.numerator) / frac.denominator


def shift(date, days):
    """date + days; a fractional day adds a time of day."""
    if isinstance(days, mpmath.mpc):
        raise CalcError("Cannot add a complex value to a date")
    seconds = mpmath.nint(days * SECONDS_PER_DAY)
    try:
        moment = date.moment + timedelta(seconds=int(seconds))
    except OverflowError as exc:
        raise CalcError("Date out of range") from exc
    has_time = date.has_time or moment.time() != datetime.min.time()
    return DateValue(moment, has_time)


def date_binary(op, a, b):
    """Arithmetic where at least one side is a DateValue."""
    a_date, b_date = isinstance(a, DateValue), isinstance(b, DateValue)
    if op == "-" and a_date and b_date:
        return days_between(a, b)
    if op == "+" and a_date and not b_date:
        return shift(a, b)
    if op == "+" and b_date and not a_date:
        return shift(b, a)
    if op == "-" and a_date and not b_date:
        return shift(a, -b)
    raise CalcError(f"'{op}' is not defined for dates")


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
