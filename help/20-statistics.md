# Statistics

The statistics functions take numbers and lists mixed, and use every
cell of a matrix. The functions menu has them under **Statistics**: the
ten used most first, then **Descriptive**, **Counting** and
**Distributions**.

## Describing a list

```calc
sum([1, 2, 3], 4)             => 10
mean([2, 4, 9])               => 5
median([3, 1, 2, 10])         => 2.5
mode([1, 2, 2, 3])            => 2
min([3, 1, 2])                => 1
max(3, 9, 2)                  => 9
prod([1, 2, 3, 4])            => 24
count([4, 5], 6)              => 3
```

- `mode` gives the value that comes most often. On a tie it gives the
  first of them in the list, and History shows them all faint on the
  right: `mode([3, 1, 1, 3, 2])` is 3, with `[3, 1]` beside it. A list
  where each value comes once only is an error.

## Spread

```calc
stdev([2, 4, 4, 4, 5, 5, 7, 9])   => 2.138089935299395077476427847038
stdevp([2, 4, 4, 4, 5, 5, 7, 9])  => 2
variance(1, 2, 3, 4)          => 1.6666666666666666666666666666667
variancep([1, 2, 3, 4])       => 1.25
```

`stdev` and `variance` are for a sample (dividing by n - 1, as Excel's
STDEV.S); `stdevp` and `variancep` for a whole population (n).

## Percentiles and quartiles

```calc
percentile([1, 3, 2, 4], 0.3)     => 1.9
quartile([1, 2, 4, 7, 8, 9, 10, 12], 1)   => 3.5
quartile([1, 2, 3, 4], 2)     => 2.5
```

- `percentile(list, p)` takes p from 0 to 1 (0.25 for the 25th
  percentile), and goes in a straight line between the sorted values, as
  Excel's PERCENTILE.INC.
- `quartile(list, q)` takes q = 0 (the least), 1, 2 (the median), 3 or
  4 (the most), as Excel's QUARTILE.INC.

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
```

| you type | you get |
|---|---|
| `normcdf(x)` | the chance of a value up to x (mean 0, sd 1) |
| `normcdf(a, b)` | the chance of a value from a to b |
| `normcdf(x, mean, sd)` | up to x, for that mean and sd |
| `normcdf(a, b, mean, sd)` | from a to b, for that mean and sd |
| `norminv(p, [mean, sd])` | the x that `normcdf` gives p for |

- sd must be above 0; p in `norminv` is between 0 and 1.
- They work at the precision set, so `:prec 64` gives 64 digits.

## Random from a bell

`randnorm(mean, sd)` draws from the normal distribution; `randnorm()`
draws with mean 0 and sd 1. The draw is always a decimal: wrap it in
`round` for a whole number. `rands(seed)` makes the draws repeat, and
`fill` draws again for every cell (see *Random*).

```calc
randnorm(170, 10)
round(randnorm(170, 10))
fill(2, 3, randnorm())
```

More: [NIST: Normal Distribution](https://www.itl.nist.gov/div898/handbook/eda/section3/eda3661.htm),
[NIST: Percentiles](https://www.itl.nist.gov/div898/handbook/prc/section2/prc262.htm),
[NIST: Measures of Location](https://www.itl.nist.gov/div898/handbook/eda/section3/eda351.htm)
and [Measures of Scale](https://www.itl.nist.gov/div898/handbook/eda/section3/eda356.htm);
[DLMF §26.3](https://dlmf.nist.gov/26.3) (binomial coefficients),
MathWorld [Combination](https://mathworld.wolfram.com/Combination.html),
[Permutation](https://mathworld.wolfram.com/Permutation.html) and
[Mode](https://mathworld.wolfram.com/Mode.html).
