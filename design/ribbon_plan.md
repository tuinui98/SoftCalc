# Customize Ribbon - plan (owner 2026-11-08)

Decisions are in `design/TODO.md` section 7. This file is how to build
them. Branch `ribbon-customize`; nothing here is started.

## Current state
- `UI/Ribbon.cs` builds layout B3 in code: groups Angle, Output,
  Decimals, Mode, History, Tools, each a table ToolStrip with a caption.
- Buttons call `RibbonActions` delegates; check marks come from
  `ShowSettings` (engine settings) and `ShowOnTop`.
- Ribbon-only pieces: the Decimals caption sample (`Decimals 0.00`),
  `Prec 32` and `Word 32` drop-downs showing the current value, and
  Graph (no menu item opens the Graph window).

## R1 - commands from the menus, ribbon from a layout
Goal: the same ribbon as now, but built from data.

- Command list, built by walking the menu bar after the menus are
  filled (and again when the catalog or the user functions change):
  - A leaf menu item is a command; its click calls the menu item's
    `PerformClick`, its check mark and Enabled follow the menu item.
    Every menu command is available with no extra code, including each
    function, each conversion pair (`Conversions/Length/ft/in`), date
    formats and user functions.
  - A submenu is a drop-down command; its items are copied from the menu
    each time it opens, so it always matches (Theme, Text Size, Word
    Size, Decimal Places, the whole Conversions menu).
  - Id = menu path without `&` and `...`: `View/Hexadecimal Output`,
    `Functions/Trig Functions/sin`. A user function uses its name only
    (`User Functions/g`), so changing its parameters keeps the button.
  - Icons: a small table from id to the embedded IconMCP icon (the six
    in use now); everything else shows text.
- New menu item Tools > Graph... (the Graph button gets a menu twin).
- Extra ribbon commands written in code, the only ones not from a menu:
  `Precision` drop-down (text `Prec 32`) and the Word Size drop-down
  text `Word 32`.
- Group caption may contain `{decimals}`; it shows the Decimals sample
  (`Decimals  0.00`). The default Decimals group uses it.
- `Logic/RibbonLayout.cs`: groups of `{caption, columns, items: [{id,
  label?, display?}]}`; `display` is icon, text or both. `Default` is B3
  with its short labels (`Deg`, `Hex`, `All`, ...) as label overrides.
- `settings.json` key `ribbon`: absent or null = default (older files and
  Reset). Sanitize: columns 1-12, unknown display = both.
- An id that resolves to nothing (deleted user function) is kept in the
  layout but not shown, so the button comes back if the function does.
- Ribbon rebuilds from (layout, commands); `RefreshChecks()` replaces
  `ShowSettings` / `ShowOnTop`; MainForm calls it after any setting,
  view, theme or on-top change.
- Check: screenshot before and after, identical layout; all ribbon
  buttons still work.

## R2 - Customize Ribbon window
- `UI/CustomizeRibbonDialog.cs`, opened from View > Customize Ribbon...
  and from a right-click menu on the ribbon (Customize Ribbon..., Hide
  Ribbon).
- Left, "Available commands": a TreeView in menu order. A combo filters
  by menu (All, File, View, Functions, Conversions, Tools); a search box
  shows a flat list of matches by name.
- Right, "Ribbon": a TreeView, groups (caption and column count) with
  their buttons; a missing id is shown greyed with "(missing)".
- Between: Add (also double-click on the left) appends to the selected
  group, or after the selected button; Remove.
- Above the right tree: New group, Reset to default.
- Selected right row: Up / Down / Edit / Remove buttons drawn over the
  row's right end, like ImageGlass; Up / Down move buttons across group
  boundaries and move whole groups.
- Edit group: caption, columns. Edit button: label (empty = the
  command's name), display icon / text / both (icon greyed when the
  command has none).
- OK / Cancel / Apply. Apply rebuilds the real ribbon; Cancel puts back
  the layout from when the window opened.

## R3 - saving across windows, theme, docs
- Every window writes settings.json when it closes, so a window opened
  earlier would overwrite a new layout. OK / Apply saves the `ribbon`
  key at once under the folder lock; a closing window writes `ribbon`
  only if it changed the layout itself, otherwise it keeps the file's.
  Other open windows show the new layout the next time they open.
- Dark theme: the dialog uses SystemColors like the rest; check both.
- Tests: layout JSON round trip and sanitize; default ids all resolve
  against the real menus; id from path (`&`, `...`, user function);
  missing id kept and hidden; the save-across-windows rule.
- Live check with a scratch `-DataDir`: add `sqrt`, a `[ft->in]` pair,
  a user function, a new group; move and rename; Apply, restart, Reset.
- Docs: `spec/extensions.md`, `design/STATUS.md`, TODO ticks.

## Later (TODO)
- Drag and drop in the right tree.
- Icon picker.
