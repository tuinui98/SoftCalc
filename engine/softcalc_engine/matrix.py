"""softcalc_engine/matrix.py - Lists and matrices: `[1, 2, 3]`, `[[1, 2], [3, 4]]` (design/matrix_plan.md X1).

A `[` where an operand starts opens a list (lexer); a `[` right after a
value stays a conversion or format tag, as before. A list holds numbers;
a list of lists of the same length is a matrix. Cells are real or complex
numbers at the working precision. The text a matrix shows as parses back
to the same value, so History, `$n`, variables and the .pcf keep it.
"""

from dataclasses import dataclass

import mpmath

from .errors import CalcError

MAX_CELLS = 10000
MAX_SOLVE = 50    # inv, det, solve: at most 50x50 (X2)


@dataclass(frozen=True)
class Matrix:
    rows: tuple    # tuples of mpf / mpc, all the same length
    flat: bool     # written as one list `[a, b]`, not `[[a, b]]`

    @property
    def shape(self):
        return len(self.rows), len(self.rows[0])

    def text(self, cell):
        """`[1, 2]` or `[[1, 2], [3, 4]]`, each cell written by cell(value)."""
        lines = ["[" + ", ".join(cell(value) for value in row) + "]" for row in self.rows]
        return lines[0] if self.flat else "[" + ", ".join(lines) + "]"


def is_cell(value):
    return isinstance(value, (mpmath.mpf, mpmath.mpc))


def from_items(items):
    """The value of `[item, ...]`: numbers make a list, equal lists a matrix."""
    if all(is_cell(item) for item in items):
        return checked(Matrix((tuple(items),), True))
    if all(isinstance(item, Matrix) for item in items):
        if any(not item.flat for item in items):
            raise CalcError("A matrix holds lists of numbers, not matrices")
        width = len(items[0].rows[0])
        if any(len(item.rows[0]) != width for item in items):
            raise CalcError("The rows of a matrix must be the same length")
        return checked(Matrix(tuple(item.rows[0] for item in items), False))
    raise CalcError("A list holds numbers, or lists of numbers of the same length")


def from_numbers(values):
    """A list from Python-side numbers (card() draws)."""
    return checked(Matrix((tuple(values),), True))


def checked(matrix):
    rows, cols = matrix.shape
    if rows * cols > MAX_CELLS:
        raise CalcError(f"A matrix may have at most {MAX_CELLS} cells")
    return matrix


#======================================================================
#  X2 - Arithmetic and functions (design/matrix_plan.md X2)
#======================================================================
def cells(m):
    return [cell for row in m.rows for cell in row]


def shaped(rows, flat):
    return checked(Matrix(tuple(tuple(row) for row in rows), flat))


def map_cells(m, fn):
    return shaped([[fn(cell) for cell in row] for row in m.rows], m.flat)


def to_mp(m):
    return mpmath.matrix([list(row) for row in m.rows])


def from_mp(mp, flat=False):
    """Cells rounded to the working precision (`+x`), as mpmath worked them out."""
    return shaped([[+mp[i, j] for j in range(mp.cols)] for i in range(mp.rows)], flat)


#----------------------------------------------------------------------
#  det, inverse and solve at twice the working digits (SymPy check,
#  owner 2026-11-24): an ill-conditioned matrix keeps its shown digits,
#  and a det smaller than the shown precision allows, against the
#  matrix's own size (Hadamard's bound), counts as 0. Decimals such as
#  0.316 are not exact in binary, so a matrix with det 0 on paper is
#  otherwise left with a det of 1e-40 and an "inverse" of 1e43.
#----------------------------------------------------------------------
GUARD = 10    # engine.GUARD_DIGITS: the working digits past the shown ones

# The most shown digits a det/inv/solve of this evaluation may have lost
# (round 2, owner 2026-11-24: show them all, with a note); reset by the engine
lost = {"digits": 0, "of": 32}    # "of": the shown precision, set by the engine


def lost_note():
    """The History note for a matrix near det 0, or ""."""
    if lost["digits"] <= 0:
        return ""
    return f"about {lost['digits']} of {lost['of']} digits may be off"


def wide():
    return mpmath.workdps(2 * mpmath.mp.dps)


