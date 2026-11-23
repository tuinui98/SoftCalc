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
    "pi": "pi - 3.14159...",
    "e": "e - 2.71828...",
    "i": "i (or j) - imaginary unit, e.g. 2+3i",
    "j": "j (or i) - imaginary unit",
    "today": "today - the current date; today+30 is 30 days later",
    "now": "now - the current date and time; now [longdatetime]",
    "answer": "answer (or ans) - the last result",
    "ans": "ans (or answer) - the last result",
}

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
    open_at = left.rfind("[")
    if open_at >= 0 and "]" not in left[open_at:]:
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
    return None


def describe(name, lookup_user):
    """A user name first, then a built-in one (any letter case)."""
    found = lookup_user(name)
    return found if found is not None else BUILTINS.get(name.lower())
