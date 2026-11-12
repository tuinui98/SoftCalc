"""softcalc_engine/engine.py - Evaluate input lines and keep session state.

One Engine instance serves one calculator window. It holds the settings,
user variables (stored by value) and functions (stored as trees, looked
up at call time), `answer`, and the result stack behind `$n`, and turns
each input line into an EvalResult. It also exports and imports the
`.pcf` definitions text (SPEC 8).
"""

import math
import random
import re
from dataclasses import dataclass, field
from fractions import Fraction

import mpmath

from .dates import (DATE_LITERAL, DURATION_LITERAL, ZONES, DateValue, DurationValue, TextValue,
                    convert_zone, now, parse_duration, parse_literal, today)
from . import dates
from .errors import CalcError
from .formatting import RADIX, format_number, format_real, is_exact, working_digits
from .functions import RANDOM_FUNCTIONS, WORD_FUNCTIONS, Functions, binary, negate, postfix
from .lexer import join_digit_spaces, tokenize
from .parser import parse, parse_line
from .rpn import parse_rpn
from .units import RNW_NAMES, canonical, convert, rnw_text, rnw_value
from . import currency
from . import fastplot

GUARD_DIGITS = 10
PLOT_DIGITS = 20
EQUATION_REFINE = 6    # [DEF] each crossed coarse cell is split 6 x 6
MAX_EQUATION_CELLS = 40000
MAX_CALL_DEPTH = 200
CONSTANT_NAMES = {"pi", "e", "i", "j", "today", "now"}
ANSWER_NAMES = {"answer", "ans"}
RESERVED = CONSTANT_NAMES | ANSWER_NAMES | {"mod"}
DEFAULT_FUNCTIONS = ["g(x)=x", "function(var)=var*cos(var)"]


@dataclass
class Settings:
    """Calculation and display settings mirrored from the View menu.

    decimals: fixed number of decimal places to show, or None for all
    significant digits; pad_zeros shows trailing zeros up to decimals
    (extensions.md E9). Neither changes the calculation precision.
    word_size: bits for twos() and signed() called without them.
    """

    precision: int = 32
    angle: str = "rad"
    base: str = "dec"
    rpn: bool = False
    decimals: int = None
    pad_zeros: bool = False
    word_size: int = 32


