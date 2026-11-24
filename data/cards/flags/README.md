# Flags: pictures of the ready-made country decks

The ready-made decks **Countries: all** and **Countries: <continent>**
show each country's flag (owner 2026-11-24).

| files | by | licence |
|---|---|---|
| `svg/<code>.svg` (196 flags, the 4:3 ones) | flag-icons 7.6.0 by Panayiotis Lipiridis and contributors, https://github.com/lipis/flag-icons (npm `flag-icons`) | MIT, `LICENSE.txt` |

Only the flags of the 196 countries in `data/decks/countries.txt` are
kept; a country's ISO 3166 code is the fourth column there. The SVG
files are as published, unchanged. `png/` is made from `svg/` by
`python tools/render_cards.py flags` (512 x 384, through Microsoft Edge)
and embedded in the exe as `cards/flags/<code>.png`.
