"""softcalc_engine/formatting.py - Turn numbers into display text (SPEC 6.2).

Layout rules observed on the original (spec/golden/S_format.tsv and
T_bases.tsv), the same for every output base, counting digits in that
base:
- round to `digits` significant digits and trim trailing zeros;
- fixed notation when the integer part has at most `digits` digits, or,
  for |x| < 1, when leading zeros plus significant digits after the
  point are at most `digits + 1`; otherwise scientific;
- a mantissa without fraction has no dot (original `1.e100`) [FIX].

Per base:   decimal  1.5e32         -0.25        (3+4i)    0.5i
            hex      0x6*0x10^0x20  -0x0.4       (0x3+0x4i) (0x0+0x0.8i)
            binary   1.1b*10b^101b  -0.01b       (11b+100bi)
            octal    0o7*0o10^0o3   [EXT, same pattern as hex]
"""

import math
from fractions import Fraction

import mpmath
from mpmath.libmp import to_str

NOISE_MARGIN = 1    # digits below working precision treated as rounding noise
RADIX = {"dec": 10, "hex": 16, "bin": 2, "oct": 8}
DIGIT_CHARS = "0123456789ABCDEF"


#----------------------------------------------------------------------
#  Digit extraction
#----------------------------------------------------------------------
def to_base(n, radix):
    """Non-negative int to a digit string in radix."""
    if n == 0:
        return "0"
    out = []
    while n:
        n, r = divmod(n, radix)
        out.append(DIGIT_CHARS[r])
    return "".join(reversed(out))


def split_digits(value, digits, radix):
    """(negative, digit string without trailing zeros, exponent) for a
    nonzero real, rounded to `digits` significant digits in radix.
    The exponent is the radix power of the leading digit.
    """
    if radix == 10:
        text = to_str(value._mpf_, digits, min_fixed=1, max_fixed=0)
        negative = text.startswith("-")
        mantissa, _, exponent = text.lstrip("-").partition("e")
        return negative, mantissa.replace(".", "").rstrip("0") or "0", int(exponent or 0)
    size = abs(value)
    exp_r = int(mpmath.floor(mpmath.log(size, radix)))
    # log can be off by one next to exact powers of the radix
    while mpmath.power(radix, exp_r) > size:
        exp_r -= 1
    while mpmath.power(radix, exp_r + 1) <= size:
        exp_r += 1
    scaled = int(mpmath.nint(size * mpmath.power(radix, digits - 1 - exp_r)))
    digit_str = to_base(scaled, radix)
    if len(digit_str) > digits:    # rounding carried into a new digit
        exp_r += 1
        digit_str = digit_str[:digits]
    return value < 0, digit_str.rstrip("0") or "0", exp_r


#----------------------------------------------------------------------
#  Layout
#----------------------------------------------------------------------
def layout(digit_str, exp_r, digits):
    """Return ("fixed", int_part, frac_part) or ("sci", mantissa, exponent)."""
    if 0 <= exp_r < digits:
        int_part = digit_str[:exp_r + 1].ljust(exp_r + 1, "0")
        return "fixed", int_part, digit_str[exp_r + 1:]
    if exp_r < 0 and (-exp_r - 1) + len(digit_str) <= digits + 1:
        return "fixed", "0", "0" * (-exp_r - 1) + digit_str
    mantissa = digit_str[0] + ("." + digit_str[1:] if len(digit_str) > 1 else "")
    return "sci", mantissa, exp_r


def with_fraction(int_part, frac_part):
    return int_part + ("." + frac_part if frac_part else "")


def signed_power(exponent, radix):
    """Exponent text in the output base: 32 -> 0x20, -6 -> -0x6, 5 -> 101."""
    sign = "-" if exponent < 0 else ""
    body = to_base(abs(exponent), radix)
    if radix == 16:
        return f"{sign}0x{body}"
    if radix == 8:
        return f"{sign}0o{body}"
    return f"{sign}{body}"


