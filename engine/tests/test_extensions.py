"""tests/test_extensions.py - M3 engine extensions (extensions.md E7-E11, SPEC 8).

Dates and epoch, History display with values, display decimals, finance
functions and the Compound Interest schedule, and .pcf export/import.
"""

import mpmath
import pytest

from softcalc_engine import finance
from softcalc_engine.engine import Engine


def run(*lines, **settings):
    engine = Engine()
    for key, value in settings.items():
        setattr(engine.settings, key, value)
    result = None
    for line in lines:
        result = engine.evaluate(line)
    return result


#----------------------------------------------------------------------
#  E7 dates
#----------------------------------------------------------------------
@pytest.mark.parametrize("line, want", [
    ("28.11.2025 - 20.11.2025", "8"),
    ("28.11.2025 + 5", "03.12.2025"),
    ("5 + 28.11.2025", "03.12.2025"),
    ("01.03.2024 - 1", "29.02.2024"),
    ("01.01.2026 - 01.01.2025", "365"),
    ("27.10.2026 14:05 - 27.10.2026", "0.58680555555555555555555555555556"),
    ("27.10.2026 + 0.25", "27.10.2026 06:00:00"),
    ("longdate(27.10.2026)", "Tuesday, 27 October 2026"),
    ("longtime(27.10.2026 14:05:09)", "14:05:09"),
    ("longdatetime(01.01.2000 00:00)", "Saturday, 1 January 2000 00:00:00"),
    ("fromepoch(epoch(27.10.2026 14:05:09))", "27.10.2026 14:05:09"),
    ("epoch(02.01.1970) - epoch(01.01.1970)", "86400"),
])
def test_dates(line, want):
    assert run(line).text == want


@pytest.mark.parametrize("line", [
    "31.02.2025", "28.11.2025 * 2", "28.11.2025 + 20.11.2025", "-28.11.2025",
    "sin(28.11.2025)", "longdate(1+2i)", "28.11.2025 [d->s]", "longdate(1.1.2025)+1",
])
def test_date_errors(line):
    assert run(line).kind == "error"


def test_date_variables_and_answer():
    assert run("d=28.11.2025", "d+2").text == "30.11.2025"
    assert run("28.11.2025", "answer - 20.11.2025").text == "8"


def test_today_is_a_date_without_time():
    text = run("today").text
    assert len(text) == 10 and text.count(".") == 2


#----------------------------------------------------------------------
#  E10 History display
#----------------------------------------------------------------------
@pytest.mark.parametrize("lines, want", [
    (["x=5", "x*2"], "5*2"),
    (["x=5", "2x+x"], "2*5+5"),
    (["x=5", "x(2)"], "5*(2)"),
    (["x=-5", "x^2"], "(-5)^2"),
    (["x=5", "1 000 + x"], "1000 + 5"),
    (["7", "answer+ans"], "7+7"),
    (["1", "2", "$1*$2"], "2*1"),
    (["f(t)=t+1", "x=3", "f(x)"], "f(3)"),
    (["2*pi"], "2*pi"),
])
def test_display_input(lines, want):
    result = run(*lines)
    assert result.kind == "result"
    assert result.display_input == want


def test_display_keeps_original_input_for_recall():
    result = run("x=5", "x*2")
    assert result.input == "x*2"


def test_definitions_and_errors_display_as_typed():
    assert run("x=5").display_input == "x=5"
    assert run("1/0").display_input == "1/0"


#----------------------------------------------------------------------
#  Literals in another base than the output (owner 2026-11-07)
#----------------------------------------------------------------------
@pytest.mark.parametrize("line, base, want", [
    ("0x1F+1", "dec", "31+1"),
    ("101b*2", "dec", "5*2"),
    ("0o17", "dec", "15"),
    ("-0x1F", "dec", "-31"),            # the minus stays an operator
    ("2*-0x1F", "dec", "2*-31"),
    ("0x" + "F" * 34, "dec", "0x" + "F" * 34),    # past 32 digits: rounded, so kept
    ("255+1", "hex", "0xFF+0x1"),
    ("0.5", "hex", "0x0.8"),
    ("0.1+1", "hex", "0.1+0x1"),         # 0.1 never ends in hex: kept
    ("1.5", "hex", "0x1.8"),
    ("0d12", "hex", "0xC"),
    ("2pi", "hex", "0x2*pi"),           # implicit product made explicit
    ("2i", "hex", "2i"),                # imaginary literals are decimal only
    ("5+0.25", "bin", "101b+0.01b"),
    ("0x10+3", "hex", "0x10+0x3"),
    ("2(3)", "hex", "0x2*(0x3)"),
])
def test_other_base_literals(line, base, want):
    assert run(line, base=base).display_input == want


def test_same_base_literals_stay():
    result = run("3+4")
    assert result.display_input == result.input


def test_other_base_literal_next_to_variable():
    assert run("x=10", "2x+0x10", base="hex").display_input == "0x2*0xA+0x10"    # one `*`
    assert run("x=10", "x2", base="hex").display_input == "x2"    # x2 is a name, not x*2


def test_other_base_literals_rpn():
    assert run("255 1 +", base="hex", rpn=True).display_input == "0xFF 0x1 +"
    assert run("-255 2 *", base="hex", rpn=True).display_input == "-0xFF 0x2 *"


def test_literal_hint():
    engine = Engine()
    assert engine.literal_hint("0x1F") == "31"
    assert engine.literal_hint("255") is None          # already the output base
    assert engine.literal_hint("1+2") is None
    engine.settings.base = "hex"
    assert engine.literal_hint("255") == "0xFF"
    assert engine.literal_hint("0.1") is None
    assert engine.literal_hint("0x") is None             # unfinished literal


