"""softcalc_engine/hints.py - The hint line under the Input box (protocol `hint`).

One place for both UIs (owner 2026-11-18; was the desktop's InputHelp.cs).
Looks at the text left of the caret: a number literal there is shown in
the output base (`0x1F = 31`, owner 2026-11-07); inside an open `[` it
shows tag examples; on `$` it explains result references; on a word
being typed it describes the name (or lists the names it could become);
inside an open call `fv(0.05, ` it describes that function. User
functions and variables come first. Text is SoftCalc's own wording, not
the original's help (owner 2026-11-06).
"""

import re

BUILTINS = {
    "sqrt": "sqrt(x) - square root",
    "abs": "abs(x) - absolute value (size of a complex number)",
    "exp": "exp(x) - e to the power x",
    "ln": "ln(x) - natural logarithm",
    "log10": "log10(x) - base-10 logarithm",
    "sin": "sin(x) - sine, in the angle unit (Deg / Rad / Grad)",
    "cos": "cos(x) - cosine, in the angle unit",
    "tan": "tan(x) - tangent, in the angle unit",
    "asin": "asin(x) - inverse sine, result in the angle unit",
    "acos": "acos(x) - inverse cosine, result in the angle unit",
    "atan": "atan(x) - inverse tangent, result in the angle unit",
    "sinh": "sinh(x) - hyperbolic sine",
    "cosh": "cosh(x) - hyperbolic cosine",
    "tanh": "tanh(x) - hyperbolic tangent",
    "asinh": "asinh(x) - inverse hyperbolic sine",
    "acosh": "acosh(x) - inverse hyperbolic cosine",
    "atanh": "atanh(x) - inverse hyperbolic tangent",
    "fv": "fv(rate, nper, pmt, [pv], [type]) - future value; paid out is negative, e.g. fv(0.05/12, 120, -100)",
    "pv": "pv(rate, nper, pmt, [fv], [type]) - present value; rate per period",
    "pmt": "pmt(rate, nper, pv, [fv], [type]) - payment per period; type 1 = at the start",
    "nper": "nper(rate, pmt, pv, [fv], [type]) - number of periods",
    "epoch": "epoch(date) - Unix seconds, e.g. epoch(today)",
    "fromepoch": "fromepoch(seconds) - date and time from Unix seconds",
    "longdate": "longdate(date) - e.g. Friday, 6 November 2026",
    "longtime": "longtime(date) - e.g. 14:05:09",
    "longdatetime": "longdatetime(date) - long date and time",
    "mod": "a mod b - remainder of a / b",
    "twos": "twos(x, [bits]) - two's complement bit pattern: twos(-1, 8) = 255; bits default to View > Word Size",
    "signed": "signed(x, [bits]) - read a bit pattern as signed: signed(0xFF, 8) = -1; bits default to View > Word Size",
    "round": "round(x, [digits]) - round half away from zero: round(2.5) = 3, round(3.14159, 2) = 3.14",
    "ceil": "ceil(x) - the next whole number up: ceil(1.2) = 2",
    "floor": "floor(x) - the next whole number down: floor(-1.2) = -2",
    "rand": "rand([a], [b]) - random: rand() 0 to 1, rand(6) 0 to 6, rand(1, 6) 1 to 6; rand(1.0, 6) a decimal",
    "card": 'card("deck", [count], [no repeat]) - cards drawn by weight: card("Dice"), card("Food", 3), card("Food", 3, 1) none twice',
    "rands": "rands([seed]) - rands(42): the next rand() draws repeat; rands(): unpredictable again",
    "randbeta": "randbeta(a, b, [from, to]) - random in a Beta shape: randbeta(4, 4, 1, 10) a bell, whole 1 to 10",
    "randtri": "randtri(peak, [from, to]) - random in a triangle: randtri(3, 1, 10) peaks at 3",
    "randexp": "randexp(rate, [from, to]) - random falling (rate > 0) or rising (rate < 0): randexp(4, 0, 100)",
    "randpeaks": "randpeaks(gap, [from, to]) - random with two humps, gap 0 to 1: randpeaks(0.5, 1.0, 10)",
    "identity": "identity(n) - the n x n identity matrix: identity(3)",
    "zeros": "zeros(n) or zeros(rows, cols) - a list or a matrix of 0: zeros(2, 3)",
    "ones": "ones(n) or ones(rows, cols) - a list or a matrix of 1: ones(3)",
    "diag": "diag(list) - a matrix with the list down its diagonal; diag(matrix) - its diagonal",
    "seq": "seq(from, to, [step]) - a list: seq(1, 5) = [1, 2, 3, 4, 5], seq(0, 1, 0.25)",
    "randm": "randm(rows, cols, [from, to]) - a random matrix, each cell as rand(): randm(3, 3, 1, 6)",
    "rows": "rows(m) - number of rows (a list has 1)",
    "cols": "cols(m) - number of columns (a list: its length)",
    "len": "len(m) - number of cells: len([4, 5, 6]) = 3",
    "at": "at(list, n) or at(matrix, row, col) - one cell, counting from 1: at([5, 6, 7], 2) = 6",
    "row": "row(m, i) - row i as a list: row([[1, 2], [3, 4]], 2) = [3, 4]",
    "col": "col(m, j) - column j as a list: col([[1, 2], [3, 4]], 1) = [1, 3]",
    "transpose": "transpose(m) - rows become columns",
    "det": "det(m) - determinant of a square matrix: det([[1, 2], [3, 4]]) = -2",
    "inv": "inv(m) - inverse of a square matrix (same as m^-1)",
    "trace": "trace(m) - sum of the diagonal",
    "solve": "solve(A, b) - x with A*x = b: solve([[2, 1], [1, -1]], [5, 1]) = [2, 1]",
    "dot": "dot(a, b) - dot product of two lists: dot([1, 2, 3], [4, 5, 6]) = 32",
    "cross": "cross(a, b) - cross product of two lists of 3",
    "sum": "sum(...) - total of numbers and lists: sum([1, 2, 3]), sum(card(\"Dice\", 3))",
    "mean": "mean(...) - average of numbers and lists: mean([1, 2, 3, 4]) = 2.5",
    "prod": "prod(...) - numbers and lists multiplied: prod([1, 2, 3, 4]) = 24",
    "min": "min(...) - smallest of numbers and lists: min([3, 1, 2]) = 1, min(3, 9)",
    "max": "max(...) - largest of numbers and lists: max([3, 1, 2]) = 3, max(3, 9)",
    "sort": "sort(list) - smallest first: sort([3, 1, 2]) = [1, 2, 3]",
    "median": "median(...) - the middle value of numbers and lists: median([3, 1, 2, 10]) = 2.5",
    "stdev": "stdev(...) - standard deviation of a sample (n - 1), like Excel STDEV.S; stdevp for a whole population",
    "stdevp": "stdevp(...) - standard deviation of a whole population (n): stdevp([2, 4, 4, 4, 5, 5, 7, 9]) = 2",
    "variance": "variance(...) - variance of a sample (n - 1): variance(1, 2, 3, 4); variancep for a whole population",
    "variancep": "variancep(...) - variance of a whole population (n): variancep([1, 2, 3, 4]) = 1.25",
    "norm": "norm(x) - length of a list: norm([3, 4]) = 5; of a matrix, over every cell",
    "rank": "rank(m) - number of independent rows: rank([[1, 2], [2, 4]]) = 1",
    "eig": "eig(m) - eigenvalues of a square matrix (at most 20x20), largest first: eig([[4, 1], [2, 3]]) = [5, 2]",
    "pi": "pi - 3.14159...",
    "e": "e - 2.71828...",
    "i": "i (or j) - imaginary unit, e.g. 2+3i",
    "j": "j (or i) - imaginary unit",
    "today": "today - the current date; today+30 is 30 days later",
    "now": "now - the current date and time; now [longdatetime]",
    "answer": "answer (or ans) - the last result",
    "ans": "ans (or answer) - the last result",
}

