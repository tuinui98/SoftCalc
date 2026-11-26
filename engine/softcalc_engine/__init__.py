"""softcalc_engine/__init__.py - SoftCalc expression engine package.

Parses calculator input, evaluates it with mpmath at the selected
precision, and formats results as specified in spec/SPEC.md. The UI
talks to it through the JSON-lines protocol (design/protocol.md).
"""

__version__ = "1.11.1"    # SoftCalc's own version (ui/SoftCalc/SoftCalc.csproj), test_version.py
