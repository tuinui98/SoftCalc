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
- [x] File > Help (disabled): new help text later; the original's help
      must not be copied. Built 2026-11-10 (section 8).

## 4. Ideas to discuss with the owner first
- [x] Graph interface: done 2026-11-06 (design/graph_plan.md G1-G3).
- [x] Dark theme (View > Theme), Text Size scaling the Font size:
      done 2026-11-06.
- [x] Add/Edit dialogs: Functions / Variables editor, done 2026-11-07.
- [x] Red error lines in History: owner keeps them (2026-11-07).

## 5. Owner requests after v1.0.0 (2026-11-07, all built; released in 1.2.0, 1.1.0 skipped)
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
- [x] Try the release exe on another PC (no .NET/Python) and with its
      antivirus; the exe is unsigned (SmartScreen warning expected).
      Owner 2026-11-11, 1.3.0: SoftCalc-net10.exe warned, then asked for
      .NET 10 and opened its download page; after installing it runs
      normally. Online it fetched rates.xml; offline it shows
      `ECB 2026-11-10, offline` (the embedded rates).
- [x] Live check of the read-only fallback (exe in Program Files, data
      to `%APPDATA%\SoftCalc`): the owner checked 1.2.0, 2026-11-09.
- [x] Graph lines vanishing on view switches: dropped 2026-11-09, the
      owner thinks it was an accidental double-click.
- [x] File > Help (section 3), built 2026-11-10.

## 7. Owner requests 2026-11-08 (all built 2026-11-09, released in 1.2.0)
Owner checked 2026-11-09: the colour dialog works, Shift/Ctrl + wheel
ends 1:1, the drag insert line is visible enough on dark.
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
- [x] Drag and drop (done 2026-11-09; also from Available commands, and back to remove) to reorder buttons and groups in the
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
- [x] Conversions > Area (done 2026-11-09; owner 2026-11-08, decided as proposed):
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

## 8. Owner requests 2026-11-10 (all built by 2026-11-11, released in 1.3.0)
- [x] Help (decided 2026-11-10, owner took all proposals): in-app Help
      window, topic list + search on the left, text on the right; text
      in `help/*.md` embedded in the exe; English first, Thai later;
      short reference with examples; topics 1-16 as proposed (getting
      started ... error messages). Extras: click an example to put it
      in Input, F1 opens the topic for the current window, a test runs
      every `line => result` example through the engine. Pictures of
      buttons and layout wanted: screenshots made by a harness
      (DrawToBitmap, light and dark) so they can be regenerated.
      First step: draft topics 1-2 for the owner to check the tone.
  - Text and pictures done 2026-11-10 (branch help-draft, owner
    checked); image links name the light file, dark theme swaps it.
    Help window built 2026-11-10 (branch help-window): File > Help
    and F1 (topic for the active window), list + search, examples
    click into Input, pictures by theme; stock WebBrowser control.
- [x] Window caption followed by the version: `SoftCalc [v1.2.0]`
      (owner 2026-11-10, done; faint/italic would need a custom-drawn title bar).
- [x] Graph Add Function / Equation: a gallery of example functions and
      equations with a small graph each; pick one to edit before adding.
      Built 2026-11-10: Add arrow > Examples..., 16 functions and 16
      equations (Logic/GraphExamples.cs), thumbnails drawn by the engine,
      Use opens the Functions editor or Add Equation filled in.
- [x] Graph Add as buttons (owner 2026-11-10): the Add split button's
      arrow is hard to hit; make three toolbar buttons Function...,
      Equation..., Examples... (shorten labels if the narrow Graph panel
      overflows them). Built 2026-11-10. Owner then: only the Graph
      window gets the buttons; the Advanced View sidebar keeps the Add
      split button (done).
- [x] Graph Examples with constants (owner 2026-11-10): each example
      gets a form with constants and a note on what each changes, e.g.
      `(x-a)^2+(y-b)^2=r^2`, note `centre (a, b), r = radius`; circle
      uses `(x-a)` (owner chose it so the centre reads (a, b)). Use...
      puts the form with the defaults filled in, not variables:
      `(x-0)^2+(y-0)^2=2^2`. Thumbnails use the defaults. Proposed
      forms, notes and defaults: `design/graph_examples_constants.md`
      (all 32; `x^y=y^x` has no constant); not to use names e, i, j, pi.
      Every form drawn through the engine before it goes in. Hint
      after Use (owner 2026-11-10): grey label with the form and the
      note, below the input; Functions editor puts it in the empty
      space under the lowest input, Add Equation grows 3-4 lines
      taller for it; shown only when opened from Examples. Built
      2026-11-10; the gallery shows the form and note under the tiles.
      Owner found sin/cos/tan thumbnails flat (calculator in Deg): the
      pictures are now drawn in radians (`plot_equation` takes an
      optional `angle`), the gallery and the hint say so outside Rad.
