"""make_districts.py - Write ui/SoftCalc/Decks/districts.txt from the Royal Society's district list.

Source: "ชื่ออำเภอ" (district names in Thai and romanized), Office of the
Royal Society, https://data.go.th/dataset/orst_0102 , file orst_0102.csv
(Windows-874), used under the Terms and Conditions for DGA Open Government
License v1.0, which ask for the source to be stated.

The file of 2022-05-01 has 926 rows; this script corrects it to the 928
districts (878 amphoe and 50 khet), owner 2026-11-15:
- adds Chaloem Phra Kiat in Nakhon Ratchasima, Buri Ram, Nan and
  Nakhon Si Thammarat (missing; Saraburi's is there);
- drops two old names listed beside the new ones (Muang, now Ban Muang,
  Sakon Nakhon; Maha Pho, now Kao Liao, Nakhon Sawan);
- swaps the romanized names of Nong Chok and Nong Khaem (Bangkok);
- trims doubled spaces;
- writes บางซ้าย (Ayutthaya) as Bang Saai, as Wikipedia does, to tell it
  from บางไทร, Bang Sai (owner 2026-11-15); any other romanized name two
  districts of one province share would get the district code after it.

    python tools/make_districts.py path/to/orst_0102.csv
"""

import csv
import io
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
DECKS = ROOT / "ui" / "SoftCalc" / "Decks"

ADDED = [
    ("30", "3032", "เฉลิมพระเกียรติ", "Chaloem Phra Kiat"),
    ("31", "3123", "เฉลิมพระเกียรติ", "Chaloem Phra Kiat"),
    ("55", "5515", "เฉลิมพระเกียรติ", "Chaloem Phra Kiat"),
    ("80", "8023", "เฉลิมพระเกียรติ", "Chaloem Phra Kiat"),
]
DROPPED = {("4710", "ม่วง"), ("6006", "มหาโพธิ")}
ROMAN_FIX = {"1003": "Nong Chok", "1023": "Nong Khaem", "1413": "Bang Saai"}


def clean(text):
    """One space between words, none at the ends."""
    return " ".join(text.split())


def main(source):
    rows = list(csv.DictReader(io.StringIO(pathlib.Path(source).read_bytes().decode("cp874"))))
    provinces = {}
    for line in (DECKS / "provinces.txt").read_text(encoding="utf-8").splitlines():
        if line and not line.startswith("#"):
            english, thai = line.split("|")[:2]
            provinces[thai] = english
    thai_of = {r["province_id"]: clean(r["province"]) for r in rows}
    districts = []
    for r in rows:
        thai = clean(r["amphoe_short"])
        if (r["amphoe_id"], thai) in DROPPED:
            continue
        roman = ROMAN_FIX.get(r["amphoe_id"], clean(r["romanized_amphoe_short"]))
        districts.append((r["amphoe_id"], provinces[thai_of[r["province_id"]]], roman, thai))
    for pid, code, thai, roman in ADDED:
        districts.append((code, provinces[thai_of[pid]], roman, thai))
    districts.sort()
    # two districts of one province with the same romanized name (Bang Sai: บางไทร and บางซ้าย,
    # Ayutthaya) get their district code after it
    names = [(d[1], d[2]) for d in districts]
    districts = [(code, province, f"{roman} ({code})" if names.count((province, roman)) > 1 else roman, thai)
                 for code, province, roman, thai in districts]
    assert len(districts) == 928, len(districts)
    assert len({d[0] for d in districts}) == 928
    out = DECKS / "districts.txt"
    with out.open("w", encoding="utf-8", newline="\n") as f:
        f.write("# districts.txt - Ready-made decks: the 928 districts of Thailand (878 amphoe, 50 khet of Bangkok)\n")
        f.write("# Source: Office of the Royal Society, district names in Thai and romanized (data.go.th orst_0102,\n")
        f.write("# 2022-05-01), under the Terms and Conditions for DGA Open Government License v1.0; corrected by\n")
        f.write("# tools/make_districts.py (four Chaloem Phra Kiat added, two old names dropped, Nong Chok and\n")
        f.write("# Nong Khaem romanized names swapped back)\n")
        f.write("# province (English)|English|Thai\n")
        for _, province, roman, thai in districts:
            f.write(f"{province}|{roman}|{thai}\n")
    print(f"wrote {out} ({len(districts)} districts)")


if __name__ == "__main__":
    main(sys.argv[1])
