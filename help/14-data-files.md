# Data and files

## Where SoftCalc keeps its data

- Next to `SoftCalc.exe`, in a folder `SoftCalc-data`, when that folder
  can be written. Copy the exe and this folder together to take
  everything to another computer.
- Otherwise (for example under Program Files) in
  `%APPDATA%\SoftCalc`.

File > About SoftCalc... shows the folder in use, with **Open Folder**.

| file | holds |
|---|---|
| `settings.json` | window, view, ribbon, graph and calculation settings |
| `history.jsonl` | History, the newest 1000 entries |
| `definitions.pcf` | your variables and functions |
| `rates.xml` | the last currency rates |
| `engine\` | the calculation engine, unpacked on first start |

## What is kept

- History, `answer` and `$n` come back when SoftCalc starts again.
- Definitions are saved after each change.
- Two SoftCalc windows can run at once; when they close, their History
  is merged. Settings and definitions: the last window to close wins.

## Files you write

- **File > Save History...** writes History as text.
- **File > Save Functions & Variables...** writes a `.pcf` file;
  **Load Functions & Variables...** adds the definitions from one.
- Graph window: **File > Save as bitmap...** writes a PNG.
- Compound Interest window: **Export CSV...**.

## Clearing

View > Clear History (ribbon: **Clear**) empties History at once; it
does not ask. `answer` keeps its value; `$n` start again.
