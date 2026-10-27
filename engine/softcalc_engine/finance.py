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


def period_rate(annual_rate, compounding, periods_per_year):
    """Effective rate for one schedule period.

    annual_rate is a fraction (0.05 for 5%). compounding is a key of
    COMPOUNDING or "continuous".
    """
    if compounding == "continuous":
        return mpmath.exp(annual_rate / periods_per_year) - 1
    per_year = COMPOUNDING[compounding]
    return mpmath.power(1 + annual_rate / per_year, mpmath.mpf(per_year) / periods_per_year) - 1


def schedule(principal, annual_rate, years, compounding="yearly",
             contribution=0, contribution_freq="yearly", at_start=False):
    """Grow principal plus regular contributions; one row per period.

    Rows follow the contribution frequency; with no contribution they
    follow the compounding frequency (daily for continuous).
    """
    if years <= 0:
        raise CalcError("years must be positive")
    if contribution == 0:
        freq_key = "daily" if compounding == "continuous" else compounding
    else:
        freq_key = contribution_freq
    per_year = COMPOUNDING[freq_key]
    count = int(mpmath.nint(years * per_year))
    rate = period_rate(mpmath.mpf(annual_rate), compounding, per_year)
    balance = mpmath.mpf(principal)
    rows = []
    for index in range(1, count + 1):
        start = balance
        added_first = contribution if at_start else 0
        interest = (start + added_first) * rate
        balance = start + added_first + interest + (0 if at_start else contribution)
        rows.append(Period(index, start, interest, mpmath.mpf(contribution), balance))
    total_contrib = mpmath.mpf(contribution) * count
    return Schedule(balance, total_contrib, balance - principal - total_contrib, rows)