#----------------------------------------------------------------------
#  E9 display decimals
#----------------------------------------------------------------------
@pytest.mark.parametrize("line, decimals, pad, want", [
    ("1/3", 2, False, "0.33"),
    ("2/3", 2, False, "0.67"),
    ("1/4", 3, False, "0.25"),
    ("1/4", 3, True, "0.250"),
    ("5", 2, True, "5.00"),
    ("-2/3", 0, False, "-1"),
    ("3+4i", 1, True, "(3.0+4.0i)"),
    ("1e40", 2, True, "1e40"),
])
def test_display_decimals(line, decimals, pad, want):
    assert run(line, decimals=decimals, pad_zeros=pad).text == want


def test_decimals_do_not_change_stored_value():
    assert run("1/3", "answer*3", decimals=2).text == "1"


#----------------------------------------------------------------------
#  E11 finance
#----------------------------------------------------------------------
def simulate_fv(rate, nper, pmt, pv, kind):
    """Independent month-by-month simulation with Excel's sign convention."""
    balance = -pv
    for _ in range(nper):
        if kind:
            balance -= pmt
        balance *= 1 + rate
        if not kind:
            balance -= pmt
    return balance


@pytest.mark.parametrize("kind", [0, 1])
def test_fv_matches_simulation(kind):
    with mpmath.workdps(50):
        rate = mpmath.mpf("0.05") / 12
        want = simulate_fv(rate, 120, mpmath.mpf(-100), mpmath.mpf(-1000), kind)
        got = finance.fv(rate, mpmath.mpf(120), mpmath.mpf(-100), -1000, kind)
        assert abs(got - want) < mpmath.mpf(10) ** -40


@pytest.mark.parametrize("line, want", [
    ("pmt(0.05/12, 120, 10000)", "-106.06551523907523221827980442955"),
    ("fv(0, 10, -100)", "1000"),
    ("pv(0.05/12, 120, -100)", "9428.1350328234999626638560164822"),
    ("round_trip = fv(0.01, 24, -50, -500)", "Variable Added"),
])
def test_finance_values(line, want):
    assert run(line).text == want


def test_nper_inverts_fv():
    result = run("v = fv(0.01, 24, -50, -500)", "nper(0.01, -50, -500, v)")
    assert result.text == "24"


def test_pv_inverts_fv():
    assert run("v = fv(0.02, 10, 0, -1000)", "pv(0.02, 10, 0, v)").text == "-1000"


@pytest.mark.parametrize("line", ["fv(1, 2)", "fv(1,2,3,4,5,6)", "pmt(0.1, 0, 100)", "fv(i, 1, 1)"])
def test_finance_errors(line):
    assert run(line).kind == "error"


def test_schedule_matches_fv():
    with mpmath.workdps(50):
        plan = finance.schedule(1000, mpmath.mpf("0.06"), 10, "monthly", 100, "monthly", at_start=False)
        rate = mpmath.mpf("0.06") / 12
        assert len(plan.rows) == 120
        assert abs(plan.future_value - finance.fv(rate, 120, -100, -1000)) < mpmath.mpf(10) ** -40
        assert plan.total_contributions == 12000
        assert abs(plan.rows[-1].end - plan.future_value) == 0


def test_schedule_without_contribution_follows_compounding():
    with mpmath.workdps(50):
        plan = finance.schedule(1000, mpmath.mpf("0.12"), 1, "quarterly")
        assert len(plan.rows) == 4
        assert abs(plan.future_value - 1000 * mpmath.mpf("1.03") ** 4) < mpmath.mpf(10) ** -40


def test_schedule_continuous():
    with mpmath.workdps(50):
        plan = finance.schedule(1000, mpmath.mpf("0.05"), 2, "continuous")
        assert abs(plan.future_value - 1000 * mpmath.exp(mpmath.mpf("0.1"))) < mpmath.mpf(10) ** -30


#----------------------------------------------------------------------
#  SPEC 8 .pcf
#----------------------------------------------------------------------
def test_pcf_round_trip_keeps_values():
    source = Engine()
    for line in ("rate=0.05", "start=28.11.2025", "grow(p, n) = p*(1+rate)^n"):
        source.evaluate(line)
    text = source.export_pcf()
    assert "rate=0.05\r\n" in text
    assert "grow(p, n) = p*(1+rate)^n\r\n" in text
    target = Engine(load_defaults=False)
    assert target.import_pcf(text) == []
    assert target.evaluate("grow(100, 1)").text == "105"
    assert target.evaluate("start + 2").text == "30.11.2025"


def test_pcf_import_skips_bad_lines():
    engine = Engine()
    skipped = engine.import_pcf("a=1\r\nzzv=\r\nnot a line\r\nsin(x) = 1\r\nb(x) = x*a\r\n")
    assert [number for number, _, _ in skipped] == [2, 3, 4]
    assert engine.evaluate("b(3)").text == "3"
    # import replaces everything, including the default functions
    assert engine.evaluate("g(1)").kind == "error"


def test_pcf_import_allows_forward_calls():
    engine = Engine(load_defaults=False)
    assert engine.import_pcf("a(x) = b(x)+1\r\nb(x) = x*2\r\n") == []
    assert engine.evaluate("a(5)").text == "11"


def test_pcf_reads_original_layout():
    """Layout of the file the original saved (spec/behavior/session.md)."""
    original = "memory=\r\nzzpa=\r\ng(x) = x\r\nfunction(var) = var*cos(var)\r\nzzpg(a, b) = a^b\r\n"
    engine = Engine(load_defaults=False)
    skipped = engine.import_pcf(original)
    assert [number for number, _, _ in skipped] == [1, 2]
    assert engine.evaluate("zzpg(2, 10)").text == "1024"
