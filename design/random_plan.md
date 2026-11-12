# Random window - plan (TODO section 11, owner 2026-11-12)

Draft for the owner; nothing is built. Custom list tabs come later.

## Window
- Tools > Random... opens a window of its own (like Compound
  Interest): owned by the main window, not modal, size, place and last
  tab remembered. Every menu command can go on the ribbon already.
- Tabs: **Numbers** (simple random) and **Password**. Custom tabs
  later.
- F1: a new help topic `17-random.md`, pictures from HelpShots.
- Randomness: `System.Security.Cryptography.RandomNumberGenerator`
  (`GetInt32` is unbiased) for everything, not `System.Random`.
- The engine gets nothing: no `rand()` in expressions, since History,
  hints and replays expect a line to give the same answer each time.

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
- Favourite word (to decide): (a) leave it out; (b) mix it into the
  secure random bytes (cannot make a password weaker, but the same word
  does not give the same password); (c) derive the password from the
  word (repeatable, as weak as the word - not recommended).

## Code
- `Logic/RandomTools.cs`: the generators behind an `IRandomSource`, so
  tests can feed fixed numbers; no WinForms.
- `UI/RandomWindow.cs`: the window and tabs; prefs in settings.json
  under `random`.
- Tests: ranges include both ends, no-repeat draws are distinct and
  refuse too many, bias over many draws, decimals rounding, every
  chosen kind appears, look-alikes left out, strength bits, prefs round
  trip, the window saves no passwords.
