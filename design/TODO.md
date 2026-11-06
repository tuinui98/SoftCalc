# SoftCalc - to do (2026-11-06)

Recorded at the owner's request after M8. Nothing here is started.

## 1. In the spec, not built yet
- [x] E9 display decimals menu: done 2026-11-06 with the ribbon
      (View > Decimal Places and the ribbon's Decimals group).

## 2. M9 packaging (details in STATUS.md, "M9 notes")
- [ ] Single portable `.exe`, data in `SoftCalc-data\` beside it
      (fallback `%APPDATA%\SoftCalc` when not writable).
- [ ] Rebuild the engine exe (engine/dist is from 2026-10-27, before
      M5-M8 engine changes); PyInstaller onedir embedded in the exe.
- [x] Program icon (2026-11-06, tools/IconMaker, owner's draft 2).
- [ ] About box (show the IconMCP licence there too).

## 3. Out of scope for v1
- [ ] File > Help (disabled): new help text later; the original's help
      must not be copied.

## 4. Ideas to discuss with the owner first
- [x] Graph interface: done 2026-11-06 (design/graph_plan.md G1-G3).
- [x] Dark theme (View > Theme), Text Size scaling the Font size:
      done 2026-11-06.
- [x] Add/Edit dialogs: Functions / Variables editor, done 2026-11-07.
- [x] Red error lines in History: owner keeps them (2026-11-07).
