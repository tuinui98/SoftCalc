# PowerCalc clean-room spec

Behavior spec for a personal-use reimplementation of the Windows XP
Calculator PowerToy, built only from black-box observation of the running
program and its help. No disassembly or decompilation. Original binaries stay
out of git (see `.gitignore`).

## Layout
- `inventory/` - what exists: menus, views, dialogs, persisted settings
- `raw/` - gitignored: registry backup, captured history dumps

## Phases
1. Inventory - menu tree (done), views/panes, dialogs, screenshots
2. Behavior - automated test inputs, golden outputs via *Save History*
3. Spec - consolidated feature and input-language spec
4. Design and build
