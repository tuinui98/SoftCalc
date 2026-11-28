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

# periods a year; weekly owner 2026-11-28 (a year is 365 days)
COMPOUNDING = {"yearly": 1, "quarterly": 4, "monthly": 12, "weekly": "365/7", "daily": 365}
MAX_PERIODS = 100000    # table rows; 270 years of daily periods
PERIOD_NAMES = {"yearly": "year", "quarterly": "quarter", "monthly": "month", "weekly": "week", "daily": "day"}


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
    """One row of the schedule table; part is the share of a period in a
    last row shorter than the rest (goal mode), else None."""

    index: int
    start: object
    interest: object
    contribution: object
    end: object
    part: object = None


@dataclass
class Schedule:
    future_value: object
    total_contributions: object
    total_interest: object
    rows: list
    periods_per_year: object = 1
    periods_per_unit: object = 1    # rows per length unit (the chart's x axis)
    partial: object = 0             # the last row's share of a period, 0 when whole


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
    times = per_year(COMPOUNDING, compounding)
    span = units / times    # rate units in one compounding period
    if span <= 1:
        each = rate * span
    else:
        each = mpmath.power(1 + rate, span) - 1
    return mpmath.power(1 + each, times / periods_per_year) - 1


def split_periods(exact):
    """(whole, part) of a number of periods; near-whole counts as whole
    (52 weeks of 7/365 years, 12 months)."""
    near = mpmath.nint(exact)
    if abs(exact - near) <= mpmath.mpf(10) ** -9 * max(1, abs(exact)):
        return int(near), mpmath.mpf(0)
    whole = int(mpmath.floor(exact))
    return whole, exact - whole


def whole_periods(length, length_unit, periods_per_year):
    """The whole periods in length; a part period is cut (owner 2026-11-28)."""
    return split_periods(length * periods_per_year / per_year(LENGTH_UNITS, length_unit))[0]


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
    rows_a_year = per_year(COMPOUNDING, freq_key)
    count = whole_periods(mpmath.mpf(years), length_unit, rows_a_year)
    if count == 0:
        raise CalcError(f"years: shorter than one {PERIOD_NAMES[freq_key]}")
    if count > MAX_PERIODS:
        raise CalcError(f"years: too many periods (at most {MAX_PERIODS})")
    rate = period_rate(mpmath.mpf(annual_rate), compounding, rows_a_year, rate_unit)
    plan = grow(principal, rate, count, 0, contribution, at_start)
    plan.periods_per_year = rows_a_year
    plan.periods_per_unit = rows_a_year / per_year(LENGTH_UNITS, length_unit)
    return plan


def grow(principal, rate, count, part, contribution, at_start):
    """count whole rows at rate each, then a part row when part > 0: its
    interest compounds for that share, (1 + rate)^part - 1, and it takes a
    contribution only at its start (owner 2026-11-29)."""
    contribution = mpmath.mpf(contribution)
    balance = mpmath.mpf(principal)
    rows = []
    for index in range(1, count + 1):
        start = balance
        added_first = contribution if at_start else 0
        interest = (start + added_first) * rate
        balance = start + added_first + interest + (0 if at_start else contribution)
        rows.append(Period(index, start, interest, contribution, balance))
    paid = contribution * count
    if part > 0:
        start = balance
        added = contribution if at_start else mpmath.mpf(0)
        interest = (start + added) * (mpmath.power(1 + rate, part) - 1)
        balance = start + added + interest
        rows.append(Period(count + 1, start, interest, added, balance, part))
        paid += added
    return Schedule(balance, paid, balance - principal - paid, rows, partial=part)


#----------------------------------------------------------------------
#  Goal mode (owner 2026-11-28): four of target, principal, rate, length
#  and contribution give the fifth. The rate's unit is the compounding
#  period; a length may end in a part period.
#----------------------------------------------------------------------
GOAL_FIELDS = ("target", "principal", "annual_rate", "years", "contribution")
RATE_COMPOUNDING = {"yearly": "yearly", "monthly": "monthly", "weekly": "weekly", "daily": "daily"}


@dataclass
class Goal:
    values: dict           # all five, the solved one included
    solved: str
    plan: Schedule
    years: object          # the length in years, for "7 y 3 m 18 d"


def growth_factors(rate, whole, part, at_start):
    """(A, B): the end balance is principal * A + contribution * B."""
    q = 1 + rate
    a = mpmath.power(q, whole + part)
    if rate == 0:
        b = mpmath.mpf(whole) + (1 if at_start and part > 0 else 0)
        return a, b
    tail = mpmath.power(q, part)
    if at_start:
        b = tail * q * (mpmath.power(q, whole) - 1) / rate + (tail if part > 0 else 0)
    else:
        b = tail * (mpmath.power(q, whole) - 1) / rate
    return a, b


