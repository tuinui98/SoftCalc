"""softcalc_engine/errors.py - Error type raised for any invalid input.

Every user-facing failure (syntax, unknown name, maths domain) is a
CalcError; the engine turns it into an `ERROR:<message>` History line.
"""


class CalcError(Exception):
    """A calculation failed; the message is shown to the user."""
