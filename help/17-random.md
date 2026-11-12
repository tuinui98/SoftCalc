# Random

Random numbers come two ways: `rand()` in any line, and the Random
window for lists of numbers, coin flips, dice and passwords.

## rand() in a line

| you type | you get |
|---|---|
| `rand()` | a decimal from 0 to 1 |
| `rand(n)` | from 0 to n |
| `rand(a, b)` | from a to b |

Whole numbers give a whole number, both ends included: `rand(1, 6)` is
a dice roll. A fraction, or a number written with a point, gives a
decimal: `rand(1.0, 6)` is any value from 1 to 6.

```calc
rand(1, 6)
rand(1.0, 10)
rand()
round(rand(0.0, 100), 2)
roll(n)=rand(1, n)
```

- History shows the line as typed and, on the right, the values drawn:
  `rand(1, 6)+1` shows `4+1`.
- Use Expression (click the line) brings back `rand(...)`, which draws
  again; Insert Result (Shift+click) takes the number already drawn.
- `rands(42)` sets the seed: the draws after it are the same each time
  you give that seed. `rands()` makes them unpredictable again.
- rand() is for games and samples. For passwords use the Password tab,
  which uses the system's secure generator.

## The Random window

Tools > Random... (it can go on the ribbon too).

![The Random window](img/random-numbers.light.png)

**Numbers** tab:

- **Whole numbers** from From to To, both included; **No repeats**
  draws each number at most once; **Sort** puts them in order.
- **Decimals** from From to To with the decimal **Places** you choose.
- **Coin**: heads or tails; **Heads %** sets the chance (50 is fair).
- **Dice** with any number of **Sides**; the sum is shown under the
  list.
- **Count** draws several at once. **Copy** copies the selected lines,
  **Copy All** every line; **Insert into Input** (or a double-click)
  puts the selected numbers in Input.

**Password** tab:

![The Password tab](img/random-password.light.png)

- **Length**, and the kinds of characters: upper case, lower case,
  digits, symbols (edit the symbol list if a site allows only some).
  Each chosen kind appears at least once.
- **Leave out look-alikes** drops `I l 1 O 0 o`.
- **Favourite word** is mixed into the secure random source: it can
  never make a password weaker, but the same word does not give the
  same password again.
- The line under the list shows the strength in bits; 80 or more is
  strong.
- Double-click or **Copy** copies one password, **Copy All** every
  one. A copied password is kept out of the Windows clipboard history
  and is wiped from the clipboard after 30 seconds (and when SoftCalc
  closes).

Nothing drawn in the window goes to History or to a file; the lists
are cleared when the window closes. The window keeps only its settings.
