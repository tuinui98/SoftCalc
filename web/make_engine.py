"""make_engine.py - Pack engine/softcalc_engine into web/engine.zip for the browser.

The web worker unpacks this zip into Pyodide's file system and imports
the engine from it, so the browser runs the same Python files as the
desktop build. Rerun after any engine change:

    python web/make_engine.py
"""

import pathlib
import zipfile

WEB = pathlib.Path(__file__).resolve().parent
PACKAGE = WEB.parent / "engine" / "softcalc_engine"


def main():
    target = WEB / "engine.zip"
    with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as archive:
        for source in sorted(PACKAGE.glob("*.py")):
            archive.write(source, f"softcalc_engine/{source.name}")
    print(f"wrote {target} ({target.stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main()
