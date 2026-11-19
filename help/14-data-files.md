# Data and files

## Where SoftCalc keeps its data

- Next to the exe (`SoftCalc.exe` or `SoftCalc-standalone.exe`), in a
  folder `SoftCalc-data`, when that folder can be written. Copy the exe
  and this folder together to take everything to another computer.
- Otherwise (for example under Program Files) in
  `%APPDATA%\SoftCalc`.

File > About SoftCalc... shows the folder in use, with **Open Folder**.

| file | holds |
|---|---|
| `settings.json` | window, view, ribbon, graph and calculation settings |
| `history.jsonl` | History, the newest 1000 entries |
| `definitions.pcf` | your variables and functions |
| `rates.xml` | the last currency rates |
| `prices.json` | the last coin prices |
| `crypto.json` | addresses watched in the Cryptocurrency window, and what happened |
| `engine\` | the calculation engine, unpacked on first start |

## What is kept

- History, `answer` and `$n` come back when SoftCalc starts again.
- Definitions are saved after each change.
- Two SoftCalc windows can run at once; when they close, their History
  is merged. Settings and definitions: the last window to close wins.

## Files you write

- **File > Save History...** writes History as text, to read.
- **File > Export History...** writes History as a `.jsonl` file, the
  form SoftCalc keeps it in; **Import History...** reads one or more
  back.
- **File > Save Functions & Variables...** writes a `.pcf` file;
  **Load Functions & Variables...** reads one.
- Graph window: **File > Save as bitmap...** writes a PNG.
- Compound Interest window: **Export CSV...**.

Export and Save suggest a name with the date and time, such as
`SoftCalc-history-2026-11-19-1530.jsonl` or
`SoftCalc-definitions-2026-11-19-1530.pcf`, so files from different
days stay apart.

### Import History

- Lines merge with the History you have: a line already there is not
  added twice, and all lines go in time order (the newest 1000 stay).
- `$n` then counts the merged results, as after a restart.
- History before your last Clear History comes back too if the file
  holds it.
- A notice line says how many lines were new.

### Load Functions & Variables

- Your current variables and functions are replaced by the file's.
- Lines it cannot read are skipped, and listed afterwards with their
  line numbers.
- Below a line `[Graph]` a `.pcf` holds Graph equations, one a line.
  Save writes all of yours there; Load adds the ones you do not have
  yet and draws them. The definitions load first, so an equation may
  use a variable from the same file.

### Between computers and phones

The web app and the Android app write and read the same two files
(⋮ > Export and ⋮ > Import), so History, definitions and Graph
equations move between SoftCalc on a computer, a phone and a tablet.
On the phone, Import adds definitions to the ones there instead of
replacing them.

## Clearing

View > Clear History (ribbon: **Clear**) empties History at once; it
does not ask. `answer` keeps its value; `$n` start again.
