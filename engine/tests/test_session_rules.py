"""tests/test_session_rules.py - M2 rules the golden data does not cover.

Variables, functions, result references, conversions, RPN, and output
bases beyond what the original recordings exercise.
"""

import pytest

from softcalc_engine.engine import Engine


def run(*lines, **settings):
    """Evaluate lines in one fresh engine; return the last EvalResult."""
    engine = Engine()
    for key, value in settings.items():
        setattr(engine.settings, key, value)
    result = None
    for line in lines:
        result = engine.evaluate(line)
    return result


def test_result_references_count_back_from_latest():
    engine = Engine()
    for line in ("1", "2", "3"):
        engine.evaluate(line)
    assert engine.evaluate("$1").text == "3"
    # $1 above became the latest result, so $3 now points at "2"
    assert engine.evaluate("$3").text == "2"


def test_definitions_do_not_count_as_results():
    engine = Engine()
    engine.evaluate("7")
    engine.evaluate("a=5")
    assert engine.evaluate("$1").text == "7"


def test_result_reference_after_clear_is_error():
    engine = Engine()
    engine.evaluate("7")
    engine.clear_history()
    assert engine.evaluate("$1").kind == "error"
    assert engine.evaluate("answer").text == "7"


@pytest.mark.parametrize("line", ["pi=3", "i=2", "ans=1", "mod=1", "sqrt(x)=x", "f(pi)=pi"])
def test_reserved_names_cannot_be_defined(line):
    assert run(line).kind == "error"


def test_name_keeps_first_spelling():
    engine = Engine()
    engine.evaluate("Rate=5")
    engine.evaluate("RATE=6")
    assert engine.variables["rate"] == ("Rate", engine.variables["rate"][1])
    assert engine.evaluate("rate").text == "6"


def test_variable_and_function_names_do_not_clash():
    assert run("f(x)=x", "f=2").kind == "error"
    assert run("v=2", "v(x)=x").kind == "error"


def test_mutual_recursion_stops():
    result = run("a(x)=b(x)", "b(x)=a(x)", "a(1)")
    assert result.kind == "error"


def test_variable_times_paren_is_multiplication():
    assert run("a=3", "a(2+1)").text == "9"


def test_function_parameters_shadow_variables():
    assert run("x=100", "sq(x)=x*x", "sq(3)").text == "9"


@pytest.mark.parametrize("line, want", [
    ("1 [IN->CM]", "2.54"),
    ("1 [lb->g]", "453.59237"),
    ("16 [oz->lb]", "1"),
    ("1 [mi/h->km/h]", "1.609344"),
    ("1 [d->min]", "1440"),
    ("(1 [ft->in])*2", "24"),
    ("1 [ft->in]*2", "24"),
    ("212 [f->c]", "100"),
])
def test_conversions(line, want):
    assert run(line).text == want


@pytest.mark.parametrize("line", ["1 [in->s]", "1 [in->]", "i [in->cm]", "1 [in-cm]", "1 [in->cm"])
def test_conversion_errors(line):
    assert run(line).kind == "error"


@pytest.mark.parametrize("line, want", [
    ("zzg(x,y)=x*y", "Function Added"),
])
def test_definition_messages(line, want):
    assert run(line).text == want


def test_rpn_user_function_takes_its_arity():
    assert run("h(a,b)=a-b", "10 3 h", rpn=True).text == "7"


@pytest.mark.parametrize("line, want", [
    ("2 3 mod", "2"),
    ("5 !", "120"),
    ("50 %", "0.5"),
    ("-2 2 ^", "4"),
])
def test_rpn_extra_operators(line, want):
    assert run(line, rpn=True).text == want


@pytest.mark.parametrize("line, want", [
    ("15", "0o17"),
    ("-8", "-0o10"),
    ("0.5", "0o0.4"),
    ("8^40", "0o1*0o10^0o50"),
])
def test_octal_output(line, want):
    assert run(line, base="oct").text == want


def test_hex_output_keeps_all_digits_at_512():
    text = run("1/3", base="hex", precision=512).text
    assert text == "0x0." + "5" * 512
