"""tools/make_matrix_reference.py - Exact answers from SymPy for the matrix functions (owner 2026-11-24).

Writes engine/tests/data/matrix_reference.json: lines the engine is asked
(random matrices from a fixed seed) with SymPy's exact answer, or the
error expected when there is no answer. engine/tests/test_matrix_reference.py
compares the engine with it, so the engine tests need no SymPy.

SymPy pins mpmath below 1.4 and the engine needs 1.4, so this runs in its
own virtual environment:
    python -m venv engine/.venv-ref
    engine/.venv-ref/Scripts/python -m pip install sympy==1.14.0
    engine/.venv-ref/Scripts/python tools/make_matrix_reference.py
"""

import json
import random
from pathlib import Path

import sympy
from sympy import I, Matrix, Rational

SEED = 20261124
OUT = Path(__file__).resolve().parent.parent / "engine" / "tests" / "data" / "matrix_reference.json"
DIGITS = 45    # numeric answers (sqrt, exp, sin) are worked out to this many digits


#==================================================================
#  BLOCK 1 - Random values, written the way a user types them
#==================================================================
class Maker:
    def __init__(self, seed):
        self.rng = random.Random(seed)

    def real(self):
        kind = self.rng.random()
        if kind < 0.6:
            return Rational(self.rng.randint(-9, 9))
        if kind < 0.9:
            return Rational(self.rng.randint(-999, 999), 100)
        return Rational(self.rng.randint(-99999, 99999), 1000)

    def cell(self, complex_share):
        if self.rng.random() < complex_share:
            return Rational(self.rng.randint(-9, 9)) + Rational(self.rng.randint(1, 9)) * self.rng.choice((1, -1)) * I
        return self.real()

    def matrix(self, rows, cols, complex_share=0.0):
        return Matrix(rows, cols, lambda i, j: self.cell(complex_share))

    def nonzero_real(self):
        value = self.real()
        while value == 0:
            value = self.real()
        return value

    def singular(self, n):
        """An n x n matrix with one row a multiple of another, so det = 0."""
        m = self.matrix(n, n)
        source, target = self.rng.sample(range(n), 2)
        m[target, :] = m[source, :] * self.nonzero_real()
        return m


def number_text(value):
    """`3`, `-2.5`, `3-2i`, `2i`: exact decimals, as typed into the Input box."""
    re_part, im_part = sympy.re(value), sympy.im(value)
    if im_part == 0:
        return real_text(re_part)
    imag = real_text(abs(im_part))
    imag = "" if imag == "1" else imag
    if re_part == 0:
        return ("-" if im_part < 0 else "") + imag + "i"
    return f"{real_text(re_part)}{'-' if im_part < 0 else '+'}{imag}i"


def real_text(value):
    value = Rational(value)
    if value.q == 1:
        return str(value.p)
    text = format(sympy.Float(value, 30), "f").rstrip("0")
    assert Rational(text) == value, value    # every value made here is a finite decimal
    return text


def matrix_text(m, flat=False):
    if flat:
        return "[" + ", ".join(number_text(x) for x in m) + "]"
    return "[" + ", ".join("[" + ", ".join(number_text(x) for x in m.row(i)) + "]" for i in range(m.rows)) + "]"


#==================================================================
#  BLOCK 2 - Answers as JSON: exact rationals "p/q", or digits
#==================================================================
def answer_cell(value):
    value = sympy.nsimplify(value) if value.is_Float else sympy.expand(value)
    re_part, im_part = sympy.re(value), sympy.im(value)
    return [cell_text(re_part), cell_text(im_part)]


def cell_text(part):
    if part.is_Rational:
        return f"{part.p}/{part.q}"
    return str(sympy.N(part, DIGITS))


def answer(value, flat=False):
    if isinstance(value, sympy.MatrixBase):
        rows = [[answer_cell(value[i, j]) for j in range(value.cols)] for i in range(value.rows)]
        return {"flat": flat, "rows": rows}
    return {"value": answer_cell(sympy.sympify(value))}


