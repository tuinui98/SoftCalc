"""tools/render_cards.py - The ready-made decks' pictures, SVG to PNG (owner 2026-11-24).

data/cards/<set>/svg/*.svg -> data/cards/<set>/png/*.png, on a clear
ground, at most 512 px a side as CardStore keeps a picture: the playing
cards 368 x 512, the flags 512 x 384 (flag-icons' 4:3). SoftCalc's
own SVG reader draws icons only (no <symbol>/<use>), so Microsoft Edge,
headless, draws each card: a small HTML page sizes the picture exactly,
since an SVG opened alone is laid out by the window. The PNGs are
committed and embedded in the exe; run this again only when a card's SVG
changes.

    python tools/render_cards.py [set ...]    (default: every set)
"""

import os
import pathlib
import subprocess
import sys
import tempfile

ROOT = pathlib.Path(__file__).resolve().parent.parent
CARDS = ROOT / "data" / "cards"
SETS = {"playing": (368, 512), "flags": (512, 384)}    # width, height of each picture
EDGE = [
    pathlib.Path(os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)")) / "Microsoft" / "Edge" / "Application" / "msedge.exe",
    pathlib.Path(os.environ.get("ProgramFiles", r"C:\Program Files")) / "Microsoft" / "Edge" / "Application" / "msedge.exe",
]


def render(edge, name, width, height):
    source, target = CARDS / name / "svg", CARDS / name / "png"
    target.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as scratch:
        for svg in sorted(source.glob("*.svg")):
            page = pathlib.Path(scratch) / (svg.stem + ".html")
            page.write_text(
                "<!doctype html><html><head><style>html,body{margin:0;background:transparent;overflow:hidden}"
                f"img{{display:block;width:{width}px;height:{height}px}}</style></head>"
                f"<body><img src=\"{svg.as_uri()}\"></body></html>", encoding="utf-8")
            png = target / (svg.stem + ".png")
            subprocess.run([str(edge), "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1",
                            "--default-background-color=00000000", f"--window-size={width},{height}",
                            "--allow-file-access-from-files", f"--screenshot={png}", page.as_uri()],
                           check=True, capture_output=True)
    print(f"wrote {len(list(target.glob('*.png')))} pictures in {target}")


def main():
    edge = next((path for path in EDGE if path.exists()), None)
    if edge is None:
        raise SystemExit("render_cards: Microsoft Edge not found")
    for name in sys.argv[1:] or SETS:
        render(edge, name, *SETS[name])


if __name__ == "__main__":
    main()
