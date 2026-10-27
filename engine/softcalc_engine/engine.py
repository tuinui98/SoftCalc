"""softcalc_engine/engine.py - Evaluate input lines and keep session state.

One Engine instance serves one calculator window. It holds the settings,
user variables (stored by value) and functions (stored as trees, looked
up at call time), `answer`, and the result stack behind `$n`, and turns
each input line into an EvalResult. It also exports and imports the
`.pcf` definitions text (SPEC 8).
"""

import re
from dataclasses import dataclass, field

import mpmath

from .dates import (DATE_LITERAL, DURATION_LITERAL, ZONES, DateValue, DurationValue, TextValue,
                    convert_zone, parse_duration, parse_literal, today)
from .errors import CalcError
from .formatting import format_number, working_digits
from .functions import Functions, binary, negate, postfix
from .lexer import join_digit_spaces, tokenize
from .parser import parse_line
from .rpn import parse_rpn
from .units import convert

GUARD_DIGITS = 10
MAX_CALL_DEPTH = 200
CONSTANT_NAMES = {"pi", "e", "i", "j", "today"}
ANSWER_NAMES = {"answer", "ans"}
RESERVED = CONSTANT_NAMES | ANSWER_NAMES | {"mod"}
DEFAULT_FUNCTIONS = ["g(x)=x", "function(var)=var*cos(var)"]


@dataclass
class Settings:
    """Calculation and display settings mirrored from the View menu.

    decimals: fixed number of decimal places to show, or None for all
    significant digits; pad_zeros shows trailing zeros up to decimals
    (extensions.md E9). Neither changes the calculation precision.
    """

    precision: int = 32
    angle: str = "rad"
    base: str = "dec"
    rpn: bool = False
    decimals: int = None
    pad_zeros: bool = False


@dataclass
class EvalResult:
    """Outcome of one input line.

    kind: "result", "definition", "error", or "empty" (nothing to add).
    input: the line as typed (recalled when a History line is clicked).
    display_input: the line as History shows it, with variables, answer
    and $n replaced by their values (extensions.md E10).
    text: the History result line (number, message, or error text).
    """

    kind: str
    input: str
    text: str = ""
    display_input: str = ""
    value: object = field(default=None, repr=False)


@dataclass
class UserFunction:
    """A user-defined function; `body_text` is the body as typed."""

    name: str
    params: list
    body: tuple
    body_text: str