def factor(m):
    """LU of a square Matrix on plain lists, with row swaps (call inside wide()).

    Returns (lu, order, det): lu holds L below the diagonal and U on and
    above it, order the original row of each row. One factorisation serves
    det, inv, solve and A^-n (matrix speed work, owner 2026-11-24: mpmath's
    det and inverse each factorised again, through slow cell access). det
    is 0 when it is below the shown precision against Hadamard's bound.
    """
    a = [[mpmath.mpmathify(cell) for cell in row] for row in m.rows]
    n = len(a)
    bound = mpmath.fprod(mpmath.sqrt(mpmath.fsum(abs(x) ** 2 for x in row)) for row in a)
    order = list(range(n))
    sign = 1
    for k in range(n):
        pivot_row = max(range(k, n), key=lambda r: abs(a[r][k]))
        if a[pivot_row][k] == 0:
            return a, order, mpmath.mpf(0)
        if pivot_row != k:
            a[k], a[pivot_row] = a[pivot_row], a[k]
            order[k], order[pivot_row] = order[pivot_row], order[k]
            sign = -sign
        top = a[k]
        pivot = top[k]
        for i in range(k + 1, n):
            row = a[i]
            if row[k] == 0:
                continue
            f = row[k] / pivot
            row[k] = f
            row[k + 1:] = [x - f * y for x, y in zip(row[k + 1:], top[k + 1:])]
    value = sign * mpmath.fprod(a[k][k] for k in range(n))
    return a, order, checked_det(value, bound)


def checked_det(value, bound):
    """0 for a det below the shown precision; else the det, noting the digits it may cost."""
    digits = mpmath.mp.dps // 2 - GUARD
    if abs(value) <= bound * mpmath.mpf(10) ** -digits:
        return mpmath.mpf(0)
    # |det| / bound is about 1 / the condition number: rounding of the input
    # (at the shown digits + GUARD) grows by that much
    off = int(mpmath.floor(mpmath.log10(bound / abs(value)))) - GUARD
    if off > lost["digits"]:
        lost["digits"] = min(off, lost["of"])
    return value


def lu_solve(lu, order, column):
    """x with A*x = column, from factor()'s lu and order."""
    n = len(lu)
    y = [column[order[i]] for i in range(n)]
    for i in range(1, n):
        y[i] -= mpmath.fdot(lu[i][:i], y[:i])
    x = [mpmath.mpf(0)] * n
    for i in range(n - 1, -1, -1):
        x[i] = (y[i] - mpmath.fdot(lu[i][i + 1:], x[i + 1:])) / lu[i][i]
    return x


def inverse_rows(m, key, singular="The matrix has no inverse (its det is 0)"):
    """The inverse as rows of cells at twice the digits (call inside wide())."""
    square(m, key, MAX_SOLVE)
    lu, order, value = factor(m)
    if value == 0:
        raise CalcError(singular)
    n = len(lu)
    columns = [lu_solve(lu, order, [mpmath.mpf(1 if i == j else 0) for i in range(n)]) for j in range(n)]
    return [[columns[j][i] for j in range(n)] for i in range(n)]


def as_matrix(value, key):
    if not isinstance(value, Matrix):
        raise CalcError(f"{key} needs a list or a matrix")
    return value


def whole_number(value, key, low=1):
    if not isinstance(value, mpmath.mpf) or value != mpmath.floor(value) or value < low:
        raise CalcError(f"{key} needs a whole number from {low}")
    return int(value)


def square(m, key, largest=None):
    rows, cols = m.shape
    if m.flat or rows != cols:
        raise CalcError(f"{key} needs a square matrix")
    if largest and rows > largest:
        raise CalcError(f"{key} takes at most {largest}x{largest}")
    return rows


