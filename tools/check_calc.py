"""check_calc.py - Check the engine's answers one by one, verbose, with a log to read later (owner 2026-11-26).

Runs the same checks as the engine's tests, but prints every case, line by
line, and writes them to a log file:

  golden  the answers recorded from the original PowerCalc (spec/golden),
          with the differences SoftCalc makes on purpose (expected_diffs.py)
  matrix  the matrix functions against SymPy's exact answers
  stats   the statistics functions against SymPy
  help    every `line => result` example in the help

    engine/.venv/Scripts/python tools/check_calc.py                 all of them
    engine/.venv/Scripts/python tools/check_calc.py stats help      some suites
    ... --filter tcdf       only lines holding this text
    ... --fails             print only what fails (the log keeps everything)
    ... --log my.txt        the log's path (default logs/check-<date-time>.txt)

tools/check_calc.ps1 runs it from PowerShell with the engine's Python.
Exit code 1 when anything fails.
"""

import argparse
import datetime
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TESTS = ROOT / "engine" / "tests"
sys.path[:0] = [str(ROOT / "engine"), str(TESTS)]

import mpmath    # noqa: E402

from softcalc_engine.engine import Engine    # noqa: E402

SUITES = ("golden", "matrix", "stats", "help")


class Report:
    """Prints each case and keeps the counts; everything also goes to the log."""

    def __init__(self, log, fails_only, text_filter):
        self.log = log
        self.fails_only = fails_only
        self.filter = text_filter.lower() if text_filter else None
        self.counts = {}

    def wanted(self, line):
        return self.filter is None or self.filter in line.lower()

    def case(self, suite, where, line, ok, got, want, note=""):
        passed, failed = self.counts.get(suite, (0, 0))
        self.counts[suite] = (passed + ok, failed + (not ok))
        lines = [f"{'PASS' if ok else 'FAIL'}  {suite:6} {where:24} {line}",
                 f"      got   {got}",
                 f"      want  {want}"]
        if note:
            lines.append(f"      {note}")
        text = "\n".join(lines)
        self.log.write(text + "\n")
        if not ok or not self.fails_only:
            print(text)

    def summary(self):
        rows = [f"{suite:8} {passed:6} passed {failed:6} failed" for suite, (passed, failed) in self.counts.items()]
        total = sum(failed for _, failed in self.counts.values())
        text = "\n".join(["", "Summary", *rows, f"{'all':8} {'':6} {'':6} {total:6} failed"])
        self.log.write(text + "\n")
        print(text)
        return total


#==================================================================
#  BLOCK 1 - golden: the original program's recorded answers
#==================================================================
def check_golden(report):
    from expected_diffs import ERROR
    from test_engine_golden import GROUPS, expected_for, run_group
    for group in GROUPS:
        for case, got in run_group(group):
            if not report.wanted(case.input):
                continue
            want = expected_for(case)
            if want is ERROR:
                ok, want_text = got.kind == "error", "an error"
            elif want is None:
                ok, want_text = got.kind == "empty", "nothing added"
            else:
                ok, want_text = got.kind in ("result", "definition") and got.text == want, want
            where = f"{group}:{case.line_no}"
            settings = f"prec {case.prec}, {case.angle}, {case.base}{', rpn' if case.rpn == 'on' else ''}"
            report.case("golden", where, case.input, ok, f"{got.kind} {got.text}", want_text, settings)