def round_decimals(value, decimals):
    """Round half away from zero to a fixed number of decimal places."""
    scale = mpmath.power(10, decimals)
    magnitude = mpmath.floor(abs(value) * scale + mpmath.mpf("0.5")) / scale
    return -magnitude if value < 0 else magnitude


def pad_fraction(text, decimals):
    """Show trailing zeros up to `decimals` places (fixed notation only)."""
    if decimals <= 0 or "e" in text:
        return text
    int_part, _, frac_part = text.partition(".")
    return int_part + "." + frac_part.ljust(decimals, "0")


def format_real(value, digits, base="dec", decimals=None, pad_zeros=False):
    """Format a real mpf in the given output base.

    decimals / pad_zeros apply to decimal output only (extensions.md E9).
    """
    radix = RADIX[base]
    if base == "dec" and decimals is not None:
        value = round_decimals(value, decimals)
    if value == 0:
        zero = {"dec": "0", "hex": "0x0", "bin": "0b", "oct": "0o0"}[base]
        return pad_fraction(zero, decimals) if base == "dec" and pad_zeros and decimals else zero
    negative, digit_str, exp_r = split_digits(value, digits, radix)
    sign = "-" if negative else ""
    kind, first, second = layout(digit_str, exp_r, digits)
    if base == "dec":
        text = sign + with_fraction(first, second) if kind == "fixed" else f"{sign}{first}e{second}"
        return pad_fraction(text, decimals) if pad_zeros and decimals else text
    if base == "bin":
        if kind == "fixed":
            return sign + with_fraction(first, second) + "b"
        return f"{sign}{first}b*10b^{signed_power(second, 2)}b"
    prefix = "0x" if base == "hex" else "0o"
    if kind == "fixed":
        return sign + prefix + with_fraction(first, second)
    return f"{sign}{prefix}{first}*{prefix}10^{signed_power(second, radix)}"


#----------------------------------------------------------------------
#  Complex
#----------------------------------------------------------------------
def snap_complex(value):
    """Drop a real or imaginary part that is only rounding noise.

    exp(i*pi) is -1 + 5e-44i at working precision; the original shows -1,
    while 1e40+i keeps its 1i.
    """
    if not isinstance(value, mpmath.mpc):
        return value
    re_part, im_part = value.real, value.imag
    size = max(abs(re_part), abs(im_part))
    if size == 0:
        return mpmath.mpf(0)
    limit = size * mpmath.mpf(10) ** (-(mpmath.mp.dps - NOISE_MARGIN))
    if abs(im_part) <= limit:
        return re_part
    if abs(re_part) <= limit:
        return mpmath.mpc(0, im_part)
    return value


def format_number(value, digits, base="dec", decimals=None, pad_zeros=False):
    """Format a real or complex result for the History pane."""
    value = snap_complex(value)
    if not isinstance(value, mpmath.mpc):
        return format_real(value, digits, base, decimals, pad_zeros)
    re_part, im_part = value.real, value.imag
    im_text = format_real(im_part, digits, base, decimals, pad_zeros) + "i"
    if re_part == 0 and base == "dec":
        return im_text
    re_text = format_real(re_part, digits, base, decimals, pad_zeros)
    joiner = "" if im_text.startswith("-") else "+"
    return f"({re_text}{joiner}{im_text})"


def is_exact(value, digits, base):
    """True when the Fraction `value` has at most `digits` significant
    digits in the output base, so format_real shows it without rounding
    (`0.5` in hex is `0x0.8`; `0.1` never ends in hex).
    """
    if value == 0:
        return True
    radix = RADIX[base]
    size = abs(value)
    exp_r = int(math.log(size.numerator, radix) - math.log(size.denominator, radix))
    # the float estimate can be off by one either way
    while Fraction(radix) ** exp_r > size:
        exp_r -= 1
    while Fraction(radix) ** (exp_r + 1) <= size:
        exp_r += 1
    return (size * Fraction(radix) ** (digits - 1 - exp_r)).denominator == 1


def working_digits(digits, base):
    """Decimal working digits needed to show `digits` digits in base."""
    radix = RADIX[base]
    return max(digits, int(digits * mpmath.log10(radix)) + 1)
