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

Round 2 (owner 2026-11-26): range, iqr, geomean, harmean, zscore; two
variables with xs first, then ys (Excel puts ys first): covar, covarp,
correl, slope, intercept, rsq, forecast(x, xs, ys); normpdf, and the
binomial and Poisson in the TI order (binompdf(n, p, k), poisspdf(mean,
k)), their cdf taking a range too. gamma, erf and erfc are one-number
functions (functions.py), randbinom and randpois draws (engine.py).
"""

import mpmath

from .errors import CalcError
from .matrix import Matrix, map_cells, numbers_of

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


#======================================================================
#  Round 2 - more description
#======================================================================
def spread_range(args):
    values = numbers_of(args, "range", real=True)
    return max(values) - min(values)


def iqr(args):
    values = numbers_of(args, "iqr", real=True)
    return percentile_of(values, mpmath.mpf("0.75")) - percentile_of(values, mpmath.mpf("0.25"))


def positive(key, args):
    values = numbers_of(args, key, real=True)
    if any(value <= 0 for value in values):
        raise CalcError(f"{key} needs numbers above 0")
    return values


def geomean(args):
    values = positive("geomean", args)
    return mpmath.exp(mpmath.fsum(mpmath.ln(value) for value in values) / len(values))


def harmean(args):
    values = positive("harmean", args)
    return len(values) / mpmath.fsum(1 / value for value in values)


def zscore(args):
    """zscore(x, mean, sd): how many sd x is from the mean (Excel STANDARDIZE);
    zscore(list): each value's, with the list's own mean and sample sd.
    """
    if len(args) == 3:
        if not all(isinstance(value, mpmath.mpf) for value in args):
            raise CalcError("zscore needs real numbers")
        x, mean, sd = args
        if sd <= 0:
            raise CalcError("zscore: sd must be above 0")
        return (x - mean) / sd
    if len(args) != 1 or not isinstance(args[0], Matrix):
        raise CalcError("zscore expects a list, or x, mean and sd")
    values = numbers_of(args, "zscore", real=True)
    if len(values) < 2:
        raise CalcError("zscore needs at least 2 numbers")
    mean = mpmath.fsum(values) / len(values)
    sd = mpmath.sqrt(mpmath.fsum((x - mean) ** 2 for x in values) / (len(values) - 1))
    if sd == 0:
        raise CalcError("zscore: the numbers are all the same")
    return map_cells(args[0], lambda x: (x - mean) / sd)


#======================================================================
#  Round 2 - two variables: xs first, then ys
#======================================================================
def pairs(key, args, least=2):
    xs, ys = (numbers_of([value], key, real=True) for value in args[:2])
    if len(xs) != len(ys):
        raise CalcError(f"{key}: xs and ys must be as long as each other")
    if len(xs) < least:
        raise CalcError(f"{key} needs at least {least} pairs")
    return xs, ys


def moments(xs, ys):
    """The means, and the sums of squares and products about them."""
    mx, my = mpmath.fsum(xs) / len(xs), mpmath.fsum(ys) / len(ys)
    sxx = mpmath.fsum((x - mx) ** 2 for x in xs)
    syy = mpmath.fsum((y - my) ** 2 for y in ys)
    sxy = mpmath.fsum((x - mx) * (y - my) for x, y in zip(xs, ys))
    return mx, my, sxx, syy, sxy


def covariance(key, sample):
    def work(args):
        xs, ys = pairs(key, args, 2 if sample else 1)
        return moments(xs, ys)[4] / (len(xs) - (1 if sample else 0))
    return work


def correl(args):
    _, _, sxx, syy, sxy = moments(*pairs("correl", args))
    if sxx == 0 or syy == 0:
        raise CalcError("correl: xs or ys are all the same")
    return sxy / mpmath.sqrt(sxx * syy)


def line_fit(key, args):
    """The least-squares line y = a + b x through the pairs: (a, b)."""
    mx, my, sxx, _, sxy = moments(*pairs(key, args))
    if sxx == 0:
        raise CalcError(f"{key}: the xs are all the same")
    b = sxy / sxx
    return my - b * mx, b


def rsq(args):
    _, _, sxx, syy, sxy = moments(*pairs("rsq", args))
    if sxx == 0 or syy == 0:
        raise CalcError("rsq: xs or ys are all the same")
    return sxy ** 2 / (sxx * syy)


def forecast(args):
    if not isinstance(args[0], mpmath.mpf):
        raise CalcError("forecast needs a real x")
    a, b = line_fit("forecast", args[1:])
    return a + b * args[0]


#======================================================================
#  Round 2 - distributions, in the TI order: n and p (or the mean) first
#======================================================================
def normpdf(args):
    if len(args) == 2:
        raise CalcError("normpdf expects x, or x, mean and sd")
    mean, sd = normal_args("normpdf", args, 1)
    return mpmath.npdf(args[0], mean, sd)


def binomial_args(key, args):
    n, p = args[0], args[1]
    whole_count(key, n, "n")
    if not isinstance(p, mpmath.mpf) or not 0 <= p <= 1:
        raise CalcError(f"{key}: p goes from 0 to 1")
    return n, p


def count_of(key, value):
    if not isinstance(value, mpmath.mpf) or value != mpmath.floor(value):
        raise CalcError(f"{key}: k must be a whole number")
    return value


def binompdf(args):
    n, p = binomial_args("binompdf", args)
    k = count_of("binompdf", args[2])
    if k < 0 or k > n:
        return mpmath.mpf(0)
    return mpmath.binomial(n, k) * p ** k * (1 - p) ** (n - k)


def binom_upto(n, p, k):
    """P(X <= k) for Binomial(n, p), through the regularized incomplete beta."""
    if k < 0:
        return mpmath.mpf(0)
    if k >= n:
        return mpmath.mpf(1)
    if p in (0, 1):
        return mpmath.mpf(1 if p == 0 else 0)
    return mpmath.betainc(n - k, k + 1, 0, 1 - p, regularized=True)


def binomcdf(args):
    """binomcdf(n, p, k): P(X <= k); binomcdf(n, p, a, b): P(a <= X <= b)."""
    n, p = binomial_args("binomcdf", args)
    if len(args) == 3:
        return binom_upto(n, p, count_of("binomcdf", args[2]))
    low, high = count_of("binomcdf", args[2]), count_of("binomcdf", args[3])
    if low > high:
        raise CalcError("binomcdf(n, p, a, b): a must not be above b")
    return binom_upto(n, p, high) - binom_upto(n, p, low - 1)


def poisson_mean(key, value):
    if not isinstance(value, mpmath.mpf) or value <= 0:
        raise CalcError(f"{key}: the mean must be above 0")
    return value


def poisspdf(args):
    mean = poisson_mean("poisspdf", args[0])
    k = count_of("poisspdf", args[1])
    if k < 0:
        return mpmath.mpf(0)
    return mpmath.exp(k * mpmath.ln(mean) - mean - mpmath.loggamma(k + 1))


def poiss_upto(mean, k):
    """P(X <= k) for Poisson(mean), through the regularized upper incomplete gamma."""
    if k < 0:
        return mpmath.mpf(0)
    return mpmath.gammainc(k + 1, mean, mpmath.inf, regularized=True)


def poisscdf(args):
    """poisscdf(mean, k): P(X <= k); poisscdf(mean, a, b): P(a <= X <= b)."""
    mean = poisson_mean("poisscdf", args[0])
    if len(args) == 2:
        return poiss_upto(mean, count_of("poisscdf", args[1]))
    low, high = count_of("poisscdf", args[1]), count_of("poisscdf", args[2])
    if low > high:
        raise CalcError("poisscdf(mean, a, b): a must not be above b")
    return poiss_upto(mean, high) - poiss_upto(mean, low - 1)


#======================================================================
#  Round 3 (owner 2026-11-26) - the shape of a list, weights
#======================================================================
def standard_moments(key, args, least):
    """The values' mean, sample sd and population sd, with at least `least` values."""
    values = numbers_of(args, key, real=True)
    n = len(values)
    if n < least:
        raise CalcError(f"{key} needs at least {least} numbers")
    mean = mpmath.fsum(values) / n
    squares = mpmath.fsum((x - mean) ** 2 for x in values)
    if squares == 0:
        raise CalcError(f"{key}: the numbers are all the same")
    return values, n, mean, mpmath.sqrt(squares / (n - 1)), mpmath.sqrt(squares / n)