FUNCTION_HEAD = re.compile(r"\s*([A-Za-z_][A-Za-z0-9_]*)\s*\([^)]*\)\s*=")
PLACEHOLDER = UserFunction("", [], ("num", "0", 10), "")    # import-time stand-in


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
        try:
            with mpmath.workdps(working_digits(self.settings.precision, self.settings.base) + GUARD_DIGITS):
                if text.strip() == "":
                    raise CalcError("Empty expression")
                builtins = Functions(self.settings.angle)
                tree = self.parse(text, builtins)
                if tree[0] == "assign":
                    return self.assign(text, tree, builtins)
                if tree[0] == "define":
                    return self.define(text, tree, builtins)
                value = self.eval_node(tree, builtins, {}, 0)
                shown = self.format_value(value)
                display = self.display_line(text, builtins)
        except CalcError as exc:
            return EvalResult("error", text, f"ERROR:{exc}", text)
        self.answer = value
        self.results.append(value)
        return EvalResult("result", text, shown, display, value)

    def parse(self, text, builtins):
        # Definitions keep infix form even in RPN mode [DEF]
        if self.settings.rpn and "=" not in text:
            return parse_rpn(text, lambda name: self.rpn_arity(name, builtins))
        return parse_line(text, is_function=lambda name: self.is_function(name, builtins))

    def is_function(self, name, builtins):
        key = name.lower()
        return key in builtins.names() or key in self.functions

    def rpn_arity(self, name, builtins):
        key = name.lower()
        if key in builtins.names():
            return builtins.min_arity(key)
        if key in self.functions:
            return len(self.functions[key].params)
        return None

    def format_value(self, value):
        if isinstance(value, (DateValue, DurationValue)):
            return value.text()
        if isinstance(value, TextValue):
            return value.text
        s = self.settings
        return format_number(value, s.precision, s.base, s.decimals, s.pad_zeros)

    #------------------------------------------------------------------
    #  History display (E10)
    #------------------------------------------------------------------
    def display_line(self, text, builtins):
        """Rebuild the input with values in place of variables, answer and $n.

        Explicit `*` is added where a value replaces a name inside an
        implicit product, so `2x` shows as `2*5`. In RPN mode each
        space-separated item is replaced on its own and spaces stay.
        """
        if self.settings.rpn:
            return re.sub(r"\S+", lambda m: self.display_rpn_item(m.group(), builtins), text)
        source = join_digit_spaces(text)
        tokens = tokenize(source)
        out = []
        cursor = 0
        for index, token in enumerate(tokens[:-1]):
            value = self.display_value(token, tokens[index + 1], builtins)
            if value is None:
                continue
            end = token.pos + len(token.text) + (1 if token.kind == "ref" else 0)
            prefix = "*" if index > 0 and self.ends_operand(tokens[index - 1]) else ""
            suffix = "*" if self.starts_operand(tokens[index + 1]) else ""
            out.append(source[cursor:token.pos] + prefix + value + suffix)
            cursor = end
        out.append(source[cursor:])
        return "".join(out)

    def display_rpn_item(self, word, builtins):
        tokens = tokenize(word)
        if len(tokens) != 2:
            return word
        value = self.display_value(tokens[0], tokens[1], builtins)
        return word if value is None else value

    def display_value(self, token, following, builtins):
        """Display text for a token that stands for a stored value, else None."""
        key = token.text.lower()
        if token.kind == "ref":
            n = int(token.text)
            return self.wrap(self.results[-n]) if 1 <= n <= len(self.results) else None
        if token.kind != "name":
            return None
        is_call = following.kind == "op" and following.text == "(" and self.is_function(key, builtins)
        if is_call:
            return None
        if key in ANSWER_NAMES and self.answer is not None:
            return self.wrap(self.answer)
        if key in self.variables:
            return self.wrap(self.variables[key][1])
        return None

    def wrap(self, value):
        text = self.format_value(value)
        return f"({text})" if text.startswith("-") else text

    @staticmethod
    def ends_operand(token):
        return token.kind in ("number", "imag") or (token.kind == "op" and token.text == ")")

    @staticmethod
    def starts_operand(token):
        return token.kind in ("name", "number", "imag") or (token.kind == "op" and token.text == "(")

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
        return EvalResult("definition", text, "Variable Added", text, value)

    def define(self, text, tree, builtins):
        _, name, params, body = tree
        self.check_name(name, builtins)
        for param in params:
            self.check_name(param, builtins)
        if name.lower() in self.variables:
            raise CalcError(f"'{name}' is already a variable")
        if self.calls_itself(body, name.lower()):
            raise CalcError(f"'{name}' cannot call itself")
        body_text = text.split("=", 1)[1].strip()
        self.functions[name.lower()] = UserFunction(self.display_name(name), params, body, body_text)
        return EvalResult("definition", text, "Function Added", text)

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
        if kind == "date":
            return parse_literal(DATE_LITERAL.fullmatch(node[1]))
        if kind == "duration":
            return parse_duration(DURATION_LITERAL.fullmatch(node[1]))
        if kind == "name":
            return self.lookup(node[1], builtins, scope)
        if kind == "ref":
            return self.result_ref(node[1])
        if kind == "call":
            args = [self.eval_node(arg, builtins, scope, depth) for arg in node[2]]
            return self.call(node[1], args, builtins, depth)
        if kind == "neg":
            return negate(self.eval_node(node[1], builtins, scope, depth))
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
            if node[1] in ZONES or node[2] in ZONES:
                return convert_zone(value, node[1], node[2])
            if not isinstance(value, mpmath.mpf):
                raise CalcError("Only real numbers can be converted")
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
        if key == "today":
            return today()
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

    #------------------------------------------------------------------
    #  .pcf definitions text (SPEC 8)
    #------------------------------------------------------------------
    def export_pcf(self):
        """Variables as `name=value`, then functions as `name(p1, p2) = body`.

        Values use decimal output at the current precision, so they load
        back the same; the original wrote empty values [FIX].
        """
        lines = []
        with mpmath.workdps(self.settings.precision + GUARD_DIGITS):
            for name, value in self.variables.values():
                if isinstance(value, (DateValue, DurationValue)):
                    lines.append(f"{name}={value.text()}")
                elif not isinstance(value, TextValue):
                    lines.append(f"{name}={format_number(value, self.settings.precision)}")
        for fn in self.functions.values():
            lines.append(f"{fn.name}({', '.join(fn.params)}) = {fn.body_text}")
        return "\r\n".join(lines) + ("\r\n" if lines else "")

    def import_pcf(self, text):
        """Replace user variables and functions with the definitions in text.

        Returns a list of (line number, line, reason) for skipped lines;
        loading never stops at a bad line (the original hung) [FIX].
        """
        self.variables = {}
        self.functions = {}
        # Register every function name first, so a body may call a function
        # defined further down the file instead of parsing as multiplication
        for line in text.splitlines():
            head = FUNCTION_HEAD.match(line)
            if head:
                self.functions[head.group(1).lower()] = PLACEHOLDER
        saved_rpn, self.settings.rpn = self.settings.rpn, False
        skipped = []
        try:
            for number, line in enumerate(text.splitlines(), start=1):
                if not line.strip():
                    continue
                if "=" not in line:
                    skipped.append((number, line, "not a definition"))
                    continue
                head = FUNCTION_HEAD.match(line)
                if head and self.functions.get(head.group(1).lower()) is PLACEHOLDER:
                    del self.functions[head.group(1).lower()]
                result = self.evaluate(line.strip())
                if result.kind != "definition":
                    skipped.append((number, line, result.text.removeprefix("ERROR:")))
        finally:
            self.settings.rpn = saved_rpn
            self.functions = {k: f for k, f in self.functions.items() if f is not PLACEHOLDER}
        return skipped
