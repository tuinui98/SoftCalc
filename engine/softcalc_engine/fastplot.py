"""softcalc_engine/fastplot.py - Float evaluation for drawing graphs (owner 2026-11-09).

Drawing needs hundreds of values per line, and mpmath at 20 digits
takes about 36 us each. When an expression uses only what plain floats
can do (numbers, real variables, + - * / ^ mod, the elementary
functions, user functions built from them), it is compiled once into a
Python closure over floats, 10-20 times faster. Anything else (dates,
complex values, finance, conversions, `!`, `%`) raises Unsupported and
the caller keeps the mpmath path. Only plots use this; results, trace
values and Copy stay at full precision.

A point that fails at run time (domain error, overflow, a complex
result) is undefined, as on the mpmath path, where such values are not
drawn either.
"""

import math

import mpmath

from .errors import CalcError
from .functions import ANGLE_TO_RAD

MAX_DEPTH = 40
NOISE = 1e-14    # float stand-in for snap_zero: sin(pi) -> 0, tan undefined at cos ~ 0


class Unsupported(Exception):
    """The expression needs the mpmath path."""


class Undefined(Exception):
    """No real value at this point."""


def compile_tree(engine, tree, params, builtins):
    """A function of floats (in params order) returning a float, or raising Undefined.

    Raises Unsupported when the tree needs more than floats.
    """
    names = [p.lower() for p in params]
    body = Compiler(engine, builtins).build(tree, names, 0)

    def run(*values):
        try:
            result = body(values)
        except (ValueError, ZeroDivisionError, OverflowError, TypeError) as exc:
            raise Undefined() from exc
        if isinstance(result, complex) or not math.isfinite(result):
            raise Undefined()
        return result

    return run


class Compiler:
    """Turns a parse tree into nested closures over a tuple of floats."""

    def __init__(self, engine, builtins):
        self.engine = engine
        self.builtins = builtins
        divisor = ANGLE_TO_RAD[builtins.angle]
        self.to_rad = 1.0 if divisor is None else math.pi / divisor
        self.from_rad = 1.0 / self.to_rad

    def build(self, node, names, depth):
        if depth > MAX_DEPTH:
            raise Unsupported()
        kind = node[0]
        if kind == "num":
            _, literal, base = node
            value = float(literal) if base == 10 else float(int(literal, base))
            return lambda v: value
        if kind == "name":
            return self.name(node[1], names)
        if kind == "neg":
            inner = self.build(node[1], names, depth + 1)
            return lambda v: -inner(v)
        if kind == "pos":
            return self.build(node[1], names, depth + 1)
        if kind == "mul":
            left, right = self.build(node[1], names, depth + 1), self.build(node[2], names, depth + 1)
            return lambda v: left(v) * right(v)
        if kind == "bin":
            return self.binary(node[1], self.build(node[2], names, depth + 1), self.build(node[3], names, depth + 1))
        if kind == "call":
            args = [self.build(arg, names, depth + 1) for arg in node[2]]
            return self.call(node[1], args, depth)
        raise Unsupported()    # imag, date, duration, ref, post, conv, fmt

    def name(self, name, names):
        key = name.lower()
        if key in names:
            index = names.index(key)
            return lambda v: v[index]
        try:
            value = self.engine.lookup(name, self.builtins, {})
        except CalcError as exc:
            raise Unsupported() from exc
        if not isinstance(value, mpmath.mpf):
            raise Unsupported()    # complex, dates, text
        number = float(value)
        return lambda v: number

    @staticmethod
    def binary(op, left, right):
        if op == "+":
            return lambda v: left(v) + right(v)
        if op == "-":
            return lambda v: left(v) - right(v)
        if op == "*":
            return lambda v: left(v) * right(v)
        if op == "/":
            return lambda v: left(v) / right(v)
        if op == "^":
            return lambda v: power(left(v), right(v))
        if op == "mod":
            return lambda v: floor_mod(left(v), right(v))
        raise Unsupported()

    def call(self, name, args, depth):
        key = name.lower()
        if key in self.builtins.names():
            fn = self.elementary(key)
            if fn is None or len(args) != 1:
                raise Unsupported()
            arg = args[0]
            return lambda v: fn(arg(v))
        user = self.engine.functions.get(key)
        if user is None or len(args) != len(user.params):
            raise Unsupported()
        body = self.build(user.body, [p.lower() for p in user.params], depth + 1)
        if len(args) == 1:
            only = args[0]
            return lambda v: body((only(v),))
        return lambda v: body(tuple(arg(v) for arg in args))

    def elementary(self, key):
        """Float versions of the mpmath built-ins; None for the others."""
        to_rad, from_rad = self.to_rad, self.from_rad
        table = {
            "sqrt": math.sqrt, "abs": abs, "exp": math.exp, "ln": math.log, "log10": math.log10,
            "sin": lambda x: snap(math.sin(x * to_rad), x * to_rad),
            "cos": lambda x: snap(math.cos(x * to_rad), x * to_rad),
            "tan": lambda x: tangent(x * to_rad),
            "asin": lambda x: math.asin(x) * from_rad, "acos": lambda x: math.acos(x) * from_rad,
            "atan": lambda x: math.atan(x) * from_rad,
            "sinh": math.sinh, "cosh": math.cosh, "tanh": math.tanh,
            "asinh": math.asinh, "acosh": math.acosh, "atanh": math.atanh,
        }
        return table.get(key)


def power(a, b):
    result = a ** b
    if isinstance(result, complex):
        raise ValueError("complex power")    # mpmath gives a complex value: not drawn
    return result


def floor_mod(a, b):
    if b == 0:
        raise ZeroDivisionError("mod by zero")
    return a - b * math.floor(a / b)


def snap(value, rad):
    return 0.0 if abs(value) <= NOISE * max(1.0, abs(rad)) else value


def tangent(rad):
    if snap(math.cos(rad), rad) == 0:
        raise ValueError("tan is undefined here")
    return snap(math.tan(rad), rad)
