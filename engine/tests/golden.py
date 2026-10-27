"""tests/golden.py - Load golden cases recorded from the original program.

Reads spec/golden/<group>.tsv as written by tools/probe_eval.ps1. Each
row holds the input, the settings it ran under, and the History text the
original added. Only evaluation groups are handled here; menu_insert and
O_keypad have their own layouts.
"""

from dataclasses import dataclass
from pathlib import Path

GOLDEN_DIR = Path(__file__).resolve().parents[2] / "spec" / "golden"
EVAL_GROUPS = [
    "A_arith", "B_literals", "C_base", "D_complex", "E_trig", "F_logs",
    "G_consts", "H_vars", "I_funcs", "J_conv", "L_precision", "M_rpn",
    "N_errors", "Q_answer", "R_retest",
]


@dataclass
class GoldenCase:
    """One recorded evaluation of the original program."""

    group: str
    line_no: int
    input: str
    prec: str
    angle: str
    base: str
    rpn: str
    raw_output: str

    @property
    def result(self):
        """Last History line of the case: the result or message."""
        tail = self.raw_output.rsplit(" \\n ", 1)[-1]
        return tail.strip()

    @property
    def is_error(self):
        """True when the original printed an error (or crashed)."""
        return self.result.startswith("ERROR:") or self.result.startswith("<CRASH")


def load_group(group):
    """Return the GoldenCase rows of one evaluation group, in file order."""
    path = GOLDEN_DIR / f"{group}.tsv"
    text = path.read_text(encoding="utf-8-sig")
    cases = []
    for line_no, line in enumerate(text.splitlines()[1:], start=2):
        cols = line.split("\t")
        if len(cols) < 7 or cols[0].startswith("@"):
            continue
        cases.append(GoldenCase(
            group=group,
            line_no=line_no,
            input=cols[0],
            prec=cols[1],
            angle=cols[2],
            base=cols[3],
            rpn=cols[4],
            raw_output="\t".join(cols[6:]),
        ))
    return cases
