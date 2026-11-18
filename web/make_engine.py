"""make_engine.py - Pack the engine, mpmath and Python's library as compiled files for the browser.

The web worker unpacks engine.zip into Pyodide's file system and imports
the engine from it, so the browser runs the same Python files as the
desktop build. Rerun after any engine change:

    python web/make_engine.py

Pyodide ships Python's own library (python_stdlib.zip) and mpmath as
.py files only, and it does not write .pyc files, so every start had
to compile all of them again: on a desktop browser that was about two
of its three seconds of Python start, and longer on a phone (owner
2026-11-19: 4 to 5 s on a desktop, 7 to 8 s on a phone). Here they are
compiled once, by this machine's Python, which must be the same 3.x as
Pyodide's (byte code changes only between minor versions):
- engine.zip: softcalc_engine/ and mpmath/ as .pyc only.
- python_stdlib_pyc.zip: Pyodide's python_stdlib.zip with each .py
  replaced by its .pyc. It has its own name, so a browser that kept the
  old library for good (sw.js) fetches this one.
The .pyc files are "unchecked hash" ones: Python uses them without
looking for a .py beside them.
"""

import importlib.util
import json
import marshal
import pathlib
import sys
import zipfile

WEB = pathlib.Path(__file__).resolve().parent
PACKAGE = WEB.parent / "engine" / "softcalc_engine"
PYODIDE = WEB / "node_modules" / "pyodide"
UNCHECKED_HASH = 0b01      # PEP 552 flags: hash-based, source not checked


def compiled(source, name):
    """The bytes of a .pyc for this source; name is the file name shown in tracebacks."""
    code = compile(source, name, "exec", dont_inherit=True)
    return (importlib.util.MAGIC_NUMBER + UNCHECKED_HASH.to_bytes(4, "little")
            + importlib.util.source_hash(source) + marshal.dumps(code))


def check_python():
    """Stop unless this Python's byte code is the one Pyodide's Python reads."""
    wanted = json.loads((PYODIDE / "pyodide-lock.json").read_text(encoding="utf-8"))["info"]["python"]
    if tuple(int(part) for part in wanted.split(".")[:2]) != sys.version_info[:2]:
        raise SystemExit(f"make_engine.py needs Python {wanted.rsplit('.', 1)[0]} (Pyodide's), "
                         f"not {sys.version.split()[0]}")


def write_engine(target):
    """softcalc_engine/ and mpmath/ (from Pyodide's wheel, the same 1.4.1 as the desktop) as .pyc."""
    wheel = next(PYODIDE.glob("mpmath-*.whl"))
    with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as archive:
        for source in sorted(PACKAGE.glob("*.py")):
            name = f"softcalc_engine/{source.name}"
            archive.writestr(name + "c", compiled(source.read_bytes(), name))
        with zipfile.ZipFile(wheel) as mpmath:
            for info in mpmath.infolist():
                # its own tests are left out: mpmath.runtests() is not offered
                if (info.filename.startswith("mpmath/") and info.filename.endswith(".py")
                        and not info.filename.startswith("mpmath/tests/")):
                    archive.writestr(info.filename + "c", compiled(mpmath.read(info), info.filename))
    print(f"wrote {target} ({target.stat().st_size // 1024} KB)")


def write_stdlib(target):
    """Pyodide's python_stdlib.zip with every .py compiled."""
    with zipfile.ZipFile(PYODIDE / "python_stdlib.zip") as source, \
            zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as archive:
        for info in source.infolist():
            data = source.read(info)
            if info.filename.endswith(".py"):
                archive.writestr(info.filename + "c", compiled(data, info.filename))
            else:
                archive.writestr(info, data)
    print(f"wrote {target} ({target.stat().st_size // 1024} KB)")


def main():
    check_python()
    write_engine(WEB / "engine.zip")
    write_stdlib(WEB / "python_stdlib_pyc.zip")


if __name__ == "__main__":
    main()
