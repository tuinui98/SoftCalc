# Getting started

Type a calculation with the keys and press **=**. The line and its result
go to **History**; Input clears for the next line.

```calc
1+2*3                 => 7
0.1+0.2               => 0.3
2^100                 => 1267650600228229401496703205376
```

SoftCalc works in decimal at the precision you choose (Settings >
Precision), so `0.1+0.2` is exactly `0.3`, and big numbers keep every
digit the precision allows. Tap an example in this Help to put it in
Input.

## The keys

- **Basic** keys show first. Swipe the keys left or right, or tap
  **Sci ▸**, for the scientific keys; on a wide screen both show side by
  side.
- Swipe **up** on the keys for the menu (the same as **⋮**).
- **⌫** beside Input takes one character; hold it to take more.
- **C** takes the last number, **AC** clears Input.
- **2nd** gives the yellow meaning of the next key (`asin` on `sin`).
- A function key types its name and a bracket at the caret: `sin(`.
- **M+**, **M−**, **MR** and **MC** work with `memory`.
- **⌨** opens the phone's own keyboard as well, for letters and names.

## History

Each entry shows what you typed. When the line uses names or earlier
results, the numbers it used are shown faint on the right, and the
result is on the line below.

- **Tap** an entry to put its line back in Input. It stays highlighted;
  tap it again to take the highlight off and keep the line.
- **Press and hold** an entry for Insert result, Insert line, Copy
  result, Copy line and Delete line.
- ⋮ > Clear History empties it.

An error keeps your text in Input, so you can fix it. The History line
shows what went wrong, for example `ERROR:Expected ')'`.

The faint line just above Input says how long the last line took, such
as `Took 0.23 s`; a line that runs past a second counts while it runs,
`Working... 1.4 s`.

## Earlier results

```calc
6*7                   => 42
answer/2              => 21
$2+1                  => 43
```

- `answer` (or `ans`) is the last result.
- `$1` is the last result too, `$2` the one before, and so on. Only
  results count; definitions and errors are skipped. After a Delete line
  the numbers close up.
- An operator key on an empty Input starts with the last result: press
  `×` and Input reads `answer*`. To start a new line with a sign
  instead (`-5`), press **C** (or Esc on a keyboard) and the `answer`
  goes; the hint line says so while it is there.
- **=** on an empty Input repeats the last History line.

## The hint line

The faint line under Input explains what is left of the caret:

- the function you are typing, with its arguments: typing `fv(` shows
  `fv(rate, nper, pmt, [pv], [type]) - future value; ...`
- names that start with what you typed, after two letters
- tag examples inside an open `[`
- a number typed in another base, shown in the output base: `0xFF`
  shows `255` when the output is decimal

- warnings first: two signs in a row (`2+-3`), an `answer` put in front
  by an operator (C takes it away), every cell drawn in `fill`

On a screen too narrow for about 80 characters the hint takes two lines.
A longer hint ends in `...`: tap it to read all of it. Settings > Input
hints turns it off.

## The menu and Settings

⋮ (or a swipe up on the keys) opens Functions, Conversions, Variables,
Graph, Compound, Random, Settings, Export, Import, Help and Clear History. The phone's Back closes the open menu
first. On the main screen the first Back shows "Press Back again to exit"; a
second Back within two seconds leaves. In a browser this works only after a
tap on the page: Chrome lets a page hold Back only then.

Settings: precision, angle, output base, decimals, pad zeros, word size,
text size, input hints and theme. They can be typed in Input too:
`:deg`, `:prec 64` (see *Display and precision*).

Help has a Search box above its topics. Type `?` and a word in Input,
then =, to open Help at the heading that fits it best: `?det`,
`?seeds`; when none fits, the topics holding the word are listed. A
More: line under a function links to a reference page outside SoftCalc,
in a new tab.

## Export and Import

- ⋮ > Export saves History, your variables and functions, or both.
  The app saves to the Download/SoftCalc folder; a browser to its
  Downloads. Names carry the date and time, such as
  `SoftCalc-history-2026-11-19-1530.jsonl`.
- ⋮ > Import reads such files back: pick one, or press and hold the
  first to pick several. History merges with yours without twins, in
  time order; definitions are added to yours.
- SoftCalc on a computer reads and writes the same files (File >
  Export History... and Load Functions & Variables...), so you can
  move your work between devices.

## On this device

- History, your variables and functions, and the settings are kept on
  this device only, and come back next time. A private window may keep
  nothing; the status line then says so.
- SoftCalc works without the internet once it has been opened; only
  currency and coin prices need it.
- Opened from a secure address (https), the browser can install
  SoftCalc on the home screen.
