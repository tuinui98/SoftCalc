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
- [ ] Numeric Calc view: better size and layout, and scientific
      calculator buttons.
  - To discuss: which buttons (sin/cos/tan, inverse, log/ln, x^y, root,
    n!, pi, e, memory, parentheses, Deg/Rad), how they insert into Input
    (text like the menus do, so History stays expression based),
    resizable keypad, font scaling with Text Size.
- [ ] Random tools (a section or window):
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
    only when asked. Then the random source (R4): Seed (through an
    engine of the window's own, matching rands() in Input) and Add
    entropy; Password always System. Help also to cover these.
  - Pick a topic at random from a list; add, remove and import topics
    (text file, one per line); then pick one of that topic's options.
  - Plain 50/50 or weighted picks (a bias per item).
  - Password generator: length, symbols, digits, upper/lower case;
    several at once, copy one or copy all; no history kept.
    "Seed with a favourite word": to discuss, since a word-seeded
    generator is repeatable and as weak as the word; maybe only as an
    extra mixed into the system's secure random source.
  - To discuss: the random source (System.Security.Cryptography
    RandomNumberGenerator for passwords), whether picks go to History,
    clipboard clearing after a time.
- [ ] Crypto wallet seeds (first: list what SoftCalc could do).
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
