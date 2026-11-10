# Numbers and operators

## Writing numbers

```calc
12.5                  => 12.5
.5                    => 0.5
1.5e-3                => 0.0015
1 000 000             => 1000000
```

- A space inside a run of digits is ignored: `1 000` is `1000`.
- Commas are not allowed: `1,000` is an error.
- `e` with digits after it is an exponent (`1e3`); on its own it is the
  constant e, so `2e` is 2 times e.

## Other bases

```calc
0xFF                  => 255
0b1010                => 10
1010b                 => 10
0o17                  => 15
```

| base | write |
|---|---|
| hexadecimal | `0x1F` |
| binary | `0b101` or `101b` |
| octal | `0o17` |
| decimal | `12` or `0d12` |

You can mix bases in one line. View > Hexadecimal / Decimal / Binary /
Octal Output picks how results are written.

## Operators, strongest first

| operator | meaning | example |
|---|---|---|
| `!` `%` | factorial, percent | `5!` = 120, `50%` = 0.5 |
| `^` | power, right to left | `2^3^2` = 2^9 = 512 |
| (nothing) | implicit multiply | `2(3+1)` = 8 |
| `-` | negate | `-2^2` = -4 |
| `*` `/` `mod` | multiply, divide, remainder | `7 mod 3` = 1 |
| `+` `-` | add, subtract | |
| `[a->b]` | convert units | `1 [ft->in]` = 12 |

```calc
2^3^2                 => 512
-2^2                  => -4
200*10%               => 20
-7 mod 3              => 2
2.5!                  => 3.3233509704478425511840640312646
```

- `^` groups from the right, and `-2^2` is `-(2^2)`.
- `mod` rounds down, so the remainder has the sign of the divisor:
  `-7 mod 3` = 2.
- `!` of a non-integer uses the gamma function; negative integers are an
  error.
- `%` divides by 100 and nothing more: `200*10%` is `200*0.1`.

## Implicit multiplication

A number or `)` followed by a name or `(` multiplies:

```calc
x=3                   => Variable Added
2x                    => 6
2(x+1)                => 8
2x^2                  => 18
1/2x                  => 0.16666666666666666666666666666667
```

- `2x^2` is `2*(x^2)`: the power binds first.
- `1/2x` is `1/(2x)`: the implicit product binds before `/`.
- Two names never multiply by being side by side: `a b` is an error;
  write `a*b`.

## Complex numbers

`i` and `j` are the imaginary unit.

```calc
(3+4i)*(1-2i)         => (11-2i)
sqrt(-4)              => 2i
abs(3+4i)             => 5
```

## Precision

View > Advanced Options sets the precision: Low (32), Medium (64),
High (128) or Extreme (512) significant digits. A result that needs more
digits than that is written in scientific form, such as `1.5e32`.
