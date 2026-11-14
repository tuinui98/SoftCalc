"""softcalc_engine/crypto.py - Coin prices for `value [btc->thb]` tags (owner 2026-11-15, design/crypto_plan.md C1).

Like the ECB rates, the engine does no network I/O: the UI fetches prices
(CoinGecko) and hands them over with `set_prices`, saying when they are
due again. A coin line before any prices, or after they are due, raises
PricesNeeded so the UI can fetch and evaluate the line again.

Coins: btc, eth, trx and usdt; their small units (sat, gwei, wei, sun)
convert to the coin first. A price is units of a currency per 1 coin;
coin to coin goes through usd.
"""

import time

import mpmath

from .errors import CalcError
from .units import LINEAR, canonical, convert

# coin unit -> the coin it counts in (usdt has no smaller unit here)
COIN_OF = {"btc": "btc", "sat": "btc", "eth": "eth", "gwei": "eth", "wei": "eth", "trx": "trx", "sun": "trx", "usdt": "usdt"}
COINS = ("btc", "eth", "trx", "usdt")


class PricesNeeded(CalcError):
    """A conversion needs fresh prices from the UI."""

    def __init__(self):
        super().__init__("Coin prices are being fetched")


class Prices:
    """Prices held by one engine, as last sent by the UI."""

    def __init__(self):
        self.time = None           # when the prices were taken, as shown, e.g. "2026-11-15 14:05"
        self.per_coin = {}         # coin -> currency (lower case) -> mpf text
        self.stale = False         # the UI's last fetch failed
        self.next_check = 0.0      # Unix seconds; prices are due after this

    def load(self, when, per_coin, stale, next_check):
        self.time = when
        self.per_coin = {coin.lower(): {cur.lower(): str(p) for cur, p in prices.items()} for coin, prices in per_coin.items()}
        self.stale = stale
        self.next_check = next_check

    def due(self, clock=time.time):
        return clock() >= self.next_check

    def note(self):
        text = f"CoinGecko {self.time}"
        return text + ", offline" if self.stale else text


def is_coin(code):
    return canonical(code) in COIN_OF


def involved(source, target):
    """True when the tag is a price conversion: a coin on one side, and not two units of one coin."""
    source, target = canonical(source), canonical(target)
    if not (source in COIN_OF or target in COIN_OF):
        return False
    both_units = source in LINEAR and target in LINEAR
    return not (both_units and LINEAR[source][0] == LINEAR[target][0])


def convert_price(value, source, target, prices, clock=time.time):
    """Convert between a coin (or its small unit) and a currency or another coin."""
    source, target = canonical(source), canonical(target)
    for side in (source, target):
        if side not in COIN_OF and (len(side) != 3 or not side.isalpha()):
            raise CalcError(f"Cannot convert {source} to {target}")    # e.g. [btc->kg]: no fetch for that
    if not prices.per_coin or prices.due(clock):
        if prices.per_coin or prices.due(clock):
            raise PricesNeeded()
        raise CalcError("Coin prices unavailable")
    if source in COIN_OF:
        coin = COIN_OF[source]
        amount = convert(value, source, coin) if source != coin else value
        if target in COIN_OF:
            other = COIN_OF[target]
            usd = amount * price(prices, coin, "usd")
            in_other = usd / price(prices, other, "usd")
            return convert(in_other, other, target) if target != other else in_other
        return amount * price(prices, coin, target)
    coin = COIN_OF[target]
    amount = value / price(prices, coin, source)
    return convert(amount, coin, target) if target != coin else amount


def price(prices, coin, currency):
    if len(currency) != 3 or not currency.isalpha():
        raise CalcError(f"Cannot convert {coin} to {currency}")
    text = prices.per_coin.get(coin, {}).get(currency)
    if text is None:
        raise CalcError(f"No CoinGecko price of {coin.upper()} in {currency.upper()}")
    return mpmath.mpf(text)
