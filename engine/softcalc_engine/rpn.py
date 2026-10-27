"""softcalc_engine/rpn.py - Parse a line of Reverse Polish Notation (SPEC 5.8).

Tokens are separated by spaces; each must be one complete token: a
number (a leading `-` is part of it), a name, an operator, or a postfix
`!` / `%`. Functions take as many operands as they have parameters.
The result is the same tree the infix parser builds, so evaluation and
formatting are shared. There is no stack across lines.
"""

from .errors import CalcError
from .lexer import tokenize

BINARY_OPS = {"+", "-", "*", "/", "^"}
POSTFIX_OPS = {"!", "%"}


def single_token(word):
    """Lex one space-separated word; it must form exactly one token."""
    negative = word.startswith("-") and len(word) > 1
    tokens = tokenize(word[1:] if negative else word)
    if len(tokens) != 2:
        raise CalcError(f"'{word}' is not a single RPN item")
    token = tokens[0]
    if negative and token.kind not in ("number", "imag"):
        raise CalcError(f"'{word}' is not a single RPN item")
    return token, negative


def parse_rpn(text, arity_of):
    """Build an expression tree from RPN text.

    arity_of(name) returns the parameter count of a function, or None if
    the name is not a function (then it is pushed as a value).
    """
    stack = []

    def pop(count, what):
        if len(stack) < count:
            raise CalcError(f"'{what}' needs {count} operand(s)")
        items = stack[-count:]
        del stack[-count:]
        return items

    for word in text.split():
        token, negative = single_token(word)
        if token.kind == "number":
            node = ("num", token.text, token.base)
            stack.append(("neg", node) if negative else node)
        elif token.kind == "imag":
            node = ("imag", token.text)
            stack.append(("neg", node) if negative else node)
        elif token.kind == "ref":
            stack.append(("ref", int(token.text)))
        elif token.kind in ("date", "duration"):
            stack.append((token.kind, token.text))
        elif token.kind == "op" and token.text in BINARY_OPS:
            left, right = pop(2, token.text)
            stack.append(("bin", token.text, left, right))
        elif token.kind == "op" and token.text in POSTFIX_OPS:
            stack.append(("post", token.text, pop(1, token.text)[0]))
        elif token.kind == "name" and token.text.lower() == "mod":
            left, right = pop(2, "mod")
            stack.append(("bin", "mod", left, right))
        elif token.kind == "name":
            arity = arity_of(token.text)
            if arity is None:
                stack.append(("name", token.text))
            else:
                stack.append(("call", token.text, pop(arity, token.text)))
        else:
            raise CalcError(f"'{word}' cannot be used in RPN")
    if not stack:
        raise CalcError("Empty expression")
    if len(stack) > 1:
        raise CalcError("Operands left without an operator")
    return stack[0]
