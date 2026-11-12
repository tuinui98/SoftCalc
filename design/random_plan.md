# Random window - plan (TODO section 11, owner 2026-11-12)

Built 2026-11-12 (R0-R2) with the owner's choices; custom list tabs
come later. Window size and place are remembered too.

## Window
- Tools > Random... opens a window of its own (like Compound
  Interest): owned by the main window, not modal, size, place and last
  tab remembered. Every menu command can go on the ribbon already.
- Tabs: **Numbers** (simple random) and **Password**. Custom tabs
  later.
- F1: a new help topic `17-random.md`, pictures from HelpShots.
- Randomness: `System.Security.Cryptography.RandomNumberGenerator`
  (`GetInt32` is unbiased) for everything, not `System.Random`.
- Owner 2026-11-12: `rand()` in expressions after all (R0 below).

## rand() in the calculator (R0, owner 2026-11-12)
- `rand()`: a decimal from 0 to 1; `rand(n)`: 0 to n; `rand(a, b)`: a
  to b. Owner: whole-number arguments give whole numbers with both ends
  included (dice: `rand(1, 6)`); a fraction or a literal with a point
  (`rand(1.0, 10)`) gives a decimal.
- Also round(x, [digits]), ceil(x), floor(x) (owner 2026-11-12).
- `rands(seed)` sets the seed, so the next draws repeat; `rands()`
  goes back to an unpredictable seed. Engine side, per window; an
  engine restart reseeds. Not for passwords (the Password tab uses the
  system's secure source).
- History: the line as typed, and on the right, faint, the line with
  each draw written in (`rand(1,6)+1` shows `4+1`), like `answer/2`
  shows `42/2`. Use Expression takes `rand(...)` (draws again), Insert
  Result takes the drawn value. The help says so.
- The hint line only shows signatures (`rand([a], [b])`), it never
  draws. History restore keeps the drawn values; nothing is drawn again.
- A user function may call it (`roll(n)=rand(1, n)`; a function needs
  a parameter); a graph of it is noise.

## Numbers tab (R1)
- Integer from Min to Max (both included), Count, "No repeats" (draw
  without replacement; refused when Count > the range), Sort.
- Decimal from Min to Max with N decimal places.
- Coin / yes-no with the chance of heads in % (50 by default: bias).
- Dice: sides and count; shows each die and the sum.
- Results in a list: Copy (selected), Copy All, Insert into Input (the
  calculator's Input, at the caret). Nothing goes to History.
- The options are saved; the results are not.

## Password tab (R2)
- Length 4-128 (16), upper case, lower case, digits, symbols (the set
  is editable), "Leave out look-alikes" (`Il1O0o`), at least one of
  each chosen kind; Count 1-50.
- A strength line: pool size and bits (length x log2 pool), e.g.
  `94 characters, 16 long: 105 bits`.
- Results in a monospace list: double-click or Copy copies one, Copy
  All copies every line. Generate again replaces them.
- No history: passwords are never written to disk or History; the
  list is cleared when the window closes. Options are saved.
- Clipboard: a copied password is kept out of Windows clipboard
  history and cloud sync, and cleared after 30 s if it is still there.
- Signature (owner 2026-11-12, replacing the "favourite word" mixed
  into the source, which in practice changed nothing): the owner's own
  mark written into each password as typed, at the end, the start or
  anywhere. Length counts it; the strength counts only the random
  characters. The place is saved, the signature never.

## Advanced random tab (R3, owner 2026-11-12)
- Tabs become Simple random (was Numbers), Advanced random, Password.
- Shape list (13): Flat, Bell, Narrow bell, Right skew, Left skew,
  Triangle, U shape, Two peaks, Rising, Falling, Decay, Growth, Custom
  (Beta a, b). One Strength control (bell sharpness, skew, peak gap),
  plus Peak at for Triangle. Owner: try it and adjust.
- Range: Whole (from-to cut into one slot per number, both ends
  included) or Decimals with places. Count, Copy, Copy All, Insert
  into Input as in Simple random. No "no repeats": results follow the
  shape as drawn.
- The chosen shape is drawn beside the list; after Generate a
  histogram of the results is drawn over it.
- Insert Command: each shape is one engine call, `randbeta(a, b, from,
  to)`, `randtri(peak, from, to)`, `randexp(rate, from, to)` (rate < 0
  rises) or `randpeaks(gap, from, to)`; whole from and to give a whole
  number, a point (`1.0`) gives decimals, so a decimal shape is
  `round(randbeta(4, 4, 1.0, 10), 2)`.
- Engine side done 2026-11-12 (`random_shapes.py`, full precision on
  mpmath, built on getrandbits so a seed repeats on any Python;
  tests/test_random_shapes.py pins seed-42 values).
- Tab done 2026-11-12: `Logic/RandomShapes.cs` (Strength 1-10, 5 =
  the pictures chosen: Bell Beta(4, 4), Narrow Beta(12, 12), skews
  Beta(2, 5) / Beta(5, 2), U Beta(0.6, 0.6), Two peaks gap 0.5, Rising
  / Falling Beta(2, 1) / Beta(1, 2), Decay / Growth rate +-4; doubles,
  SecureRandom), `UI/ShapePlot.cs`, `UI/RandomWindow.Advanced.cs`;
  Count up to 10000; the line under the buttons says what the shape
  does, and after Generate the mean, lowest and highest.

## Random source (R4, owner 2026-11-12; layout to discuss with the UI)
- Both kinds: Seed (repeatable) and Add entropy (mixed into the
  system's generator: mouse, typing, dice or coin results; a bits
  meter). Neither is ever saved.
- Password tab: always System; Seed is disabled there.
- Seed mode draws through an engine of the window's own (option 1),
  so the window's results match the same `rands(seed)` and calls typed
  in Input, at the same precision and in the same order. Insert
  Command then puts `rands(seed)` first.

## Code
- `Logic/RandomTools.cs`: the generators behind an `IRandomSource`, so
  tests can feed fixed numbers; no WinForms.
- `UI/RandomWindow.cs`: the window and tabs; prefs in settings.json
  under `random`.
- Tests: ranges include both ends, no-repeat draws are distinct and
  refuse too many, bias over many draws, decimals rounding, every
  chosen kind appears, look-alikes left out, strength bits, prefs round
  trip, the window saves no passwords.
