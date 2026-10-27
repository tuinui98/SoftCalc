"""softcalc_engine/engine.py - Evaluate input lines and keep session state.

One Engine instance serves one calculator window. It holds the settings,
user variables (stored by value) and functions (stored as trees, looked
up at call time), `answer`, and the result stack behind `$n`, and turns
each input line into an EvalResult.
"""

from dataclasses import dataclass, field

import mpmath

from .errors import CalcError
from .formatting import format_number, working_digits
from .functions import Functions, binary, postfix
from .parser import parse_line
from .rpn import parse_rpn
from .units import convert

GUARD_DIGITS = 10
MAX_CALL_DEPTH = 200
CONSTANT_NAMES = {"pi", "e", "i", "j"}
ANSWER_NAMES = {"answer", "ans"}
RESERVED = CONSTANT_NAMES | ANSWER_NAMES | {"mod"}
DEFAULT_FUNCTIONS = ["g(x)=x", "function(var)=var*cos(var)"]


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


@dataclass
class UserFunction:
    """A user-defined function; `source` is the text as typed."""

    name: str
    params: list
    body: tuple
    source: str


class Engine:
    """Stateful evaluator for one window."""

    def __init__(self, load_defaults=True):
        self.settings = Settings()
        self.answer = None
        self.results = []
        self.variables = {}    # lower-case name -> (display name, value)
        self.functions = {}    # lower-case name -> UserFunction
        if load_defaults:
            for line in DEFAULT_FUNCTIONS:
                self.evaluate(line)

    def clear_history(self):
        """Forget `$n` results; `answer` survives (SPEC 5.3)."""
        self.results = []

    #------------------------------------------------------------------
    #  Entry point
    #------------------------------------------------------------------
    def evaluate(self, text):
        """Evaluate one input line and record its result."""
        if text == "":
            return EvalResult("empty", text)
        digits = self.settings.precision
        base = self.settings.base
        try:
            with mpmath.workdps(working_digits(digits, base) + GUARD_DIGITS):
                if text.strip() == "":
                    raise CalcError("Empty expression")
                builtins = Functions(self.settings.angle)
                tree = self.parse(text, builtins)
                if tree[0] == "assign":
                    return self.assign(text, tree, builtins)
                if tree[0] == "define":
                    return self.define(text, tree, builtins)
                value = self.eval_node(tree, builtins, {}, 0)
                shown = format_number(value, digits, base)
        except CalcError as exc:
            return EvalResult("error", text, f"ERROR:{exc}")
        self.answer = value
        self.results.append(value)
        return EvalResult("result", text, shown, value)

    def parse(self, text, builtins):
        # Definitions keep infix form even in RPN mode [DEF]
        if self.settings.rpn and "=" not in text:
            return parse_rpn(text, lambda name: self.arity_of(name, builtins))
        return parse_line(text, is_function=lambda name: self.arity_of(name, builtins) is not None)

    def arity_of(self, name, builtins):
        key = name.lower()
        if key in builtins.names():
            return 1
        if key in self.functions:
            return len(self.functions[key].params)
        return None

    #------------------------------------------------------------------
    #  Definitions
    #------------------------------------------------------------------
    def check_name(self, name, builtins):
        key = name.lower()
        if key in RESERVED or key in builtins.names():
            # Original accepted sin(x)=1 and ignored it [FIX]
            raise CalcError(f"'{name}' is a reserved name")

    def assign(self, text, tree, builtins):
        _, name, expr = tree
        self.check_name(name, builtins)
        if name.lower() in self.functions:
            raise CalcError(f"'{name}' is already a function")
        value = self.eval_node(expr, builtins, {}, 0)
        self.variables[name.lower()] = (self.display_name(name), value)
        return EvalResult("definition", text, "Variable Added", value)

    def define(self, text, tree, builtins):
        _, name, params, body = tree
        self.check_name(name, builtins)
        for param in params:
            self.check_name(param, builtins)
        if name.lower() in self.variables:
            raise CalcError(f"'{name}' is already a variable")
        if self.calls_itself(body, name.lower()):
            raise CalcError(f"'{name}' cannot call itself")
        self.functions[name.lower()] = UserFunction(self.display_name(name), params, body, text)
        return EvalResult("definition", text, "Function Added")

    def display_name(self, name):
        """A name keeps the spelling it was first defined with (SPEC 5.1)."""
        key = name.lower()
        if key in self.variables:
            return self.variables[key][0]
        if key in self.functions:
            return self.functions[key].name
        return name

    def calls_itself(self, node, own):
        """True if the tree contains a call to `own` (direct recursion)."""
        if isinstance(node, list):
            return any(self.calls_itself(child, own) for child in node)
        if not isinstance(node, tuple):
            return False
        if node[0] == "call" and node[1].lower() == own:
            return True
        return any(self.calls_itself(child, own) for child in node[1:])

    #------------------------------------------------------------------
    #  Evaluation
    #------------------------------------------------------------------
    def eval_node(self, node, builtins, scope, depth):
        kind = node[0]
        if kind == "num":
            _, literal, base = node
            if base == 10:
                return mpmath.mpf(literal)
            return mpmath.mpf(int(literal, base))
        if kind == "imag":
            return mpmath.mpc(0, mpmath.mpf(node[1]))
        if kind == "name":
            return self.lookup(node[1], builtins, scope)
        if kind == "ref":
            return self.result_ref(node[1])
        if kind == "call":
            args = [self.eval_node(arg, builtins, scope, depth) for arg in node[2]]
            return self.call(node[1], args, builtins, depth)
        if kind == "neg":
            return -self.eval_node(node[1], builtins, scope, depth)
        if kind == "pos":
            return self.eval_node(node[1], builtins, scope, depth)
        if kind == "post":
            return postfix(node[1], self.eval_node(node[2], builtins, scope, depth))
        if kind == "mul":
            left = self.eval_node(node[1], builtins, scope, depth)
            return binary("*", left, self.eval_node(node[2], builtins, scope, depth))
        if kind == "bin":
            left = self.eval_node(node[2], builtins, scope, depth)
            return binary(node[1], left, self.eval_node(node[3], builtins, scope, depth))
        if kind == "conv":
            value = self.eval_node(node[3], builtins, scope, depth)
            if isinstance(value, mpmath.mpc):
                raise CalcError("Cannot convert a complex value")
            return convert(value, node[1], node[2])
        raise CalcError(f"Cannot evaluate '{kind}'")

    def call(self, name, args, builtins, depth):
        key = name.lower()
        if key in builtins.names():
            return builtins.call(name, args)
        fn = self.functions.get(key)
        if fn is None:
            raise CalcError(f"Unknown function '{name}'")
        if len(args) != len(fn.params):
            raise CalcError(f"{fn.name} expects {len(fn.params)} argument(s)")
        if depth >= MAX_CALL_DEPTH:
            raise CalcError("Functions call each other too deeply")
        scope = {p.lower(): v for p, v in zip(fn.params, args)}
        return self.eval_node(fn.body, builtins, scope, depth + 1)

    def lookup(self, name, builtins, scope):
        key = name.lower()
        if key in scope:
            return scope[key]
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
        if key in self.variables:
            return self.variables[key][1]
        if key in builtins.names() or key in self.functions:
            raise CalcError(f"{key} needs arguments in ( )")
        raise CalcError(f"Unknown name '{name}'")

    def result_ref(self, n):
        """`$1` is the latest result, `$2` the one before (SPEC 5.3)."""
        if n < 1 or n > len(self.results):
            raise CalcError(f"No result ${n}")
        return self.results[-n]
