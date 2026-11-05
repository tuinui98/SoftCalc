"""softcalc_engine/currency.py - Currency conversion for `value [usd->thb]` tags (E8).

The engine does no network I/O: the UI fetches the ECB daily reference
rates and hands them over with `set_rates`. Rates are units of a
currency per 1 EUR, so any pair converts through EUR. The UI also says
when the rates are due again (`next_check`); a conversion after that,
or before any rates arrived, raises RatesNeeded so the UI can fetch and
evaluate the line again.
"""

import time

import mpmath

from .errors import CalcError

# Currencies the ECB publishes, and the menu's common ones (owner 2026-11-06)
CODES = ["AUD", "BRL", "CAD", "CHF", "CNY", "CZK", "DKK", "EUR", "GBP", "HKD",
         "HUF", "IDR", "ILS", "INR", "ISK", "JPY", "KRW", "MXN", "MYR", "NOK",
         "NZD", "PHP", "PLN", "RON", "SEK", "SGD", "THB", "TRY", "USD", "ZAR"]
COMMON = ["THB", "USD", "EUR", "JPY", "GBP", "CNY", "SGD", "HKD", "AUD", "KRW"]


class RatesNeeded(CalcError):
    """A conversion needs fresh rates from the UI."""

    def __init__(self):
        super().__init__("Currency rates are being fetched")


class Rates:
    """Rates held by one engine, as last sent by the UI."""

    def __init__(self):
        self.date = None           # publication date text, e.g. "2026-11-05"
        self.per_eur = {}          # lower-case code -> mpf text
        self.stale = False         # the UI's last fetch failed
        self.next_check = 0.0      # Unix seconds; rates are due after this

    def load(self, date, per_eur, stale, next_check):
        self.date = date
        self.per_eur = {code.lower(): str(rate) for code, rate in per_eur.items()}
        if self.per_eur:
            self.per_eur["eur"] = "1"
        self.stale = stale
        self.next_check = next_check

    def due(self, clock=time.time):
        return clock() >= self.next_check

    def note(self):
        """Text shown with every result that used a rate."""
        text = f"ECB {self.date}"
        return text + ", offline" if self.stale else text


def is_currency(code, rates):
    return code.upper() in CODES or code in rates.per_eur


def convert(value, source, target, rates, clock=time.time):
    """Convert value from currency `source` to `target` (lower-case codes).

    Called when at least one side is a known currency; the other side must
    look like a currency code too (`[usd->kg]` is not a conversion).
    """
    for code in (source, target):
        if not (len(code) == 3 and code.isalpha()):
            raise CalcError(f"Cannot convert {source} to {target}")
    if not rates.per_eur or rates.due(clock):
        if rates.per_eur or rates.due(clock):
            raise RatesNeeded()
        raise CalcError("Currency rates unavailable")
    for code in (source, target):
        if code not in rates.per_eur:
            raise CalcError(f"No ECB rate for {code.upper()}")    # e.g. BGN, in the euro since 2026
    return value / mpmath.mpf(rates.per_eur[source]) * mpmath.mpf(rates.per_eur[target])
