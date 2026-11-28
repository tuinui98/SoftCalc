"""Copy for Excel (owner 2026-11-29): the tab text works out as SoftCalc did.

A small evaluator stands in for the spreadsheet: it reads the cells the
sheet uses (numbers, TRUE/FALSE, + - * / ^, IF, INT, EXP, SUM of a
column range, A1 references with or without $).
"""

import math
import re

import pytest

from softcalc_engine.server import Dispatcher


def ask(dispatcher, op, **arguments):
    return dispatcher.handle({"id": 1, "op": op, **arguments})


class Sheet:
    def __init__(self, text):
        assert text.endswith("\r\n")
        self.cells = {}
        for number, line in enumerate(text[:-2].split("\r\n"), start=1):
            for column, cell in zip("ABCDE", line.split("\t")):
                self.cells[f"{column}{number}"] = cell
        self.done = {}

    def value(self, name):
        if name not in self.done:
            self.done[name] = self.work(self.cells.get(name, ""))
        return self.done[name]

    def work(self, cell):
        if not cell.startswith("="):
            if cell in ("TRUE", "FALSE"):
                return cell == "TRUE"
            try:
                return float(cell)
            except ValueError:
                return cell
        text = cell[1:].replace("$", "").replace("^", "**")
        text = re.sub(r"SUM\(([A-E])(\d+):\1(\d+)\)", r'SUM_("\1", \2, \3)', text)
        text = re.sub(r"\b([A-E]\d+)\b", r'V("\1")', text)
        text = re.sub(r"(\d)E-(\d)", r"\1e-\2", text)
        names = {"V": self.value, "IF": lambda a, b, c: b if a else c, "INT": math.floor, "EXP": math.exp,
                 "SUM_": lambda column, first, last: sum(self.value(f"{column}{n}") for n in range(first, last + 1))}
        return eval(text, {"__builtins__": {}}, names)    # noqa: S307 - the test's own formulas

    def find(self, label):
        """The B cell's value on the line whose A cell starts with label."""
        if not self.done:
            # the table first, top down: a row needs only the one above and the inputs
            header = min(int(name[1:]) for name, cell in self.cells.items() if name.startswith("A") and cell == "Period")
            for number in range(header + 1, max(int(name[1:]) for name in self.cells) + 1):
                for column in "BCDE":
                    self.value(f"{column}{number}")
        for name, cell in self.cells.items():
            if name.startswith("A") and cell.startswith(label):
                return self.value("B" + name[1:])
        raise KeyError(label)


@pytest.fixture
def dispatcher():
    return Dispatcher()


def sheet_of(dispatcher, **arguments):
    reply = ask(dispatcher, "schedule", row_limit=0, **arguments)
    assert reply["ok"], reply
    text = ask(dispatcher, "schedule_sheet", schedule_id=reply["schedule_id"])["text"]
    return reply, Sheet(text)


@pytest.mark.parametrize("arguments", [
    {"compounding": "monthly"},
    {"compounding": "monthly", "contribution": "100", "contribution_freq": "monthly", "at_start": True},
    {"compounding": "daily", "contribution": "50", "contribution_freq": "weekly"},
    {"compounding": "continuous", "contribution": "300", "contribution_freq": "quarterly"},
    {"compounding": "yearly", "rate_unit": "daily", "annual_rate": "0.02", "length_unit": "months", "years": "30"},
    {"compounding": "weekly", "rate_unit": "monthly", "annual_rate": "0.4"},
])
def test_normal_sheet_matches(dispatcher, arguments):
    base = {"principal": "1000", "annual_rate": "6", "years": "3"}
    reply, sheet = sheet_of(dispatcher, **{**base, **arguments})
    assert sheet.find("Future value") == pytest.approx(float(reply["future_value"]), rel=1e-11)
    assert sheet.find("Total interest") == pytest.approx(float(reply["total_interest"]), rel=1e-9)
    assert sheet.cells["A1"].startswith("SoftCalc compound interest, normal")


GOAL = {"mode": "goal", "target": "10000000", "principal": "50000", "annual_rate": "0.5", "years": "20",
        "contribution": "15000", "rate_unit": "monthly", "length_unit": "years", "contribution_freq": "monthly"}


@pytest.mark.parametrize("solve", ["target", "principal", "annual_rate", "years", "contribution"])
def test_goal_sheet_meets_the_target(dispatcher, solve):
    order = ["principal", "contribution", "target", "years", "annual_rate"]
    order = [solve] + [name for name in order if name != solve]
    reply, sheet = sheet_of(dispatcher, **{**GOAL, "solve": solve, solve: "", "order": order})
    assert sheet.find("End balance - target") == pytest.approx(0, abs=1e-4)
    assert sheet.find("End balance") == pytest.approx(float(reply["future_value"]), rel=1e-11)
    # the Result box's order: the worked-out field first, then the one set last
    assert sheet.cells["A3"].endswith("(result)")
    assert sheet.cells["A4"].startswith({"annual_rate": "Interest", "years": "Length", "target": "Target",
                                         "contribution": "Contribution", "principal": "Principal"}[order[-1]])


def test_goal_sheet_with_a_part_row(dispatcher):
    reply, sheet = sheet_of(dispatcher, mode="goal", solve="years", target="2000", principal="1000",
                            annual_rate="10", years="", rate_unit="yearly", length_unit="years",
                            contribution="0", at_start=True)
    assert sheet.find("End balance") == pytest.approx(2000, rel=1e-12)
    assert sheet.find("Part of last row") == pytest.approx(0.2725409, rel=1e-6)
    last = [cell for name, cell in sheet.cells.items() if name.startswith("A") and cell.startswith("8 (")]
    assert last == ["8 (0.2725)"]


def test_goal_sheet_recalculates_from_an_input(dispatcher):
    # a sheet, not a picture: a new principal gives a new end balance
    reply, sheet = sheet_of(dispatcher, mode="goal", solve="target", target="", principal="1000",
                            annual_rate="1", years="12", rate_unit="monthly", length_unit="months")
    row = next(name for name, cell in sheet.cells.items() if cell == "Principal")
    sheet.cells["B" + row[1:]] = "2000"
    assert sheet.find("End balance") == pytest.approx(2000 * 1.01 ** 12)


def test_sheet_of_a_stale_schedule_is_an_error(dispatcher):
    first = ask(dispatcher, "schedule", principal="1", annual_rate="1", years="1")
    ask(dispatcher, "schedule", principal="1", annual_rate="1", years="1")
    assert ask(dispatcher, "schedule_sheet", schedule_id=first["schedule_id"])["ok"] is False
