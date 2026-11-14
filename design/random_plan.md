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
- Tabs become Simple (was Numbers), Advanced, Password (owner: the
  window is already called Random).
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
  in Input, at the same precision and in the same order.
- Seed done 2026-11-12: a Source row above the tabs (System | Seed
  [number]); typing a seed picks Seed; the Password tab switches to
  System and greys Seed out, and gives it back on the other tabs.
  `Engine/SeedEngine.cs` starts the window's engine on the first
  seeded Generate (protocol `draws`: rands(seed), then the tab's
  Insert Command line count times; unique for No repeats). Each
  Generate starts again from the seed, so the same seed gives the
  same list. Insert Command inserts only the line (Input takes one
  line); the note after Generate says to type rands(seed) first.
- New seed (owner 2026-11-12): a button beside the Seed box fills it
  with 128 bits (39 digits) from the system generator.
  Owner 2026-11-13: while the Entropy tab is open, New seed is Get from
  that tab's source then Use as Seed; on the other tabs it stays the
  system generator.

## Entropy tab (R5, owner 2026-11-12)
- Replaces the planned Add entropy window. To try sources and
  standards side by side: System (CNG), TPM 2.0 (TPM2_GetRandom via
  tbs.dll; works as a standard user here), CPU jitter (1024 timings,
  min-entropy by the most common step, SP 800-90B 6.3.1), Clock (the
  weak classic), GUID v4, Mouse, Keys, Dice (1-6), NIST Randomness
  Beacon and drand (public: 0 secret bits; Get x10 fetches the last
  10 pulses/rounds; a GET with no user data).
- Each row: raw value, 128-bit seed (SHA-256 cut to 128 bits), change
  from the row before (clock), raw bits changed, seed bits changed,
  estimated bits. Get x10 adds a summary (mean bits changed; 64 of 128
  is chance). Mouse, Keys and Dice give one sample from what was put in.
- Use as Seed puts a row's seed in the Seed box. Mix into System
  hashes the chosen rows (all when none) into a key: each draw is then
  HMAC-SHA256(key, 32 system bytes + counter) (`MixedRandom`), for every
  tab, Password too, until Clear mix or the window closes; the Source
  row says "System + entropy (N bits)". Nothing here is saved but the
  source shown.
- Code: `Logic/Entropy.cs`, `Logic/TpmRandom.cs`,
  `Engine/RandomBeacons.cs`, `UI/RandomWindow.Entropy.cs`; EntropyTests
  (network only with SOFTCALC_NET_TESTS=1).

## Card decks (R6, owner 2026-11-14)
- The main job of a custom draw is a pick from a pile of cards. The
  tab strip ends in a "..." tab (no other tab until a draw is made):
  New draw, New deck, the user's decks (Edit, Copy, Delete, Import
  CSV, Export CSV) and the ready-made decks (View, Copy to my decks,
  Export CSV), named in English or Thai (owner: both languages).