LIST_HINT = "[1, 2, 3] is a list, [[1, 2], [3, 4]] a matrix (rows in brackets); 2*[1, 2], A*B, det(A)"
TAG_HINT = "[from->to] converts: [ft->in] [c->f] [h->min] [usd->thb] [utc->local]; [longdate] formats a date"
REF_HINT = "$1 = last result, $2 = the one before, ..."
MAX_NAMES = 6    # names listed for a word being typed

WORD_AT_END = re.compile(r"[A-Za-z_][A-Za-z0-9_]*\Z")
REF_AT_END = re.compile(r"\$\d*\Z")
# A literal ending at the caret, not part of a name, date, duration or $n
LITERAL_AT_END = re.compile(
    r"(?<![A-Za-z0-9_.:$])(0[xX][0-9A-Fa-f]+|0[bBoOdD][0-9]+|[01]+b|(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?)\Z")
# A rai-ngan-wa group with its tag before the caret: `4-1-50 [rnw`, `4-1-50 [rnw]`, `4-1-50 [rnw->m2]`
RNW_AT_END = re.compile(
    r"(?<![0-9.])(\d+(?:\.\d+)?-\d+(?:\.\d+)?-\d+(?:\.\d+)?)\s*\[\s*(?:rnw|rai-ngan-wa)(?:\s*->[^\]]*)?\]?\s*\Z",
    re.IGNORECASE)


