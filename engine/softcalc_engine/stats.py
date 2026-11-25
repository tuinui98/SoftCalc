"""softcalc_engine/stats.py - Statistics functions, round 1 (owner 2026-11-26).

comb and perm count choices; mode, percentile and quartile describe a
list; normcdf and norminv are the normal distribution. The sums, means
and spreads that came with the matrices stay in matrix.py; randnorm is a
draw, so the engine keeps it with rand (engine.random_call).

- comb(n, k) / perm(n, k): whole n, k >= 0; k above n gives 0. The value
  is whole and exact while it fits the working digits.
- mode(list): the value that comes most often; on a tie the first of
  them in the list, and `ties` keeps them all so History can show them
  faint on the right (owner 2026-11-26). Each value once only: an error,
  as Excel's MODE.SNGL.
- percentile(list, p): p from 0 to 1, linear between the sorted values
  as Excel's PERCENTILE.INC; quartile(list, q): q = 0 to 4, its
  QUARTILE.INC.
- normcdf(x), normcdf(x, mean, sd): P(X <= x); normcdf(a, b) and
  normcdf(a, b, mean, sd): P(a <= X <= b), as the TI calculators'
  normalcdf (owner 2026-11-26: both). norminv(p, [mean, sd]).
"""

import mpmath

from .errors import CalcError
from .matrix import numbers_of

ties = []    # mode()'s tied values in this line, for the History display; the engine clears it


def whole_count(key, value, name):
    if not isinstance(value, mpmath.mpf) or value != mpmath.floor(value) or value < 0:
        raise CalcError(f"{key}: {name} must be a whole number, 0 or more")
    return value


def comb(args):
    n, k = (whole_count("comb", value, name) for value, name in zip(args, ("n", "k")))
    if k > n:
        return mpmath.mpf(0)
    return whole(mpmath.binomial(n, k))


def perm(args):
    n, k = (whole_count("perm", value, name) for value, name in zip(args, ("n", "k")))
    if k > n:
        return mpmath.mpf(0)
    return whole(mpmath.ff(n, k))


def whole(value):
    """A count as a whole number: binomial and ff work through gamma, so a few last bits may be off."""
    return mpmath.nint(value) if abs(value) < mpmath.mpf(10) ** mpmath.mp.dps else value


def mode(args):
    values = numbers_of(args, "mode", real=True)
    counts = {}
    for value in values:
        counts[value] = counts.get(value, 0) + 1
    most = max(counts.values())
    if most < 2:
        raise CalcError("mode: no value comes more than once")
    tied = [value for value in counts if counts[value] == most]
    if len(tied) > 1:
        ties.append(tied)
    return tied[0]


def percentile_of(values, p):
    values = sorted(values)
    rank = p * (len(values) - 1)
    below = int(mpmath.floor(rank))
    if below + 1 >= len(values):
        return values[-1]
    return values[below] + (rank - below) * (values[below + 1] - values[below])


def percentile(args):
    values = numbers_of(args[:1], "percentile", real=True)
    p = args[1]
    if not isinstance(p, mpmath.mpf) or not 0 <= p <= 1:
        raise CalcError("percentile: p goes from 0 to 1 (0.25 for the 25th)")
    return percentile_of(values, p)


def quartile(args):
    values = numbers_of(args[:1], "quartile", real=True)
    q = args[1]
    if not isinstance(q, mpmath.mpf) or q not in (0, 1, 2, 3, 4):
        raise CalcError("quartile: q is 0, 1, 2, 3 or 4")
    return percentile_of(values, q / 4)


def normal_args(key, args, given):
    """mean and sd after `given` leading values; sd must be above 0."""
    rest = args[given:]
    for value in args:
        if not isinstance(value, mpmath.mpf):
            raise CalcError(f"{key} needs real numbers")
    mean, sd = rest if rest else (mpmath.mpf(0), mpmath.mpf(1))
    if sd <= 0:
        raise CalcError(f"{key}: sd must be above 0")
    return mean, sd


def cdf(x, mean, sd):
    return mpmath.ncdf(x, mean, sd)


def normcdf(args):
    if len(args) in (1, 3):
        mean, sd = normal_args("normcdf", args, 1)
        return cdf(args[0], mean, sd)
    mean, sd = normal_args("normcdf", args, 2)
    low, high = args[0], args[1]
    if low > high:
        raise CalcError("normcdf(a, b): a must not be above b")
    return cdf(high, mean, sd) - cdf(low, mean, sd)


def norminv(args):
    if len(args) == 2:
        raise CalcError("norminv expects p, or p, mean and sd")
    mean, sd = normal_args("norminv", args, 1)
    p = args[0]
    if not 0 < p < 1:
        raise CalcError("norminv: p must be between 0 and 1")
    return mean + sd * mpmath.sqrt(2) * mpmath.erfinv(2 * p - 1)


# name -> (function, fewest arguments, most arguments), as matrix.FUNCTIONS
FUNCTIONS = {
    "comb": (comb, 2, 2), "perm": (perm, 2, 2),
    "mode": (mode, 1, None), "percentile": (percentile, 2, 2), "quartile": (quartile, 2, 2),
    "normcdf": (normcdf, 1, 4), "norminv": (norminv, 1, 3),
}
