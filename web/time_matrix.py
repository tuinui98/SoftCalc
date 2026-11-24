"""time_matrix.py - Timing of the big matrix functions, run by time_matrix.mjs (web) and engine/.venv (desktop).

Each line runs 3 times and the fastest counts, so other programs on the
computer disturb the numbers less.
"""

import time

from softcalc_engine.engine import Engine

for precision in (64, 512):
    e = Engine()
    e.settings.precision = precision
    e.evaluate("rands(1)")
    for n in (20, 50):
        e.evaluate(f"M{n}=randm({n}, {n}, -9, 9)")
    for line in ("det(M50)", "inv(M50)", "solve(M50, seq(1, 50))", "M50^-1", "eig(M20)"):
        best = None
        for _ in range(3):
            start = time.perf_counter()
            e.evaluate(line)
            took = time.perf_counter() - start
            best = took if best is None else min(best, took)
        print(f"{precision:4} {line:24} {best:7.2f} s", flush=True)
