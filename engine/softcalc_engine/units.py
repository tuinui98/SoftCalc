"""softcalc_engine/units.py - Unit conversion for `value [from->to]` tags (SPEC 5.9).

Linear units convert through a base unit per category with exact
international definitions (1 lb = 0.45359237 kg [FIX]). Temperature is
affine and converts through kelvin. Time adds year, month, week and hour
[EXT]; a year is 365 days and a month 30 days (owner's choice), so a
year is not exactly 12 months. Long names (`year`, `hours`, ...) are aliases of the tokens.
"""

import mpmath

from .errors import CalcError

# unit token -> (category, size in the category's base unit, as exact text)
LINEAR = {
    "in": ("length", "0.0254"), "ft": ("length", "0.3048"),
    "yd": ("length", "0.9144"), "mile": ("length", "1609.344"),
    "mm": ("length", "0.001"), "cm": ("length", "0.01"),
    "m": ("length", "1"), "km": ("length", "1000"),
    "lb": ("mass", "0.45359237"), "oz": ("mass", "0.45359237/16"),
    "g": ("mass", "0.001"), "kg": ("mass", "1"),
    "yr": ("time", "31536000"), "mo": ("time", "2592000"), "wk": ("time", "604800"),
    "d": ("time", "86400"), "h": ("time", "3600"), "min": ("time", "60"), "s": ("time", "1"),
    "mi/h": ("velocity", "1609.344/3600"), "m/s": ("velocity", "1"),
    "ft/h": ("velocity", "0.3048/3600"), "km/h": ("velocity", "1000/3600"),
    "knot": ("velocity", "1852/3600"),
}
TEMPERATURE = ("c", "f", "k")
ALIASES = {
    "year": "yr", "years": "yr", "month": "mo", "months": "mo",
    "week": "wk", "weeks": "wk", "day": "d", "days": "d",
    "hour": "h", "hours": "h", "hr": "h", "minute": "min", "minutes": "min",
    "sec": "s", "second": "s", "seconds": "s",
}

# Menu order of categories and units (Conversions menu, SPEC 3)
MENU = {
    "Length": ["in", "ft", "yd", "mile", "mm", "cm", "m", "km"],
    "Mass": ["lb", "oz", "g", "kg"],
    "Time": ["yr", "mo", "wk", "d", "h", "min", "s"],
    "Velocity": ["mi/h", "m/s", "ft/h", "km/h", "knot"],
    "Temperature": ["c", "f", "k"],
}


def exact(text):
    """Evaluate a factor like '1609.344/3600' at the working precision."""
    num, _, den = text.partition("/")
    value = mpmath.mpf(num)
    return value / mpmath.mpf(den) if den else value


def category(unit):
    if unit in TEMPERATURE:
        return "temperature"
    entry = LINEAR.get(unit)
    return entry[0] if entry else None


def to_kelvin(value, unit):
    if unit == "c":
        return value + mpmath.mpf("273.15")
    if unit == "f":
        return (value - 32) * 5 / 9 + mpmath.mpf("273.15")
    return value


def from_kelvin(value, unit):
    if unit == "c":
        return value - mpmath.mpf("273.15")
    if unit == "f":
        return (value - mpmath.mpf("273.15")) * 9 / 5 + 32
    return value


def canonical(unit):
    return ALIASES.get(unit, unit)


def convert(value, source, target):
    """Convert value from unit `source` to unit `target` (lower-case tokens)."""
    source, target = canonical(source), canonical(target)
    src_cat, dst_cat = category(source), category(target)
    if src_cat is None:
        raise CalcError(f"Unknown unit '{source}'")
    if dst_cat is None:
        raise CalcError(f"Unknown unit '{target}'")
    if src_cat != dst_cat:
        raise CalcError(f"Cannot convert {source} to {target}")
    if src_cat == "temperature":
        return from_kelvin(to_kelvin(value, source), target)
    return value * exact(LINEAR[source][1]) / exact(LINEAR[target][1])
