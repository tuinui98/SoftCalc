"""make_notices.py - Write ui/SoftCalc/THIRD-PARTY-NOTICES.txt for the About box (M9).

The published SoftCalc-standalone.exe carries the .NET runtime, a frozen Python with
mpmath, the PyInstaller bootloader and IconMCP icons; their licences ask
for their notices to travel with the binary. The ready-made district deck
comes from Thai open government data, whose terms ask for the source. The texts are read from the
installed packages, so rerun this after upgrading any of them:

    engine/.venv/Scripts/python tools/make_notices.py
"""

import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
SITE = ROOT / "engine" / ".venv" / "Lib" / "site-packages"
NUGET = pathlib.Path.home() / ".nuget" / "packages"
DOTNET = pathlib.Path(r"C:\Program Files\dotnet")
OUT = ROOT / "ui" / "SoftCalc" / "THIRD-PARTY-NOTICES.txt"

DISTRICTS = """The ready-made district decks (Random window) are made from "ชื่ออำเภอ",
district names of Thailand in Thai script and romanized, published by the
Office of the Royal Society (สำนักงานราชบัณฑิตยสภา) at
https://data.go.th/dataset/orst_0102 (file orst_0102.csv, 2022-05-01).

The right to use the data is subject to the Terms and Conditions for DGA
Open Government License (version 1.0, 10 June 2021):
https://data.go.th/pages/dga-open-government-license

SoftCalc corrects the file to the 928 districts (tools/make_districts.py):
four Chaloem Phra Kiat districts added (Nakhon Ratchasima, Buri Ram, Nan,
Nakhon Si Thammarat), two old names dropped (Muang, Sakon Nakhon; Maha Pho,
Nakhon Sawan), the romanized names of Nong Chok and Nong Khaem swapped back,
doubled spaces trimmed, and Bang Sai (บางซ้าย, Ayutthaya) written Bang Saai,
as Wikipedia does, to tell it from Bang Sai (บางไทร).
"""

NBITCOIN_MIT = """The MIT License (MIT)

Copyright (c) Metaco SA 2017 (as the NuGet package states; NBitcoin by
Nicolas Dorier and contributors, https://github.com/MetacoSA/NBitcoin)

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


# The ready-made playing cards' pictures (data/cards/playing/README.md, owner 2026-11-24)
CARDS = """
The 52 card faces of the ready-made deck "Playing cards" are heratexx's
"Classic 4 color poker deck (SVG)",
https://opengameart.org/content/classic-4-color-poker-deck-svg,
dedicated to the public domain under CC0 1.0 Universal
(https://creativecommons.org/publicdomain/zero/1.0/). No credit is
required; it is given with thanks. The two jokers were drawn for
SoftCalc to match.
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
        "SoftCalc includes software and data from the projects below. Their\n"
        "notices and licences follow.\n\n",
        section("Icons: IconMCP (https://iconmcp.com/), MIT",
                (ROOT / "ui" / "SoftCalc" / "Icons" / "LICENSE.txt").read_text(encoding="utf-8")),
        section("District names: Office of the Royal Society, DGA Open Government License", DISTRICTS),
        section("Playing card pictures: heratexx, \"Classic 4 color poker deck (SVG)\", CC0 1.0", CARDS),
        section("NBitcoin (https://github.com/MetacoSA/NBitcoin), MIT", NBITCOIN_MIT),
        section("Newtonsoft.Json (https://www.newtonsoft.com/json), MIT, used by NBitcoin",
                (NUGET / "newtonsoft.json" / "13.0.4" / "LICENSE.md").read_text(encoding="utf-8")),
        section("mpmath (https://mpmath.org/), BSD 3-Clause", one("mpmath-*.dist-info/licenses/LICENSE")),
        section(f"Python {sys.version.split()[0]} (https://www.python.org/), PSF License "
                "and the licences of the libraries in its Windows build",
                (base_python / "LICENSE.txt").read_text(encoding="utf-8")),
        section("PyInstaller bootloader (https://pyinstaller.org/), GPL 2 with the Bootloader Exception",
                one("pyinstaller-*.dist-info/licenses/COPYING.txt")),
        section(".NET runtime, Windows Desktop and Microsoft.Extensions.Logging.Abstractions "
                "(https://github.com/dotnet), MIT", DOTNET_MIT),
        section(".NET runtime third-party notices", (DOTNET / "ThirdPartyNotices.txt").read_text(encoding="utf-8")),
    ]
    OUT.write_text("".join(parts), encoding="utf-8", newline="\r\n")
    print(f"wrote {OUT} ({OUT.stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main()
