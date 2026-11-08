# SoftCalc - to do (2026-11-06, updated 2026-11-07)

Recorded at the owner's request after M8. Nothing here is started.

## 1. In the spec, not built yet
- [x] E9 display decimals menu: done 2026-11-06 with the ribbon
      (View > Decimal Places and the ribbon's Decimals group).

## 2. M9 packaging (done 2026-11-07, STATUS.md "M9 result")
- [x] Single portable `.exe`, data in `SoftCalc-data\` beside it
      (fallback `%APPDATA%\SoftCalc` when not writable).
- [x] Rebuild the engine exe (engine/dist is from 2026-10-27, before
      M5-M8 engine changes); PyInstaller onedir embedded in the exe.
- [x] Program icon (2026-11-06, tools/IconMaker, owner's draft 2).
- [x] About box (show the IconMCP licence there too).

## 3. Out of scope for v1
- [ ] File > Help (disabled): new help text later; the original's help
      must not be copied.

## 4. Ideas to discuss with the owner first
- [x] Graph interface: done 2026-11-06 (design/graph_plan.md G1-G3).
- [x] Dark theme (View > Theme), Text Size scaling the Font size:
      done 2026-11-06.
- [x] Add/Edit dialogs: Functions / Variables editor, done 2026-11-07.
- [x] Red error lines in History: owner keeps them (2026-11-07).

## 5. Owner requests after v1.0.0 (2026-11-07, all built; 1.1.0 not released)
- [x] About box (done 2026-11-07): a short note only (name, version, data folder). The
      third-party licence box is hidden at first; a link (e.g. "Show
      third-party licences") opens it.
- [x] Number literals in another base than the current output base (done 2026-11-07)
      (e.g. `0x1F` or `101b` while output is Decimal, `255` while
      output is Hex):
  - Input hint line: show the literal at the caret converted to the
    current output base.
  - History: the faint right-aligned numeric form (E10, as for
    variables, `ans`, `$n`) also shows such literals in the current
    base, so the line reads as one operation in that base.
  - Owner 2026-11-07: the base at calculation time (History never
    changes later); a line with such literals and no variables gets
    the right-hand form too; RPN the same.
  - Owner 2026-11-07, option B: a literal is converted only when the
    current base can show it exactly (`0.5` shows `0x0.8`); otherwise
    it stays as typed (`0.1`, or a hex literal past 32 digits in decimal).
  - Owner 2026-11-07, negatives: `-` stays the operator and only the
    number converts (`-0x1F` shows `-31`, `2*-0x1F` shows `2*-31`, no
    parentheses); an RPN negative literal converts as one token.
    Output stays sign and magnitude (`-0xFF`).
- [x] History context menu (owner 2026-11-07, done): add Clear History and
      Delete (removes the selected entry). No confirmation for either;
      Delete removes one line at a time; `$n` numbering follows the
      deletion (delete the `$2` line and the old `$3` becomes `$2`).
  - Impact found 2026-11-07:
    - The engine needs a new op to drop one value from its result
      stack by position. Rebuilding through `restore_results` would
      reparse the shown texts and lose precision (and E9 decimals
      would round them).
    - Only result lines are in the stack; deleting an error,
      assignment or definition line leaves `$n` as is.
    - Owner 2026-11-07: `answer` stays equal to `$1`; deleting the
      newest result moves `answer` back to the one before. Deleting
      the last remaining result keeps `answer` as it is, like Clear
      History (SPEC 5.3).
    - history.jsonl merges every window's entries by id at close, so
      a deleted entry needs a "deleted" mark or it comes back from the
      file or another window.
    - Older lines that typed `$n` keep their text and their faint E10
      values; recalling one now means the renumbered `$n`.
    - Existing mismatch: a text-only result (e.g. a long date text)
      counts in `$n` while running but is skipped by
      `restore_results` after a restart, so numbers can shift then.
      Fixed together (restore keeps them as text).
- [x] Release both builds (publish done 2026-11-07, GitHub release waits): the self-contained exe (58 MB) and a
      framework-dependent one (9.3 MB, needs the .NET 10 Desktop
      Runtime x64) as `SoftCalc-net10.exe`. `build/publish.ps1` builds
      and smoke-runs both.
- [x] Two's complement (owner uses it often), owner 2026-11-07, done:
  - Functions `twos(x, [bits])` (signed value to its unsigned bit
    pattern: `twos(-1, 8)` = 255) and `signed(x, [bits])` (back:
    `signed(0xFF, 8)` = -1). No global word mode.
  - Out of range wraps like hardware (`twos(300, 8)` = 44,
    `signed(0x1FF, 8)` = -1); a non-integer x is an error.
  - One argument uses the word size from settings (default 32 bits),
    taken at calculation time; two arguments use the given bits.
  - The result is shown in the current output base; the faint
    right-hand side of its History line shows it in hex (used often).
  - Right-hand text: the E10 form in the current base, with the bits
    filled in when one argument was used, then `= ` and the hex result
    of the whole line: `twos(-1)` shows `twos(-1, 32) = 0xFFFFFFFF`,
    `twos(x, 8)` with x = -1 shows `twos(-1, 8) = 0xFF`,
    `twos(-1, 8) + 1` shows `twos(-1, 8)+1 = 0x100`.
  - Word size: View > Word Size (8 / 16 / 32 / 64) and the same choice
    in the ribbon's Output group; saved in settings, default 32.
  - The bits argument takes any integer 1-1024 (`twos(x, 12)`); the
    setting offers only the four sizes.

## 6. Still open from v1.0.0
- [ ] Try the release exe on another PC (no .NET/Python) and with its
      antivirus; the exe is unsigned (SmartScreen warning expected).
- [ ] Live check of the read-only fallback (exe in Program Files, data
      to `%APPDATA%\SoftCalc`); unit-tested only.
- [ ] Graph lines vanishing on view switches: reported once, not
      reproduced, skipped by the owner (STATUS.md "Open items").
- [ ] File > Help (section 3).

## 7. Owner requests 2026-11-08 (not started; plan and discuss first)
- [x] Ribbon customisation (done 2026-11-08, design/ribbon_plan.md): add, remove, edit and rearrange the ribbon's
      buttons and groups. To plan: a Customize Ribbon window (available
      commands on the left, groups and their buttons on the right, Add /
      Remove / Up / Down / Rename / New Group, Reset to default) or a
      lighter form; which commands can go on it (every menu command, or
      a set); saved in settings.json; what Reset restores.
  - Owner 2026-11-08: (a) a Customize Ribbon window; every menu
    command can go on the ribbon.
  - Layout decided 2026-11-08 (owner showed ImageGlass Settings >
    Layout > Toolbar as the model; adapted because the ribbon has
    groups):
    - Opened from View > Customize Ribbon... and the ribbon's
      right-click menu (SoftCalc has no Settings window).
    - Left: "Available commands" by menu (File, View, Functions,
      Conversions, User Functions) with a menu filter and a search box.
      Right: the ribbon as a tree, groups with their buttons. Between
      them Add (also double-click) and Remove; Add appends to the
      selected group.
    - Toolbar on the right list: New group, Reset to default (the
      current layout). The selected row shows Up / Down / Edit /
      Remove like ImageGlass; buttons move across groups, groups move
      too.
    - Edit a group: caption and column count. Edit a button: shown
      label and display (icon, text, both).
    - OK / Cancel / Apply; Apply updates the real ribbon at once.
    - Saved in settings.json as groups of stable command ids
      (`view.base.hex`, `func.sqrt`, `conv.ft.in`); an id that no longer
      exists (a deleted user function) is skipped.
    - Function and conversion buttons: a function inserts like its menu
      item (`sqrt(`); a conversion button is one pair, picked from the
      Available tree (`Conversions > Length > ft > in`), no extra picker. The whole Conversions drop-down is an item
      too.
    - User functions are listed and can be added.
    - Icons: commands that have one keep it; the rest show text only
      (no icon picker in this round).
    - Order by Up / Down buttons in this round.
- [ ] Later: drag and drop to reorder buttons and groups in the
      Customize Ribbon tree (owner 2026-11-08).
- [x] Graph line colours (done 2026-11-09, design/graph2_plan.md G4): let the owner change a function's colour.
      To plan: where to pick it (swatch in the function list, the
      editor, the legend), a colour dialog or a fixed palette, saved per
      function name, and how it looks in light and dark themes.
  - Owner 2026-11-08: in the Graph window; click a colour swatch beside
    the function in its list and pick with the Windows colour dialog.
    The function list box may need a new layout for the swatches.
- [x] Equations in x and y (done 2026-11-09, graph2_plan G5). Question from the owner 2026-11-08: can the graph take an equation
      that is not `f(x)=...` and convert it itself, e.g. the circle
      `r^2=x^2+y^2`? Answered: yes, as an implicit plot (sample
      lhs-rhs on a grid, draw where it changes sign).
  - Owner 2026-11-08: build it, in the same batch as line colours
    (the function list gets a new layout for both). The equation is
    typed in the Graph window; x and y are the axes, other names
    (like `r`) are variables.
  - Trace: pick x or y automatically, whichever gives one point: near
    the mouse, fix x and solve for y where the curve is flatter, fix y
    and solve for x where it is steeper (from the slope of lhs-rhs),
    then mark that point on the curve.
  - Copy (owner 2026-11-08): a left click copies the solved value (y
    when x was fixed, x when y was fixed); the right-click menu has
    Copy x, Copy y and Copy (x, y), and Insert in Input the same three.
    `f(x)` lines keep their current Copy Value / Insert Value.
- [x] Trace snaps to the nearest line (done 2026-11-09, graph2_plan G6; owner 2026-11-08, same batch):
  - Measure the pixel distance from the mouse to every shown line
    (`f(x)`, polar, equations) using the points already drawn; trace
    the nearest one within 20 px. Farther away: no dot, the status
    line shows only the mouse coordinates.
  - Snap to the nearest point on that line, not only the mouse's x:
    `f(x)` uses that point's x (rounded as the status line shows it)
    and asks the engine for the exact value as now; polar uses its
    angle; an equation starts the fix-x-or-y solve from it.
  - The dot takes the line's colour and the status line names the
    function. Left click, Copy and Insert act on the snapped line.
  - The drop-down's selected function wins only a tie (lines equally
    near or on top of each other).
  - No flicker between two close lines: stay on the current line until
    another is nearer by more than 4 px.
- [ ] Conversions > Area (owner 2026-11-08, decided as proposed):
      metric `mm2 cm2 m2 km2 ha`; imperial `in2 ft2 yd2 acre mi2`; Thai
      `rai` (1600 m2), `ngan` (400 m2), `wa2` (square wa, 4 m2). All
      exact through m2 (1 acre = 4046.8564224 m2). No are (`a` is too
      easy to misread). Aliases such as `m^2`, `sqm`, `sqft`, `hectare`,
      `acres`, `ac`, `sqwa`. Menu order: metric, imperial, Thai, each
      small to large, like Length.
  - Owner wants rai-ngan-wa (used often). Decided 2026-11-08:
    - Unit `rnw` (long form `rai-ngan-wa`). As a number it is rai:
      `6800 [m2->rnw]` = 4.25, `4.25 [rnw->m2]` = 6800.
    - The result stays a number (rai); the faint right-hand side of the
      History line adds ` = 4-1-0`, like the twos hex suffix.
    - Format `4-1-0`, negatives `-4-1-0`, always decimal digits.
    - The wa part rounds by the Decimals setting; 100 wa carries to a
      ngan.
  - Input, decided 2026-11-08 (owner: keep it simple, adjust after use):
    - Typing `4-1-50` in: three plain numbers joined by `-` right before
      a tag whose source is rnw (`[rnw]` or `[rnw->...]`) read as one
      value, 4 rai + 1 ngan + 50 wa = 4.375 rai. Ngan and wa may exceed
      their range (`0-2-400` = 1.5 rai).
    - `[rnw]` alone is `[rnw->rnw]`: `4-1-50[rnw]+0-2-400[rnw]` gives
      5.875 with ` = 5-3-50` on the right. A line shows the rnw text
      when it has an rnw value or an `->rnw` tag.
    - Pitfall: without the tag right after, `4-1-50` is subtraction
      (`4-1-50 + 0-2-400 [rnw]` reads the first group as -47), so does
      `(4-1-50)[rnw]`; `6800 [rnw]` is 6800 rai; `10-4-1-50 [rnw]` is
      10 minus 4-1-50. The Input hint line shows how a group is read
      (`4-1-50 = 4.375 rai`); owner expects this to help most.
    - Simple rule kept: any line with an rnw value shows the rnw text,
      even a ratio (`4-1-50[rnw] / 0-2-0[rnw]` shows ` = 8-3-0`).