def skew(args):
    """The sample skewness, as Excel's SKEW."""
    values, n, mean, sd, _ = standard_moments("skew", args, 3)
    return n * mpmath.fsum(((x - mean) / sd) ** 3 for x in values) / ((n - 1) * (n - 2))


def skewp(args):
    values, n, mean, _, sd = standard_moments("skewp", args, 1)
    return mpmath.fsum(((x - mean) / sd) ** 3 for x in values) / n


def kurt(args):
    """The sample excess kurtosis (a normal shape gives 0), as Excel's KURT."""
    values, n, mean, sd, _ = standard_moments("kurt", args, 4)
    total = mpmath.fsum(((x - mean) / sd) ** 4 for x in values)
    return (n * (n + 1) * total / ((n - 1) * (n - 2) * (n - 3))
            - 3 * mpmath.mpf(n - 1) ** 2 / ((n - 2) * (n - 3)))


def kurtp(args):
    values, n, mean, _, sd = standard_moments("kurtp", args, 1)
    return mpmath.fsum(((x - mean) / sd) ** 4 for x in values) / n - 3


def wmean(args):
    values, weights = (numbers_of([value], "wmean", real=True) for value in args)
    if len(values) != len(weights):
        raise CalcError("wmean: values and weights must be as long as each other")
    if any(w < 0 for w in weights) or mpmath.fsum(weights) == 0:
        raise CalcError("wmean: weights must be 0 or more, not all 0")
    return mpmath.fsum(v * w for v, w in zip(values, weights)) / mpmath.fsum(weights)


