"""softcalc_engine/finance.py - Time value of money (extensions.md E11).

Input-box functions use Excel semantics and sign convention: money paid
out is negative, money received positive; `rate` is per period; `kind`
0 = payment at period end, 1 = at period start.

    fv(rate, nper, pmt [, pv [, kind]])     pv(rate, nper, pmt [, fv [, kind]])
    pmt(rate, nper, pv [, fv [, kind]])     nper(rate, pmt, pv [, fv [, kind]])

schedule() backs the Compound Interest tool window: totals plus a
period-by-period table.
"""

from dataclasses import dataclass

import mpmath

from .errors import CalcError

COMPOUNDING = {"yearly": 1, "quarterly": 4, "monthly": 12, "daily": 365}
MAX_PERIODS = 100000    # table rows; 270 years of daily periods
PERIOD_NAMES = {"yearly": "year", "quarterly": "quarter", "monthly": "month", "daily": "day"}


def real_args(name, args):
    """Return args as mpf; complex numbers, dates and text are rejected."""
    out = []
    for arg in args:
        if isinstance(arg, int):
            arg = mpmath.mpf(arg)
        if not isinstance(arg, mpmath.mpf):
            raise CalcError(f"{name} needs real numbers")
        out.append(arg)
    return out


def growth(rate, nper):
    return mpmath.power(1 + rate, nper)


def fv(rate, nper, pmt, pv=0, kind=0):
    rate, nper, pmt, pv, kind = real_args("fv", [rate, nper, pmt, pv, kind])
    if rate == 0:
        return -(pv + pmt * nper)
    g = growth(rate, nper)
    return -(pv * g + pmt * (1 + rate * kind) * (g - 1) / rate)


def pv(rate, nper, pmt, fv_value=0, kind=0):
    rate, nper, pmt, fv_value, kind = real_args("pv", [rate, nper, pmt, fv_value, kind])
    if rate == 0:
        return -(fv_value + pmt * nper)
    g = growth(rate, nper)
    return -(fv_value + pmt * (1 + rate * kind) * (g - 1) / rate) / g


def pmt(rate, nper, pv_value, fv_value=0, kind=0):
    rate, nper, pv_value, fv_value, kind = real_args(
        "pmt", [rate, nper, pv_value, fv_value, kind])
    if nper == 0:
        raise CalcError("pmt needs nper > 0")
    if rate == 0:
        return -(pv_value + fv_value) / nper
    g = growth(rate, nper)
    return -(fv_value + pv_value * g) * rate / ((1 + rate * kind) * (g - 1))


def nper(rate, pmt_value, pv_value, fv_value=0, kind=0):
    rate, pmt_value, pv_value, fv_value, kind = real_args(
        "nper", [rate, pmt_value, pv_value, fv_value, kind])
    if rate == 0:
        if pmt_value == 0:
            raise CalcError("nper needs pmt or rate")
        return -(pv_value + fv_value) / pmt_value
    a = pmt_value * (1 + rate * kind)
    ratio = (a - fv_value * rate) / (a + pv_value * rate)
    if ratio <= 0:
        raise CalcError("nper has no solution for these values")
    return mpmath.ln(ratio) / mpmath.ln(1 + rate)


# name -> (function, min args, max args)
FUNCTIONS = {
    "fv": (fv, 3, 5),
    "pv": (pv, 3, 5),
    "pmt": (pmt, 3, 5),
    "nper": (nper, 3, 5),
}


#----------------------------------------------------------------------
#  Compound Interest tool
#----------------------------------------------------------------------
@dataclass
class Period:
    """One row of the schedule table."""

    index: int
    start: object
    interest: object
    contribution: object
    end: object


@dataclass
class Schedule:
    future_value: object
    total_contributions: object
    total_interest: object
    rows: list
    periods_per_year: int = 1
    periods_per_unit: object = 1    # rows per length unit (the chart's x axis)


# How many of each fit in a year (owner 2026-11-28: a year is 365 days,
# a month a twelfth of it, a week 7 days)
RATE_UNITS = {"yearly": 1, "monthly": 12, "weekly": "365/7", "daily": 365}
LENGTH_UNITS = {"years": 1, "months": 12, "weeks": "365/7", "days": 365}


def per_year(table, unit):
    """How many of `unit` fit in a year, as mpf."""
    value = table[unit]
    if isinstance(value, str):
        top, bottom = value.split("/")
        return mpmath.mpf(top) / int(bottom)
    return mpmath.mpf(value)


def period_rate(rate, compounding, periods_per_year, rate_unit="yearly"):
    """Effective rate for one schedule period.

    rate is a fraction (0.05 for 5%) per rate_unit. compounding is a key
    of COMPOUNDING or "continuous". Compounded more often than the rate's
    unit, the rate is split evenly (5% a year monthly: 5/12% a month, as
    before); less often, it compounds over the longer period (1% a day
    monthly: 1.01^(365/12) - 1, owner 2026-11-28, not 1% x 30.4).
    """
    units = per_year(RATE_UNITS, rate_unit)
    if compounding == "continuous":
        return mpmath.exp(rate * units / periods_per_year) - 1
    times = COMPOUNDING[compounding]
    span = units / times    # rate units in one compounding period
    if span <= 1:
        each = rate * span
    else:
        each = mpmath.power(1 + rate, span) - 1
    return mpmath.power(1 + each, mpmath.mpf(times) / periods_per_year) - 1


def whole_periods(length, length_unit, periods_per_year):
    """The whole periods in length; a part period is cut (owner 2026-11-28)."""
    exact = length * periods_per_year / per_year(LENGTH_UNITS, length_unit)
    near = mpmath.nint(exact)
    if abs(exact - near) <= mpmath.mpf(10) ** -9 * max(1, abs(exact)):
        return int(near)    # 52 weeks of 7/365 years, 12 months: whole despite rounding
    return int(mpmath.floor(exact))


def schedule(principal, annual_rate, years, compounding="yearly",
             contribution=0, contribution_freq="yearly", at_start=False,
             rate_unit="yearly", length_unit="years"):
    """Grow principal plus regular contributions; one row per period.

    annual_rate is per rate_unit and years is in length_unit (the names
    stay from when both were yearly; the protocol fields keep them).
    Rows follow the contribution frequency; with no contribution they
    follow the compounding frequency (daily for continuous).
    """
    if years <= 0:
        raise CalcError("years: must be positive")
    if contribution == 0:
        freq_key = "daily" if compounding == "continuous" else compounding
    else:
        freq_key = contribution_freq
    per_year_count = COMPOUNDING[freq_key]
    count = whole_periods(mpmath.mpf(years), length_unit, per_year_count)
    if count == 0:
        raise CalcError(f"years: shorter than one {PERIOD_NAMES[freq_key]}")
    if count > MAX_PERIODS:
        raise CalcError(f"years: too many periods (at most {MAX_PERIODS})")
    per_unit = per_year_count / per_year(LENGTH_UNITS, length_unit)
    rate = period_rate(mpmath.mpf(annual_rate), compounding, per_year_count, rate_unit)
    balance = mpmath.mpf(principal)
    rows = []
    for index in range(1, count + 1):
        start = balance
        added_first = contribution if at_start else 0
        interest = (start + added_first) * rate
        balance = start + added_first + interest + (0 if at_start else contribution)
        rows.append(Period(index, start, interest, mpmath.mpf(contribution), balance))
    total_contrib = mpmath.mpf(contribution) * count
    return Schedule(balance, total_contrib, balance - principal - total_contrib, rows, per_year_count, per_unit)