#----------------------------------------------------------------------
#  Operators
#----------------------------------------------------------------------
def binary(op, a, b, scalar):
    """a op b with a list or matrix on either side; scalar(op, x, y) for one cell."""
    if isinstance(a, Matrix) and isinstance(b, Matrix):
        if op in ("+", "-"):
            if a.shape != b.shape:
                raise CalcError(f"'{op}' needs the same size: {size(a)} and {size(b)}")
            rows = [[scalar(op, x, y) for x, y in zip(ra, rb)] for ra, rb in zip(a.rows, b.rows)]
            return shaped(rows, a.flat and b.flat)
        if op == "*":
            return product(a, b)
        if op == "/":
            raise CalcError("Divide by a matrix: multiply by inv() instead")
        raise CalcError(f"'{op}' takes a matrix and a number")
    if not is_cell(b if isinstance(a, Matrix) else a):
        raise CalcError(f"'{op}' needs a number")
    if isinstance(a, Matrix):
        if op == "^" and not a.flat:
            return power(a, b)
        return map_cells(a, lambda x: scalar(op, x, b))
    if not b.flat and op in ("/", "^", "mod"):
        raise CalcError(f"'{op}' with a matrix on the right is not defined")
    return map_cells(b, lambda y: scalar(op, a, y))


def size(m):
    rows, cols = m.shape
    return f"a list of {cols}" if m.flat else f"{rows}x{cols}"


def product(a, b):
    """Matrix times matrix; a list fits as a row (on the left) or a column (on the right)."""
    if a.flat and b.flat:
        raise CalcError("Two lists: use dot(a, b), or write one as a matrix")
    left = a.rows
    right = [[cell] for cell in b.rows[0]] if b.flat else b.rows
    if len(left[0]) != len(right):
        raise CalcError(f"Sizes do not fit: {size(a)} times {size(b)}")
    rows = [[mpmath.fsum(x * right[k][j] for k, x in enumerate(r)) for j in range(len(right[0]))] for r in left]
    if b.flat:
        return shaped([[r[0] for r in rows]], True)
    return shaped(rows, a.flat)


def power(m, n):
    count = square(m, "'^'", MAX_SOLVE)
    if not isinstance(n, mpmath.mpf) or n != mpmath.floor(n) or abs(n) > 1000:
        raise CalcError("A matrix to a power needs a whole number from -1000 to 1000")
    if n == 0:
        return identity([mpmath.mpf(count)])
    base = inverse_mp(m, "'^'") if n < 0 else to_mp(m)
    with wide():
        result = base ** int(abs(n))
    return from_mp(result)


def inverse_mp(m, key):
    with wide():
        return mpmath.matrix(inverse_rows(m, key))


#----------------------------------------------------------------------
#  Making lists and matrices
#----------------------------------------------------------------------
def identity(args):
    n = whole_number(args[0], "identity")
    if n * n > MAX_CELLS:
        raise CalcError(f"A matrix may have at most {MAX_CELLS} cells")
    return shaped([[mpmath.mpf(1 if i == j else 0) for j in range(n)] for i in range(n)], False)


def filled(key, value):
    """zeros(n) / ones(n): a list of n; zeros(r, c) / ones(r, c): an r x c matrix."""
    def make(args):
        rows = whole_number(args[0], key)
        cols = whole_number(args[1], key) if len(args) > 1 else None
        if rows * (cols or 1) > MAX_CELLS:
            raise CalcError(f"A matrix may have at most {MAX_CELLS} cells")
        if cols is None:
            return shaped([[mpmath.mpf(value)] * rows], True)
        return shaped([[mpmath.mpf(value)] * cols for _ in range(rows)], False)
    return make


def diag(args):
    """diag(list): the square matrix with the list down its diagonal; diag(matrix): its diagonal."""
    m = as_matrix(args[0], "diag")
    if m.flat:
        n = len(m.rows[0])
        if n * n > MAX_CELLS:
            raise CalcError(f"A matrix may have at most {MAX_CELLS} cells")
        return shaped([[m.rows[0][i] if i == j else mpmath.mpf(0) for j in range(n)] for i in range(n)], False)
    n = square(m, "diag")
    return shaped([[m.rows[i][i] for i in range(n)]], True)


def seq(args):
    """seq(a, b, [step]): a, a+step, ... up to b. The step goes toward b
    whatever its sign, as a deck's [10,1,3] does (owner 2026-11-24):
    seq(10, 1, 3) and seq(10, 1, -3) are both 10, 7, 4, 1."""
    for value in args:
        if not isinstance(value, mpmath.mpf):
            raise CalcError("seq needs real numbers")
    start, end = args[0], args[1]
    step = abs(args[2]) if len(args) > 2 else mpmath.mpf(1)
    if step == 0:
        raise CalcError("seq: the step cannot be 0")
    if end < start:
        step = -step
    count = int(mpmath.floor((end - start) / step + mpmath.mpf(10) ** (2 - mpmath.mp.dps))) + 1
    if count > MAX_CELLS:
        raise CalcError(f"A list may have at most {MAX_CELLS} numbers")
    return shaped([[start + k * step for k in range(count)]], True)