- [x] Owner test (2026-11-10): open examples from the gallery and edit
      them in the graph (change the numbers, check the notes hold).
      Owner 2026-11-10: graph OK.
- [x] Customize Ribbon: own icons for buttons (built 2026-11-11 as planned below). Owner asked: send the
      user to iconmcp.com to pick one and the program fetches it. To
      check: how the site offers an icon (copy SVG / download / a
      fetchable URL); `SvgIcon` draws only a small SVG subset (absolute
      M L H V C Z, line, circle, ellipse, rect), so other icons may
      need more of SVG; MIT licence note stays in About.
  - Checked 2026-11-11: iconmcp.com has no public SVG URL or API (JS
    site; `?icon=<name>` is a page). Its icon dialog has Copy SVG and
    Download SVG; outline icons match ours, Solid uses `fill-rule`,
    `stroke="none"`, and the site's SVGs use path `A` and `Q`.
  - Owner 2026-11-11: (1) no program fetch: the user copies the SVG
    on the site and pastes it, or picks a downloaded .svg file;
    (2) the SVG text is kept in settings.json on the ribbon item
    (`icon_svg`); (3) text-only commands (Deg, Hex, ...) can take an
    icon too, then Show offers Icon / Text / Both.
  - Plan: Edit Button gets an Icon row (20 px preview, Choose...).
    Choose Icon: Built-in, Open iconmcp.com, Paste (also offered when
    the window is reactivated with SVG on the clipboard), From
    file..., None; light and dark preview. `SvgIcon` gains path A, Q,
    S, T and relative commands, polyline/polygon, `fill-rule`,
    `stroke="none"`, `<g>` inheritance; anything else is refused with
    "can't draw: <element>". XML read with DTD off and no resolver,
    64 KB limit. Tests with outline/solid trash and an arc; Help
    12-ribbon and its picture.
  - Built 2026-11-11: `UI/IconPicker.cs` (Choose Icon), Edit Button
    Icon row, `RibbonItem.IconSvg`, `SvgIcon` rewritten (the 9 shipped
    icons render pixel-identical), `SvgIconTests`; a hand-edited
    icon that can't be drawn falls back to the command's icon. Help
    12-ribbon "Own icons" with two pictures. Owner checked 2026-11-11: OK.
- [x] Currency rates before the first fetch (owner 2026-11-10, built
      2026-11-10 as proposed): today a PC that was never online gets `Currency rates
      unavailable`. Proposed: `build/publish.ps1` fetches the ECB file
      at build time and embeds it (about 2 KB); with no `rates.xml` the
      app uses it, the note shows its date and `, offline`; a normal
      fetch replaces it once online. Build goes on without it when the
      ECB cannot be reached; dev builds embed nothing.

## 9. Owner requests 2026-11-11 (all built 2026-11-11, released in 1.3.0)
- [x] Customize Ribbon: Insert Space, a gap between buttons about half
      an icon wide (10 px at 100%).
- [x] Customize Ribbon: Insert Separator, a line between buttons that
      splits a group into parts.
  - To decide: a line inside a group only, or also something between
    groups; in a group of several rows a space or line fills one grid
    cell like a button; space width fixed or editable.
  - Sketch: special ids `Ribbon/Space` and `Ribbon/Separator` (like
    `Ribbon/Precision`), listed first in Available commands under
    "Ribbon", usable many times; added, dragged and removed like a
    button; no Edit; shown in the tree as `(space)` / `(separator)`.
  - Owner 2026-11-11: (1) a line inside a group; (2) one grid cell in
    a group of several rows is fine; (3) the space width is fixed.
  - Built 2026-11-11 as sketched (`RibbonGap` in Ribbon.cs, both 10 px
    at 100%; the separator is a ControlDark line as tall as the row).
    Owner checked 2026-11-11: OK.
- [x] Sidebar graph range (owner 2026-11-11: "looks auto-fitted all the
      time"). Cause: G3 (2026-11-07) fits the sidebar's Y to the
      functions; equations take no part. With the cubic example over the
      Graph window's wide 1:1 X range (about -28..28) Y runs to about
      +-22000, so the other lines go flat and the circle is a dot.
      Proposed: the sidebar draws the Graph window's range (X and Y as
      set), stretched to the panel; polar likewise uses R Max.
  - Owner 2026-11-11: the window's range at the window's scale; all of
    it in the panel, the other axis grown around the centre (shows at
    least what the window shows).
  - Built 2026-11-11: `PlotMath.SameShape`; the Graph window keeps its
    drawing area's width / height in `GraphSettings.PlotAspect`, saved
    as `graph.plot_aspect` (until the window was opened, the panel's
    own shape: the range as set). Polar draws a square in both, so
    the set R Max. The sidebar grid uses the set steps, coarser when
    lines would be under 4 px apart. Owner checked 2026-11-11: OK.