#======================================================================
#  Round 3 - t, chi-square and F: pdf, cdf (a value or a range), inv
#======================================================================
def degrees(key, value, name="df"):
    if not isinstance(value, mpmath.mpf) or value <= 0:
        raise CalcError(f"{key}: {name} must be above 0")
    return value


def real(key, value):
    if not isinstance(value, mpmath.mpf):
        raise CalcError(f"{key} needs real numbers")
    return value


def t_cdf(x, df):
    tail = mpmath.betainc(df / 2, mpmath.mpf("0.5"), 0, df / (df + x * x), regularized=True) / 2
    return 1 - tail if x > 0 else tail


def t_pdf(x, df):
    return (mpmath.gamma((df + 1) / 2) / (mpmath.sqrt(df * mpmath.pi) * mpmath.gamma(df / 2))
            * (1 + x * x / df) ** (-(df + 1) / 2))


def chi2_cdf(x, df):
    return mpmath.mpf(0) if x <= 0 else mpmath.gammainc(df / 2, 0, x / 2, regularized=True)


def chi2_pdf(x, df):
    if x < 0 or (x == 0 and df > 2):
        return mpmath.mpf(0)
    if x == 0:
        return mpmath.inf if df < 2 else mpmath.mpf("0.5")
    return x ** (df / 2 - 1) * mpmath.exp(-x / 2) / (2 ** (df / 2) * mpmath.gamma(df / 2))


def f_cdf(x, d1, d2):
    return mpmath.mpf(0) if x <= 0 else mpmath.betainc(d1 / 2, d2 / 2, 0, d1 * x / (d1 * x + d2), regularized=True)


def f_pdf(x, d1, d2):
    if x <= 0:
        return mpmath.mpf(0) if x < 0 or d1 > 2 else (mpmath.inf if d1 < 2 else mpmath.mpf(1))
    return (mpmath.sqrt((d1 * x) ** d1 * d2 ** d2 / (d1 * x + d2) ** (d1 + d2))
            / (x * mpmath.beta(d1 / 2, d2 / 2)))


def inverse(cdf, p, low):
    """The x with cdf(x) = p: a bracket grown by doubling, then Anderson's bracketed root finder."""
    high = mpmath.mpf(1)
    while cdf(high) < p:
        low, high = high, high * 2
        if high > mpmath.mpf(10) ** 30:
            raise CalcError("no x gives this p")
    return mpmath.findroot(lambda x: cdf(x) - p, (low, high), solver="anderson")


def probability(key, p):
    if not isinstance(p, mpmath.mpf) or not 0 < p < 1:
        raise CalcError(f"{key}: p must be between 0 and 1")
    return p


def ranged(key, cdf, args, shape):
    """cdf(x, *shape) after one value, or cdf(b) - cdf(a) after two."""
    if len(args) == shape + 1:
        return cdf(real(key, args[0]))
    low, high = real(key, args[0]), real(key, args[1])
    if low > high:
        raise CalcError(f"{key}(a, b, ...): a must not be above b")
    return cdf(high) - cdf(low)


def tcdf(args):
    df = degrees("tcdf", args[-1])
    return ranged("tcdf", lambda x: t_cdf(x, df), args, 1)


def tinv(args):
    p, df = probability("tinv", args[0]), degrees("tinv", args[1])
    if p == mpmath.mpf("0.5"):
        return mpmath.mpf(0)
    upper = inverse(lambda x: t_cdf(x, df), max(p, 1 - p), mpmath.mpf(0))
    return upper if p > mpmath.mpf("0.5") else -upper


def chi2cdf(args):
    df = degrees("chi2cdf", args[-1])
    return ranged("chi2cdf", lambda x: chi2_cdf(x, df), args, 1)