- A deck: cards with a name, a weight (whole number 1-1000) and an
  optional picture (shrunk to 128 px, stored as PNG in `<data>\cards\`
  under a name from its content, dropped when no deck uses it; a
  picture added in the last hour stays, for another open window).
  Cards come from an Add box (Auto: lines, else commas, else spaces;
  owner ok), Edit all as text (`name | weight` lines), or CSV
  (`name,weight,image`; UTF-8 with or without a BOM, UTF-16, else the
  Thai code page 874 Excel writes; export as UTF-8 with a BOM).
- Ready-made (embedded `Decks/*.txt`, `English|Thai`): playing cards
  (52, and two jokers that start unticked), months, days (Monday
  first), the 77 provinces, countries by continent after UN M49 (193
  UN members plus Palestine, Taiwan and the Vatican, owner; the
  Americas split North with Central and the Caribbean, and South;
  also all 196), 12 colours with swatches, the zodiac, the twelve
  animal years. Alphabetical lists sort in the shown language.
  Districts (928) wait for a data source (to ask the owner).
- Saved in `<data>\cards.json` (decks and draw tabs), apart from
  settings.json, written at once after each change under the data
  folder lock; between two Random windows the last write wins.

## Draw tabs (R7, owner 2026-11-14)
- New draw on "..." asks for the name (Custom random) and the steps;
  the tab goes before "...". Edit tab, Delete tab (asks first).
- A step pools any decks with extra cards typed for this draw only
  (not saved as a deck); any card can be unticked (Days + "end of the
  month, payday" = 9 cards; untick Saturday and Sunday). Use weights,
  No repeat per step. One Draw gives one line, a card from each step
  in turn.
- Conditions (owner 2026-11-14): an extra card takes a weight after
  "|" and a condition after ">>": `Rice soup>>stop|2` (also
  `Rice soup|2>>stop`). A deck card gets its condition in Edit tab
  (the Then row: Next step, Stop the line, Skip to step, Draw from
  deck with a count and "then stop"); conditions belong to the draw
  tab, not the deck. Conditions: `next` (default), `stop`, `step 3`
  (later steps only), `Gym` (a card from the deck named Gym, then the
  next step), `Gym*2` (two different cards), `Gym,stop`,
  `deck:stop` for a deck named like a keyword. Splitting extras by
  commas or spaces keeps `Gym,stop` and `step 3` whole. The deck a
  condition draws from is one level only (its cards' own conditions
  are not used), follows the step's weights and No repeat (its own
  pile, back on Shuffle; its jokers stay out), and shows as
  `Gym → Yoga + Weights · 1 hour`, an extra reel or board, and Stats
  rows `1 → Gym`. Edit tab checks conditions on OK (unknown deck,
  step not later). Save as deck turns the extra cards into a deck
  of my own (opens the deck editor) and moves their conditions,
  ticks, pile and counts to it. Runs of numbers (owner 2026-11-14):
  `[1,100]` or `[from,to,step]` (step 1 if left out, decimals allowed,
  leading zeros and decimals kept as typed) gives a card per number,
  with the words around it on each (`fish [1,3] each`); in Extra and
  the deck editor's Add box. The deck editor's Add box takes
  "|weight" too and cuts off any ">>" condition, with a note (owner
  2026-11-14).
- No repeat works like a pile (owner): a drawn card stays out until
  Shuffle, also across closing the window. Shuffle also mixes where the
  cards sit on the dartboard and spin wheel (owner 2026-11-14; kept per
  step, for show only: draws and seeds do not use it).
- Card colours (owner 2026-11-14): Colour... / No colour in the deck
  editor (several cards at once), a colour column in CSV; the
  dartboard and spin wheel fill sectors with it, the slot machine shows
  a strip. The ready-made pack is four-colour (spades black, hearts red,
  diamonds blue, clubs green), two shades in turn.
- Theme (set in Edit tab, owner 2026-11-14; new tabs start on Slot
  machine): List (lines roll in Results, slowing down), Slot machine
  (a reel per step, stopping one by one),
  Dartboard (a board per step, sectors sized by chance; owner
  2026-11-14: the board spins 2-4 s and the dart is thrown 1-3 s in, each
  random; it flies 0.25 s (owner), lands in the drawn card's sector and turns with
  the board if it still turns; the dart's point goes into the board;
  over 36 cards the board gets rings, at most 4 (owner 2026-11-14,
  Logic/BoardRings.cs): every card's area is its share of the chances,
  rings start equally wide and each boundary moves at most 5% so whole
  cards fill them, the heaviest cards (then the first listed) go in
  the middle, and the fewest rings with every sector 10 degrees or more
  (owner: was 15) are used; 52 cards: 7/18/27, 77 cards: 7/15/23/32,
  outer sectors near 7 degrees at 125 cards),
  Spin wheel (owner 2026-11-14, design X: up to three rings, a card each,
  turning clockwise, anticlockwise, clockwise for 3-5 s and stopping
  outside in; a marker at the top points down, one on the hub points up,
  each ring's card is on that line, framed; the top marker flicks as
  edges pass; 4-6 cards spin 2 wheels in turn, shared out evenly).
  Slot machine with Count 2-5 (owner 2026-11-14, layout A): one card a
  line gives reels side by side, else a row of reels per line, top to
  bottom, each row stopping left to right; more lines show the last one
  still. Dartboard plays the last line only.
  A bar under the theme's area drags it taller, Results shorter, live as it
  moves (owner
  2026-11-14); the height is kept per tab until the theme changes.
  No spoilers (owner 2026-11-14): Results and History fill only after
  the reels or the dart. Effects play only for one line; the cards
  are drawn first and the effect plays towards them (System.Random for
  the show only); a click skips.
- Results, History (newest first, with the time, at most 1000) and
  Stats (times, share, the share the weights give) per tab, saved.
  Clear draw (owner) empties the results only; Clear history and
  Clear stats (owner 2026-11-14: buttons and a right-click menu, both
  ask first) empty their list. The window has no Minimize button
  (owner 2026-11-14). Nothing goes to the calculator's
  History.
- Source row as on the other tabs: System (with any Entropy mix), or
  Seed: the window's engine draws rand() once per pick after
  rands(seed), and a pick is floor(u * points), so the same seed and
  the same piles give the same cards.
- Code: `Logic/Cards.cs`, `Logic/ReadyDecks.cs`, `Logic/CardDraws.cs`,
  `Persistence/CardStore.cs`, `UI/RandomWindow.Cards.cs`,
  `UI/RandomWindow.Draws.cs`, `UI/DeckEditor.cs`, `UI/DrawEditor.cs`,
  `UI/DrawPage.cs`, `UI/DrawStages.cs`, `UI/SpinStage.cs`, `UI/CardPictures.cs`;
  CardsTests. Seed asks the engine for the most picks a line can need
  (a card per step plus its conditions' cards).

## Code
- `Logic/RandomTools.cs`: the generators behind an `IRandomSource`, so
  tests can feed fixed numbers; no WinForms.
- `UI/RandomWindow.cs`: the window and tabs; prefs in settings.json
  under `random`.
- Tests: ranges include both ends, no-repeat draws are distinct and
  refuse too many, bias over many draws, decimals rounding, every
  chosen kind appears, look-alikes left out, strength bits, prefs round
  trip, the window saves no passwords.