## 10. Making the GitHub repo public (owner asked 2026-11-11; to decide)
- [x] Licence for SoftCalc's own code: MIT (owner 2026-11-11), `LICENSE`.
- [x] Root README.md, English only (owner 2026-11-11).
- [x] Commit author e-mail shows in the public history. Done
      2026-11-11: all 179 commits and the v1.0.0 tag rewritten
      (git-filter-repo --mailmap), main and 13 tags force-pushed; trees
      unchanged; releases kept; backup mirror of the old history in
      D:\Projects\softcalc-history-backup-20261111.git. Owner
      2026-11-11: rewrite the history to the GitHub noreply address
      (tuinui98@users.noreply.github.com, as GitHub shows it); force-push, all 13
      tags re-made. Confirm the exact steps with the owner first.
- [x] Issues / pull requests from others: owner 2026-11-11, issues
      open, pull requests not accepted (README says why: clean room).
- [x] "private" wording in CLAUDE.md and STATUS.md.
- [x] After the push: owner turns on "Block command line pushes that
      expose my email" (GitHub Settings > Emails), reads the README and
      switches the repo to public. Owner 2026-11-11: all done.
- [x] Owner switched the repo to public (2026-11-11); backup of the
      old history deleted. Releases v1.0.0 and v1.2.0 kept; v1.3.0
      notes carry the SHA-256 of both exes, and publish.ps1 now prints
      them for every release.

