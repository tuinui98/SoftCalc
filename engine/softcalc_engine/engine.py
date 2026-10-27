"""softcalc_engine/engine.py - Evaluate input lines and keep session state.

One Engine instance serves one calculator window: it holds the settings,
`answer`, and the result stack behind `$n`, and turns each input line
into an EvalResult (a result, a definition, an error, or nothing).
"""

from dataclasses import dataclass, field

import mpmath

from .errors import CalcError
from .formatting import format_number
from .functions import Functions, binary, postfix
from .parser import parse

GUARD_DIGITS = 10
PRECISIONS = (32, 64, 128, 512)
CONSTANT_NAMES = {"pi", "e", "i", "j"}
ANSWER_NAMES = {"answer", "ans"}


@dataclass
class Settings:
    """Calculation settings mirrored from the View menu."""

    precision: int = 32
    angle: str = "rad"
    base: str = "dec"
    rpn: bool = False


@dataclass
class EvalResult:
    """Outcome of one input line.

    kind: "result", "definition", "error", or "empty" (nothing to add).
    text: the History result line (number, message, or error text).
    """

    kind: str
    input: str
    text: str = ""
    value: object = field(default=None, repr=False)


class Engine:
    """Stateful evaluator for one window."""

    def __init__(self):
        self.settings = Settings()
        self.answer = None
        self.results = []

    def clear_history(self):
        """Forget `$n` results; `answer` survives (SPEC 5.3)."""
        self.results = []

    def evaluate(self, text):
        """Evaluate one input line and record its result."""
        if text == "":
            return EvalResult("empty", text)
        digits = self.settings.precision
        try:
            with mpmath.workdps(digits + GUARD_DIGITS):
                if text.strip() == "":
                    raise CalcError("Empty expression")
                value = self.compute(text)
                shown = format_number(value, digits)
        except CalcError as exc:
            return EvalResult("error", text, f"ERROR:{exc}")
        self.answer = value
        self.results.append(value)
        return EvalResult("result", text, shown, value)

    #------------------------------------------------------------------
    #  Evaluation
    #------------------------------------------------------------------
    def compute(self, text):
        if "=" in text:
            raise CalcError("Definitions are not supported yet")
        functions = Functions(self.settings.angle)
        tree = parse(text, is_function=lambda name: name.lower() in functions.names())
        return self.eval_node(tree, functions)

    def eval_node(self, node, functions):
        kind = node[0]
        if kind == "num":
            _, literal, base = node
            if base == 10:
                return mpmath.mpf(literal)
            return mpmath.mpf(int(literal, base))
        if kind == "imag":
            return mpmath.mpc(0, mpmath.mpf(node[1]))
        if kind == "name":
            return self.lookup(node[1], functions)
        if kind == "call":
            args = [self.eval_node(arg, functions) for arg in node[2]]
            return functions.call(node[1], args)
        if kind == "neg":
            return -self.eval_node(node[1], functions)
        if kind == "pos":
            return self.eval_node(node[1], functions)
        if kind == "post":
            return postfix(node[1], self.eval_node(node[2], functions))
        if kind == "mul":
            left = self.eval_node(node[1], functions)
            return binary("*", left, self.eval_node(node[2], functions))
        if kind == "bin":
            left = self.eval_node(node[2], functions)
            return binary(node[1], left, self.eval_node(node[3], functions))
        raise CalcError(f"Cannot evaluate '{kind}'")

    def lookup(self, name, functions):
        key = name.lower()
        if key == "pi":
            return +mpmath.pi
        if key == "e":
            return +mpmath.e
        if key in ("i", "j"):
            return mpmath.mpc(0, 1)
        if key in ANSWER_NAMES:
            if self.answer is None:
                raise CalcError("No previous result")
            return self.answer
        if key in functions.names():
            raise CalcError(f"{key} needs an argument in ( )")
        raise CalcError(f"Unknown name '{name}'")
