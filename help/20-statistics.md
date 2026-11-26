# Statistics

The statistics functions take numbers and lists mixed, and use every
cell of a matrix. The functions menu has them under **Statistics**: the
ten used most first, then **Descriptive**, **Two variables**,
**Counting** and **Distributions**.

## Describing a list

```calc
sum([1, 2, 3], 4)             => 10
mean([2, 4, 9])               => 5
median([3, 1, 2, 10])         => 2.5
mode([1, 2, 2, 3])            => 2
min([3, 1, 2])                => 1
max(3, 9, 2)                  => 9
range([3, 9, 1])              => 8
prod([1, 2, 3, 4])            => 24
count([4, 5], 6)              => 3
geomean(2, 8)                 => 4
harmean(1, 2, 4)              => 1.7142857142857142857142857142857
```

- `mode` gives the value that comes most often. On a tie it gives the
  first of them in the list, and History shows them all faint on the
  right: `mode([3, 1, 1, 3, 2])` is 3, with `[3, 1]` beside it. A list
  where each value comes once only is an error.
- `geomean` and `harmean` take numbers above 0.

## Spread

```calc
stdev([2, 4, 4, 4, 5, 5, 7, 9])   => 2.138089935299395077476427847038
stdevp([2, 4, 4, 4, 5, 5, 7, 9])  => 2
variance(1, 2, 3, 4)          => 1.6666666666666666666666666666667
variancep([1, 2, 3, 4])       => 1.25
zscore(130, 100, 15)          => 2
zscore([1, 2, 3])             => [-1, 0, 1]
```

- `stdev` and `variance` are for a sample (dividing by n - 1, as Excel's
  STDEV.S); `stdevp` and `variancep` for a whole population (n).
- `zscore(x, mean, sd)` says how many sd x is from the mean (Excel's
  STANDARDIZE); `zscore(list)` gives each value's, with the list's own
  mean and sample sd.

## Shape and weights

```calc
skew([3, 4, 5, 2, 3, 4, 5, 6, 4, 7])  => 0.35954307140679714325638586263628
kurt([3, 4, 5, 2, 3, 4, 5, 6, 4, 7])  => -0.1517996372084142188842567546065
wmean([90, 80], [3, 1])       => 87.5
```

- `skew` says how lopsided a list is: 0 when it is symmetric, above 0
  when it trails off to the right. `kurt` says how heavy its tails are
  against the normal shape, which gives 0 (excess kurtosis).
- `skew` and `kurt` are for a sample, as Excel's SKEW and KURT;
  `skewp` and `kurtp` for a whole population.
- `wmean(values, weights)` is the weighted mean: each value counts as
  often as its weight says.

## Percentiles and quartiles

```calc
percentile([1, 3, 2, 4], 0.3)     => 1.9
quartile([1, 2, 4, 7, 8, 9, 10, 12], 1)   => 3.5
quartile([1, 2, 3, 4], 2)     => 2.5
iqr([1, 2, 3, 4, 5, 6, 7, 8]) => 3.5
```

- `percentile(list, p)` takes p from 0 to 1 (0.25 for the 25th
  percentile), and goes in a straight line between the sorted values, as
  Excel's PERCENTILE.INC.
- `quartile(list, q)` takes q = 0 (the least), 1, 2 (the median), 3 or
  4 (the most), as Excel's QUARTILE.INC; `iqr` is the third quartile
  less the first.

## Two variables

The xs come first, then the ys, the same length:

```calc
covar([1, 2, 3], [2, 4, 7])   => 2.5
covarp([1, 2, 3], [2, 4, 7])  => 1.6666666666666666666666666666667
correl([1, 2, 3], [2, 4, 7])  => 0.99339926779878285489956379038765
slope([1, 2, 3], [2, 4, 7])   => 2.5
intercept([1, 2, 3], [2, 4, 7])   => -0.66666666666666666666666666666667
rsq([1, 2, 3], [2, 4, 7])     => 0.98684210526315789473684210526316
forecast(4, [1, 2, 3], [2, 4, 7]) => 9.3333333333333333333333333333333
```

- `covar` is for a sample (n - 1), `covarp` for a whole population.
- `slope`, `intercept`, `rsq` and `forecast` use the best straight line
  through the points (least squares): y = intercept + slope × x.
- **Excel puts the ys first** (`SLOPE(ys, xs)`); SoftCalc reads them in
  the order you would plot them, xs then ys.

## Counting

```calc
comb(5, 2)                    => 10
perm(5, 2)                    => 20
comb(100, 50)                 => 100891344545564193334812497256
comb(3, 5)                    => 0
```

- `comb(n, k)`: the ways to choose k things of n when the order does
  not count (nCr); `perm(n, k)`: when it does (nPr).
- n and k are whole numbers, 0 or more; k above n gives 0. The answer
  is whole and exact while it fits the precision.

## The normal distribution

```calc
normcdf(1.96)                 => 0.97500210485177956586341573095916
normcdf(-1, 1)                => 0.68268949213708589717046509126408
normcdf(110, 100, 10)         => 0.84134474606854294858523254563204
normcdf(90, 110, 100, 10)     => 0.68268949213708589717046509126408
norminv(0.975)                => 1.9599639845400542355245944305206
norminv(0.5, 10, 2)           => 10
normpdf(0)                    => 0.39894228040143267793994605993438
```

