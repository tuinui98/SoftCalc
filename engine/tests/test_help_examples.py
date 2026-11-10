"""tests/test_help_examples.py - Every `line => result` example in help/*.md (owner 2026-11-10).

Help topics keep their examples in ```calc and ```rpn blocks. A line with
`=>` is checked: the engine must give exactly the text after it. Lines
without `=>` (dates, currency) are only shown. Each file runs in a fresh
engine at the default settings, top to bottom, so a topic may define a
variable and use it below.
"""

from pathlib import Path

import pytest

from softcalc_engine.engine import Engine

HELP = Path(__file__).resolve().parents[2] / "help"
TOPICS = sorted(HELP.glob("[0-9][0-9]-*.md"))


def examples(path):
    """(line number, rpn, input, expected) for each checked example."""
    found = []
    fence = None
    for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        if line.startswith("```"):
            fence = None if fence else line[3:].strip()
            continue
        if fence in ("calc", "rpn") and "=>" in line:
            source, expected = (part.strip() for part in line.split("=>", 1))
            found.append((number, fence == "rpn", source, expected))
    return found


def test_help_has_topics():
    assert len(TOPICS) >= 16


@pytest.mark.parametrize("path", TOPICS, ids=lambda p: p.stem)
def test_examples(path):
    engine = Engine()
    wrong = []
    for number, rpn, source, expected in examples(path):
        engine.settings.rpn = rpn
        got = engine.evaluate(source).text
        if got != expected:
            wrong.append(f"{path.name}:{number}: {source} gave {got}, help says {expected}")
    assert not wrong, "\n".join(wrong)
