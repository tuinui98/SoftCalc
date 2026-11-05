# M7 plan - persistence and files

Agreed with the owner 2026-11-05. Builds on SPEC 8 and extensions.md
E12. Branch `m7-persistence`; merged, tagged and pushed when tests pass.

## Files in `%APPDATA%\SoftCalc\` (never the registry)
| file | content | written |
|---|---|---|
| `settings.json` | precision, angle, base, RPN, decimals, pad zeros, view, Show Samples, window rect + maximized, graph mode, grid, XY and Polar ranges, selected graph function, history cap | on close |
| `definitions.pcf` | user variables and functions (engine `pcf_export`) | on close and after every definition or delete |
| `history.jsonl` | one JSON object per entry: id, time, kind, input, display_input, text; plus `{"cleared_at": time}` marks | on close |

All writes: short exclusive lock, write to a temp file, atomic replace.

## Several windows
- Settings and definitions: **last writer wins** (owner's choice). With
  definitions saved on every change, a window writes its whole set each
  time, so two windows defining different names keep only the set of
  the window that wrote last.
- History: **merged** on close (E12): entry id = process start time +
  PID + sequence; re-read the file, union by id, drop entries older than
  the newest `cleared_at` mark, sort by time, keep the newest 1000
  (setting `history_cap`).

## Start-up order
1. Read settings (defaults if missing; an unreadable file is renamed to
   `settings.json.bak` and defaults are used). Apply window rect, moved
   back on screen if it no longer intersects any monitor; apply view.
2. Start the engine, send the calculation settings.
3. `pcf_import` the definitions file.
4. Load history (bad lines skipped) into the History pane; Up/Down
   recall and Enter-repeat work on the loaded entries.
5. New protocol op `restore_results {texts: [...]}`: the engine turns
   the loaded result texts back into values for `answer` and `$n`
   (owner: yes). Text-only results (long dates) are skipped. Values come
   back at the precision they were shown with.

## File menu
- Save History: UTF-8 text, each entry as on screen (input, tab +
  result).
- Save Functions & Variables: `.pcf`, UTF-16 LE without BOM, CRLF (the
  original's layout, values kept).
- Load Functions & Variables: replaces all user definitions; reads
  UTF-16 LE or UTF-8 with or without BOM; skipped lines listed in a
  dialog.

## Tests
- UI: history merge with two simulated windows (union, cap, cleared_at),
  settings round trip, corrupt settings fallback, off-screen rect fix,
  definitions saved after a definition and a delete.
- Engine: `restore_results` sets `answer` and `$n`, skips text results,
  keeps hex/date/duration/complex values.
- Load a `.pcf` in the original's layout (UTF-16 LE, empty values).