#----------------------------------------------------------------------
#  Size and cells (counting from 1)
#----------------------------------------------------------------------
def index(value, count, key, what):
    i = whole_number(value, key)
    if i > count:
        raise CalcError(f"{key}: {what} {i} is past the end ({count})")
    return i - 1


def at(args):
    m = as_matrix(args[0], "at")
    if m.flat:
        if len(args) != 2:
            raise CalcError("at(list, n) takes one place")
        return m.rows[0][index(args[1], len(m.rows[0]), "at", "place")]
    if len(args) != 3:
        raise CalcError("at(matrix, row, column) takes a row and a column")
    rows, cols = m.shape
    return m.rows[index(args[1], rows, "at", "row")][index(args[2], cols, "at", "column")]


def row(args):
    m = as_matrix(args[0], "row")
    return shaped([m.rows[index(args[1], m.shape[0], "row", "row")]], True)


def col(args):
    m = as_matrix(args[0], "col")
    j = index(args[1], m.shape[1], "col", "column")
    return shaped([[r[j] for r in m.rows]], True)


#----------------------------------------------------------------------
#  Linear algebra
#----------------------------------------------------------------------
def vector(value, key):
    m = as_matrix(value, key)
    rows, cols = m.shape
    if rows != 1 and cols != 1:
        raise CalcError(f"{key} needs lists, or matrices of one row or one column")
    return cells(m)


def dot(args):
    a, b = vector(args[0], "dot"), vector(args[1], "dot")
    if len(a) != len(b):
        raise CalcError(f"dot needs two lists of the same length ({len(a)} and {len(b)})")
    return mpmath.fsum(x * y for x, y in zip(a, b))


def cross(args):
    a, b = vector(args[0], "cross"), vector(args[1], "cross")
    if len(a) != 3 or len(b) != 3:
        raise CalcError("cross needs two lists of 3 numbers")
    return shaped([[a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]], True)


def transpose(args):
    """Rows become columns; a list becomes a one-column matrix."""
    m = as_matrix(args[0], "transpose")
    return shaped([list(column) for column in zip(*m.rows)], False)


def det(args):
    m = as_matrix(args[0], "det")
    square(m, "det", MAX_SOLVE)
    with wide():
        value = factor(m)[2]
    return +value


def inv(args):
    m = as_matrix(args[0], "inv")
    with wide():
        rows = inverse_rows(m, "inv")
    return shaped([[+cell for cell in row] for row in rows], False)


def trace(args):
    m = as_matrix(args[0], "trace")
    n = square(m, "trace")
    return mpmath.fsum(m.rows[i][i] for i in range(n))


def solve(args):
    """solve(A, b): x with A*x = b; b a list (the answer a list) or a matrix."""
    a = as_matrix(args[0], "solve")
    n = square(a, "solve", MAX_SOLVE)
    b = as_matrix(args[1], "solve")
    rhs = [[cell] for cell in b.rows[0]] if b.flat else [list(r) for r in b.rows]
    if len(rhs) != n:
        raise CalcError(f"solve: A has {n} rows, b has {len(rhs)}")
    with wide():
        lu, order, value = factor(a)
        if value == 0:
            raise CalcError("solve: the matrix has no inverse (its det is 0), so no single answer")
        columns = [lu_solve(lu, order, [row[j] for row in rhs]) for j in range(len(rhs[0]))]
    if b.flat:
        return shaped([[+x for x in columns[0]]], True)
    return shaped([[+columns[j][i] for j in range(len(columns))] for i in range(n)], False)


#----------------------------------------------------------------------
#  Lists: every cell of the arguments
#----------------------------------------------------------------------
def numbers_of(args, key, real=False):
    out = []
    for value in args:
        out.extend(cells(value) if isinstance(value, Matrix) else [value])
    for value in out:
        if not is_cell(value) or (real and isinstance(value, mpmath.mpc)):
            raise CalcError(f"{key} needs {'real ' if real else ''}numbers")
    return out