## 11. Owner ideas 2026-11-11 (noted only; plan and discuss first)
- [x] Numeric Calc view: better size and layout, and scientific
      calculator buttons.
  - To discuss: which buttons (sin/cos/tan, inverse, log/ln, x^y, root,
    n!, pi, e, memory, parentheses, Deg/Rad), how they insert into Input
    (text like the menus do, so History stays expression based),
    resizable keypad, font scaling with Text Size.
  - Owner 2026-11-16: redesign the keypad with two modes. Basic: bigger
    keys filling the space, a key to show Scientific mode, AC, C and %.
    Scientific: with a 2nd key, modelled on the TI-89 the owner uses
    (owner works in History view otherwise). Design proposed, waiting
    for the owner's choice.
  - Built 2026-11-16 (not released; help after the owner's test):
    drawn keypad filling its row, a drag bar above it, Text Size. Basic
    5x5: AC, C (the last number), % (appends %, the engine's x/100),
    memory, 0 wide, = tall. Scientific adds five TI-89-like columns with
    2nd (next key only), STO then x/y, entry, dec/deg keys that cycle (a
    line in Input is then worked out in the new base), func and units
    open the Functions and Conversions menus. Owner 2026-11-16: % key back
    to appending % (the A*(1+B%) rewrite was tried and dropped); keypad
    no lower than 260 px; the base key keeps Input; 2nd in yellow.
    Help 2026-11-16: Views topic (Basic and Scientific keys, two
    pictures), RPN topic (= key). The engine has no nCr, cbrt or log (it has
    log10): rand, ^(1/3) and log10 are used.
- [x] Random tools (a section or window): built 2026-11-12/13, help
      updated; released in 1.4.0 (2026-11-15).
  - Owner 2026-11-12: a window of its own with tabs: Simple random,
    Password generator, and tabs the user makes for own lists (e.g.
    "what to eat": a list to pick from, options such as fried or
    omelette egg, and a list of shops to order from).
  - Owner 2026-11-12: Simple random and Password first, custom tabs
    later. Plan: design/random_plan.md.
  - Owner 2026-11-12: rand() whole/decimal by the arguments (`1.0`
    gives decimals), favourite word mixed in (b), Numbers tab as
    planned, clipboard wiped after 30 s, Numbers results not in
    History; plus round(), ceil(), floor().
  - Built 2026-11-12 (R0-R2): engine rand/rands/round/ceil/floor
    (tests/test_random.py), Tools > Random... with Numbers and Password
    tabs (Logic/RandomTools.cs, UI/RandomWindow.cs, RandomToolsTests),
    help 17-random and 03-functions Rounding. Owner to try it. Custom
    list tabs still to plan.
  - Owner 2026-11-12: coin words (Head/Tail default) and Insert Command
    done; Sort is now a button; Password symbol groups as tick boxes
    (tick appends the group after taking its symbols out, untick takes
    them out; Quotes and Slashes start unticked). Favourite word became
    a Signature written into each password (end / start / anywhere;
    strength counts the random part). Help waits until the
    Random work is finished (owner): 17-random still describes the old
    Sort tick and lacks the symbol groups.
  - Owner 2026-11-12: Advanced random tab with a shape list (13 shapes,
    drawn, with a histogram of the results), whole or decimal ranges,
    no "no repeats"; first tab renamed Simple random. Engine functions
    randbeta/randtri/randexp/randpeaks done (R3 in random_plan.md);
    the tab done too; owner tried it (tabs renamed Simple / Advanced /
    Password). Seed done (Source row, the window's own engine,
    protocol `draws`); New seed and the Entropy tab done (R5: sources
    side by side, Use as Seed, Mix into System). At release: README
    to list the Random window and that the beacons go on the network
    only when asked.
  - Owner 2026-11-13: help updated (17-random rewritten: seeds with
    checked examples, shaped functions, Source row, Simple, Advanced,
    Password with signature, Entropy; 03-functions Random row); all
    help pictures regenerated, new random-advanced and random-entropy.
- [x] Random window: card decks and draw tabs (owner 2026-11-12, design
      2026-11-14; design/random_plan.md R6-R7). Built 2026-11-14: the
      "..." tab (New draw, New deck, own and ready-made decks in English
      and Thai, CSV, pictures), draw tabs (steps pooling decks and
      extras, unticked cards, weights, No repeat pile with Shuffle,
      List / Slot machine / Dartboard with effects, Results, History,
      Stats, Clear draw, System or Seed), cards.json; help topic updated.
      Released in 1.4.0 (2026-11-15).
  - [x] Ready-made districts (928, English and Thai): built 2026-11-15
        from the owner's source, the Office of the Royal Society's list
        (data.go.th orst_0102, DGA Open Government License, credited in
        THIRD-PARTY-NOTICES); tools/make_districts.py corrects its slips
        (4 Chaloem Phra Kiat missing, 2 old names, Nong Chok/Nong Khaem
        swapped). Decks: Districts: all, and one per province. Not
        released yet.
  - [x] Conditions (owner 2026-11-14, built 2026-11-14): extra cards
        with "|weight" and ">>condition", Then row for deck cards, Save
        as deck, a card drawing from another deck (random_plan.md R7).
  - [x] Help and pictures for the 2026-11-14 fix rounds (done
        2026-11-15 when the owner called the topic done): Theme and
        Effects in Edit tab, List effect, Results/History/Stats buttons,
        Clear history, right-click menus, Dartboard spin, conditions
        (>>, |, Then row, Save as deck, Stats rows "1 → deck"), Add box
        weights, no spoilers, dartboard timing, slot rows, Spin wheel,
        drag bar, dartboard rings, card colours, [1,100] runs, Shuffle
        mixes the board.
- [x] New-user defaults (owner 2026-11-15, released in 1.4.0): the
      owner's ribbon (Angle Deg/Rad, Decimals, Tools with Random, View,
      History, Window; its icons built in), degrees, Advanced view at
      693x728, Samples with a rand line, graph y=sin(x) and y=x/360 over
      -360..360, a Spin draw tab (zodiac wheel). Existing settings keep
      theirs; an uncustomized ribbon follows the new default.
- [x] Crypto: balances and address checks; topic done 2026-11-16 (owner).
  - Possible: BIP39 mnemonic words (12/24) from a secure random source
    with the checksum word; check a typed mnemonic's checksum; maybe
    dice/coin entropy entered by hand.
  - Saving: encrypt the seed with a password (e.g. AES-GCM with a
    slow key derivation such as scrypt/Argon2) for import/export, and
    possibly an encrypted file to keep in e-mail.
  - To discuss before any build: risks of a seed ever touching a
    general-purpose PC, the clipboard and e-mail; the BIP39 word list
    licence; no network use; whether this belongs in a calculator.
  - Owner 2026-11-12 asked whether a real wallet can be made. Answered:
    making one (mnemonic, seed, keys, receive addresses) is possible
    offline with tested libraries (NBitcoin for Bitcoin, Nethereum for
    Ethereum, both MIT) checked against the BIP test vectors; sending
    needs transactions signed and broadcast (a node or service, or a
    PSBT handed to another wallet). Suggested: SoftCalc makes and checks
    the mnemonic and shows addresses; spending stays in an established
    wallet that imports the same words.
  - Owner 2026-11-15: scope narrowed to **checking balances and checking
    that an address is valid** (no keys, no seeds, no signing in
    SoftCalc). Owner 2026-11-15: BTC, ETH, USDT on ERC-20 and TRC-20;
    CoinGecko prices; public APIs with endpoints that can point at an
    own node. Plan: design/crypto_plan.md (C0 offline address checks,
    C1 balances on demand, C2 monitor); owner's answers in it.
  - [x] C0 built 2026-11-15: Tools > Crypto... (Address, Fees), coin
        units in lines. Not released.
  - [x] C1 built 2026-11-15: Balance tab (BTC, xpub/zpub, ETH +
        USDT-ERC20, TRX + USDT-TRC20), CoinGecko prices also in lines
        (`[btc->thb]`), default Ethereum RPC ethereum-rpc.publicnode.com.
        Not released.
  - [x] C1 tested by the owner 2026-11-16 (BTC, ETH, Tron, x/y/zpub and
        Taproot xpub through blockstream.info). Fixed: public services
        asked 1 s apart with waits when busy (mempool.space blocked the
        IP after ~20 quick calls), one-case ETH warned in Balance too,
        Services dialog resizable.
  - [x] C2a built 2026-11-16: Watch tab, tray notifications, poisoning
        and dust warnings (design/crypto_plan.md). Not released.
  - Later, with the hardware-wallet project (not a TODO here): C2b watch
        xpubs / descriptors through an own node. Owner 2026-11-16:
        today's addresses are exchange deposit addresses (Bitkub,
        Binance; money only passes through), so C2a watches single
        addresses; xpub/descriptor watching (C2b) waits for the
        hardware-wallet project, which needs an own node.
  - [x] Help 2026-11-16: help/18-cryptocurrency.md (coin units, prices,
        the Address / Balance / Watch / Fees tabs, own nodes, privacy),
        three pictures; menu renamed Tools > Cryptocurrency... Owner
        2026-11-16: the crypto topic is done; full service (C2b, own node,
        Tron through an own node, Lightning invoices) comes with the
        hardware-wallet project.
    The hardware-wallet discussion went to
    D:\Projects\esp32-hardware-wallet\NOTES.md (a separate project).

