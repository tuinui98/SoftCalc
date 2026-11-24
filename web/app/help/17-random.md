# Random

⋮ > Random draws numbers, values that follow a shape, passwords and
cards. Nothing it draws goes to History, and results are not kept; the
options are. Done, or the phone's Back, goes back to the calculator.

In Input, `rand()` draws too: `rand(1, 6)` rolls a die, and
`card("Months")` draws a card from a deck (see *Functions*).
`randm(3, 3, 1, 6)` rolls nine dice into a matrix, and
`fill(2, 3, rand(1, 6))` draws every cell again (see *Lists and matrices*).

## Simple

- **Whole**: numbers from From to To, both included. **No repeats**
  draws each number at most once.
- **Decimal**: numbers from From to To with the decimal places asked.
- **Coin**: heads or tails, with the chance of heads in %; the words
  can be your own (Yes and No).
- **Dice**: the sides and how many dice; the sum is shown.
- **Sort** puts the list in order, **Copy all** copies it, a tap on a
  number puts it in Input or copies it.
- **Insert command** puts the `rand()` line that draws one such value
  in Input, such as `rand(1, 100)`.

## Advanced

Values that follow a shape: Bell, Narrow bell, Right or Left skew,
Triangle, U shape, Two peaks, Rising, Falling, Decay, Growth, or Custom
(Beta a, b). **Strength** changes how much. The picture shows the
shape; after **Generate** the results are drawn over it as bars, and
the line under the buttons gives their mean, lowest and highest.
**Whole** or **Decimals** with places; up to 10000 values. **Insert
command** gives the engine call, such as `randbeta(4, 4, 1, 10)`.

## Seed

Simple and Advanced draw from the phone's secure generator. **Seed**
draws repeatably instead: the same seed gives the same list each time,
the same as typing `rands(seed)` and then the Insert command line in
Input. **New** makes a seed. The seed is not kept, and it does not
touch the calculator's own `rand()`.

## Password

Length, how many, and the kinds of characters: A-Z, a-z, 0-9 and
symbols (tick the groups, or edit the list). **Leave out look-alikes**
drops `Il1O0o`. Every password has at least one of each kind chosen.

- **Signature**: your own mark, written into each password as typed, at
  the end, the start or anywhere. It counts in Length; the strength
  line counts only the random characters. It is never kept.
- A tap copies a password; **Copy all** copies them all. SoftCalc
  clears the clipboard 30 seconds later if it is still open, but the
  phone's own clipboard history may keep it.
- Passwords always come from the secure generator, never a seed.

## Cards

Draws cards from a deck.

- **Ready-made decks**, in English or ไทย: a pack of playing cards (the
  two jokers start unticked), months, days, the provinces of Thailand,
  the districts (all, or by province), countries (all, or by
  continent), colours, the zodiac and the animal years.
- **Your own decks**: **New deck**, then one card a line; a weight after
  a bar makes a card more likely (`Fried rice | 3`). **Edit** and
  **Delete** change them; **Copy to my decks** makes a ready-made deck
  your own.
- **Import CSV** and **Export CSV** read and write the desktop's deck
  file (name, weight, image, colour; a CSV's pictures are not read here).
- **Export all decks** saves all your decks, with their pictures, in one
  zip file, the desktop's; **Import decks** adds the decks from one. A
  deck already here is skipped, one of the same name with other cards
  comes in as `name (2)`. ⋮ > Export and Import do the same. The
  pictures are kept, though not shown yet.
- **Insert command** puts the `card()` line for the deck, Count and No
  repeat in Input, such as `card("Months", 3, 1)`.
- Tap the card count to see the cards; untick any to leave them out.
- **No repeat** works like a pile: a drawn card stays out until
  **Shuffle**, also next time.