def total(args):
    return mpmath.fsum(numbers_of(args, "sum"))


def mean(args):
    values = numbers_of(args, "mean")
    return mpmath.fsum(values) / len(values)


def prod(args):
    return mpmath.fprod(numbers_of(args, "prod"))


def sort(args):
    m = as_matrix(args[0], "sort")
    if not m.flat:
        raise CalcError("sort takes a list")
    return shaped([sorted(numbers_of(args, "sort", real=True))], True)


def median(args):
    values = sorted(numbers_of(args, "median", real=True))
    middle = len(values) // 2
    return values[middle] if len(values) % 2 else (values[middle - 1] + values[middle]) / 2


def spread(key, sample, root):
    """variance / stdev of a sample (n - 1) or a whole population (n)."""
    def work(args):
        values = numbers_of(args, key, real=True)
        if len(values) < (2 if sample else 1):
            raise CalcError(f"{key} needs at least {2 if sample else 1} numbers")
        average = mpmath.fsum(values) / len(values)
        value = mpmath.fsum((x - average) ** 2 for x in values) / (len(values) - (1 if sample else 0))
        return mpmath.sqrt(value) if root else value
    return work


def norm(args):
    """Length of a list (sqrt of the sum of squares); of a matrix, the same over every cell (Frobenius)."""
    return mpmath.sqrt(mpmath.fsum(abs(cell) ** 2 for cell in cells(as_matrix(args[0], "norm"))))


