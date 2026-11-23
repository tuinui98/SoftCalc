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
