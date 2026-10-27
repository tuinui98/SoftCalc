"""softcalc_engine/lexer.py - Split calculator input into tokens.

Implements SPEC 5.1 and 5.2: whitespace separates tokens except inside
a digit run (`1 000` = 1000), C-style names, decimal / scientific /
hex / binary / octal literals, the `101b` binary suffix, imaginary
literals such as `2i` or `3j`, result references `$1`, and conversion
tags `[from->to]` (SPEC 5.9).
"""

import re
from dataclasses import dataclass

from .errors import CalcError

OPERATORS = "+-*/^()!%,="
NAME_START = re.compile(r"[A-Za-z_]")
NAME_CHARS = re.compile(r"[A-Za-z0-9_]*")
DECIMAL = re.compile(r"(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?")
PREFIXED = {"x": 16, "b": 2, "o": 8, "d": 10}
PREFIX_DIGITS = {
    16: re.compile(r"[0-9A-Fa-f]+"),
    2: re.compile(r"[01]+"),
    8: re.compile(r"[0-7]+"),
    10: re.compile(r"\d+"),
}
SUFFIX_BINARY = re.compile(r"[01]+b")


@dataclass
class Token:
    """One lexical token.

    kind is one of: number, imag, name, op, ref, tag, end. For numbers,
    text holds the literal and base its radix; value conversion happens
    later so the evaluator can pick the working precision. A ref holds
    the digits after `$`; a tag holds `from->to` in lower case.
    """

    kind: str
    text: str
    pos: int
    base: int = 10


def join_digit_spaces(text):
    """Remove whitespace that sits between two digits (`1 000` -> `1000`)."""
    return re.sub(r"(?<=\d)\s+(?=\d)", "", text)


def is_name_char(text, i):
    """True if text[i] exists and can continue a name."""
    # ASCII only: str.isalnum() would also accept Thai and other letters
    return i < len(text) and NAME_CHARS.fullmatch(text[i]) is not None


def read_number(text, i):
    """Read a numeric literal at text[i]; return (Token, next index)."""
    #==================================================================
    #  BLOCK 1 - Prefixed literals : 0x / 0b / 0o / 0d
    #==================================================================
    if text[i] == "0" and i + 1 < len(text) and text[i + 1].lower() in PREFIXED:
        base = PREFIXED[text[i + 1].lower()]
        match = PREFIX_DIGITS[base].match(text, i + 2)
        if not match:
            # Original crashed on a bare `0x`; the clone reports it [FIX]
            raise CalcError(f"Missing digits after '{text[i:i + 2]}'")
        end = match.end()
        if is_name_char(text, end):
            raise CalcError(f"Invalid digit in '{text[i:end + 1]}'")
        return Token("number", match.group(), i, base), end
    # end BLOCK 1

    #==================================================================
    #  BLOCK 2 - Binary suffix : 101b, only when not followed by a name char
    #==================================================================
    match = SUFFIX_BINARY.match(text, i)
    if match and not is_name_char(text, match.end()):
        return Token("number", match.group()[:-1], i, 2), match.end()
    # end BLOCK 2

    #==================================================================
    #  BLOCK 3 - Decimal and scientific
    #==================================================================
    match = DECIMAL.match(text, i)
    end = match.end()
    if end < len(text) and text[end] == ".":
        raise CalcError(f"Malformed number near '{text[i:end + 1]}'")
    return Token("number", match.group(), i), end
    # end BLOCK 3


def read_tag(text, i):
    """Read `[from->to]` at text[i]; units may contain `/` (mi/h)."""
    close = text.find("]", i)
    if close < 0:
        raise CalcError("Missing ']' in conversion")
    body = "".join(text[i + 1:close].split()).lower()
    source, arrow, target = body.partition("->")
    if not arrow:
        raise CalcError("Conversion needs '->' inside [ ]")
    return Token("tag", f"{source}->{target}", i), close + 1


def tokenize(text):
    """Convert input text into a list of Tokens ending with an `end` token."""
    text = join_digit_spaces(text)
    tokens = []
    i = 0
    while i < len(text):
        ch = text[i]
        if ch.isspace():
            i += 1
            continue
        if ch.isdigit() or (ch == "." and i + 1 < len(text) and text[i + 1].isdigit()):
            token, i = read_number(text, i)
            # A number directly followed by i or j (not a longer name) is imaginary
            if (token.base == 10 and i < len(text) and text[i] in "ijIJ"
                    and not is_name_char(text, i + 1)):
                token.kind = "imag"
                i += 1
            tokens.append(token)
            continue
        if NAME_START.match(ch):
            match = NAME_CHARS.match(text, i + 1)
            tokens.append(Token("name", text[i:match.end()], i))
            i = match.end()
            continue
        if ch in OPERATORS:
            tokens.append(Token("op", ch, i))
            i += 1
            continue
        if ch == "$":
            match = re.compile(r"\d+").match(text, i + 1)
            if not match:
                raise CalcError("Expected a number after '$'")
            tokens.append(Token("ref", match.group(), i))
            i = match.end()
            continue
        if ch == "[":
            token, i = read_tag(text, i)
            tokens.append(token)
            continue
        raise CalcError(f"Unknown character '{ch}'")
    tokens.append(Token("end", "", len(text)))
    return tokens