def rank(args):
    """Rows that are not a mix of the others; a pivot below the shown precision counts as 0 (like det)."""
    m = as_matrix(args[0], "rank")
    rows, cols = m.shape
    if rows * cols > MAX_SOLVE * MAX_SOLVE:
        raise CalcError(f"rank takes at most {MAX_SOLVE * MAX_SOLVE} cells")
    with wide():
        a = [[mpmath.mpmathify(cell) for cell in row] for row in m.rows]
        largest = max(abs(cell) for row in a for cell in row)
        if largest == 0:
            return mpmath.mpf(0)
        tiny = largest * mpmath.mpf(10) ** -(mpmath.mp.dps // 2 - GUARD)
        found = 0
        for col in range(cols):
            pivot = max(range(found, rows), key=lambda r: abs(a[r][col]), default=None)
            if pivot is None or abs(a[pivot][col]) <= tiny:
                continue
            a[found], a[pivot] = a[pivot], a[found]
            for r in range(found + 1, rows):
                factor = a[r][col] / a[found][col]
                a[r] = [x - factor * y for x, y in zip(a[r], a[found])]
            found += 1
            if found == rows:
                break
    return mpmath.mpf(found)


MAX_EIG = 20    # eig is much slower than inv (round 2: timed before choosing)


def eig(args):
    """Eigenvalues of a square matrix, as a list: by real part, largest first; complex ones as a+bi."""
    m = as_matrix(args[0], "eig")
    square(m, "eig", MAX_EIG)
    with mpmath.workdps(mpmath.mp.dps + GUARD):    # was twice the digits: half the time, same shown digits
        values = mpmath.eig(to_mp(m), left=False, right=False)
        values = [mpmath.mpc(v) for v in values]
        shown = mpmath.mpf(10) ** -(mpmath.mp.dps - 2 * GUARD)
        scale = max([abs(v) for v in values] + [mpmath.mpf(1)])
        values = [v.real if abs(v.imag) <= scale * shown else v for v in values]
        # rounded first, so a pair a+bi, a-bi whose real parts differ in the last digits keeps +bi first
        step = scale * shown * 1000
        values.sort(key=lambda v: (-mpmath.nint(mpmath.re(v) / step), -mpmath.im(v)))
    return shaped([[+v for v in values]], True)


#----------------------------------------------------------------------
#  Filling, reshaping, running totals (owner 2026-11-24)
#----------------------------------------------------------------------
def fill(args):
    """fill(n, value): a list of n; fill(rows, cols, value): a matrix;
    fill(rows, cols, list): the list as every row (its length = cols)."""
    return fill_with(args[:-1], lambda: args[-1])


def fill_with(size, make):
    """fill with make() giving each value: once per cell, or once per row for
    a list. The engine passes a make() that draws again when the value holds
    rand() or card() (owner 2026-11-24); otherwise it gives the same value."""
    rows = whole_number(size[0], "fill")
    cols = whole_number(size[1], "fill") if len(size) > 1 else None
    if rows * (cols or 1) > MAX_CELLS:
        raise CalcError(f"A matrix may have at most {MAX_CELLS} cells")
    first = make()
    if isinstance(first, Matrix):
        if cols is None:
            raise CalcError("fill(rows, cols, list) repeats a list as each row")
        out = []
        for value in [first] + [make() for _ in range(rows - 1)]:
            if not isinstance(value, Matrix) or not value.flat:
                raise CalcError("fill takes a number or a list, not a matrix")
            if len(value.rows[0]) != cols:
                raise CalcError(f"fill: the list has {len(value.rows[0])} numbers, the rows {cols}")
            out.append(list(value.rows[0]))
        return shaped(out, False)
    values = [first] + [make() for _ in range(rows * (cols or 1) - 1)]
    if not all(is_cell(value) for value in values):
        raise CalcError("fill needs a number or a list to fill with")
    if cols is None:
        return shaped([values], True)
    return shaped([values[r * cols:(r + 1) * cols] for r in range(rows)], False)


def reshape(args):
    """reshape(list, rows, cols): the cells row by row (a matrix's cells read row by row)."""
    values = cells(as_matrix(args[0], "reshape"))
    rows, cols = whole_number(args[1], "reshape"), whole_number(args[2], "reshape")
    if rows * cols != len(values):
        raise CalcError(f"reshape: {len(values)} numbers do not make {rows}x{cols} ({rows * cols})")
    return shaped([values[r * cols:(r + 1) * cols] for r in range(rows)], rows == 1 and args[0].flat)


def a_list(value, key):
    m = as_matrix(value, key)
    if not m.flat:
        raise CalcError(f"{key} takes a list")
    return list(m.rows[0])


def cumsum(args):
    """Running totals: cumsum([1, 2, 3]) = [1, 3, 6]."""
    totals, running = [], mpmath.mpf(0)
    for value in a_list(args[0], "cumsum"):
        running += value
        totals.append(running)
    return shaped([totals], True)


# name -> (function, fewest arguments, most arguments; None: any number)
FUNCTIONS = {
    "identity": (identity, 1, 1), "zeros": (filled("zeros", 0), 1, 2), "ones": (filled("ones", 1), 1, 2),
    "diag": (diag, 1, 1), "seq": (seq, 2, 3),
    "rows": (lambda a: mpmath.mpf(as_matrix(a[0], "rows").shape[0]), 1, 1),
    "cols": (lambda a: mpmath.mpf(as_matrix(a[0], "cols").shape[1]), 1, 1),
    "len": (lambda a: mpmath.mpf(len(cells(as_matrix(a[0], "len")))), 1, 1),
    "at": (at, 2, 3), "row": (row, 2, 2), "col": (col, 2, 2),
    "dot": (dot, 2, 2), "cross": (cross, 2, 2), "transpose": (transpose, 1, 1),
    "det": (det, 1, 1), "inv": (inv, 1, 1), "trace": (trace, 1, 1), "solve": (solve, 2, 2),
    "sum": (total, 1, None), "mean": (mean, 1, None), "prod": (prod, 1, None),
    "min": (lambda a: min(numbers_of(a, "min", real=True)), 1, None),
    "max": (lambda a: max(numbers_of(a, "max", real=True)), 1, None),
    "sort": (sort, 1, 1),
    "median": (median, 1, None),
    "stdev": (spread("stdev", True, True), 1, None), "stdevp": (spread("stdevp", False, True), 1, None),
    "variance": (spread("variance", True, False), 1, None), "variancep": (spread("variancep", False, False), 1, None),
    "norm": (norm, 1, 1), "rank": (rank, 1, 1), "eig": (eig, 1, 1),
    "fill": (fill, 2, 3), "reshape": (reshape, 3, 3), "cumsum": (cumsum, 1, 1),
    "reverse": (lambda a: shaped([a_list(a[0], "reverse")[::-1]], True), 1, 1),
    "count": (lambda a: mpmath.mpf(len(numbers_of(a, "count"))), 1, None),
}