def left_of(text, caret):
    """The text before the caret (a caret out of range is clamped)."""
    return text[:max(0, min(caret, len(text)))]


def literal_at(text, caret):
    """The number literal left of the caret, for the output-base hint, or None.

    A rai-ngan-wa group (`4-1-50 [rnw`) gives the whole group (owner 2026-11-08).
    """
    left = left_of(text, caret)
    group = RNW_AT_END.search(left)
    if group:
        return group.group(1)
    match = LITERAL_AT_END.search(left)
    return match.group(0) if match else None


def hint_for(text, caret, lookup_user):
    """The plain hint for the caret position, or None.

    lookup_user(name) describes a user function or variable, or returns None.
    """
    left = left_of(text, caret)

    #==================================================================
    #  BLOCK 1 - An open [ tag or a $ reference right before the caret
    #==================================================================
    open_at = open_bracket(left)
    if open_at >= 0 and not opens_list(left[:open_at]):
        return TAG_HINT
    if REF_AT_END.search(left):
        return REF_HINT

    #==================================================================
    #  BLOCK 2 - The word being typed: exact name, or the names it can become
    #==================================================================
    word = WORD_AT_END.search(left)
    if word:
        name = word.group(0)
        exact = describe(name, lookup_user)
        if exact is not None:
            return exact
        lowered = name.lower()
        starts = sorted(key for key in BUILTINS if len(key) > len(name) and key.startswith(lowered))
        if len(name) >= 2 and starts:
            return BUILTINS[starts[0]] if len(starts) == 1 else "  ".join(starts[:MAX_NAMES])

    #==================================================================
    #  BLOCK 3 - Inside an unclosed call: the innermost function
    #==================================================================
    depth = 0
    for index in range(len(left) - 1, -1, -1):
        char = left[index]
        if char == ")":
            depth += 1
        elif char == "(":
            if depth == 0:
                name = WORD_AT_END.search(left[:index])
                return describe(name.group(0), lookup_user) if name else None
            depth -= 1
    return LIST_HINT if open_at >= 0 else None


def open_bracket(left):
    """Where the innermost `[` still open at the caret is, or -1."""
    depth = 0
    for index in range(len(left) - 1, -1, -1):
        if left[index] == "]":
            depth += 1
        elif left[index] == "[":
            if depth == 0:
                return index
            depth -= 1
    return -1


def opens_list(before):
    """A `[` after this text opens a list: no value ends right before it (lexer.ends_value)."""
    before = before.rstrip()
    if before == "" or before[-1] in "+-*/^(,[=":
        return True
    word = WORD_AT_END.search(before)
    return bool(word) and word.group(0).lower() == "mod"


def describe(name, lookup_user):
    """A user name first, then a built-in one (any letter case)."""
    found = lookup_user(name)
    return found if found is not None else BUILTINS.get(name.lower())