## Next release (owner 2026-11-19, after 1.6.0)
- [x] Desktop Graph > File > Save as bitmap...: suggest the name with the
      local time, as the web/APK PNG and Export do:
      `SoftCalc-graph-YYYY-MM-DD-HHmm.png` (now `graph.png`,
      UI/GraphWindow.cs SaveBitmap; HistoryStore.ExportName has the stamp).
- [x] Desktop Numeric view: a smallest window size, so the keypad is
      never too small to draw all its keys, and the height always shows
      at least one History entry.
- [x] Desktop Numeric view hides the ribbon now; owner: like the other
      views, hide it only when View > Show Ribbon is off.
- [x] Owner 2026-11-20, built: every view's smallest window: the menu bar
      and the widest ribbon group across; the menu, the whole ribbon when
      shown, Samples and Input and one History entry down (Numeric: the
      keypad at its lowest and its labels' width). Advanced: the sidebar
      keeps Graph's toolbar, list and a plot as tall as the Variables box
      at its smallest, and Variables one row; narrowing it narrows History
      first, then both together once they are as wide (MainForm.Sizes.cs).

## Next release (owner 2026-11-20, after 1.7.0)
- [x] Export / Import also carry the user's own card decks: a third file
      `SoftCalc-decks-<local time>.json` beside the History and .pcf, on
      the desktop (File > Export / Import) and the web (⋮ > Export /
      Import); Import adds, never replaces (as the .pcf). Proposed: decks
      only, without draw tabs and pictures; unticked cards, No repeat
      piles and tab options stay per device. To confirm with the owner
      before building.
  - Owner 2026-11-23: one zip file `SoftCalc-decks-<local time>.zip`
    (`decks.json` + `pictures/<hash>.png`, a shared picture stored once)
    instead of the .json; pictures go along. Desktop: File > Export Decks /
    Import Decks and also the Random window's "..." tab; web: ⋮ Export /
    Import and the Cards tab. Only own decks (ready-made ones are in both
    apps); a card's `off` goes along. Import: a deck with the same name
    and the same cards is skipped; same name, other cards: added as
    `name (2)`; past the deck limit skipped and counted. The web keeps
    the pictures it cannot show yet, so an export from the web still
    carries them. Zip read with limits (size, file count, no paths out of
    the folder). Draw tabs not exported.
  - Built 2026-11-23 (DeckTransfer.cs; web decks.ts and its own small zip
    reader/writer zip.ts, no library): File > Export Decks / Import Decks,
    the "..." tab's Import decks / Export all decks; web ⋮ Export > My card
    decks / All, ⋮ Import takes the zip, Cards tab Import decks / Export
    all decks. A small PNG comes in as it was, so its name stays the same
    both ways. To check by the owner on the desktop and the phone.
- [x] Desktop ribbon rows (owner 2026-11-22): a group can be locked to
      start row 2 (Customize Ribbon). Every group after it follows it on
      row 2. The groups before it fill row 1; when the window is narrow,
      what does not fit runs on to row 2, and the locked group carries on
      straight after it on that row, without starting a new one. The first
      group cannot be locked. Maybe also rows 3 and 4 (rare): rows are
      put in order by themselves (a lock to row 3 with none to row 2
      acts as row 2; locks never go backwards). A row that is too narrow
      runs on to the next as now (owner 2026-11-22). Built 2026-11-23,
      owner OK: Edit Group > "Start a new row" tick box; the Customize
      tree shows "row n" (RibbonLayout.Rows).

- [x] Input draws from a deck (owner 2026-11-24): one engine function
      `card("Deck name", [count], [no repeat])`. The app sends the engine
      the decks (my own and the ready-made ones in the language chosen;
      card names and weights) at start and after each change; the engine
      stays without file I/O. Draws by weight with the engine's generator,
      so `rands(seed)` repeats them. Leaves out only the cards saved to
      start unticked (the jokers), not the Cards tab's ticks or piles;
      no repeat holds within the one call. A card named like a number
      gives a number (usable in sums), else text in History; several
      cards: a list. A quoted name is new to the parser (only here).
      No card values for the playing cards (not needed). Desktop, web
      and APK alike.
  - Built 2026-11-24 (engine cards.py, protocol `decks`): several number
    cards come out as `{3, 5, 1}`, the form a matrix row will take
    (owner: numbers drawn should go on into matrices; the engine has no
    matrices yet, so it is text for now); curly quotes from a phone work.
    Desktop sends cards.json's decks before a line when the file changed;
    the web when the decks change. Owner 2026-11-24: the deck name typed
    picks the language (ready-made decks go in English with Thai aliases);
    Insert Command on the draw tab, the ... tab and the web Cards tab.
    Owner OK; help updated (03, 14, 17, 12-ribbon).

## Next release (owner 2026-11-24, after 1.8.0)
- [x] (1.9.0: engine 1.9.0, test_version.py, publish.ps1 BLOCK 0) The web app and APK 1.8.0 say "Engine 1.7.0 ready in ...": the
      engine's own version (`engine/softcalc_engine/__init__.py`
      `__version__` and `engine/pyproject.toml`) was not raised with
      `<Version>` in SoftCalc.csproj. Fix next release: raise both to the
      release's version, and make the release steps (publish.ps1, a test)
      fail when the engine's version and SoftCalc's differ.
