# Error messages

An error shows in History in red, starting with `ERROR:`. Your line
stays in Input so you can fix it.

```calc
zzx+1                 => ERROR:Unknown name 'zzx'
(1+2                  => ERROR:Expected ')' at end of input
2+                    => ERROR:Incomplete expression
1/0                   => ERROR:Division by zero
1 [in->kg]            => ERROR:Cannot convert in to kg
10 [m->qq]            => ERROR:Unknown unit 'qq'
$9                    => ERROR:No result $9
```

| message | what to do |
|---|---|
| `Unknown name 'zzx'` | a typo, or a variable not defined yet |
| `Expected ')'` | a bracket is missing |
| `Incomplete expression` | the line ends with an operator, or two numbers sit side by side in RPN |
| `Unexpected 'b'` | two names side by side (`a b`): put an operator between them; or a `,` in a number |
| `Division by zero` | the divisor is 0 |
| `ln is undefined here` | the function has no value there: `ln(0)`, `tan(90)` in degrees |
| `f expects 2 argument(s)` | a user function called with the wrong number of values |
| `f needs arguments in ( )` | a function name used without brackets |
| `Cannot convert in to kg` | the units are from different groups |
| `Unknown unit 'qq'` | a unit name SoftCalc does not know (see *Unit conversions*) |
| `No ECB rate for BGN` | the ECB does not publish that currency |
| `Coin prices unavailable` | no coin prices yet and CoinGecko could not be reached (see *Cryptocurrency*) |
| `No CoinGecko price of TRX in ISK` | CoinGecko gave no price for that pair |
| `No result $9` | there are fewer results than that in History |
| `'sin' is a reserved name` | built-in names cannot be redefined |
| `'f' cannot call itself` | a function may not use its own name |
| `'+' is not defined for a duration and this value` | write the number as a duration: `1:30 + 2:00` |
| `Missing digits after '0x'` | a base prefix needs digits |
| `Factorial of a negative integer` | `!` works for 0, 1, 2, ... and non-integers |

## Lists and matrices

```calc
A=[[1, 2], [3, 4]]                   => Variable Added
2[1, 2]                              => ERROR:Write * before a list (2*[1, 2]); at(A, 1) takes a cell
[1, 2]*[3, 4]                        => ERROR:Two lists: use dot(a, b), or write one as a matrix
A+[1, 2]                             => ERROR:'+' needs the same size: 2x2 and a list of 2
inv([[1, 2], [2, 4]])                => ERROR:The matrix has no inverse (its det is 0)
det([[0.316, 1.2], [0.632, 2.4]])    => 0
```

| message | when |
|---|---|
| `Write * before a list` | a number or name right before `[`: write `2*[1, 2]`; a cell is `at(A, 1)`, not `A[1]` |
| `Empty list` / `Missing ']' after a list` | `[]`, or a list not closed |
| `The rows of a matrix must be the same length` | `[[1, 2], [3]]` |
| `Two lists: use dot(a, b)` | `*` between two lists; `dot` gives one number, or write one as a matrix `[[1], [2]]` |
| `'+' needs the same size` | `+` and `-` take two lists or matrices of one size |
| `Sizes do not fit` | `A*B` needs as many columns in A as rows in B; a list on the left is a row, on the right a column |
| `Divide by a matrix` / `'/' with a matrix on the right` | there is no `A/B` or `2/A`: use `A*inv(B)` |
| `A matrix to a power needs a whole number` | `A^n` takes n from -1000 to 1000; `A^0.5` has no meaning here |
| `'^' needs a square matrix`, `det needs a square matrix` | `^`, `det`, `inv`, `trace` and `solve` need as many rows as columns |
| `The matrix has no inverse (its det is 0)` | `inv`, `A^-1` and `solve` of a matrix whose det is 0 (see below) |
| `solve: A has 2 rows, b has 3` | b needs one number per row of A |
| `at: row 3 is past the end (2)` | `at`, `row`, `col` count from 1 up to the size |
| `dot needs two lists of the same length` / `cross needs two lists of 3 numbers` | the lists do not match |
| `sort takes a list` / `min needs real numbers` | `sort` works on one list; `min`, `max` and `sort` need real numbers |
| `A list may have at most 10000 numbers` / `... 10000 cells` | the size limit; `det`, `inv`, `solve` and `^` take at most 50x50 |
| `Only real numbers can be converted` | a unit tag on a list (`[1, 2] [m->ft]`) does not work yet |
| `Lists and matrices are not available in RPN` | switch RPN off to use `[ ]` |

### Note: when a det counts as 0

SoftCalc keeps numbers in binary, and a decimal such as `0.316` is not
exact in binary: it is off in about the 40th digit. A matrix whose det
is 0 on paper is then left with a det of about 1e-40 instead of 0.
So `det`, `inv`, `solve` and `A^n` work at twice the digits, and **a det
smaller than the shown precision allows, for a matrix of that size,
counts as 0**: `det` shows `0`, and `inv`, `A^-1` and `solve` say there
is no inverse. At 32 digits that is a det below about 1e-32 times the
size of the rows multiplied together.

### Note: nearly singular matrices lose digits

A matrix close to having det 0 magnifies the rounding of its own
numbers. In `inv([[1, 1], [1, 1.00000000000000000001]])` the two rows
differ by 1e-20; the answer is about 1e20, and only its first 24 or so
of the 32 shown digits are right. The closer to det 0, the fewer digits
can be trusted: a matrix that magnifies rounding 10^k times loses about
k digits. Every calculator that works with decimals does the same. If
it matters, use Display Precision 64 or more, or check the answer:
`A*inv(A)` should show the identity matrix.
