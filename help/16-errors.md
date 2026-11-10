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
| `No result $9` | there are fewer results than that in History |
| `'sin' is a reserved name` | built-in names cannot be redefined |
| `'f' cannot call itself` | a function may not use its own name |
| `'+' is not defined for a duration and this value` | write the number as a duration: `1:30 + 2:00` |
| `Missing digits after '0x'` | a base prefix needs digits |
| `Factorial of a negative integer` | `!` works for 0, 1, 2, ... and non-integers |