- [x] (1.9.0: desktop, Android and web icons; IconMaker --web) The program icon's number: 1.23 becomes 1.98 (owner 2026-11-24);
      tools/IconMaker draws ui/SoftCalc/softcalc.ico, and the web/APK icons
      come from it too (check which).

## Next release (owner 2026-11-24, after 1.9.0)
- [x] (released in 1.10.0; in the hint line under Input, owner) Desktop: the time each line took, as the web shows it (status
      "Took 0.23 s" after every line, "Working... 1.4 s" counting after a
      second). The desktop has no status line yet: where it goes is to
      settle (the hint line under Input, or the window's title).

## Matrices (owner 2026-11-24; plan in design/matrix_plan.md)
- [x] (released in 1.9.0) Matrix functions in the engine. Owner 2026-11-24: written with
      `[ ]` (`[1, 2, 3]`, `[[1, 2], [3, 4]]`), but first check for
      certain that it cannot clash with conversions and formats (plan
      step X0); one-line display, a table later; a list fits as a row or
      a column; at most 10000 cells, inv/det/solve at most 50x50;
      variables, $n and the .pcf hold matrices; no RPN.
  - [x] Round 1 built and merged 2026-11-24 (X0-X3): lists and matrices,
        25 functions, Functions > Matrix, hints; checked against SymPy
        (1388 cases), which found and fixed det 0 with decimal cells.
        Help 16-errors has the list/matrix errors and the accuracy notes.
  - [x] Help topic 19 "Lists and matrices" (2026-11-24, owner: matrix
        topic done), shared by desktop and web; Random and Functions point
        to it; pictures matrix.*.png, menu-functions and menu-conversions
        (Matrix group, Cryptocurrency) remade.
  - [x] Round 2 A-C (2026-11-24): unit tags on a list; median, stdev,
        stdevp, variance, variancep, norm, rank, eig; the "digits may be
        off" note.
  - [x] Round 2 D (2026-11-24): a matrix result as a table in History,
        desktop and web; Copy as Table.
  - [x] fill, reshape, seq stepping like a deck, reverse, cumsum, count
        (owner B4, 2026-11-24).
  - [x] Timed 2026-11-24 (web/time_matrix.mjs, owner's phone 3 s and
        tablet 4 s for inv 50x50); then one LU on plain lists for det, inv,
        solve and A^-n, eig at the working digits: inv 50x50 at 64 digits
        1.26 s -> 0.23 s desktop, 1.48 s -> 0.35 s web on the PC. The web
        status line shows "Working... 1.4 s" after a second, then "Took 2.1 s".

  - [x] (built 2026-11-26: catalog "subgroups", ribbon ids kept) Functions > Matrix in groups (owner 2026-11-25): the 38 sit in
        one flat submenu today (server.py CATALOG "Matrix"). On top the
        ten used most: det inv solve transpose eig identity fill seq sum
        mean; a separator; then five submenus:
        Build (identity zeros ones diag fill seq reshape randm),
        Size and pick (rows cols len at row col),
        Linear algebra (transpose det inv trace rank solve eig dot cross norm),
        Statistics (sum prod min max count mean median stdev stdevp
        variance variancep), Arrange (sort reverse cumsum).
        The ten stay in their groups too, so each group is whole.
        CATALOG must nest (items and submenus in one group); desktop menu
        and web; ribbon buttons made from the old Matrix menu keep working;
        menu-functions help picture after the owner finishes the topic.

## Statistics (owner 2026-11-26)
- [x] (built 2026-11-26) Functions > Statistics, a group of its own after
      Matrix, with the 11 that Matrix > Statistics has (sum prod min max
      count mean median stdev stdevp variance variancep; both places).
- [ ] Functions still to add, then submenus (Descriptive, Counting,
      Distributions, Two variables). Names proposed, check each against
      units and names first (range, mode, ...); mpmath has gamma,
      binomial, erf/erfinv, ncdf and the incomplete gamma/beta, so all at
      the working precision.
      Round 1: comb(n, k) perm(n, k), mode, percentile, quartile, normcdf
      norminv, randnorm(mean, sd) (the shaped draws have no normal yet).
      Round 2: normpdf, binompdf binomcdf, poisspdf poisscdf, covar covarp,
      correl, slope intercept rsq forecast; gamma, erf erfc; range iqr
      geomean harmean zscore; randbinom randpois.
      Later: tcdf chi2cdf, skew kurt, weighted mean, sumsq; statistics per
      column of a matrix (colmean or mean(A, 1)) and cov(A). Out of scope:
      hypothesis tests.

## Conversions (owner 2026-11-24)
- [x] One Conversions > Cryptocurrency submenu (Bitcoin, Ethereum, Tron,
      Tether); a coin unit also goes to the other coins and the common
      currencies; old ribbon ids still work. Merged 2026-11-24.

## Later (owner 2026-11-23, a separate piece of work)
- [x] (released in 1.10.0; help 17 and 14 updated 2026-11-24) Deck covers, desktop built 2026-11-24 (branch deck-covers; mock-up
      https://claude.ai/artifact/EaFVN9n5friaqiXuDvgLnB): the ... tab shows
      decks as covers or a list; Cover... and the editor's Cover row set a
      deck's own picture, else the first card's picture, its colours or
      its first letter (DeckCovers); Edit deck shows cards as pictures,
      View large / double-click / Space opens CardViewer (arrows, Esc,
      Use as cover); pictures are kept at 512 px (was 128); the cover goes
      in the decks zip. Left: the web (it drops a deck's cover on its own
      export until then), Help 17 and its pictures, showing a drawn
      card's picture (owner to decide).
      Ready-made Playing cards has pictures (owner 2026-11-24): heratexx's
      four-colour deck (CC0) and jokers drawn to match, data/cards/playing/,
      tools/render_cards.py, embedded as ready:playing/*.png; Copy to my
      decks stores them as the user's own. Web: to do with its covers.
      Countries have their flags (owner 2026-11-24): flag-icons 7.6.0 (MIT),
      the 196 used in data/cards/flags/, ISO code as countries.txt's 4th
      column, 512 x 384, embedded as ready:flags/<code>.png.
      Web built 2026-11-24 (branch web-deck-covers): the Cards tab's deck
      button opens the decks as covers; a deck's cards show as pictures
      (on white), a tap shows one full screen (swipe, Use as cover, Leave
      out); ready-made pictures come from the build (covers.ts); Copy to my
      decks keeps them; the cover goes through the decks zip. A letter
      cover takes the part after ": " (Districts: Bangkok is B), both apps.
- [x] (first note) Deck covers: a new deck chooser that shows each deck's cover; the
      default cover is one of the deck's card pictures. Maybe pictures for
      the ready-made decks too, at least the playing cards. Free-licence
      candidates found 2026-11-23 (licences to check again before use):
      GreyWyvern set (CC0, SVG, opengameart.org/content/greywyvern-playing-card-set),
      Kenney Playing Cards Pack (CC0, PNG pixel art, kenney.nl/assets/playing-cards-pack),
      Classic 4-color poker deck (CC0, SVG), Jumbo index cards (CC0).
      Avoid Byron Knoll's set (public-domain claim not backed up).

## Hints (owner 2026-11-24)
- [x] (built 2026-11-26: hints.py RANDS_EMPTY_HINT) rands hint: today one hint for `rands(` (hints.py "rands"), and
      "rands(): unpredictable again" sits at its end, cut off on a narrow
      window. Give `rands()` (empty brackets) a hint of its own that says
      it takes a new seed from the system, e.g. "rands() - a new seed
      from the system: the draws after it are unpredictable again"; keep
      `rands(` for the seeded form. Desktop and web.

## Help from the Input box (owner 2026-11-24)
- [ ] Type help + a keyword in Input to open Help at that heading, desktop
      and web. Today Help opens a whole topic only (20 files); headings
      `#`-`####` render with no id, and search only filters topics and
      scrolls to the first mark. Needs: an id per heading (## and ###),
      a keyword index (heading words, function names such as det, fill,
      card), the command's form (`?det` / `help det`; neither is taken in
      the engine) and what a keyword with several matches does. Owner
      2026-11-24: both desktop and web; the command's form is settled
      when the work starts.
  - [ ] Links out for the harder functions (owner 2026-11-24): links,
        not fetched text (offline, the typed word stays private,
        licences). A "More:" line under a function in Help, from one
        function -> URL table: NIST DLMF for special functions (stable
        section URLs, e.g. dlmf.nist.gov/5.2 for gamma), the NIST/SEMATECH
        e-Handbook for statistics, OEIS for sequences, MathWorld as the
        fallback, mpmath docs for what the engine computes (branch cuts).
        Check each site's terms again before use.

## QR codes (owner 2026-11-26; details when the work starts)
- [ ] A QR code from the Input text or a History line (the answer or
      the whole line), desktop and web. Offline; a library rather than
      our own encoder (candidates: QRCoder MIT for C#, qrcode-generator
      MIT for the web; check versions and licences, notices, exact npm
      pin). To settle then: shown text or full digits, a matrix's form,
      too long (version 40: 7089 digits, 2953 bytes, Thai ~980 letters),
      error level, dark on white with a 4-module quiet zone in both
      themes, Copy image / Save PNG / Share. Tests decode it back
      (ZXing.Net in the test project, jsQR in vitest). Scanning a QR
      into Input is separate work (camera permission).

## Chrome extension (owner 2026-11-26, no hurry)
- [ ] SoftCalc's engine in a simple Chrome extension: Input and a short
      History (2-3 lines, or as the user sizes it), like Cloudy Calculator
      (the owner used it: a box under the toolbar icon). Offline works:
      the web build already bundles Pyodide 314.0.7 from npm (~20 MB,
      216 files, engine in a Worker); only ECB rates and CoinGecko prices
      need the net (cache otherwise). Shape to choose: the toolbar popup
      (that box; closes when focus leaves, at most 800 x 600, not sized by
      the user; keep the engine warm in an offscreen document or Pyodide
      reloads at every open), a pop-out window (floating, resizable), or
      the side panel (stays open, width only). Needs: MV3 manifest, CSP
      'wasm-unsafe-eval', host permissions for ECB/CoinGecko, no remote
      code (Pyodide must not loadPackage from a CDN), storage in the
      extension (chrome.storage or its IndexedDB), no sw.js, a compact
      layout, `npm run extension`. Maybe a context menu "calculate the
      selected text". Check Chrome's current docs before building.
      Owner 2026-11-26: the toolbar popup with the engine kept warm in an
      offscreen document, plus a Pop out button (a small resizable
      window). Input-box only: keep what typing does (arithmetic, every
      function, variables and f(x) =, ans and $n, units, currency and
      coins, dates, finance functions, bases, rand/randm/shapes, matrices
      as one line, hints under Input); leave out what needs its own UI
      (Graph, Compound window, Random window and Cards tab, deck editor
      and pictures, Names sheet, the matrix table, file import/export).
      A small settings menu (precision, angle, number format; owner OK).
      Numpad (owner 2026-11-26): hidden at first, a button shows it and
      the choice is kept; digits, . ( ) + - * / = ⌫ C only, no scientific
      keys (Keypad.svelte with a "numpad" layout). card() stays, with the
      ready-made decks as text only (no pictures, no own decks).

## Settings typed in Input (owner 2026-11-26)
- [ ] Commands typed in Input that change settings (precision, angle,
      base, number format, ...), desktop, web and the extension. The form
      (a keyword or a symbol) is chosen when the work starts; check it
      cannot clash with names, functions, tags or `?keyword` (Help).

## Before the APK goes on Google Play (owner 2026-11-22)
- [ ] Foldables: Play blocks new apps and updates that miss its baseline
      large-screen / foldable quality from April 2027 and wants foldable
      screenshots; target API 37 from August 2027 (no opt-out of the
      orientation and resizability changes). SoftCalc already targets 36,
      locks no orientation or aspect ratio, and handles screen changes
      without a restart. To check on an emulator (Pixel Fold and the
      resizable device) and a real foldable if there is one: the near-square
      inner screen (the side-by-side layouts go by landscape, maybe by width
      instead), a flip phone's small outer screen (keypad and page heads),
      folding while dragging the Graph or with an editor open; then the
      foldable screenshots.
