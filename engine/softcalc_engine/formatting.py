"""softcalc_engine/formatting.py - Turn numbers into display text (SPEC 6.2).

Decimal rules observed on the original (spec/golden/S_format.tsv):
- round to `digits` significant digits and trim trailing zeros;
- fixed notation when the integer part has at most `digits` digits, or,
  for |x| < 1, when leading zeros plus significant digits after the
  point are at most `digits + 1`; otherwise scientific `m.mmme<exp>`;
- a mantissa without fraction has no dot: `1e100` (original `1.e100`) [FIX].
Complex values print as `1i`, `-2i`, `(3+4i)`, `(1-2i)`.
"""

import mpmath
from mpmath.libmp import to_str

NOISE_MARGIN = 1    # digits below working precision treated as rounding noise


def split_digits(value, digits):
    """Return (negative, digit string without trailing zeros, exponent10)
    for a nonzero real mpf rounded to `digits` significant digits.
    """
    text = to_str(value._mpf_, digits, min_fixed=1, max_fixed=0)
    negative = text.startswith("-")
    mantissa, _, exponent = text.lstrip("-").partition("e")
    digit_str = mantissa.replace(".", "").rstrip("0") or "0"
    return negative, digit_str, int(exponent or 0)


def format_real(value, digits):
    """Format a real mpf in decimal."""
    if value == 0:
        return "0"
    negative, digit_str, exp10 = split_digits(value, digits)
    sign = "-" if negative else ""
    if 0 <= exp10 < digits:
        int_part = digit_str[:exp10 + 1].ljust(exp10 + 1, "0")
        frac_part = digit_str[exp10 + 1:]
        return sign + int_part + ("." + frac_part if frac_part else "")
    if exp10 < 0 and (-exp10 - 1) + len(digit_str) <= digits + 1:
        return sign + "0." + "0" * (-exp10 - 1) + digit_str
    mantissa = digit_str[0] + ("." + digit_str[1:] if len(digit_str) > 1 else "")
    return f"{sign}{mantissa}e{exp10}"


def snap_complex(value, digits):
    """Drop a real or imaginary part that is negligible next to the other.

    exp(i*pi) is -1 + 5e-44i at working precision; the original shows -1.
    Only rounding noise is dropped, so 1e40+i keeps its 1i.
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


def format_number(value, digits):
    """Format a real or complex result for the History pane."""
    value = snap_complex(value, digits)
    if not isinstance(value, mpmath.mpc):
        return format_real(value, digits)
    re_part, im_part = value.real, value.imag
    im_text = format_real(im_part, digits) + "i"
    if re_part == 0:
        return im_text
    re_text = format_real(re_part, digits)
    joiner = "" if im_text.startswith("-") else "+"
    return f"({re_text}{joiner}{im_text})"
