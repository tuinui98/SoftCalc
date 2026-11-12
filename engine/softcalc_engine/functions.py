"""softcalc_engine/functions.py - Built-in functions and operators (SPEC 5.4, 5.5).

All maths runs on mpmath at the working precision chosen by the caller.
Circular trig takes its input in the current angle unit and inverse trig
returns it; hyperbolic functions ignore the unit. Results that come out
infinite or NaN are domain errors.
"""

import mpmath

from . import dates, finance
from .errors import CalcError
from .random_shapes import SHAPE_FUNCTIONS

ANGLE_TO_RAD = {"rad": None, "deg": 180, "grad": 200}
WORD_FUNCTIONS = ("twos", "signed")    # two's complement (owner 2026-11-07)
ROUNDING_FUNCTIONS = ("round", "ceil", "floor")    # owner 2026-11-12
RANDOM_FUNCTIONS = ("rand", "rands", *SHAPE_FUNCTIONS)    # drawn by the Engine, which keeps the seed (owner 2026-11-12)
MAX_ROUND_DIGITS = 1000
MAX_BITS = 1024
NOISE_MARGIN = 1    # digits below working precision treated as rounding noise
NUMBER_TYPES = (mpmath.mpf, mpmath.mpc)
TIME_TYPES = (dates.DateValue, dates.DurationValue)


def check_finite(value, what):
    """Raise for infinite or NaN results (ln(0), atanh(1), ...)."""
    if isinstance(value, NUMBER_TYPES) and (mpmath.isinf(value) or mpmath.isnan(value)):
        raise CalcError(f"{what} is undefined here")
    return value


def require_numbers(what, *values):
    """Dates and text cannot enter ordinary maths."""
    for value in values:
        if not isinstance(value, NUMBER_TYPES):
            raise CalcError(f"{what} needs a number")


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


def whole(value, key):
    """An integer argument; fractions and complex values are errors."""
    if not is_real(value) or value != mpmath.floor(value):
        raise CalcError(f"{key} needs a whole number")
    return int(value)


class Functions:
    """Function table bound to one angle unit and one word size."""

    def __init__(self, angle, word_size=32):
        self.angle = angle
        self.word_size = word_size
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
        return (set(self.table) | set(dates.FUNCTIONS) | set(finance.FUNCTIONS) | set(WORD_FUNCTIONS)
                | set(ROUNDING_FUNCTIONS) | set(RANDOM_FUNCTIONS))

    def min_arity(self, name):
        """Argument count used in RPN, where optional arguments cannot be told apart."""
        key = name.lower()
        if key in SHAPE_FUNCTIONS:
            return SHAPE_FUNCTIONS[key][1]    # with the range
        if key in RANDOM_FUNCTIONS:
            return 0
        entry = finance.FUNCTIONS.get(key)
        return entry[1] if entry else 1

    def call(self, name, args):
        key = name.lower()
        if key in dates.FUNCTIONS:
            self.check_count(key, args, 1, 1)
            return dates.FUNCTIONS[key](args[0])
        if key in finance.FUNCTIONS:
            fn, low, high = finance.FUNCTIONS[key]
            self.check_count(key, args, low, high)
            return check_finite(fn(*args), key)
        if key in WORD_FUNCTIONS:
            self.check_count(key, args, 1, 2)
            return self.word(key, args)
        if key in ROUNDING_FUNCTIONS:
            self.check_count(key, args, 1, 2 if key == "round" else 1)
            return rounded(key, args)
        fn = self.table.get(key)
        if fn is None:
            raise CalcError(f"Unknown function '{name}'")
        self.check_count(key, args, 1, 1)
        require_numbers(key, args[0])
        try:
            result = fn(args[0])
        except (ValueError, ZeroDivisionError) as exc:
            raise CalcError(f"{key} is undefined here") from exc
        return check_finite(result, key)

    def word(self, key, args):
        """twos(x, [bits]) and signed(x, [bits]): two's complement in a word.

        twos gives the unsigned bit pattern of a signed value (twos(-1, 8)
        = 255); signed reads a pattern back (signed(0xFF, 8) = -1). Values
        outside the word wrap like hardware (twos(300, 8) = 44). Without
        bits the word size setting applies.
        """
        require_numbers(key, *args)
        x = whole(args[0], key)
        bits = whole(args[1], key) if len(args) > 1 else self.word_size
        if not 1 <= bits <= MAX_BITS:
            raise CalcError(f"{key}: bits must be 1 to {MAX_BITS}")
        size = 1 << bits
        pattern = x % size    # Python's modulo is never negative here
        if key == "signed" and pattern >= size >> 1:
            pattern -= size
        return mpmath.mpf(pattern)

    @staticmethod
    def check_count(key, args, low, high):
        if not low <= len(args) <= high:
            wanted = str(low) if low == high else f"{low} to {high}"
            raise CalcError(f"{key} expects {wanted} argument(s)")

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
#  Rounding (owner 2026-11-12)
#----------------------------------------------------------------------
def snap_whole(y):
    """y itself, or the nearest integer when y is only rounding noise away
    from it (0.1*30 is 2.99999...9 in binary and floor() must give 3).
    """
    nearest = mpmath.nint(y)
    tolerance = mpmath.mpf(10) ** (NOISE_MARGIN + 2 - mpmath.mp.dps) * max(1, abs(nearest))
    return nearest if abs(y - nearest) <= tolerance else y


def rounded(key, args):
    """round(x, [digits]) half away from zero; ceil(x), floor(x).

    round(2.5) = 3, round(-2.5) = -3, round(3.14159, 2) = 3.14,
    round(1234, -2) = 1200.
    """
    require_numbers(key, *args)
    if not is_real(args[0]):
        raise CalcError(f"{key} needs a real number")
    x = args[0]
    digits = whole(args[1], key) if len(args) > 1 else 0
    if abs(digits) > MAX_ROUND_DIGITS:
        raise CalcError(f"{key}: digits must be -{MAX_ROUND_DIGITS} to {MAX_ROUND_DIGITS}")
    scale = mpmath.mpf(10) ** abs(digits)    # an exact integer, unlike 10^-2
    y = x * scale if digits >= 0 else x / scale
    if key == "floor":
        result = mpmath.floor(snap_whole(y))
    elif key == "ceil":
        result = mpmath.ceil(snap_whole(y))
    else:
        size = snap_whole(abs(y) * 2) / 2    # 2.675*100 is 267.4999...: snapped to the true half
        result = mpmath.floor(size + mpmath.mpf("0.5"))
        result = result if y >= 0 else -result
    if digits == 0:
        return mpmath.mpf(result)
    return result / scale if digits > 0 else result * scale


#----------------------------------------------------------------------
#  Operators
#----------------------------------------------------------------------
def binary(op, a, b):
    """Apply + - * / ^ mod; date arithmetic goes to the dates module."""
    if isinstance(a, TIME_TYPES) or isinstance(b, TIME_TYPES):
        require_numbers_or_dates(op, a, b)
        return dates.date_binary(op, a, b)
    require_numbers(f"'{op}'", a, b)
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


def require_numbers_or_dates(op, a, b):
    for value in (a, b):
        if isinstance(value, dates.TextValue):
            raise CalcError(f"'{op}' cannot use text")


def postfix(op, x):
    require_numbers(op, x)
    if op == "!":
        return factorial(x)
    return x / 100    # '%' is percent [EXT]


def negate(x):
    if isinstance(x, dates.DurationValue):
        return dates.DurationValue(-x.seconds)
    require_numbers("'-'", x)
    return -x
