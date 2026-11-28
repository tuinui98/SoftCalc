"""softcalc_engine/compound_sheet.py - Copy for Excel: a schedule as tab text with formulas.

Owner 2026-11-29: pasted at A1 of an empty sheet, the text gives the
inputs (goal mode: in the Result box's order, the worked-out field
first, then from the one set last to the one set longest ago), helper
cells, the totals and the whole table as formulas, so the sheet works
the table out again when an input changes. A worked-out field other
than Target goes in as SoftCalc's number, with a check cell (end
balance - target) for Goal Seek. The rows stay as many as SoftCalc
made; the cells keep Excel's General format.

The formulas follow finance.py: the rate a row as period_rate / goal
work it out, a row's interest on its start (plus the contribution when
it comes at the start), a part last row compounding for its share.
"""

from dataclasses import dataclass

from . import finance
from .formatting import format_number

NAMES = {"target": "Target", "principal": "Principal", "annual_rate": "Interest rate %",
         "years": "Length", "contribution": "Contribution"}


@dataclass
class SheetInput:
    mode: str              # "normal" or "goal"
    solve: object          # goal: the worked-out field
    order: list            # goal fields, set longest ago first
    values: dict           # field -> number text, every digit
    rate_unit: str
    length_unit: str
    compounding: str
    contribution_freq: str
    at_start: bool
    rows: list             # finance.Period
    periods_per_year: object


def per_year_text(table, unit):
    """How many of unit fit in a year, as a cell: 12, or =365/7."""
    value = table[unit]
    return f"={value}" if isinstance(value, str) else str(value)


def sheet_text(sheet):
    """The tab text, CRLF lines."""
    lines = []

    def add(*cells):
        lines.append(list(cells))
        return len(lines)    # this line's row number

    goal = sheet.mode == "goal"
    add(f"SoftCalc compound interest, {sheet.mode} (paste at A1)")
    add("Field", "Value", "Unit", "Note")

    #==================================================================
    #  BLOCK 1 - Inputs, in the Result box's order
    #==================================================================
    if goal:
        fields = [sheet.solve] + [name for name in reversed(sheet.order) if name != sheet.solve]
    else:
        fields = ["principal", "annual_rate", "years", "contribution"]
    every = finance.PERIOD_NAMES.get(sheet.contribution_freq, sheet.contribution_freq)
    units = {"annual_rate": sheet.rate_unit, "years": sheet.length_unit, "contribution": every}
    at = {}    # field -> its row
    for name in fields:
        at[name] = add(NAMES[name] + (" (result)" if goal and name == sheet.solve else ""),
                       sheet.values[name], units.get(name, ""), "")
    at["start"] = add("Contribution at start", "TRUE" if sheet.at_start else "FALSE", "", "")
    if not goal:
        add("Compounding", sheet.compounding, "", "")
    add()    # end BLOCK 1

    #==================================================================
    #  BLOCK 2 - Helpers: units a year, the rate a row, the rows
    #==================================================================
    rows_key = next(key for key in finance.COMPOUNDING
                    if abs(finance.per_year(finance.COMPOUNDING, key) - sheet.periods_per_year) < 1e-9)
    rows_a_year = add("Rows a year", per_year_text(finance.COMPOUNDING, rows_key), "", "")
    rate_units = add("Rate units a year", per_year_text(finance.RATE_UNITS, sheet.rate_unit), "", "")
    length_units = add("Length units a year", per_year_text(finance.LENGTH_UNITS, sheet.length_unit), "", "")
    rate = f"B{at['annual_rate']}/100"
    if goal:
        formula = f"=(1+{rate})^(B{rate_units}/B{rows_a_year})-1"
    elif sheet.compounding == "continuous":
        formula = f"=EXP({rate}*B{rate_units}/B{rows_a_year})-1"
    else:
        times = add("Compounding a year", per_year_text(finance.COMPOUNDING, sheet.compounding), "", "")
        span = finance.per_year(finance.RATE_UNITS, sheet.rate_unit) / finance.per_year(finance.COMPOUNDING, sheet.compounding)
        if span <= 1:
            # split evenly: 5% a year monthly is 5/12% a month
            each = f"{rate}*B{rate_units}/B{times}"
        else:
            # compounds over the longer period: 1% a day monthly is 1.01^(365/12) - 1
            each = f"(1+{rate})^(B{rate_units}/B{times})-1"
        formula = f"=(1+{each})^(B{times}/B{rows_a_year})-1"
    rate_row = add("Rate a row", formula, "", "")
    part = sheet.rows[-1].part if sheet.rows and sheet.rows[-1].part is not None else None
    whole = len(sheet.rows) - (1 if part is not None else 0)
    periods = add("Periods", f"=B{at['years']}*B{rows_a_year}/B{length_units}", "",
                  "a part period is cut" if not goal else "")
    part_row = None
    if goal:
        add("Whole rows", f"=INT(B{periods}+1E-9)", "", "")
        part_row = add("Part of last row", f"=IF(B{periods}-B{periods + 1}<1E-9,0,B{periods}-B{periods + 1})", "", "")
    add()    # end BLOCK 2

    #==================================================================
    #  BLOCK 3 - Totals and the table
    #==================================================================
    totals_at = len(lines) + 1
    first = totals_at + (4 if goal else 3) + 2    # the totals, a blank line, the header
    last = first + len(sheet.rows) - 1
    end = add("Future value" if not goal else "End balance", f"=E{last}", "", "")
    add("Total contributions", f"=SUM(D{first}:D{last})", "", "")
    add("Total interest", f"=SUM(C{first}:C{last})", "", "")
    if goal:
        add("End balance - target", f"=B{end}-B{at['target']}", "", "the check: 0 when the target is met")
        if sheet.solve == "target":
            lines[at["target"] - 1][1] = f"=B{end}"
        else:
            lines[at[sheet.solve] - 1][3] = "worked out by SoftCalc; Goal Seek can redo it"
    add()
    add("Period", "Start balance", "Interest", "Contribution", "End balance")
    c = f"$B${at['contribution']}"
    added = f"IF($B${at['start']},{c},0)"
    for index in range(1, whole + 1):
        row = add(str(index), f"=B{at['principal']}" if index == 1 else f"=E{len(lines)}",
                  "", f"={c}", "")
        lines[row - 1][2] = f"=(B{row}+{added})*$B${rate_row}"
        lines[row - 1][4] = f"=B{row}+C{row}+D{row}"
    if part is not None:
        index = whole + 1
        row = add(f"{index} ({format_number(part, 4)})",
                  f"=B{at['principal']}" if index == 1 else f"=E{len(lines)}", "", f"={added}", "")
        lines[row - 1][2] = f"=(B{row}+{added})*((1+$B${rate_row})^$B${part_row}-1)"
        lines[row - 1][4] = f"=B{row}+C{row}+D{row}"
    assert len(lines) == last, (len(lines), last)    # end BLOCK 3
    return "".join("\t".join(line) + "\r\n" for line in lines)
