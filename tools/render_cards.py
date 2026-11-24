"""tools/render_cards.py - The ready-made playing cards' pictures, SVG to PNG (owner 2026-11-24).

data/cards/playing/svg/*.svg -> data/cards/playing/png/*.png, 368 x 512
(512 px tall, as CardStore keeps a picture), on a clear ground. SoftCalc's
own SVG reader draws icons only (no <symbol>/<use>), so Microsoft Edge,
headless, draws each card: a small HTML page sizes the picture exactly,
since an SVG opened alone is laid out by the window. The PNGs are
committed and embedded in the exe; run this again only when a card's SVG
changes.

    python tools/render_cards.py
"""

import os
import pathlib
import subprocess
import tempfile

ROOT = pathlib.Path(__file__).resolve().parent.parent
SOURCE = ROOT / "data" / "cards" / "playing" / "svg"
TARGET = ROOT / "data" / "cards" / "playing" / "png"
WIDTH, HEIGHT = 368, 512
EDGE = [
    pathlib.Path(os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)")) / "Microsoft" / "Edge" / "Application" / "msedge.exe",
    pathlib.Path(os.environ.get("ProgramFiles", r"C:\Program Files")) / "Microsoft" / "Edge" / "Application" / "msedge.exe",
]


def main():
    edge = next((path for path in EDGE if path.exists()), None)
    if edge is None:
        raise SystemExit("render_cards: Microsoft Edge not found")
    TARGET.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as scratch:
        for svg in sorted(SOURCE.glob("*.svg")):
            page = pathlib.Path(scratch) / (svg.stem + ".html")
            page.write_text(
                "<!doctype html><html><head><style>html,body{margin:0;background:transparent;overflow:hidden}"
                f"img{{display:block;width:{WIDTH}px;height:{HEIGHT}px}}</style></head>"
                f"<body><img src=\"{svg.as_uri()}\"></body></html>", encoding="utf-8")
            png = TARGET / (svg.stem + ".png")
            subprocess.run([str(edge), "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1",
                            "--default-background-color=00000000", f"--window-size={WIDTH},{HEIGHT}",
                            "--allow-file-access-from-files", f"--screenshot={png}", page.as_uri()],
                           check=True, capture_output=True)
    print(f"wrote {len(list(TARGET.glob('*.png')))} pictures in {TARGET}")


if __name__ == "__main__":
    main()
