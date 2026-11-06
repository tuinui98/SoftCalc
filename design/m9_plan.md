# M9 - single portable SoftCalc.exe (owner 2026-11-07)

Owner's choices: self-contained .NET (no runtime to install), the
portable copy starts empty (no copy from %APPDATA%), version 1.0.0,
About under File.

## Shape of a release
```
SoftCalc.exe            one file: UI + .NET runtime + engine.zip resource
SoftCalc-data\          created on first start, next to the exe
  settings.json  history.jsonl  definitions.pcf  rates.xml
  engine\<hash>\        engine unpacked once; reused while the hash matches
```

## Steps
1. Engine: `build/build_engine.ps1` switches PyInstaller to `--onedir`
   (`engine/dist/softcalc-engine/softcalc-engine.exe` + `_internal`).
   One-file extracted to %TEMP% on every start (one engine per window,
   plus restarts). Engine version 1.0.0. `test_frozen_exe_answers`
   follows the new path.
2. UI build: `SoftCalc.csproj` embeds `engine.zip` as a resource only
   when `-p:EngineZip=<path>` is given; dev builds keep using
   engine/.venv. Version 1.0.0.
3. Data folder (`DataFolder.Choose`): SOFTCALC_DATA_DIR; else, for the
   published single-file exe, `SoftCalc-data\` beside it if it can be
   written; else %APPDATA%\SoftCalc. Dev builds stay on %APPDATA%.
4. Engine (`EngineLocator`): SOFTCALC_ENGINE; embedded engine.zip
   unpacked to `<data>\engine\<first 16 hex of SHA-256>\` (into a temp
   folder, a `.complete` marker last, then renamed; two windows starting
   together are safe; older hash folders are removed when unused);
   softcalc-engine.exe beside the exe; dev venv.
5. File > About SoftCalc: version, data folder with Open Folder, and
   THIRD-PARTY-NOTICES (IconMCP MIT, Python PSF, mpmath BSD, PyInstaller
   bootloader GPL with its exception, .NET MIT), embedded.
6. `build/publish.ps1`: engine build + frozen test, zip, `dotnet publish`
   (win-x64, self-contained, single file, compressed) to `dist\SoftCalc.exe`,
   then a smoke run of the exe from an empty folder (data folder and
   engine created, a line evaluated).
7. Tests: data folder choice, engine unpacking (fresh, reuse, broken
   folder), locator order. Tag `milestone/<date>-m9-portable`.

## Risks
Antivirus false positives on an unsigned exe (onedir is flagged less
than one-file); the first start is slower while the engine unpacks.
Fallback: ship a folder (exe + engine\).
