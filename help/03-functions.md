# Functions

## From the Functions menu

The **Functions** menu lists every built-in function by group. Picking one:

- **wraps what is already in Input**: with `2` in Input, Functions > Basic
  Operations > sqrt gives `sqrt(2)`;
- on an empty Input it gives `sqrt()` with the caret inside the brackets;
- an operator (`+`, `^`, `mod`, `!`, ...) is put at the caret instead.

![Functions menu with Trig Functions open](img/menu-functions.light.png)

User Functions, at the bottom, lists the functions you defined
(section *Variables and user functions*).

## Maths

```calc
sqrt(2)               => 1.4142135623730950488016887242097
abs(-7)               => 7
exp(1)                => 2.7182818284590452353602874713527
ln(100)               => 4.6051701859880913680359829093687
log10(1000)           => 3
```

| group | functions |
|---|---|
| Basic Operations | `sqrt` `abs`, and the operators `+ - * / ^ mod ! %` |
| Trig Functions | `sin cos tan`, `asin acos atan`, `sinh cosh tanh`, `asinh acosh atanh` |
| Log Functions | `exp` `ln` `log10` |
| Date Functions | `epoch fromepoch longdate longtime longdatetime` (section *Dates and time*) |
| Finance Functions | `fv pv pmt nper` (section *Finance*) |
| Two's Complement | `twos` `signed` |
| Rounding | `round` `ceil` `floor` |
| Random | `rand` `rands` `randbeta` `randtri` `randexp` `randpeaks` (section *Random*) |

## Angles

`sin`, `cos` and `tan` read their argument in the angle unit, and `asin`,
`acos`, `atan` answer in it. Pick the unit with the ribbon's **Angle**
group (Deg / Rad) or View > Advanced Options > Degrees / Radians /
Grads. SoftCalc starts in degrees. Hyperbolic functions do not use it.

```calc
sin(pi/6)             => 0.5
```

The line above is in radians; in degrees (the start) write `sin(30)`.

- A pole is an error, not a huge number: `tan(90)` in degrees,
  `ln(0)`, `1/0`.
- Functions accept complex numbers: `sqrt(-1)` is `1i`, `ln(-1)` is
  `3.14...i`.

## Two's complement

`twos(x, bits)` gives the bit pattern that stores a signed value;
`signed(x, bits)` reads a pattern back as signed.

```calc
twos(-1, 8)           => 255
signed(0xFF, 8)       => -1
twos(300, 8)          => 44
```

- Without `bits`, View > Word Size (8, 16, 32 or 64 bits, also on the
  ribbon, see *Ribbon*) is used.
- A value outside the word wraps, like hardware: `twos(300, 8)` is 44.
- History adds the result in hex on the right: `twos(-1)` shows
  `twos(-1, 32) = 0xFFFFFFFF`.

## Rounding

`round(x)` rounds half away from zero; `round(x, digits)` keeps that
many decimal places (a negative count rounds to tens, hundreds, ...).
`floor(x)` goes down to a whole number, `ceil(x)` up.

```calc
round(2.5)            => 3
round(-2.5)           => -3
round(3.14159, 2)     => 3.14
round(1234, -2)       => 1200
floor(-1.2)           => -2
ceil(1.2)             => 2
```

A value only a hair off a whole number from binary rounding counts as
that number: `floor(0.1*30)` is 3.
