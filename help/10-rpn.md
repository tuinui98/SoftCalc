# RPN mode

In RPN (Reverse Polish Notation) you write the numbers first and the
operator after them. Turn it on with the ribbon's **RPN** button or
View > Advanced Options > RPN Mode; the Input box title then reads
`Input - RPN mode`.

```rpn
56 34 +               => 90
1 2 3 + *             => 5
5 sqrt                => 2.2360679774997896964091736687313
2 3 ^                 => 8
```

- Separate every item with a space.
- One line is one whole calculation; nothing is kept between lines
  except `answer` and `$n`.
- Functions take their usual number of arguments from the items before
  them: `0.05 12 / 120 -100 fv`. Optional arguments are not used.
- A number left over, or an operator without enough numbers, is an
  error.
- Definitions (`x=5`, `f(x)=x^2`) keep the normal form.

## Keypad in Numeric Calc

The keypad's **Ent** key separates items: `1`, Ent, `2`, `+` gives
`1 2 +` and calculates at once. Ent after an operator, or on an empty
Input, calculates.
