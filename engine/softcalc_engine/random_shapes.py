"""softcalc_engine/random_shapes.py - Shaped random draws (owner 2026-11-12, design/random_plan.md R3).

randbeta(a, b), randtri(peak), randexp(rate) and randpeaks(gap) draw
from 0 to 1 in a shape; a range after the shape parameters stretches
the draw to from-to. Whole-number from and to give a whole number with
both ends included: the range is cut into one slot per number and the
shape picks the slot.

Everything is built on getrandbits of the engine's random.Random, with
the rejection loops written out here, so rands(seed) gives the same
draws whatever Python version runs the engine. The maths runs on
mpmath at the working precision, so a draw has as many digits as the
precision.
"""

import mpmath

from .errors import CalcError

# name -> (shape parameters, with the range); the range is optional
SHAPE_FUNCTIONS = {
    "randbeta": (2, 4),
    "randtri": (1, 3),
    "randexp": (1, 3),
    "randpeaks": (1, 3),
}
PEAKS_SIZE = 20    # a + b of each hump of randpeaks: Beta(6, 14) + Beta(14, 6) at gap 0.44


def below(source, n):
    """A uniform whole number from 0 to n - 1 (n >= 1): getrandbits of
    n's bit length, drawn again until it is below n.
    """
    bits = n.bit_length()
    while True:
        value = source.getrandbits(bits)
        if value < n:
            return value


def uniform(source):
    """A uniform value strictly between 0 and 1 at the working precision."""
    bits = mpmath.mp.prec
    while True:
        value = source.getrandbits(bits)
        if value:
            return mpmath.ldexp(mpmath.mpf(value), -bits)


def normal(source):
    """Standard normal by Box-Muller (one of the pair; the other is dropped)."""
    radius = mpmath.sqrt(-2 * mpmath.log(uniform(source)))
    return radius * mpmath.cos(2 * mpmath.pi * uniform(source))


def gamma(source, alpha):
    """Gamma(alpha, 1) by Marsaglia and Tsang (2000); alpha below 1 is
    boosted to alpha + 1 and scaled back by U^(1/alpha).
    """
    if alpha < 1:
        return gamma(source, alpha + 1) * uniform(source) ** (1 / alpha)
    d = alpha - mpmath.mpf(1) / 3
    c = 1 / mpmath.sqrt(9 * d)
    while True:
        x = normal(source)
        v = 1 + c * x
        if v <= 0:
            continue
        v = v ** 3
        if mpmath.log(uniform(source)) < x * x / 2 + d - d * v + d * mpmath.log(v):
            return d * v


def beta(source, a, b):
    x = gamma(source, a)
    return x / (x + gamma(source, b))


def triangle(source, peak):
    """Rises from 0 to peak (0 to 1), falls to 1."""
    u = uniform(source)
    if u < peak:
        return mpmath.sqrt(u * peak)
    return 1 - mpmath.sqrt((1 - u) * (1 - peak))


def exponential(source, rate):
    """Density e^(-rate x) cut to 0-1: rate > 0 falls (decay), rate < 0
    rises (growth), 0 is flat. The inverse of its cumulative curve.
    """
    u = uniform(source)
    if rate == 0:
        return u
    return -mpmath.log1p(u * mpmath.expm1(-rate)) / rate


def peaks(source, gap):
    """Two humps at 0.5 -+ gap/2 (gap 0 to 1), half the draws in each."""
    middle = (1 - gap) / 2 if below(source, 2) == 0 else (1 + gap) / 2
    return beta(source, 1 + middle * (PEAKS_SIZE - 2), 1 + (1 - middle) * (PEAKS_SIZE - 2))


#----------------------------------------------------------------------
#  The functions as called from a line
#----------------------------------------------------------------------
def shaped(source, key, args, whole):
    """One draw of randbeta / randtri / randexp / randpeaks; args are
    real mpf. whole: the range is to give a whole number.
    """
    fixed, full = SHAPE_FUNCTIONS[key]
    if len(args) not in (fixed, full):
        raise CalcError(f"{key} expects {fixed} or {full} argument(s)")
    params = args[:fixed]
    low, high = args[fixed:] if len(args) == full else (mpmath.mpf(0), mpmath.mpf(1))
    if low > high:
        low, high = high, low
    slots = int(high - low) + 1 if whole else None

    if key == "randbeta":
        a, b = params
        if a <= 0 or b <= 0:
            raise CalcError("randbeta: a and b must be above 0")
        x = beta(source, a, b)
    elif key == "randtri":
        peak = params[0]
        if not low <= peak <= high:
            raise CalcError("randtri: the peak must be from 'from' to 'to'")
        if whole:
            x = triangle(source, (peak - low + mpmath.mpf("0.5")) / slots)    # the peak number's slot
        elif high == low:
            return low
        else:
            x = triangle(source, (peak - low) / (high - low))
    elif key == "randexp":
        x = exponential(source, params[0])
    else:
        gap = params[0]
        if not 0 <= gap <= 1:
            raise CalcError("randpeaks: the gap must be 0 to 1")
        x = peaks(source, gap)

    if whole:
        return min(low + mpmath.floor(x * slots), high)
    return low + x * (high - low)
