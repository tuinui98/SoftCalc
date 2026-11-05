# SoftCalc - to do (2026-11-06)

Recorded at the owner's request after M8. Nothing here is started.

## 1. In the spec, not built yet
- [ ] E9 display decimals menu: View > Increase / Decrease decimal places,
      Show all decimal places (pad zeros). Engine `decimals` / `pad_zeros`
      and settings.json already support it; only the UI menu (and tests)
      is missing. Note: the M8 report wrongly said it could already be
      set from the UI.

## 2. M9 packaging (details in STATUS.md, "M9 notes")
- [ ] Single portable `.exe`, data in `SoftCalc-data\` beside it
      (fallback `%APPDATA%\SoftCalc` when not writable).
- [ ] Rebuild the engine exe (engine/dist is from 2026-10-27, before
      M5-M8 engine changes); PyInstaller onedir embedded in the exe.
- [ ] Icon and About box.

## 3. Out of scope for v1
- [ ] File > Help (disabled): new help text later; the original's help
      must not be copied.

## 4. Ideas to discuss with the owner first
- [ ] Graph interface (spec/ideas.md): zoom/pan with the mouse,
      coordinates under the cursor, several functions at once, auto-fit
      Y, trace cursor.
- [ ] Add/Edit dialogs (spec/ideas.md): separate name / parameters /
      body fields, list of definitions inside the dialog (after the clone).
- [ ] Red error lines in History: added without being asked; keep or
      remove.
