"""tests/test_version.py - The engine says SoftCalc's version (owner 2026-11-24).

1.8.0 shipped saying "Engine 1.7.0 ready": the engine's version was not
raised with SoftCalc's. ui/SoftCalc/SoftCalc.csproj <Version> is the one
version; the engine package and pyproject.toml must say the same.
"""

import re
import tomllib
from pathlib import Path

from softcalc_engine import __version__

ROOT = Path(__file__).resolve().parents[2]


def test_engine_version_is_softcalcs():
    csproj = (ROOT / "ui" / "SoftCalc" / "SoftCalc.csproj").read_text(encoding="utf-8")
    softcalc = re.search(r"<Version>([\d.]+)</Version>", csproj).group(1)
    project = tomllib.loads((ROOT / "engine" / "pyproject.toml").read_text(encoding="utf-8"))["project"]["version"]
    assert (__version__, project) == (softcalc, softcalc)
