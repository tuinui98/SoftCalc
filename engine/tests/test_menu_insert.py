"""tests/test_menu_insert.py - Menu insertion against spec/golden/menu_insert.tsv.

Each row gives a menu path and what the original wrote into an empty
Input and into an Input holding `1` (caret at the end).
"""

import re

from golden import GOLDEN_DIR
from softcalc_engine.menu import insert_conversion, insert_function, insert_operator

OPERATORS = {"+", "-", "*", "/", "^"}
UNIT_TOKEN = re.compile(r"\(([^)]*)\)\s*$")


def apply(path, text):
    parts = [p.strip() for p in path.split(">")]
    if parts[0] == "Conversions":
        source = UNIT_TOKEN.search(parts[2]).group(1)
        target = UNIT_TOKEN.search(parts[3]).group(1)
        return insert_conversion(text, source, target)[0]
    item = parts[-1]
    if item in OPERATORS:
        return insert_operator(text, len(text), item)[0]
    return insert_function(text, item.split("(")[0])[0]


def test_menu_insert_matches_original():
    rows = (GOLDEN_DIR / "menu_insert.tsv").read_text(encoding="utf-8-sig").splitlines()[1:]
    failures = []
    for row in rows:
        cmd_id, path, into_empty, after_one = row.split("\t")
        for seed, want in (("", into_empty), ("1", after_one)):
            got = apply(path, seed)
            if got != want:
                failures.append(f"{cmd_id} {path} seed={seed!r}: want {want!r} got {got!r}")
    assert len(rows) == 124
    assert not failures, "\n".join(failures)