def time_to(target, principal, rate, contribution, at_start):
    """Periods (with a part) until the balance first reaches target."""
    def between(value, a, b):
        return min(a, b) <= value <= max(a, b)

    if target == principal:
        raise CalcError("years: the principal already is the target")
    q = 1 + rate
    balance = mpmath.mpf(principal)
    for whole in range(MAX_PERIODS):
        start = balance + (contribution if at_start else 0)
        if at_start and between(target, balance, start):
            if whole == 0:
                raise CalcError("years: the first contribution already reaches the target")
            return mpmath.mpf(whole)
        end = start * q
        if end != start and between(target, start, end) and target * start > 0:
            return whole + mpmath.ln(target / start) / mpmath.ln(q)
        balance = end + (0 if at_start else contribution)
        if not at_start and between(target, end, balance):
            return mpmath.mpf(whole + 1)
    raise CalcError(f"years: does not reach the target in {MAX_PERIODS} periods")


def rate_to(target, principal, contribution, whole, part, at_start, per_row):
    """The rate per rate unit that reaches target, by halving; per_row turns
    it into the rate per row."""
    def gap(rate):
        a, b = growth_factors(per_row(rate), whole, part, at_start)
        return principal * a + contribution * b - target

    low, high = mpmath.mpf("-0.999999"), mpmath.mpf(1)
    low_gap = gap(low)
    while gap(high) * low_gap > 0:
        high *= 2
        if high > 10 ** 6:
            raise CalcError("annual_rate: no rate reaches the target")
    for _ in range(mpmath.mp.prec + 20):
        middle = (low + high) / 2
        middle_gap = gap(middle)
        if middle_gap == 0:
            return middle
        if middle_gap * low_gap > 0:
            low, low_gap = middle, middle_gap
        else:
            high = middle
        if high - low <= mpmath.eps * max(1, abs(high)):
            break
    return (low + high) / 2


def goal(values, solve, rate_unit="yearly", length_unit="years",
         contribution_freq="yearly", at_start=False):
    """Work out the field `solve` from the other four (values: field ->
    mpf, rate in percent, length in length_unit) and the table."""
    if solve not in GOAL_FIELDS:
        raise CalcError(f"solve must be one of {list(GOAL_FIELDS)}")
    v = {name: (mpmath.mpf(value) if value is not None else None) for name, value in values.items()}
    contribution = v.get("contribution") or mpmath.mpf(0)
    if solve == "contribution" or contribution != 0:
        freq_key = contribution_freq
    else:
        freq_key = RATE_COMPOUNDING[rate_unit]
    rows_a_year = per_year(COMPOUNDING, freq_key)
    span = per_year(RATE_UNITS, rate_unit) / rows_a_year    # rate units in one row

    def per_row(rate):
        return mpmath.power(1 + rate, span) - 1

    if solve != "annual_rate" and v["annual_rate"] <= -100:
        raise CalcError("annual_rate: must be more than -100")
    if solve != "years" and v["years"] <= 0:
        raise CalcError("years: must be positive")
    length_units = per_year(LENGTH_UNITS, length_unit)
    if solve == "years":
        periods = time_to(v["target"], v["principal"], per_row(v["annual_rate"] / 100), contribution, at_start)
        v["years"] = periods * length_units / rows_a_year
    whole, part = split_periods(v["years"] * rows_a_year / length_units)
    if whole + (1 if part > 0 else 0) > MAX_PERIODS:
        raise CalcError(f"years: too many periods (at most {MAX_PERIODS})")
    if solve == "annual_rate":
        v["annual_rate"] = 100 * rate_to(v["target"], v["principal"], contribution, whole, part, at_start, per_row)
    rate = per_row(v["annual_rate"] / 100)
    a, b = growth_factors(rate, whole, part, at_start)
    if solve == "target":
        v["target"] = v["principal"] * a + contribution * b
    elif solve == "principal":
        v["principal"] = (v["target"] - contribution * b) / a
    elif solve == "contribution":
        if b == 0:
            raise CalcError("contribution: none falls within the length")
        contribution = v["contribution"] = (v["target"] - v["principal"] * a) / b
    v["contribution"] = contribution
    plan = grow(v["principal"], rate, whole, part, contribution, at_start)
    plan.periods_per_year = rows_a_year
    plan.periods_per_unit = rows_a_year / length_units
    return Goal(v, solve, plan, v["years"] / length_units)


def years_text(years):
    """7.3 years -> "7 y 3 m 18 d" (a month a twelfth of 365 days)."""
    whole = int(mpmath.floor(years))
    months = (years - whole) * 12
    month = int(mpmath.floor(months))
    day = int(mpmath.nint((months - month) * 365 / 12))
    parts = [f"{n} {unit}" for n, unit in ((whole, "y"), (month, "m"), (day, "d")) if n]
    return " ".join(parts) or "0 d"
