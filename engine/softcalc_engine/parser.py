"""softcalc_engine/parser.py - Build an expression tree from tokens.

Recursive-descent parser for the precedence table in SPEC 5.4 (highest
first): primary, postfix `!` `%`, right-associative `^`, implicit
multiplication, unary sign, `* / mod`, `+ -`. Nodes are tuples:

    ("num", text, base)      ("imag", text)       ("name", name)
    ("call", name, [args])   ("neg", node)        ("pos", node)
    ("bin", op, left, right) ("post", op, node)   ("mul", left, right)

("mul" marks implicit multiplication, kept apart for display.)
"""

from .errors import CalcError
from .lexer import tokenize

KEYWORD_MOD = "mod"


class Parser:
    """Parses one input line; use parse_expression() or parse_line()."""

    def __init__(self, text):
        self.tokens = tokenize(text)
        self.pos = 0

    #------------------------------------------------------------------
    #  Token helpers
    #------------------------------------------------------------------
    def peek(self, offset=0):
        return self.tokens[min(self.pos + offset, len(self.tokens) - 1)]

    def advance(self):
        token = self.tokens[self.pos]
        self.pos += 1
        return token

    def at_op(self, symbol):
        token = self.peek()
        return token.kind == "op" and token.text == symbol

    def at_mod(self):
        token = self.peek()
        return token.kind == "name" and token.text.lower() == KEYWORD_MOD

    def expect_op(self, symbol):
        if not self.at_op(symbol):
            raise CalcError(f"Expected '{symbol}'" + self.where())
        self.advance()

    def where(self):
        token = self.peek()
        if token.kind == "end":
            return " at end of input"
        return f" before '{token.text}'"

    #------------------------------------------------------------------
    #  Grammar
    #------------------------------------------------------------------
    def parse_expression(self):
        """Parse a full expression and require the end of input."""
        node = self.additive()
        if self.peek().kind != "end":
            raise CalcError("Unexpected '" + self.peek().text + "'")
        return node

    def additive(self):
        node = self.multiplicative()
        while self.at_op("+") or self.at_op("-"):
            op = self.advance().text
            node = ("bin", op, node, self.multiplicative())
        return node

    def multiplicative(self):
        node = self.signed()
        while self.at_op("*") or self.at_op("/") or self.at_mod():
            op = self.advance().text.lower()
            node = ("bin", op, node, self.signed())
        return node

    def signed(self):
        """Optional single sign. A sign may not follow + or -: `--2`, `1++2` [KEEP]."""
        if self.at_op("-") or self.at_op("+"):
            previous = self.tokens[self.pos - 1] if self.pos > 0 else None
            if previous is not None and previous.kind == "op" and previous.text in "+-":
                raise CalcError("Two signs in a row")
            op = self.advance().text
            if self.at_op("-") or self.at_op("+"):
                raise CalcError("Two signs in a row")
            inner = self.implicit_product()
            return ("neg", inner) if op == "-" else ("pos", inner)
        return self.implicit_product()

    def implicit_product(self):
        node = self.power()
        while self.implicit_follows(node):
            node = ("mul", node, self.power())
        return node

    def implicit_follows(self, left):
        """SPEC 5.4: after a number or `)`, before a name or `(`; after a
        name that is not a call, before `(`. Never two names, never two numbers.
        """
        token = self.peek()
        starts_name = token.kind == "name" and token.text.lower() != KEYWORD_MOD
        starts_paren = token.kind == "op" and token.text == "("
        tail = self.tokens[self.pos - 1]
        tail_is_number = tail.kind in ("number", "imag")
        tail_is_paren = tail.kind == "op" and tail.text == ")"
        # A name right before `(` was not taken as a call, so it is a value
        tail_is_name = tail.kind == "name"
        if tail_is_number or tail_is_paren:
            return starts_name or starts_paren
        if tail_is_name:
            return starts_paren
        return False

    def power(self):
        base = self.postfix()
        if self.at_op("^"):
            self.advance()
            # Right operand is a signed implicit product: 2^-1, 2^3x, 2^3^2 [FIX]
            if self.at_op("-") or self.at_op("+"):
                op = self.advance().text
                exponent = self.implicit_product()
                if op == "-":
                    exponent = ("neg", exponent)
            else:
                exponent = self.implicit_product()
            return ("bin", "^", base, exponent)
        return base

    def postfix(self):
        node = self.primary()
        while self.at_op("!") or self.at_op("%"):
            node = ("post", self.advance().text, node)
        return node

    def primary(self):
        token = self.peek()
        if token.kind == "number":
            self.advance()
            return ("num", token.text, token.base)
        if token.kind == "imag":
            self.advance()
            return ("imag", token.text)
        if token.kind == "name":
            if token.text.lower() == KEYWORD_MOD:
                raise CalcError("'mod' needs a left operand")
            self.advance()
            if self.at_op("(") and self.is_call_name(token.text):
                return self.call(token.text)
            return ("name", token.text)
        if self.at_op("("):
            self.advance()
            node = self.additive()
            self.expect_op(")")
            return node
        if token.kind == "end":
            raise CalcError("Incomplete expression")
        raise CalcError(f"Unexpected '{token.text}'")

    def is_call_name(self, name):
        """Overridden by the evaluator-aware parser to know function names."""
        return True

    def call(self, name):
        self.expect_op("(")
        args = []
        if self.at_op(")"):
            raise CalcError(f"{name}() needs an argument")
        args.append(self.additive())
        while self.at_op(","):
            self.advance()
            args.append(self.additive())
        self.expect_op(")")
        return ("call", name, args)


def parse(text, is_function=None):
    """Parse text as an expression. is_function(name) tells calls from
    implicit multiplication (`a(2)` with variable a is `a*2`).
    """
    parser = Parser(text)
    if is_function is not None:
        parser.is_call_name = is_function
    return parser.parse_expression()
