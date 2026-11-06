"""make_notices.py - Write ui/SoftCalc/THIRD-PARTY-NOTICES.txt for the About box (M9).

The published SoftCalc.exe carries the .NET runtime, a frozen Python with
mpmath, the PyInstaller bootloader and IconMCP icons; their licences ask
for their notices to travel with the binary. The texts are read from the
installed packages, so rerun this after upgrading any of them:

    engine/.venv/Scripts/python tools/make_notices.py
"""

import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SITE = ROOT / "engine" / ".venv" / "Lib" / "site-packages"
DOTNET = pathlib.Path(r"C:\Program Files\dotnet")
OUT = ROOT / "ui" / "SoftCalc" / "THIRD-PARTY-NOTICES.txt"

DOTNET_MIT = """The MIT License (MIT)

Copyright (c) .NET Foundation and Contributors

All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
"""


def one(path_glob):
    """The single file matching a glob under site-packages."""
    found = sorted(SITE.glob(path_glob))
    if not found:
        sys.exit(f"not found: {path_glob}")
    return found[-1].read_text(encoding="utf-8")


def section(title, body):
    rule = "=" * 72
    return f"{rule}\n{title}\n{rule}\n\n{body.strip()}\n\n"


def main():
    base_python = pathlib.Path(sys.base_prefix)
    parts = [
        "SoftCalc includes software from the projects below. Their notices\n"
        "and licences follow.\n\n",
        section("Icons: IconMCP (https://iconmcp.com/), MIT",
                (ROOT / "ui" / "SoftCalc" / "Icons" / "LICENSE.txt").read_text(encoding="utf-8")),
        section("mpmath (https://mpmath.org/), BSD 3-Clause", one("mpmath-*.dist-info/licenses/LICENSE")),
        section(f"Python {sys.version.split()[0]} (https://www.python.org/), PSF License "
                "and the licences of the libraries in its Windows build",
                (base_python / "LICENSE.txt").read_text(encoding="utf-8")),
        section("PyInstaller bootloader (https://pyinstaller.org/), GPL 2 with the Bootloader Exception",
                one("pyinstaller-*.dist-info/licenses/COPYING.txt")),
        section(".NET runtime and Windows Desktop (https://github.com/dotnet), MIT", DOTNET_MIT),
        section(".NET runtime third-party notices", (DOTNET / "ThirdPartyNotices.txt").read_text(encoding="utf-8")),
    ]
    OUT.write_text("".join(parts), encoding="utf-8", newline="\r\n")
    print(f"wrote {OUT} ({OUT.stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main()
