"""softcalc_engine/parser.py - Build an expression tree from tokens.

Recursive-descent parser for the precedence table in SPEC 5.4 (highest
first): primary, postfix `!` `%`, right-associative `^`, implicit
multiplication, unary sign, `* / mod`, `+ -`, and conversion tags last
(SPEC 5.9). A line may also be a definition (SPEC 5.6, 5.7).

Nodes are tuples:
    ("num", text, base)      ("imag", text)        ("name", name)
    ("ref", n)               ("date", text)        ("duration", text)
    ("call", name, [args])
    ("neg", node)            ("pos", node)         ("post", op, node)
    ("bin", op, left, right) ("mul", left, right)  ("conv", src, dst, node)
    ("fmt", name, node)
    ("assign", name, node)   ("define", name, [params], node)
("mul" marks implicit multiplication, kept apart for display.)
"""

from .errors import CalcError
from .lexer import tokenize
from .units import RNW_NAMES

KEYWORD_MOD = "mod"
CONTINUE_OPS = ("+", "-", "*", "/")


class Parser:
    """Parses one input line with parse_line() or parse_expression()."""

    def __init__(self, text, is_function=None):
        self.tokens = tokenize(text)
        self.pos = 0
        self.is_function = is_function or (lambda name: True)

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

    def expect_end(self):
        if self.peek().kind != "end":
            raise CalcError("Unexpected '" + self.peek().text + "'")

    #------------------------------------------------------------------
    #  Lines and definitions
    #------------------------------------------------------------------
    def parse_expression(self):
        """Parse a full expression and require the end of input."""
        node = self.chain()
        self.expect_end()
        return node

    def parse_line(self):
        """Parse an expression or a variable/function definition."""
        equals = [i for i, t in enumerate(self.tokens) if t.kind == "op" and t.text == "="]
        if not equals:
            return self.parse_expression()
        if len(equals) > 1:
            raise CalcError("Only one '=' is allowed")
        head = self.tokens[:equals[0]]
        if not head or head[0].kind != "name":
            raise CalcError("A definition needs a name before '='")
        name = head[0].text
        if len(head) == 1:
            self.pos = equals[0] + 1
            return ("assign", name, self.definition_body())
        params = self.parameter_list(head)
        self.pos = equals[0] + 1
        own = name.lower()
        outer = self.is_function
        # The function's own name counts as a function, so recursion shows up as a call
        self.is_function = lambda n: n.lower() == own or outer(n)
        return ("define", name, params, self.definition_body())

    def definition_body(self):
        if self.peek().kind == "end":
            raise CalcError("Nothing after '='")
        return self.parse_expression()

    def parameter_list(self, head):
        """Check `name ( p1 , p2 )` before '=' and return the parameter names."""
        texts = [t.text if t.kind == "op" else t.kind for t in head[1:]]
        if len(head) < 4 or texts[0] != "(" or texts[-1] != ")":
            raise CalcError("Malformed definition before '='")
        params = []
        for index, token in enumerate(head[2:-1]):
            if index % 2 == 0:
                if token.kind != "name":
                    raise CalcError("Parameters must be names")
                params.append(token.text)
            elif not (token.kind == "op" and token.text == ","):
                raise CalcError("Parameters must be separated by ','")
        if len(params) != len(head[2:-1]) // 2 + 1:
            raise CalcError("Malformed parameter list")
        if len({p.lower() for p in params}) != len(params):
            raise CalcError("Duplicate parameter name")
        return params

    #------------------------------------------------------------------
    #  Expressions
    #------------------------------------------------------------------
    def chain(self):
        """Additive expression followed by any number of conversion tags.

        A tag converts everything to its left; the expression may go on
        after it: `1 [ft->in]+1` = 13 [KEEP].
        """
        node = self.additive()
        while self.peek().kind == "tag":
            body = self.advance().text
            if "->" in body:
                source, _, target = body.partition("->")
                node = ("conv", source, target, node)
            elif body in RNW_NAMES:
                node = ("conv", "rnw", "rnw", node)    # `[rnw]` is `[rnw->rnw]`
            else:
                node = ("fmt", body, node)    # `now [longdatetime]` [EXT]
            if self.peek().kind == "op" and self.peek().text in CONTINUE_OPS or self.at_mod():
                node = self.additive(self.multiplicative(node))
        return node

    def additive(self, left=None):
        node = left if left is not None else self.multiplicative()
        while self.at_op("+") or self.at_op("-"):
            op = self.advance().text
            node = ("bin", op, node, self.multiplicative())
        return node

    def multiplicative(self, left=None):
        node = left if left is not None else self.signed()
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
        while self.implicit_follows():
            node = ("mul", node, self.power())
        return node

    def implicit_follows(self):
        """SPEC 5.4: after a number or `)`, before a name or `(`; after a
        name that was not taken as a call, before `(`. Never two names,
        never two numbers.
        """
        token = self.peek()
        starts_name = token.kind == "name" and token.text.lower() != KEYWORD_MOD
        starts_paren = token.kind == "op" and token.text == "("
        tail = self.tokens[self.pos - 1]
        if tail.kind in ("number", "imag") or (tail.kind == "op" and tail.text == ")"):
            return starts_name or starts_paren
        if tail.kind == "name":
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
        if token.kind == "ref":
            self.advance()
            return ("ref", int(token.text))
        if token.kind == "rnw":
            self.advance()
            return ("rnw", token.text)
        if token.kind in ("date", "duration"):
            self.advance()
            return (token.kind, token.text)
        if token.kind == "name":
            if token.text.lower() == KEYWORD_MOD:
                raise CalcError("'mod' needs a left operand")
            self.advance()
            if self.at_op("(") and self.is_function(token.text):
                return self.call(token.text)
            return ("name", token.text)
        if self.at_op("("):
            self.advance()
            node = self.chain()
            self.expect_op(")")
            return node
        if token.kind == "end":
            raise CalcError("Incomplete expression")
        raise CalcError(f"Unexpected '{token.text}'")

    def call(self, name):
        self.expect_op("(")
        if self.at_op(")"):
            raise CalcError(f"{name}() needs an argument")
        args = [self.chain()]
        while self.at_op(","):
            self.advance()
            args.append(self.chain())
        self.expect_op(")")
        return ("call", name, args)


def parse(text, is_function=None):
    """Parse text as an expression. is_function(name) tells calls from
    implicit multiplication (`a(2)` with variable a is `a*2`).
    """
    return Parser(text, is_function).parse_expression()


def parse_line(text, is_function=None):
    """Parse an expression or a definition line."""
    return Parser(text, is_function).parse_line()
