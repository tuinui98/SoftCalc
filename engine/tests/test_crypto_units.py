"""tests/test_crypto_units.py - Crypto amounts in tags (owner 2026-11-15, design/crypto_plan.md C0)."""

import pytest

from softcalc_engine.engine import Engine
from softcalc_engine.units import CRYPTO_MENU, MENU


def run(line):
    return Engine().evaluate(line)


@pytest.mark.parametrize("line, want", [
    ("1 [btc->sat]", "100000000"),
    ("150000 [sat->btc]", "0.0015"),
    ("12345 [sats->btc]", "0.00012345"),
    ("1 [eth->gwei]", "1000000000"),
    ("21000*30 [gwei->eth]", "0.00063"),
    ("1 [eth->wei]", "1000000000000000000"),
    ("1 [wei->gwei]", "0.000000001"),
    ("1 [trx->sun]", "1000000"),
    ("2500000 [sun->trx]", "2.5"),
])
def test_coin_units(line, want):
    result = run(line)
    assert (result.kind, result.text) == ("result", want)


def test_coins_need_prices_to_mix():
    assert run("1 [btc->eth]").kind == "prices_needed"    # through usd prices (C1)
    result = run("1 [btc->kg]")
    assert (result.kind, result.text) == ("error", "ERROR:Cannot convert btc to kg")


def test_one_cryptocurrency_menu_with_a_group_per_coin():
    # owner 2026-11-24: one Cryptocurrency submenu, Tether (usdt) in it too
    assert CRYPTO_MENU == {"Bitcoin": ["btc", "sat"], "Ethereum": ["eth", "gwei", "wei"],
                           "Tron": ["trx", "sun"], "Tether": ["usdt"]}
    assert not set(CRYPTO_MENU) & set(MENU)


#------------------------------------------------------------------
#  Prices (C1): [btc->thb] and friends
#------------------------------------------------------------------
PRICES = {"btc": {"thb": 2200000, "usd": 65000}, "eth": {"thb": 60000, "usd": 1800},
          "trx": {"thb": 10, "usd": 0.3}, "usdt": {"thb": 34, "usd": 1}}


def priced(line, prices=PRICES, next_check=4e9):
    engine = Engine()
    engine.prices.load("2026-11-15 14:05", prices, False, next_check)
    return engine.evaluate(line)


@pytest.mark.parametrize("line, want", [
    ("0.5 [btc->thb]", "1100000"),
    ("100000000 [sat->thb]", "2200000"),
    ("1100000 [thb->btc]", "0.5"),
    ("100 [usdt->thb]", "3400"),
    ("1 [btc->eth]", "36.111111111111111111111111111111"),
    ("21000*30 [gwei->usd]", "1.134"),
    ("1 [btc->sat]", "100000000"),
])
def test_prices(line, want):
    result = priced(line)
    assert (result.kind, result.text) == ("result", want)
    if "sat]" not in line:
        assert result.note == "CoinGecko 2026-11-15 14:05"


def test_prices_needed_and_missing():
    assert Engine().evaluate("1 [btc->thb]").kind == "prices_needed"
    assert priced("1 [btc->thb]", next_check=0).kind == "prices_needed"
    result = priced("1 [btc->jpy]")
    assert (result.kind, result.text) == ("error", "ERROR:No CoinGecko price of BTC in JPY")


def test_set_prices_request():
    from softcalc_engine.server import Dispatcher
    dispatcher = Dispatcher(Engine())
    reply = dispatcher.handle({"id": 1, "op": "set_prices", "time": "14:05", "prices": PRICES, "next_check": 4e9})
    assert reply.get("ok", True) is not False, reply
    assert dispatcher.handle({"id": 2, "op": "eval", "input": "2 [eth->thb]"})["text"] == "120000"
    bad = dispatcher.handle({"id": 3, "op": "set_prices", "time": "x", "prices": {"doge": {"thb": 1}}, "next_check": 1})
    assert bad.get("ok") is False