@dataclass
class EvalResult:
    """Outcome of one input line.

    kind: "result", "definition", "error", "empty" (nothing to add), or
    "rates_needed" (fetch currency rates and evaluate again; E8).
    input: the line as typed (recalled when a History line is clicked).
    display_input: the line as History shows it, with variables, answer
    and $n replaced by their values (extensions.md E10).
    text: the History result line (number, message, or error text).
    note: the ECB rate date when a currency rate was used, else "".
    """

    kind: str
    input: str
    text: str = ""
    display_input: str = ""
    value: object = field(default=None, repr=False)
    note: str = ""


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
        self.rates = currency.Rates()
        self.used_rates = False
        self.random = random.Random()    # rand(); seeded from the system until rands(seed)
        self.draws = {}    # id of a rand() node in the current line -> its value, for History
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
        self.used_rates = False
        self.draws = {}
        result = self.evaluate_line(text)
        if self.used_rates and result.kind in ("result", "definition"):
            result.note = self.rates.note()
        return result

    def evaluate_line(self, text):
        if text == "":
            return EvalResult("empty", text)
        try:
            with mpmath.workdps(working_digits(self.settings.precision, self.settings.base) + GUARD_DIGITS):
                if text.strip() == "":
                    raise CalcError("Empty expression")
                builtins = Functions(self.settings.angle, self.settings.word_size)
                tree = self.parse(text, builtins)
                if tree[0] == "assign":
                    return self.assign(text, tree, builtins)
                if tree[0] == "define":
                    return self.define(text, tree, builtins)
                value = self.eval_node(tree, builtins, {}, 0)
                shown = self.format_value(value)
                display = (self.display_line(text, builtins, self.drawn_in(tree)) + self.word_suffix(text, value)
                           + self.rnw_suffix(text, value))
        except currency.RatesNeeded:
            return EvalResult("rates_needed", text)
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
    def display_line(self, text, builtins, drawn=()):
        """Rebuild the input with values in place of variables, answer and $n.

        Explicit `*` is added where a value replaces a name inside an
        implicit product, so `2x` shows as `2*5`. Number literals typed
        in another base than the output are shown in the output base
        when that is exact (`literal_text`). In RPN mode each
        space-separated item is replaced on its own and spaces stay.
        """
        if self.settings.rpn:
            return re.sub(r"\S+", lambda m: self.display_rpn_item(m.group(), builtins), text)
        source = self.fill_draws(self.fill_word_size(join_digit_spaces(text)), drawn)
        tokens = tokenize(source)
        out = []
        cursor = 0
        starred = -1    # index of a token that already got a `*` after it
        for index, token in enumerate(tokens[:-1]):
            value = self.display_value(token, tokens[index + 1], builtins)
            if value is None:
                continue
            end = token.end if token.end >= 0 else token.pos + len(token.text) + (1 if token.kind == "ref" else 0)
            # `2x` with both replaced gets one `*`, not `2**5`
            joined = index > 0 and self.ends_operand(tokens[index - 1]) and starred != index - 1
            prefix = "*" if joined else ""
            suffix = "*" if self.starts_operand(tokens[index + 1]) else ""
            if suffix:
                starred = index
            out.append(source[cursor:token.pos] + prefix + value + suffix)
            cursor = end
        out.append(source[cursor:])
        return "".join(out)

    def fill_word_size(self, source):
        """`twos(-1)` -> `twos(-1, 32)`: the bits used, so the line still
        reads right after the word size setting changes (owner 2026-11-07).
        """
        tokens = tokenize(source)
        inserts = []
        for index, token in enumerate(tokens[:-1]):
            opens = tokens[index + 1]
            if token.kind != "name" or token.text.lower() not in WORD_FUNCTIONS or opens.text != "(":
                continue
            depth = commas = 0
            for later in tokens[index + 1:]:
                if later.kind != "op":
                    continue
                if later.text == "(":
                    depth += 1
                elif later.text == ")":
                    depth -= 1
                    if depth == 0:
                        if commas == 0:
                            inserts.append(later.pos)
                        break
                elif later.text == "," and depth == 1:
                    commas += 1
        for pos in reversed(inserts):
            source = f"{source[:pos]}, {self.settings.word_size}{source[pos:]}"
        return source

    def drawn_in(self, tree):
        """The rand() values of the line, in the order the calls are written."""
        values = []

        def walk(node):
            if isinstance(node, list):
                for child in node:
                    walk(child)
                return
            if not isinstance(node, tuple):
                return
            if node[0] == "call" and node[1].lower() == "rand" and id(node) in self.draws:
                values.append(self.draws[id(node)])
            for child in node[1:]:
                walk(child)

        walk(tree)
        return values

    def fill_draws(self, source, drawn):
        """`rand(1, 6)+1` -> `4+1`: each drawn value in place of its call
        (owner 2026-11-12). A rand() inside another one goes with it.
        """
        if not drawn:
            return source
        tokens = tokenize(source)
        out = []
        cursor = 0
        count = 0
        for index, token in enumerate(tokens[:-1]):
            if token.kind != "name" or token.text.lower() != "rand" or tokens[index + 1].text != "(":
                continue
            number = count
            count += 1
            if token.pos < cursor or number >= len(drawn):
                continue    # inside a call already replaced
            close = self.closing(tokens, index + 1)
            if close is None:
                return source
            before = tokens[index - 1] if index > 0 else None
            joined = before is not None and (self.ends_operand(before) or before.kind == "name")
            out.append(source[cursor:token.pos] + ("*" if joined else "") + self.wrap(drawn[number]))
            cursor = tokens[close].pos + 1
        out.append(source[cursor:])
        return "".join(out)

    @staticmethod
    def closing(tokens, opening):
        """Index of the `)` that closes the `(` at tokens[opening]."""
        depth = 0
        for index in range(opening, len(tokens)):
            token = tokens[index]
            if token.kind == "op" and token.text == "(":
                depth += 1
            elif token.kind == "op" and token.text == ")":
                depth -= 1
                if depth == 0:
                    return index
        return None

    #------------------------------------------------------------------
    #  rand() and rands() (owner 2026-11-12)
    #------------------------------------------------------------------
    def random_call(self, node, args):
        """rand(): 0 to 1; rand(n): 0 to n; rand(a, b): a to b. Whole
        numbers in, both ends included, give a whole number; a fraction or
        a literal with a point (`rand(1.0, 10)`) gives a decimal.
        rands(seed) makes the next draws repeat; rands() unseeds.
        """
        key = node[1].lower()
        Functions.check_count(key, args, 0, 1 if key == "rands" else 2)
        for arg in args:
            if not isinstance(arg, mpmath.mpf):
                raise CalcError(f"{key} needs real numbers")
        if key == "rands":
            if not args:
                self.random.seed()
                return TextValue("Random seed")
            seed = args[0]
            self.random.seed(str(int(seed)) if seed == mpmath.floor(seed) else mpmath.nstr(seed, 50))
            return TextValue(f"Seed {self.format_value(seed)}")
        if not args:
            return self.random_fraction()
        low, high = (mpmath.mpf(0), args[0]) if len(args) == 1 else (args[0], args[1])
        if low > high:
            low, high = high, low
        decimal = any(self.has_point(arg) for arg in node[2]) or any(a != mpmath.floor(a) for a in args)
        if not decimal:
            return mpmath.mpf(self.random.randint(int(low), int(high)))
        return low + (high - low) * self.random_fraction()

    def random_fraction(self):
        """A uniform value in [0, 1) with as many decimal digits as the precision."""
        digits = self.settings.precision
        return mpmath.mpf(self.random.randrange(10 ** digits)) / mpmath.mpf(10) ** digits

    def has_point(self, node):
        """True if the argument is written with a decimal point or exponent (`1.0`, `1e3`)."""
        if isinstance(node, list):
            return any(self.has_point(child) for child in node)
        if not isinstance(node, tuple):
            return False
        if node[0] == "num":
            return node[2] == 10 and any(c in node[1] for c in ".eE")
        return any(self.has_point(child) for child in node[1:])

    def word_suffix(self, text, value):
        """` = 0xFF` after a line that uses twos() or signed(): the whole
        line's result in hex, the base those are read in (owner 2026-11-07).
        Nothing when the output is hex already or the result is not real.
        """
        if self.settings.base == "hex" or not isinstance(value, mpmath.mpf):
            return ""
        try:
            names = {token.text.lower() for token in tokenize(text) if token.kind == "name"}
        except CalcError:
            return ""
        if not names & set(WORD_FUNCTIONS):
            return ""
        return " = " + format_real(value, self.settings.precision, "hex")

    def rnw_suffix(self, text, value):
        """` = 4-1-0` when the line's last tag converts to rai-ngan-wa
        (`[rnw]`, `[m2->rnw]`): the result, in rai, as rai-ngan-wa
        (owner 2026-11-08). Nothing for a result that is not real.
        """
        if not isinstance(value, mpmath.mpf):
            return ""
        try:
            tags = [token.text for token in tokenize(text) if token.kind == "tag"]
        except CalcError:
            return ""
        if not tags or canonical(tags[-1].partition("->")[2] or tags[-1]) not in RNW_NAMES:
            return ""
        s = self.settings
        return " = " + rnw_text(value, s.precision, s.decimals, s.pad_zeros, format_number)

    def display_rpn_item(self, word, builtins):
        tokens = tokenize(word)
        # An RPN negative literal (`-0x1F`) is one item: keep the sign
        if len(tokens) == 3 and tokens[0].text == "-" and tokens[1].kind == "number":
            value = self.literal_text(tokens[1].text, tokens[1].base)
            return word if value is None else "-" + value
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
        if token.kind == "number":
            return self.literal_text(token.text, token.base)
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

    def literal_text(self, literal, base):
        """A number literal in the output base, or None to keep it as typed.

        Only for literals typed in another base, and only when the output
        base shows them exactly (owner 2026-11-07, option B): `0x1F` is
        `31` in decimal, `0.5` is `0x0.8` in hex, `0.1` stays. A literal
        has no sign; a `-` before it stays an operator.
        """
        output = self.settings.base
        if base == RADIX[output]:
            return None
        exact = Fraction(literal) if base == 10 else Fraction(int(literal, base))
        if not is_exact(exact, self.settings.precision, output):
            return None
        bits = exact.numerator.bit_length() + exact.denominator.bit_length() + 16
        with mpmath.workprec(max(bits, mpmath.mp.prec)):
            value = mpmath.mpf(exact.numerator) / exact.denominator
            return format_real(value, self.settings.precision, output)

    def literal_hint(self, text):
        """Hint-line text for one literal (`0x1F` -> `31`), or None."""
        try:
            tokens = tokenize(text.strip())
        except CalcError:
            return None
        if re.fullmatch(r"\d+(?:\.\d+)?-\d+(?:\.\d+)?-\d+(?:\.\d+)?", text.strip()):
            with self.working_context():
                return self.format_value(rnw_value(text.strip())) + " rai"    # `4-1-50 = 4.375 rai`
        if len(tokens) != 2 or tokens[0].kind != "number":
            return None
        with self.working_context():
            return self.literal_text(tokens[0].text, tokens[0].base)

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
        if kind == "rnw":
            return rnw_value(node[1])
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
            if node[1].lower() in RANDOM_FUNCTIONS:
                value = self.random_call(node, args)
                if depth == 0:
                    self.draws[id(node)] = value
                return value
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
            if currency.is_currency(node[1], self.rates) or currency.is_currency(node[2], self.rates):
                converted = currency.convert(value, node[1], node[2], self.rates)
                self.used_rates = True
                return converted
            return convert(value, node[1], node[2])
        if kind == "fmt":
            fn = dates.FUNCTIONS.get(node[1])
            if fn is None:
                raise CalcError(f"Unknown format [{node[1]}]")
            return fn(self.eval_node(node[2], builtins, scope, depth))
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
        if key == "now":
            return now()
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
    #  Queries for the UI (design/protocol.md)
    #------------------------------------------------------------------
    def working_context(self):
        return mpmath.workdps(working_digits(self.settings.precision, self.settings.base) + GUARD_DIGITS)

    def value_of(self, text):
        """Evaluate an expression without touching History, answer or $n."""
        with self.working_context():
            builtins = Functions(self.settings.angle, self.settings.word_size)
            tree = parse_line(text, is_function=lambda name: self.is_function(name, builtins))
            if tree[0] in ("assign", "define"):
                raise CalcError("A value is needed here, not a definition")
            return self.eval_node(tree, builtins, {}, 0)

    def state(self):
        """Constants, user variables and user functions for the sidebar."""
        with self.working_context():
            constants = [{"name": "pi", "value": self.format_value(+mpmath.pi)},
                         {"name": "e", "value": self.format_value(+mpmath.e)}]
            variables = [{"name": name, "value": self.format_value(value)}
                         for name, value in self.variables.values()]
        functions = []
        for fn in self.functions.values():
            head = f"{fn.name}({', '.join(fn.params)})="
            functions.append({"name": fn.name, "params": list(fn.params), "body": fn.body_text,
                              "head": head, "source": head + fn.body_text})
        return {"constants": constants, "variables": variables, "functions": functions}

    def restore_results(self, texts):
        """Rebuild `answer` and the `$n` stack from saved History result texts.

        Texts are oldest first, as shown on screen (hex, binary, dates,
        durations and complex forms parse back). A text-only result, or
        one that no longer parses, comes back as text, so `$n` counts
        the same lines as before the restart and as History shows them.
        Returns the count that parsed back as values.
        """
        restored = 0
        for text in texts:
            try:
                value = self.value_of(text)
            except CalcError:
                value = TextValue(text)
            if isinstance(value, TextValue):
                value = TextValue(text)
            else:
                restored += 1
            self.results.append(value)
            self.answer = value
        return restored

    def delete_result(self, n):
        """Remove `$n` (1 = newest) when its History line is deleted.

        Later references renumber; `answer` stays `$1`, and keeps its
        value when the last result goes (as Clear History does).
        """
        if not 1 <= n <= len(self.results):
            raise CalcError(f"${n} does not exist")
        del self.results[-n]
        if self.results:
            self.answer = self.results[-1]

    def delete(self, name):
        """Remove a user variable or function; returns which one it was."""
        key = name.lower()
        if key in self.variables:
            del self.variables[key]
            return "variable"
        if key in self.functions:
            del self.functions[key]
            return "function"
        raise CalcError(f"No user variable or function '{name}'")

    def plot(self, name, mode, start, end, samples):
        """Sample a one-parameter user function; None where it is undefined."""
        fn = self.functions.get(name.lower())
        if fn is None or len(fn.params) != 1:
            raise CalcError(f"'{name}' is not a one-parameter function")
        if samples < 2:
            raise CalcError("samples must be at least 2")
        points = []
        with mpmath.workdps(PLOT_DIGITS):
            builtins = Functions(self.settings.angle, self.settings.word_size)
            fast = self.fast_function(fn.body, fn.params, builtins)
            if fast is not None:
                step = (end - start) / (samples - 1)
                return [self.fast_point(fast, start + step * index, mode) for index in range(samples)]
            step = (mpmath.mpf(end) - start) / (samples - 1)
            for index in range(samples):
                t = mpmath.mpf(start) + step * index
                points.append(self.plot_point(fn, builtins, t, mode))
        return points

    def fast_function(self, tree, params, builtins):
        """The float version of a tree for drawing, or None (fastplot)."""
        try:
            return fastplot.compile_tree(self, tree, params, builtins)
        except fastplot.Unsupported:
            return None

    @staticmethod
    def fast_point(fast, t, mode):
        try:
            value = fast(t)
        except fastplot.Undefined:
            return None
        if mode == "polar":
            return [value * math.cos(t), value * math.sin(t)]
        return [t, value]

    def plot_point(self, fn, builtins, t, mode):
        try:
            value = self.call(fn.name, [t], builtins, 0)
        except CalcError:
            return None
        if isinstance(value, mpmath.mpc):
            if abs(value.imag) > abs(value) * mpmath.mpf(10) ** -10:
                return None
            value = value.real
        if not isinstance(value, mpmath.mpf) or mpmath.isinf(value) or mpmath.isnan(value):
            return None
        if mode == "polar":
            return [float(value * mpmath.cos(t)), float(value * mpmath.sin(t))]
        return [float(t), float(value)]

    #------------------------------------------------------------------
    #  Equations in x and y (implicit plot, design/graph2_plan.md G5)
    #------------------------------------------------------------------
    def equation_tree(self, text, builtins):
        """Parse `lhs = rhs` into the tree of lhs - rhs; x and y are the axes."""
        sides = text.split("=")
        if len(sides) != 2:
            raise CalcError("An equation needs one '='")
        lhs, rhs = (side.strip() for side in sides)
        if not lhs or not rhs:
            raise CalcError("An equation needs something on both sides of '='")
        is_function = lambda name: self.is_function(name, builtins)
        tree = ("bin", "-", parse(lhs, is_function), parse(rhs, is_function))
        names = set(self.names_in(tree))
        if not names & {"x", "y"}:
            raise CalcError("An equation needs x or y")
        axes = {"x": mpmath.mpf(1), "y": mpmath.mpf(1)}
        for name in names - {"x", "y"}:
            self.lookup(name, builtins, axes)    # unknown names fail here, not as an empty plot
        return tree

    def names_in(self, node):
        """Every variable name read in a tree, lower case."""
        if isinstance(node, list):
            for child in node:
                yield from self.names_in(child)
            return
        if not isinstance(node, tuple):
            return
        if node[0] == "name":
            yield node[1].lower()
            return
        for child in node[1:]:
            yield from self.names_in(child)

    def equation_value(self, tree, builtins, x, y):
        """lhs - rhs at (x, y) as a float; None where undefined or not real."""
        try:
            value = self.eval_node(tree, builtins, {"x": x, "y": y}, 0)
        except CalcError:
            return None
        if isinstance(value, mpmath.mpc):
            if abs(value.imag) > abs(value) * mpmath.mpf(10) ** -10:
                return None
            value = value.real
        if not isinstance(value, mpmath.mpf) or mpmath.isinf(value) or mpmath.isnan(value):
            return None
        return float(value)

    def plot_equation(self, text, x_min, x_max, y_min, y_max, cols, rows, angle=None):
        """Line segments [x1, y1, x2, y2] where lhs = rhs in the rectangle.

        angle overrides the angle setting for this drawing only (the
        Graph Examples pictures are drawn in radians whatever the mode).

        lhs - rhs is sampled on a cols x rows grid; only cells where it
        changes sign (or becomes undefined) are split EQUATION_REFINE
        times and traced by marching squares. A sign change through a
        pole (`y = 1/x` at x = 0) is dropped: there the middle of the
        cell is farther from zero than its corners. A curve that only
        touches zero without crossing, or is smaller than a coarse cell,
        is not drawn.
        """
        if cols < 1 or rows < 1 or cols * rows > MAX_EQUATION_CELLS:
            raise CalcError("Bad equation grid")
        if not x_max > x_min or not y_max > y_min:
            raise CalcError("Empty range")
        refine = EQUATION_REFINE
        with mpmath.workdps(PLOT_DIGITS):
            builtins = Functions(angle or self.settings.angle, self.settings.word_size)
            tree = self.equation_tree(text, builtins)
            dx = (x_max - x_min) / (cols * refine)
            dy = (y_max - y_min) / (rows * refine)
            grid = {}
            fast = self.fast_function(tree, ["x", "y"], builtins)

            def value(x, y):
                if fast is None:
                    return self.equation_value(tree, builtins, mpmath.mpf(x), mpmath.mpf(y))
                try:
                    return fast(x, y)
                except fastplot.Undefined:
                    return None

            def at(i, j):
                key = (i, j)
                if key not in grid:
                    grid[key] = value(x_min + i * dx, y_min + j * dy)
                return grid[key]

            segments = []
            for ci in range(cols):
                for cj in range(rows):
                    corners = [at(ci * refine, cj * refine), at((ci + 1) * refine, cj * refine),
                               at((ci + 1) * refine, (cj + 1) * refine), at(ci * refine, (cj + 1) * refine)]
                    known = [v for v in corners if v is not None]
                    mixed = any(v < 0 for v in known) and any(v >= 0 for v in known)
                    if not mixed and (len(known) == 4 or not known):
                        continue
                    for i in range(ci * refine, (ci + 1) * refine):
                        for j in range(cj * refine, (cj + 1) * refine):
                            self.trace_cell(value, at, i, j, x_min, y_min, dx, dy, segments)
        return segments

    @staticmethod
    def trace_cell(value, at, i, j, x_min, y_min, dx, dy, segments):
        """Marching squares on one fine cell; appends its segments."""
        values = [at(i, j), at(i + 1, j), at(i + 1, j + 1), at(i, j + 1)]
        if any(v is None for v in values):
            return
        if all(v < 0 for v in values) or all(v >= 0 for v in values):
            return
        corners = [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)]
        crossings = []
        for edge in range(4):
            a, b = values[edge], values[(edge + 1) % 4]
            if (a < 0) != (b < 0):
                share = a / (a - b)
                (ai, aj), (bi, bj) = corners[edge], corners[(edge + 1) % 4]
                crossings.append((x_min + (ai + (bi - ai) * share) * dx, y_min + (aj + (bj - aj) * share) * dy))
        middle = value(x_min + (i + 0.5) * dx, y_min + (j + 0.5) * dy)
        if middle is None or abs(middle) > 2 * max(abs(v) for v in values):
            return    # a pole or a jump, not a crossing
        if len(crossings) == 2:
            segments.append([*crossings[0], *crossings[1]])
        elif len(crossings) == 4:
            # saddle: the middle's sign says which corners connect
            first, second = ((0, 1), (2, 3)) if (middle < 0) == (values[0] < 0) else ((0, 3), (1, 2))
            for a, b in (first, second):
                segments.append([*crossings[a], *crossings[b]])

    def equation_at(self, text, axis, value, guess, window):
        """Point on the curve with one coordinate fixed (graph trace, G6).

        axis: "x" fixes x = value (text, exact) and solves for y near
        guess; "y" the other way round. Full precision. Returns
        (x_text, y_text, x_float, y_float), or None when no root lies
        within `window` of the guess.
        """
        with mpmath.workdps(working_digits(self.settings.precision, self.settings.base) + GUARD_DIGITS):
            builtins = Functions(self.settings.angle, self.settings.word_size)
            tree = self.equation_tree(text, builtins)
            try:
                fixed = mpmath.mpf(value)
            except ValueError:
                raise CalcError(f"Not a number: '{value}'") from None

            def f(t):
                point = {"x": fixed, "y": t} if axis == "x" else {"x": t, "y": fixed}
                result = self.eval_node(tree, builtins, point, 0)
                if isinstance(result, mpmath.mpc):
                    result = result.real
                return result

            try:
                root = mpmath.findroot(f, (mpmath.mpf(guess) - window / 4, mpmath.mpf(guess) + window / 4), solver="secant")
            except (CalcError, ValueError, ZeroDivisionError, TypeError):
                return None
            if isinstance(root, mpmath.mpc) or abs(root - mpmath.mpf(guess)) > window:
                return None
            x, y = (fixed, root) if axis == "x" else (root, fixed)
            return self.format_value(x), self.format_value(y), float(x), float(y)

    @staticmethod
    def trace_number(t):
        """t from the UI: decimal text ("1.43", exact as shown) or a float."""
        try:
            return mpmath.mpf(t)
        except ValueError:
            raise CalcError(f"Not a number: '{t}'") from None

    def value_at(self, name, t):
        """Full-precision value of a one-parameter function at t (graph trace, G2).

        Returns (text, float) formatted like a result, or (None, None)
        where the function is undefined or not real.
        """
        fn = self.functions.get(name.lower())
        if fn is None or len(fn.params) != 1:
            raise CalcError(f"'{name}' is not a one-parameter function")
        with mpmath.workdps(working_digits(self.settings.precision, self.settings.base) + GUARD_DIGITS):
            builtins = Functions(self.settings.angle, self.settings.word_size)
            x = self.trace_number(t)    # bad text is an error, not an undefined point
            try:
                value = self.call(fn.name, [x], builtins, 0)
            except CalcError:
                return None, None
            if isinstance(value, mpmath.mpc):
                if abs(value.imag) > abs(value) * mpmath.mpf(10) ** -10:
                    return None, None
                value = value.real
            if not isinstance(value, mpmath.mpf) or mpmath.isinf(value) or mpmath.isnan(value):
                return None, None
            return self.format_value(value), float(value)

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
