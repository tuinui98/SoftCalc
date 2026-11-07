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

## 5. Owner requests after v1.0.0 (2026-11-07, not started; discuss first)
- [ ] About box: a short note only (name, version, data folder). The
      third-party licence box is hidden at first; a link (e.g. "Show
      third-party licences") opens it.
- [ ] Number literals in another base than the current output base
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
    it stays as typed (`0.1`, or `1e40` past the fixed-digit limit).
  - Owner 2026-11-07, negatives: `-` stays the operator and only the
    number converts (`-0x1F` shows `-31`, `2*-0x1F` shows `2*-31`, no
    parentheses); an RPN negative literal converts as one token.
    Output stays sign and magnitude (`-0xFF`).
- [ ] Two's complement (owner uses it often): to design. Proposal:
      functions with the bit width as an argument rather than a global
      word size; see the discussion of 2026-11-07.

## 6. Still open from v1.0.0
- [ ] Try the release exe on another PC (no .NET/Python) and with its
      antivirus; the exe is unsigned (SmartScreen warning expected).
- [ ] Live check of the read-only fallback (exe in Program Files, data
      to `%APPDATA%\SoftCalc`); unit-tested only.
- [ ] Graph lines vanishing on view switches: reported once, not
      reproduced, skipped by the owner (STATUS.md "Open items").
- [ ] File > Help (section 3).