#==================================================================
#  BLOCK 2 - matrix and stats: SymPy's answers, within a tolerance
#==================================================================
def check_matrix(report):
    from test_matrix_reference import DATA, TOLERANCE, shown, wanted
    engine = Engine()
    engine.settings.angle = DATA["angle"]
    for number, case in enumerate(DATA["cases"]):
        if not report.wanted(case["line"]):
            continue
        text = engine.evaluate(case["line"]).text
        where = f"{case['group']}#{number}"
        if "error" in case:
            ok = text.startswith("ERROR:") and case["error"] in text
            report.case("matrix", where, case["line"], ok, text, f"an error holding {case['error']!r}")
            continue
        if text.startswith("ERROR:"):
            report.case("matrix", where, case["line"], False, text, "a value")
            continue
        with mpmath.workdps(60):
            got, flat = shown(text)
            want = [[wanted(case["value"])]] if "value" in case else [[wanted(c) for c in row] for row in case["rows"]]
            same_shape = [len(r) for r in got] == [len(r) for r in want] and ("rows" not in case or flat == case["flat"])
            scale = max([mpmath.mpf(1)] + [abs(w) for row in want for w in row])
            worst = max((abs(g - w) for gr, wr in zip(got, want) for g, w in zip(gr, wr)), default=mpmath.inf)
            allowed = max(TOLERANCE, mpmath.mpf(case.get("cond", 0)) * mpmath.mpf("1e-40"))
            ok = same_shape and worst <= allowed * scale
            want_text = case.get("value") or json.dumps(case["rows"])
            note = f"off by {mpmath.nstr(worst / scale, 3)} of its size, allowed {mpmath.nstr(allowed, 3)}"
            report.case("matrix", where, case["line"], ok, text, want_text, note if same_shape else "another shape")


def check_stats(report):
    from test_stats_reference import DATA, TOLERANCE, wanted
    engine = Engine()
    for number, case in enumerate(DATA):
        if not report.wanted(case["line"]):
            continue
        text = engine.evaluate(case["line"]).text
        where = f"{case['group']}#{number}"
        if case["error"]:
            ok = text.startswith("ERROR:" + case["error"])
            report.case("stats", where, case["line"], ok, text, f"ERROR:{case['error']}...")
            continue
        with mpmath.workdps(60):
            try:
                got = mpmath.mpf(text)
            except (ValueError, TypeError):
                report.case("stats", where, case["line"], False, text, case["value"])
                continue
            want = wanted(case["value"])
            scale = max(1, abs(want))
            ok = abs(got - want) <= TOLERANCE * scale
            note = f"off by {mpmath.nstr(abs(got - want) / scale, 3)} of its size, allowed {mpmath.nstr(TOLERANCE, 3)}"
            report.case("stats", where, case["line"], ok, text, case["value"], note)


#==================================================================
#  BLOCK 3 - help: the examples, exactly as written
#==================================================================
def check_help(report):
    from test_help_examples import TOPICS, examples
    for path in TOPICS:
        engine = Engine()
        for number, rpn, source, expected in examples(path):
            engine.settings.rpn = rpn
            got = engine.evaluate(source).text
            if report.wanted(source):
                where = f"{path.parent.parent.name}/{path.stem}:{number}"
                report.case("help", where, source, got == expected, got, expected)


def main():
    parser = argparse.ArgumentParser(description="Check the engine's answers one by one.")
    parser.add_argument("suites", nargs="*", choices=SUITES, help="golden, matrix, stats, help (default: all)")
    parser.add_argument("--filter", help="only lines holding this text (any letter case)")
    parser.add_argument("--fails", action="store_true", help="print only what fails; the log keeps everything")
    parser.add_argument("--log", help="the log file (default logs/check-<date-time>.txt)")
    args = parser.parse_args()
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    suites = args.suites or list(SUITES)
    stamp = datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
    log_path = Path(args.log) if args.log else ROOT / "logs" / f"check-{stamp}.txt"
    log_path.parent.mkdir(parents=True, exist_ok=True)
    with log_path.open("w", encoding="utf-8") as log:
        log.write(f"check_calc {stamp}: {', '.join(suites)}" + (f", filter {args.filter!r}" if args.filter else "") + "\n\n")
        report = Report(log, args.fails, args.filter)
        for suite in suites:
            {"golden": check_golden, "matrix": check_matrix, "stats": check_stats, "help": check_help}[suite](report)
        failed = report.summary()
    print(f"\nLog: {log_path}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