def chi2inv(args):
    p, df = probability("chi2inv", args[0]), degrees("chi2inv", args[1])
    return inverse(lambda x: chi2_cdf(x, df), p, mpmath.mpf(0))


def fcdf(args):
    d1, d2 = degrees("fcdf", args[-2], "d1"), degrees("fcdf", args[-1], "d2")
    return ranged("fcdf", lambda x: f_cdf(x, d1, d2), args, 2)


def finv(args):
    p = probability("finv", args[0])
    d1, d2 = degrees("finv", args[1], "d1"), degrees("finv", args[2], "d2")
    return inverse(lambda x: f_cdf(x, d1, d2), p, mpmath.mpf(0))


#======================================================================
#  Round 3 - geometric (TI's geometpdf: tries up to the first success) and exponential
#======================================================================
def geometric_args(key, args):
    p = args[0]
    if not isinstance(p, mpmath.mpf) or not 0 < p <= 1:
        raise CalcError(f"{key}: p must be above 0, at most 1")
    return p


def geometric_upto(p, k):
    return mpmath.mpf(0) if k < 1 else 1 - (1 - p) ** k


def geometpdf(args):
    p = geometric_args("geometpdf", args)
    k = count_of("geometpdf", args[1])
    return mpmath.mpf(0) if k < 1 else (1 - p) ** (k - 1) * p


def geometcdf(args):
    """geometcdf(p, k): the first success by try k; geometcdf(p, a, b): at try a to b."""
    p = geometric_args("geometcdf", args)
    if len(args) == 2:
        return geometric_upto(p, count_of("geometcdf", args[1]))
    low, high = count_of("geometcdf", args[1]), count_of("geometcdf", args[2])
    if low > high:
        raise CalcError("geometcdf(p, a, b): a must not be above b")
    return geometric_upto(p, high) - geometric_upto(p, low - 1)


def expcdf(args):
    """expcdf(x, rate): P(X <= x) for an exponential wait; expcdf(a, b, rate): from a to b."""
    rate = degrees("expcdf", args[-1], "rate")
    return ranged("expcdf", lambda x: mpmath.mpf(0) if x <= 0 else -mpmath.expm1(-rate * x), args, 1)


# name -> (function, fewest arguments, most arguments), as matrix.FUNCTIONS
FUNCTIONS = {
    "skew": (skew, 1, None), "skewp": (skewp, 1, None), "kurt": (kurt, 1, None), "kurtp": (kurtp, 1, None),
    "wmean": (wmean, 2, 2),
    "tpdf": (lambda a: t_pdf(real("tpdf", a[0]), degrees("tpdf", a[1])), 2, 2),
    "tcdf": (tcdf, 2, 3), "tinv": (tinv, 2, 2),
    "chi2pdf": (lambda a: chi2_pdf(real("chi2pdf", a[0]), degrees("chi2pdf", a[1])), 2, 2),
    "chi2cdf": (chi2cdf, 2, 3), "chi2inv": (chi2inv, 2, 2),
    "fpdf": (lambda a: f_pdf(real("fpdf", a[0]), degrees("fpdf", a[1], "d1"), degrees("fpdf", a[2], "d2")), 3, 3),
    "fcdf": (fcdf, 3, 4), "finv": (finv, 3, 3),
    "geometpdf": (geometpdf, 2, 2), "geometcdf": (geometcdf, 2, 3), "expcdf": (expcdf, 2, 3),
    "range": (spread_range, 1, None), "iqr": (iqr, 1, None), "geomean": (geomean, 1, None),
    "harmean": (harmean, 1, None), "zscore": (zscore, 1, 3),
    "covar": (covariance("covar", True), 2, 2), "covarp": (covariance("covarp", False), 2, 2),
    "correl": (correl, 2, 2), "slope": (lambda a: line_fit("slope", a)[1], 2, 2),
    "intercept": (lambda a: line_fit("intercept", a)[0], 2, 2), "rsq": (rsq, 2, 2),
    "forecast": (forecast, 3, 3),
    "normpdf": (normpdf, 1, 3), "binompdf": (binompdf, 3, 3), "binomcdf": (binomcdf, 3, 4),
    "poisspdf": (poisspdf, 2, 2), "poisscdf": (poisscdf, 2, 3),
    "comb": (comb, 2, 2), "perm": (perm, 2, 2),
    "mode": (mode, 1, None), "percentile": (percentile, 2, 2), "quartile": (quartile, 2, 2),
    "normcdf": (normcdf, 1, 4), "norminv": (norminv, 1, 3),
}
