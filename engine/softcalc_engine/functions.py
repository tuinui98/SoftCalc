"""softcalc_engine/functions.py - Built-in functions and operators (SPEC 5.4, 5.5).

All maths runs on mpmath at the working precision chosen by the caller.
Circular trig takes its input in the current angle unit and inverse trig
returns it; hyperbolic functions ignore the unit. Results that come out
infinite or NaN are domain errors.
"""

import mpmath

from .errors import CalcError

ANGLE_TO_RAD = {"rad": None, "deg": 180, "grad": 200}
NOISE_MARGIN = 1    # digits below working precision treated as rounding noise


def check_finite(value, what):
    """Raise for infinite or NaN results (ln(0), atanh(1), ...)."""
    if mpmath.isinf(value) or mpmath.isnan(value):
        raise CalcError(f"{what} is undefined here")
    return value


def is_real(value):
    return not isinstance(value, mpmath.mpc)


def snap_zero(value, scale):
    """Return 0 for a real value that is only rounding noise relative to
    the argument size (sin(pi) -> 0, while sin(1e-40) stays 1e-40).
    """
    noise = scale * mpmath.mpf(10) ** (-(mpmath.mp.dps - NOISE_MARGIN))
    if is_real(value) and abs(value) <= noise:
        return mpmath.mpf(0)
    return value


class Functions:
    """Function table bound to one angle unit."""

    def __init__(self, angle):
        self.angle = angle
        self.table = {
            "sqrt": mpmath.sqrt, "abs": abs, "exp": mpmath.exp,
            "ln": mpmath.ln, "log10": mpmath.log10,
            "sin": self.sin, "cos": self.cos, "tan": self.tan,
            "asin": self.inverse(mpmath.asin), "acos": self.inverse(mpmath.acos),
            "atan": self.inverse(mpmath.atan),
            "sinh": mpmath.sinh, "cosh": mpmath.cosh, "tanh": mpmath.tanh,
            "asinh": mpmath.asinh, "acosh": mpmath.acosh, "atanh": mpmath.atanh,
        }

    def names(self):
        return set(self.table)

    def call(self, name, args):
        fn = self.table.get(name.lower())
        if fn is None:
            raise CalcError(f"Unknown function '{name}'")
        if len(args) != 1:
            raise CalcError(f"{name.lower()} expects 1 argument")
        try:
            result = fn(args[0])
        except (ValueError, ZeroDivisionError) as exc:
            raise CalcError(f"{name.lower()} is undefined here") from exc
        return check_finite(result, name.lower())

    #------------------------------------------------------------------
    #  Angle handling
    #------------------------------------------------------------------
    def to_rad(self, x):
        divisor = ANGLE_TO_RAD[self.angle]
        return x if divisor is None else x * mpmath.pi / divisor

    def from_rad(self, x):
        divisor = ANGLE_TO_RAD[self.angle]
        return x if divisor is None else x * divisor / mpmath.pi

    def sin(self, x):
        rad = self.to_rad(x)
        return snap_zero(mpmath.sin(rad), abs(rad))

    def cos(self, x):
        rad = self.to_rad(x)
        return snap_zero(mpmath.cos(rad), abs(rad))

    def tan(self, x):
        rad = self.to_rad(x)
        if snap_zero(mpmath.cos(rad), abs(rad)) == 0:
            raise CalcError("tan is undefined here")
        return snap_zero(mpmath.tan(rad), abs(rad))

    def inverse(self, fn):
        return lambda x: self.from_rad(fn(x))


#----------------------------------------------------------------------
#  Operators
#----------------------------------------------------------------------
def binary(op, a, b):
    """Apply + - * / ^ mod."""
    try:
        if op == "+":
            result = a + b
        elif op == "-":
            result = a - b
        elif op == "*":
            result = a * b
        elif op == "/":
            if b == 0:
                raise CalcError("Division by zero")
            result = a / b
        elif op == "^":
            result = mpmath.power(a, b)
        elif op == "mod":
            result = floor_mod(a, b)
        else:
            raise CalcError(f"Unknown operator '{op}'")
    except ZeroDivisionError as exc:
        raise CalcError("Division by zero") from exc
    return check_finite(result, f"'{op}'")


def floor_mod(a, b):
    """Mathematical modulo: result has the sign of b (-7 mod 3 = 2) [EXT]."""
    if not (is_real(a) and is_real(b)):
        raise CalcError("mod needs real operands")
    if b == 0:
        raise CalcError("mod by zero")
    return a - b * mpmath.floor(a / b)


def factorial(x):
    """n! for integers, gamma(x+1) otherwise; negative integers fail [EXT]."""
    if is_real(x) and x == mpmath.floor(x) and x < 0:
        raise CalcError("Factorial of a negative integer")
    return check_finite(mpmath.gamma(x + 1), "!")


def postfix(op, x):
    if op == "!":
        return factorial(x)
    return x / 100    # '%' is percent [EXT]