#==================================================================
#  BLOCK 3 - The cases
#==================================================================
def cases():
    make = Maker(SEED)
    rng = make.rng
    out = []

    def add(line, value=None, flat=False, error=None, group="", cond=None):
        case = {"group": group, "line": line}
        if cond is not None:
            case["cond"] = str(sympy.N(cond, 5))    # how much the input's rounding grows (see the test)
        if error is not None:
            case["error"] = error
        else:
            case.update(answer(value, flat))
        out.append(case)

    def as_list(values):
        return Matrix(1, len(values), list(values))

    for _ in range(40):    # product, sum and difference, scaling
        n, k, m = rng.randint(1, 6), rng.randint(1, 6), rng.randint(1, 6)
        share = rng.choice((0.0, 0.0, 0.3))
        a, b = make.matrix(n, k, share), make.matrix(k, m, share)
        add(f"{matrix_text(a)}*{matrix_text(b)}", a * b, group="product")
        c = make.matrix(n, k, share)
        add(f"{matrix_text(a)}+{matrix_text(c)}", a + c, group="add")
        add(f"{matrix_text(a)}-{matrix_text(c)}", a - c, group="add")
        s = make.nonzero_real()
        add(f"{number_text(s)}*{matrix_text(a)}", s * a, group="scale")
        add(f"{matrix_text(a)}/{number_text(s)}", a / s, group="scale")

    for _ in range(60):    # det, trace, transpose, inverse, powers
        n = rng.randint(1, 6)
        share = rng.choice((0.0, 0.0, 0.0, 0.25))
        a = make.matrix(n, n, share)
        text = matrix_text(a)
        add(f"det({text})", a.det(), group="det")
        add(f"trace({text})", a.trace(), group="trace")
        add(f"transpose({text})", a.T, group="transpose")
        power = rng.randint(0, 5)
        add(f"{text}^{power}", a ** power, group="power")
        if a.det() == 0:
            add(f"inv({text})", error="no inverse", group="inv")
            continue
        add(f"inv({text})", a.inv(), group="inv")
        negative = rng.randint(1, 3)
        add(f"{text}^-{negative}", a.inv() ** negative, group="power")
        b = make.matrix(n, 1, share)
        add(f"solve({text}, {matrix_text(list(b), flat=True)})", as_list(a.LUsolve(b)), flat=True, group="solve")
        bm = make.matrix(n, rng.randint(2, 3), share)
        add(f"solve({text}, {matrix_text(bm)})", a.LUsolve(bm), group="solve")
        row = make.matrix(1, n, share)
        add(f"{matrix_text(list(row), flat=True)}*{text}", row * a, flat=True, group="list times matrix")
        add(f"{text}*{matrix_text(list(row), flat=True)}", as_list(a * row.T), flat=True, group="list times matrix")

    for _ in range(20):    # singular: no inverse, no single answer
        n = rng.randint(2, 6)
        a = make.singular(n)
        text = matrix_text(a)
        add(f"det({text})", 0, group="det singular")
        add(f"inv({text})", error="no inverse", group="inv singular")
        add(f"{text}^-1", error="no inverse", group="power singular")
        add(f"solve({text}, {matrix_text(list(make.matrix(n, 1)), flat=True)})", error="no inverse", group="solve singular")

    tiny = [Rational(1, 10 ** k) for k in (10, 20, 25)]
    for eps in tiny:    # nearly singular: the 32-digit engine must still see the difference
        a = Matrix([[1, 1], [1, 1 + eps]])
        cond = a.norm(1) * a.inv().norm(1)
        add(f"det({matrix_text(a)})", a.det(), group="near singular", cond=cond)
        add(f"inv({matrix_text(a)})", a.inv(), group="near singular", cond=cond)
    hilbert = Matrix(5, 5, lambda i, j: Rational(1, i + j + 1))
    hilbert_text = "[" + ", ".join("[" + ", ".join(f"1/{i + j + 1}" for j in range(5)) + "]" for i in range(5)) + "]"
    cond = hilbert.norm(1) * hilbert.inv().norm(1)
    add(f"det({hilbert_text})", hilbert.det(), group="hilbert", cond=cond)
    add(f"inv({hilbert_text})", hilbert.inv(), group="hilbert", cond=cond)

    for _ in range(40):    # lists
        n = rng.randint(1, 8)
        share = rng.choice((0.0, 0.0, 0.3))
        a = [make.cell(share) for _ in range(n)]
        b = [make.cell(share) for _ in range(n)]
        ta, tb = matrix_text(a, flat=True), matrix_text(b, flat=True)
        add(f"dot({ta}, {tb})", sum(x * y for x, y in zip(a, b)), group="dot")
        add(f"sum({ta})", sum(a), group="sum")
        add(f"mean({ta})", sum(a) / n, group="mean")
        add(f"prod({ta})", sympy.prod(a), group="prod")
        add(f"{ta}+{tb}", as_list([x + y for x, y in zip(a, b)]), flat=True, group="list add")
        s = make.nonzero_real()
        add(f"{ta}*{number_text(s)}", as_list([x * s for x in a]), flat=True, group="list scale")
        add(f"{ta}^2", as_list([x ** 2 for x in a]), flat=True, group="list power")
        real = [make.real() for _ in range(n)]
        tr = matrix_text(real, flat=True)
        add(f"min({tr})", min(real), group="min max")
        add(f"max({tr})", max(real), group="min max")
        add(f"sort({tr})", as_list(sorted(real)), flat=True, group="sort")
        u, v = [make.cell(share) for _ in range(3)], [make.cell(share) for _ in range(3)]
        cross = Matrix(u).cross(Matrix(v))
        add(f"cross({matrix_text(u, flat=True)}, {matrix_text(v, flat=True)})", as_list(cross), flat=True, group="cross")

    for _ in range(15):    # one-number functions cell by cell (angles in radians)
        n = rng.randint(1, 5)
        positive = [abs(make.nonzero_real()) for _ in range(n)]
        text = matrix_text(positive, flat=True)
        add(f"sqrt({text})", as_list([sympy.sqrt(x) for x in positive]), flat=True, group="cell sqrt")
        add(f"ln({text})", as_list([sympy.log(x) for x in positive]), flat=True, group="cell ln")
        add(f"sin({text})", as_list([sympy.sin(x) for x in positive]), flat=True, group="cell sin")
        add(f"exp({text})", as_list([sympy.exp(x) for x in positive]), flat=True, group="cell exp")
    return out


def main():
    data = {"seed": SEED, "sympy": sympy.__version__, "angle": "rad", "cases": cases()}
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(data, indent=0, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print(f"{len(data['cases'])} cases -> {OUT}")


if __name__ == "__main__":
    main()