| you type | you get |
|---|---|
| `normcdf(x)` | the chance of a value up to x (mean 0, sd 1) |
| `normcdf(a, b)` | the chance of a value from a to b |
| `normcdf(x, mean, sd)` | up to x, for that mean and sd |
| `normcdf(a, b, mean, sd)` | from a to b, for that mean and sd |
| `norminv(p, [mean, sd])` | the x that `normcdf` gives p for |
| `normpdf(x, [mean, sd])` | the height of the bell curve at x |

- sd must be above 0; p in `norminv` is between 0 and 1.
- They work at the precision set, so `:prec 64` gives 64 digits.

## t, chi-square and F

```calc
tcdf(2.1, 15)                 => 0.97347237192397863262399646449103
tinv(0.975, 10)               => 2.2281388519862747483954906632018
chi2cdf(3.84, 1)              => 0.94995647875129490105233633583759
chi2inv(0.95, 1)              => 3.8414588206941259583613754373626
fcdf(3.33, 5, 10)             => 0.95016872420277869568067559396988
finv(0.95, 5, 10)             => 3.3258345304130120113024637451587
```

| you type | you get |
|---|---|
| `tcdf(x, df)`, `tcdf(a, b, df)` | Student's t: up to x, or from a to b |
| `chi2cdf(x, df)`, `chi2cdf(a, b, df)` | chi-square, the same way |
| `fcdf(x, d1, d2)`, `fcdf(a, b, d1, d2)` | F, with d1 and d2 degrees of freedom |
| `tinv(p, df)`, `chi2inv(p, df)`, `finv(p, d1, d2)` | the x their cdf gives p for |
| `tpdf`, `chi2pdf`, `fpdf` | the height of the curve at x |

These give the critical values and p-values a test needs: a two-sided
t-test's p-value is `2*(1 - tcdf(abs(t), df))`, its 95% critical value
`tinv(0.975, df)`.

## Binomial and Poisson

In the TI calculators' order, n and p (or the mean) first:

```calc
binompdf(10, 0.5, 5)          => 0.24609375
binomcdf(10, 0.5, 5)          => 0.623046875
binomcdf(10, 0.5, 3, 7)       => 0.890625
poisspdf(3, 2)                => 0.22404180765538774340704087042528
poisscdf(3, 2)                => 0.42319008112684351532441053302553
poisscdf(3, 1, 4)             => 0.7654761761559081233073896406197
```

- `binompdf(n, p, k)`: the chance of exactly k successes in n tries of
  chance p; `binomcdf(n, p, k)`: of k or fewer; `binomcdf(n, p, a, b)`:
  of a to b.
- `poisspdf(mean, k)`: the chance of exactly k events when mean are
  expected; `poisscdf(mean, k)` and `poisscdf(mean, a, b)` as above.

```calc
geometpdf(0.2, 3)             => 0.128
geometcdf(0.2, 3)             => 0.488
expcdf(1, 2)                  => 0.86466471676338730810600050502752
```

- `geometpdf(p, k)`: the chance that the first success comes at try k;
  `geometcdf(p, k)`: by try k; `geometcdf(p, a, b)`: at try a to b.
- `expcdf(x, rate)`: the chance an exponential wait (rate events per
  unit) is over by x; `expcdf(a, b, rate)`: that it ends from a to b.

## Random draws

`randnorm(mean, sd)` draws from the normal distribution; `randnorm()`
draws with mean 0 and sd 1. The draw is always a decimal: wrap it in
`round` for a whole number. `randbinom(n, p)` draws a number of
successes, `randpois(mean)` a Poisson count, both whole. `rands(seed)`
makes the draws repeat, and `fill` draws again for every cell (see
*Random*).

```calc
randnorm(170, 10)
round(randnorm(170, 10))
randbinom(10, 0.5)
randpois(4)
fill(2, 3, randnorm())
```

More: [NIST: Normal Distribution](https://www.itl.nist.gov/div898/handbook/eda/section3/eda3661.htm),
[Binomial Distribution](https://www.itl.nist.gov/div898/handbook/eda/section3/eda366i.htm),
[Poisson Distribution](https://www.itl.nist.gov/div898/handbook/eda/section3/eda366j.htm),
[t Distribution](https://www.itl.nist.gov/div898/handbook/eda/section3/eda3664.htm),
[Chi-Square Distribution](https://www.itl.nist.gov/div898/handbook/eda/section3/eda3666.htm),
[F Distribution](https://www.itl.nist.gov/div898/handbook/eda/section3/eda3665.htm),
[Measures of Skewness and Kurtosis](https://www.itl.nist.gov/div898/handbook/eda/section3/eda35b.htm),
[Percentiles](https://www.itl.nist.gov/div898/handbook/prc/section2/prc262.htm),
[Measures of Location](https://www.itl.nist.gov/div898/handbook/eda/section3/eda351.htm),
[Measures of Scale](https://www.itl.nist.gov/div898/handbook/eda/section3/eda356.htm)
and [Linear Least Squares Regression](https://www.itl.nist.gov/div898/handbook/pmd/section1/pmd141.htm);
[DLMF §26.3](https://dlmf.nist.gov/26.3) (binomial coefficients),
MathWorld [Combination](https://mathworld.wolfram.com/Combination.html),
[Permutation](https://mathworld.wolfram.com/Permutation.html),
[Mode](https://mathworld.wolfram.com/Mode.html) and
[Correlation Coefficient](https://mathworld.wolfram.com/CorrelationCoefficient.html).
