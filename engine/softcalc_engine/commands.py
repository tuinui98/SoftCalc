"""commands.py - Settings typed in Input: a line starting with ':' (owner 2026-11-26).

`:deg`, `:hex`, `:prec 64`, `:digits 2`, `:pad on`, `:rpn off`, `:word 16`,
several on one line (`:deg :prec 64`), a short or a full name each
(`:prec` / `:precision`). A bare `:` shows them all, as the line that
sets them again. The line is caught
before parsing, in infix and RPN alike; it changes the engine's Settings,
which the UI reads back from the eval reply (server `eval` "settings")
and keeps as it keeps a menu's change. It gives no value, so no `$n`.
"""

from .errors import CalcError

PREFIX = ":"
PRECISIONS = (32, 64, 128, 512)
WORD_SIZES = (8, 16, 32, 64)
MAX_DECIMALS = 512

# A word that sets a value by itself
ANGLES = {"deg": "deg", "degree": "deg", "degrees": "deg",
          "rad": "rad", "radian": "rad", "radians": "rad",
          "grad": "grad", "gradian": "grad", "gradians": "grad"}
BASES = {"dec": "dec", "decimal": "dec", "hex": "hex", "hexadecimal": "hex",
         "bin": "bin", "binary": "bin", "oct": "oct", "octal": "oct"}
INFIX = ("infix",)

# A word followed by its value
PRECISION = ("prec", "precision")
DECIMALS = ("digits", "decimals", "places")
PAD = ("pad", "padzeros", "pad_zeros")
RPN = ("rpn",)
WORD = ("word", "wordsize", "word_size")
ANGLE = ("angle",)
BASE = ("base",)
ON = ("on", "yes", "1", "true")
OFF = ("off", "no", "0", "false")
ALL = ("all", "off", "auto")

ANGLE_NAMES = {"deg": "Degrees", "rad": "Radians", "grad": "Gradians"}
BASE_NAMES = {"dec": "Decimal", "hex": "Hexadecimal", "bin": "Binary", "oct": "Octal"}
USAGE = (":deg :rad :grad, :dec :hex :bin :oct, :prec 32|64|128|512, "
         ":digits 0-512|all, :pad on|off, :rpn on|off, :word 8|16|32|64")


def is_command(text):
    """A settings line: ':' first, spaces before it allowed."""
    return text.lstrip().startswith(PREFIX)


def parse(text):
    """The changes a settings line asks for, in the order typed: [(name, value)].

    Raises CalcError naming what is wrong and what is allowed.
    """
    words = text.replace(PREFIX, " ").replace("=", " ").lower().split()
    changes = []
    index = 0
    while index < len(words):
        word = words[index]
        index += 1
        if word in ANGLES:
            changes.append(("angle", ANGLES[word]))
            continue
        if word in BASES:
            changes.append(("base", BASES[word]))
            continue
        if word in INFIX:
            changes.append(("rpn", False))
            continue
        value = words[index] if index < len(words) else None
        if word in RPN + PAD:
            name = "rpn" if word in RPN else "pad_zeros"
            if value in ON or value in OFF:
                index += 1
                changes.append((name, value in ON))
            else:
                changes.append((name, True))    # a bare :rpn or :pad turns it on
            continue
        if word not in PRECISION + DECIMALS + WORD + ANGLE + BASE:
            raise CalcError(f"Unknown setting '{word}'; settings are {USAGE}")
        if value is None:
            raise CalcError(f"':{word}' needs a value; settings are {USAGE}")
        index += 1
        changes.append(valued(word, value))
    return changes


def valued(word, value):
    """(name, value) for a word that takes the next word as its value."""
    if word in ANGLE:
        if value not in ANGLES:
            raise CalcError("The angle is deg, rad or grad")
        return "angle", ANGLES[value]
    if word in BASE:
        if value not in BASES:
            raise CalcError("The base is dec, hex, bin or oct")
        return "base", BASES[value]
    if word in DECIMALS:
        if value in ALL:
            return "decimals", None
        number = whole(value)
        if number is None or not 0 <= number <= MAX_DECIMALS:
            raise CalcError(f"Decimal places are 0 to {MAX_DECIMALS}, or all")
        return "decimals", number
    number = whole(value)
    if word in PRECISION:
        if number not in PRECISIONS:
            raise CalcError("Precision is 32, 64, 128 or 512")
        return "precision", number
    if number not in WORD_SIZES:
        raise CalcError("The word size is 8, 16, 32 or 64")
    return "word_size", number


def whole(text):
    return int(text) if text.isdigit() else None


# The hint for ':' itself: short enough not to be cut off (the values come after a word)
SHORT_HINT = ":deg rad grad  :dec hex bin oct  :prec  :digits  :pad  :rpn  :word  (: alone shows them)"
# What a word's value can be, for the hint line
CHOICES = {PRECISION: "32 64 128 512", DECIMALS: "0 to 512, or all", WORD: "8 16 32 64",
           RPN: "on off", PAD: "on off (zeros up to the decimal places)", ANGLE: "deg rad grad",
           BASE: "dec hex bin oct"}


def hint(left):
    """The hint line for a settings line, the text left of the caret."""
    words = left.replace(PREFIX, " ").replace("=", " ").lower().split()
    if left.endswith((" ", "=")) and words:
        for names, choices in CHOICES.items():
            if words[-1] in names:
                return f":{names[0]} - {choices}"
    return SHORT_HINT


def describe(name, value):
    """One setting as History shows it: Precision 64, Degrees, 2 decimal places..."""
    if name == "precision":
        return f"Precision {value}"
    if name == "angle":
        return ANGLE_NAMES[value]
    if name == "base":
        return BASE_NAMES[value]
    if name == "rpn":
        return "RPN" if value else "Infix"
    if name == "decimals":
        return "All digits" if value is None else f"{value} decimal place{'' if value == 1 else 's'}"
    if name == "pad_zeros":
        return f"Pad zeros {'on' if value else 'off'}"
    return f"Word size {value}"


def describe_all(settings):
    """Every setting, for a bare ':', as the line that sets them all again (short enough for History)."""
    decimals = "all" if settings.decimals is None else settings.decimals
    return (f":prec {settings.precision} :{settings.angle} :{settings.base} :rpn {'on' if settings.rpn else 'off'} "
            f":digits {decimals} :pad {'on' if settings.pad_zeros else 'off'} :word {settings.word_size}")
