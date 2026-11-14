"""tests/test_crypto_units.py - Crypto amounts in tags (owner 2026-11-15, design/crypto_plan.md C0)."""

import pytest

from softcalc_engine.engine import Engine
from softcalc_engine.units import MENU


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


def test_coins_do_not_mix():
    result = run("1 [btc->eth]")
    assert (result.kind, result.text) == ("error", "ERROR:Cannot convert btc to eth")


def test_menu_has_a_group_per_coin():
    assert MENU["Bitcoin"] == ["btc", "sat"]
    assert MENU["Ethereum"] == ["eth", "gwei", "wei"]
    assert MENU["Tron"] == ["trx", "sun"]
