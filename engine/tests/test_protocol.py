"""tests/test_protocol.py - The JSON-lines server (design/protocol.md).

Most tests drive the Dispatcher directly; TestProcess starts the real
server as a child process and talks to it over pipes, as the UI will.
"""

import json
import subprocess
import sys
from pathlib import Path

import pytest

from softcalc_engine.server import CATALOG, Dispatcher

ENGINE_DIR = Path(__file__).resolve().parents[1]


@pytest.fixture
def dispatcher():
    return Dispatcher()


def ask(dispatcher, op, **arguments):
    return dispatcher.handle({"id": 7, "op": op, **arguments})


#----------------------------------------------------------------------
#  Envelope
#----------------------------------------------------------------------
def test_response_echoes_id(dispatcher):
    response = ask(dispatcher, "eval", input="1+1")
    assert response["id"] == 7 and response["ok"] is True


@pytest.mark.parametrize("request_obj, fragment", [
    ({"id": 1, "op": "nope"}, "unknown op"),
    ({"id": 1, "op": "eval"}, "missing argument 'input'"),
    ({"id": 1, "op": "eval", "input": 5}, "'input' must be str"),
    ({"id": 1, "op": "settings", "precision": 33}, "'precision' must be one of"),
    ({"id": 1, "op": "settings", "rpn": "yes"}, "'rpn' must be bool"),
    ({"id": 1, "op": "settings", "precision": True}, "'precision' must be int"),
    ({"id": 1, "op": "delete", "name": "pi"}, "No user variable"),
    ({"id": 1, "op": "plot", "function": "nope", "start": 0, "end": 1}, "not a one-parameter"),
    (["not", "an", "object"], "must be a JSON object"),
])
def test_bad_requests(dispatcher, request_obj, fragment):
    response = dispatcher.handle(request_obj)
    assert response["ok"] is False
    assert fragment in response["error"]


#----------------------------------------------------------------------
#  Operations
#----------------------------------------------------------------------
def test_eval_reports_kinds(dispatcher):
    assert ask(dispatcher, "eval", input="x=5")["kind"] == "definition"
    response = ask(dispatcher, "eval", input="2x+1")
    assert (response["kind"], response["display_input"], response["text"]) == ("result", "(2*5)+1", "11")
    assert ask(dispatcher, "eval", input="1/0")["kind"] == "error"
    assert ask(dispatcher, "eval", input="")["kind"] == "empty"


def test_settings_change_output(dispatcher):
    settings = ask(dispatcher, "settings", base="hex", precision=64)
    assert settings["base"] == "hex" and settings["precision"] == 64
    assert ask(dispatcher, "eval", input="255")["text"] == "0xFF"
    assert ask(dispatcher, "settings")["base"] == "hex"


def test_state_lists_definitions(dispatcher):
    ask(dispatcher, "eval", input="Rate=0.05")
    ask(dispatcher, "eval", input="grow(p, n)=p*(1+Rate)^n")
    state = ask(dispatcher, "state")
    assert {"name": "Rate", "value": "0.05"} in state["variables"]
    assert state["constants"][0]["name"] == "pi"
    grow = next(f for f in state["functions"] if f["name"] == "grow")
    assert grow["params"] == ["p", "n"]
    assert grow["head"] == "grow(p, n)=" and grow["body"] == "p*(1+Rate)^n"


def test_delete(dispatcher):
    ask(dispatcher, "eval", input="a=1")
    assert ask(dispatcher, "delete", name="A")["deleted"] == "variable"
    assert ask(dispatcher, "delete", name="g")["deleted"] == "function"
    assert ask(dispatcher, "eval", input="a")["kind"] == "error"


def test_clear_history(dispatcher):
    ask(dispatcher, "eval", input="7")
    ask(dispatcher, "clear_history")
    assert ask(dispatcher, "eval", input="$1")["kind"] == "error"
    assert ask(dispatcher, "eval", input="answer")["text"] == "7"


def test_catalog(dispatcher):
    response = ask(dispatcher, "catalog")
    assert response["conversions"]["Time zone"] == ["utc", "local"]
    assert "fv" in response["functions"]["Finance Functions"]
    assert response["functions"] == CATALOG["functions"]


@pytest.mark.parametrize("arguments, want", [
    ({"kind": "operator", "symbol": "+", "text": "1", "caret": 1}, ("1+", 2)),
    ({"kind": "operator", "symbol": "*", "text": "12", "caret": 1}, ("1*2", 2)),
    ({"kind": "function", "name": "sqrt", "text": ""}, ("sqrt()", 5)),
    ({"kind": "conversion", "source": "in", "target": "cm", "text": "1"}, ("1 [in->cm]", 10)),
])
def test_menu_insert(dispatcher, arguments, want):
    response = ask(dispatcher, "menu_insert", **arguments)
    assert (response["text"], response["caret"]) == want


def test_plot_xy_and_gaps(dispatcher):
    ask(dispatcher, "eval", input="r(x)=sqrt(x)")
    points = ask(dispatcher, "plot", function="r", start=-1, end=1, samples=3)["points"]
    assert points[0] is None                  # sqrt(-1) is complex
    assert points[1] == [0.0, 0.0]
    assert points[2] == [1.0, 1.0]


def test_plot_polar(dispatcher):
    ask(dispatcher, "eval", input="one(t)=1")
    points = ask(dispatcher, "plot", function="one", mode="polar", start=0, end=3.141592653589793, samples=3)["points"]
    assert points[0] == pytest.approx([1.0, 0.0])
    assert points[1] == pytest.approx([0.0, 1.0], abs=1e-12)


def test_value_at_full_precision(dispatcher):
    ask(dispatcher, "eval", input="r(x)=sqrt(x)")
    reply = ask(dispatcher, "value_at", function="r", x=2)
    assert reply["text"] == "1.4142135623730950488016887242097"    # 32 digits, like a result
    assert reply["value"] == pytest.approx(1.41421356)
    undefined = ask(dispatcher, "value_at", function="r", x=-1)
    assert (undefined["text"], undefined["value"]) == (None, None)
    assert ask(dispatcher, "eval", input="answer")["kind"] == "error"    # tracing is not a calculation


def test_literal_op(dispatcher):
    assert ask(dispatcher, "literal", text="0x1F")["text"] == "31"
    assert ask(dispatcher, "literal", text="12")["text"] is None


def test_pcf_round_trip(dispatcher):
    ask(dispatcher, "eval", input="k=3")
    text = ask(dispatcher, "pcf_export")["text"]
    other = Dispatcher()
    response = ask(other, "pcf_import", text=text + "bad line\r\n")
    assert response["skipped"][0]["text"] == "bad line"
    assert ask(other, "eval", input="k*2")["text"] == "6"


def test_schedule(dispatcher):
    response = ask(dispatcher, "schedule", principal="1000", annual_rate="12", years="1",
                   compounding="quarterly")
    assert len(response["rows"]) == 4
    assert response["future_value"] == "1125.50881"
    assert response["total_interest"] == "125.50881"
    assert response["series"]["interest"][-1] == pytest.approx(125.50881)


def test_schedule_with_contributions(dispatcher):
    response = ask(dispatcher, "schedule", principal="0", annual_rate="0", years="1",
                   compounding="monthly", contribution="100", contribution_freq="monthly")
    assert response["future_value"] == "1200"
    assert response["series"]["contributions"][-1] == 1200


#----------------------------------------------------------------------
#  Rate and length units (owner 2026-11-28)
#----------------------------------------------------------------------
def test_schedule_units_default_to_yearly_and_years(dispatcher):
    response = ask(dispatcher, "schedule", principal="1000", annual_rate="12", years="1",
                   compounding="quarterly")
    assert (response["length_unit"], response["periods_per_unit"]) == ("years", 4)


def test_schedule_monthly_rate_compounded_monthly(dispatcher):
    # 1% a month, monthly, 12 months: 1.01^12
    response = ask(dispatcher, "schedule", principal="1000", annual_rate="1", years="12",
                   compounding="monthly", rate_unit="monthly", length_unit="months")
    assert len(response["rows"]) == 12
    assert float(response["future_value"]) == pytest.approx(1126.82503013197)
    assert (response["length_unit"], response["periods_per_unit"]) == ("months", 1)


def test_schedule_daily_rate_compounds_over_a_longer_period(dispatcher):
    # 0.1% a day, yearly compounding, 2 years: 1.001^730, not 1 + 0.365 twice
    response = ask(dispatcher, "schedule", principal="1000", annual_rate="0.1", years="2",
                   compounding="yearly", rate_unit="daily")
    assert float(response["future_value"]) == pytest.approx(2074.32384583546)


def test_schedule_yearly_rate_split_as_before(dispatcher):
    # 12% a year monthly over 1 year: 1.01^12, the same as before the units
    response = ask(dispatcher, "schedule", principal="1000", annual_rate="12", years="1",
                   compounding="monthly", rate_unit="yearly", length_unit="years")
    assert float(response["future_value"]) == pytest.approx(1126.82503013197)


def test_schedule_length_in_days_and_weeks(dispatcher):
    days = ask(dispatcher, "schedule", principal="1000", annual_rate="5", years="365",
               compounding="monthly", length_unit="days")
    assert len(days["rows"]) == 12
    assert days["periods_per_unit"] == pytest.approx(12 / 365)
    weeks = ask(dispatcher, "schedule", principal="1000", annual_rate="0", years="10",
                compounding="daily", length_unit="weeks")
    assert len(weeks["rows"]) == 70


def test_schedule_part_period_is_cut(dispatcher):
    # 1.9 years yearly: one year; 100 days monthly: 3 months (a month is 365/12 days)
    years = ask(dispatcher, "schedule", principal="1000", annual_rate="5", years="1.9")
    assert len(years["rows"]) == 1
    days = ask(dispatcher, "schedule", principal="1000", annual_rate="5", years="100",
               compounding="monthly", length_unit="days")
    assert len(days["rows"]) == 3
    short = ask(dispatcher, "schedule", principal="1000", annual_rate="5", years="10",
                compounding="monthly", length_unit="days")
    assert short["error"].startswith("years: shorter than one month")


def test_schedule_weekly(dispatcher):
    # owner 2026-11-28: weekly for compounding and contributions; 52 weeks of a 365-day year
    response = ask(dispatcher, "schedule", principal="0", annual_rate="0", years="1",
                   compounding="weekly", contribution="10", contribution_freq="weekly")
    assert len(response["rows"]) == 52
    assert response["periods_per_year"] == pytest.approx(365 / 7)


def test_schedule_table_decimals(dispatcher):
    # owner 2026-11-28: the table's own places; the totals keep the View's
    two = ask(dispatcher, "schedule", principal="1000", annual_rate="12", years="1",
              compounding="monthly", table_decimals=2)
    assert two["rows"][-1]["end"] == "1126.83"
    assert two["rows"][0]["contribution"] == "0.00"
    assert two["future_value"] == "1126.83"    # owner 2026-11-29: the totals take the places too
    assert two["total_contributions"] == "0.00"
    every = ask(dispatcher, "schedule", principal="1000", annual_rate="12", years="1",
                compounding="monthly", table_decimals="all")
    assert every["rows"][-1]["end"].startswith("1126.825030")
    assert ask(dispatcher, "schedule", principal="1000", annual_rate="5", years="1",
               table_decimals=-1)["ok"] is False


#----------------------------------------------------------------------
#  Goal mode (owner 2026-11-28): four fields give the fifth
#----------------------------------------------------------------------
GOAL = {"mode": "goal", "target": "10000000", "principal": "50000", "annual_rate": "0.5",
        "years": "20", "contribution": "15000", "rate_unit": "monthly", "length_unit": "years",
        "contribution_freq": "monthly"}


def goal(dispatcher, solve, **changes):
    return ask(dispatcher, "schedule", **{**GOAL, "solve": solve, **changes})


def test_goal_target_is_the_balance(dispatcher):
    response = goal(dispatcher, "target")
    assert response["solved"]["field"] == "target"
    assert response["solved"]["value"] == response["future_value"]
    assert len(response["rows"]) == 240


@pytest.mark.parametrize("solve", ["principal", "annual_rate", "contribution", "years"])
def test_goal_each_field_comes_back(dispatcher, solve):
    # work out the target, then each other field from it: each gives its value back
    target = goal(dispatcher, "target")["solved"]["value"]
    response = goal(dispatcher, solve, target=target, **{solve: ""})
    assert float(response["solved"]["value"]) == pytest.approx(float(GOAL[solve]), rel=1e-9)
    assert float(response["future_value"]) == pytest.approx(float(target), rel=1e-12)


def test_goal_places_and_every_value(dispatcher):
    # owner 2026-11-29: the worked-out field in the places, every digit kept beside it; all five back
    response = goal(dispatcher, "annual_rate", annual_rate="", table_decimals=2)
    solved = response["solved"]
    assert solved["value"] == "0.73"
    assert solved["exact"].startswith("0.72") or solved["exact"].startswith("0.73") and len(solved["exact"]) > 10
    assert solved["values"]["annual_rate"] == "0.73"
    assert solved["values"]["target"] == "10000000.00"
    assert solved["values"]["years"] == "20.00"
    assert response["future_value"] == "10000000.00"


def test_goal_principal_for_ten_million(dispatcher):
    # the owner's example: a 10M goal, the principal it needs
    response = goal(dispatcher, "principal", principal="")
    assert float(response["future_value"]) == pytest.approx(1e7, rel=1e-15)


def test_goal_length_ends_in_a_part_period(dispatcher):
    # 1000 at 10% a year to 2000: ln 2 / ln 1.1 = 7.2725 years, the last row 0.2725 of a year
    response = ask(dispatcher, "schedule", mode="goal", solve="years", target="2000", principal="1000",
                   annual_rate="10", rate_unit="yearly", length_unit="years")
    assert float(response["solved"]["value"]) == pytest.approx(7.272540897)
    assert response["solved"]["length_ymd"] == "7 y 3 m 8 d"
    assert len(response["rows"]) == 8
    assert response["rows"][-1]["part"] == "0.2725"
    assert response["partial"] == pytest.approx(0.272540897)
    assert float(response["future_value"]) == pytest.approx(2000, rel=1e-15)
    months = ask(dispatcher, "schedule", mode="goal", solve="years", target="2000", principal="1000",
                 annual_rate="10", rate_unit="yearly", length_unit="months")
    assert float(months["solved"]["value"]) == pytest.approx(7.272540897 * 12)


def test_goal_given_length_may_end_in_a_part_period(dispatcher):
    response = ask(dispatcher, "schedule", mode="goal", solve="target", principal="1000",
                   annual_rate="10", years="1.5", rate_unit="yearly")
    assert len(response["rows"]) == 2
    assert float(response["future_value"]) == pytest.approx(1000 * 1.1 ** 1.5)


def test_years_text():
    from softcalc_engine import finance
    import mpmath
    assert finance.years_text(mpmath.mpf("7.3")) == "7 y 3 m 18 d"
    assert finance.years_text(mpmath.mpf(2)) == "2 y"
    assert finance.years_text(mpmath.mpf(0)) == "0 d"


@pytest.mark.parametrize("changes, prefix", [
    ({"solve": "years", "annual_rate": "0", "contribution": "0", "years": ""}, "years:"),
    ({"solve": "years", "target": "50000", "years": ""}, "years:"),
    ({"solve": "annual_rate", "target": "-5", "annual_rate": ""}, "annual_rate:"),
    ({"solve": "principal", "target": "", "principal": ""}, "target:"),
    ({"solve": "target", "years": "0"}, "years:"),
])
def test_goal_errors_name_the_field(dispatcher, changes, prefix):
    response = ask(dispatcher, "schedule", **{**GOAL, **changes})
    assert response["ok"] is False
    assert response["error"].startswith(prefix)


@pytest.mark.parametrize("arguments", [
    {"principal": "1000", "annual_rate": "5", "years": "0"},
    {"principal": "abc", "annual_rate": "5", "years": "1"},
    {"principal": "1000", "annual_rate": "5", "years": "1", "compounding": "hourly"},
    {"principal": "1000", "annual_rate": "5", "years": "1", "rate_unit": "hourly"},
    {"principal": "1000", "annual_rate": "5", "years": "1", "length_unit": "decades"},
])
def test_schedule_errors(dispatcher, arguments):
    assert ask(dispatcher, "schedule", **arguments)["ok"] is False


#----------------------------------------------------------------------
#  Real child process over pipes
#----------------------------------------------------------------------
class TestProcess:
    def start(self):
        return subprocess.Popen(
            [sys.executable, "-m", "softcalc_engine"], cwd=ENGINE_DIR,
            stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
            encoding="utf-8", bufsize=1)

    def send(self, proc, obj):
        proc.stdin.write(json.dumps(obj, ensure_ascii=False) + "\n")
        proc.stdin.flush()
        return json.loads(proc.stdout.readline())

    def test_session_over_pipes(self):
        proc = self.start()
        try:
            ready = json.loads(proc.stdout.readline())
            assert ready == {"id": 0, "op": "ready", "protocol": 1, "version": ready["version"]}
            assert self.send(proc, {"id": 1, "op": "eval", "input": "2^3^2"})["text"] == "512"
            # non-ASCII survives the pipe in both directions
            response = self.send(proc, {"id": 2, "op": "eval", "input": "ก=1"})
            assert response["kind"] == "error" and response["input"] == "ก=1"
            proc.stdin.write("{not json\n")
            proc.stdin.flush()
            assert json.loads(proc.stdout.readline())["ok"] is False
            assert self.send(proc, {"id": 3, "op": "shutdown"}) == {"id": 3, "ok": True}
            assert proc.wait(timeout=10) == 0
        finally:
            if proc.poll() is None:
                proc.kill()

    def test_closing_stdin_ends_process(self):
        proc = self.start()
        proc.stdout.readline()
        proc.stdin.close()
        assert proc.wait(timeout=10) == 0


FROZEN_EXE = ENGINE_DIR / "dist" / "softcalc-engine" / "softcalc-engine.exe"    # onedir build (M9)


@pytest.mark.skipif(not FROZEN_EXE.exists(), reason="run build/build_engine.ps1 first")
def test_frozen_exe_answers():
    """The PyInstaller build behaves like `python -m softcalc_engine`."""
    proc = subprocess.Popen([str(FROZEN_EXE)], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                            stderr=subprocess.PIPE, encoding="utf-8", bufsize=1)
    try:
        assert json.loads(proc.stdout.readline())["op"] == "ready"
        lines = [{"id": 1, "op": "settings", "precision": 512},
                 {"id": 2, "op": "eval", "input": "1/3"},
                 {"id": 3, "op": "eval", "input": "longdate(27.10.2026)"},
                 {"id": 4, "op": "shutdown"}]
        proc.stdin.write("".join(json.dumps(line) + "\n" for line in lines))
        proc.stdin.flush()
        replies = [json.loads(proc.stdout.readline()) for _ in lines]
        assert replies[1]["text"] == "0." + "3" * 512
        assert replies[2]["text"] == "Tuesday, 27 October 2026"
        assert proc.wait(timeout=20) == 0
    finally:
        if proc.poll() is None:
            proc.kill()


def test_format_insert_and_catalog(dispatcher):
    response = ask(dispatcher, "menu_insert", kind="format", name="longdatetime", text="now")
    assert response["text"] == "now [longdatetime]"
    assert "longdatetime" in ask(dispatcher, "catalog")["formats"]


RESTORE_TEXTS = ["5", "0xFF", "Tuesday, 27 October 2026", "(3+4i)", "1:30:00", "27.10.2026", "1b*10b^101000b"]


def test_restore_results_counts_parsable_values(dispatcher):
    # the long date comes back as text: it keeps its `$n` place but is no value
    assert ask(dispatcher, "restore_results", texts=RESTORE_TEXTS)["restored"] == 6


@pytest.mark.parametrize("line, want", [
    ("answer", "1099511627776"),      # 2^40, parsed back from the binary form
    ("$1", "1099511627776"),
    ("$2", "27.10.2026"),
    ("$3", "1:30:00"),
    ("$4+1", "(4+4i)"),
    ("$5", "Tuesday, 27 October 2026"),    # text results count, as before the restart
    ("$6", "255"),
    ("$7", "5"),
])
def test_restore_results_rebuilds_answer_and_refs(line, want):
    fresh = Dispatcher()
    ask(fresh, "restore_results", texts=RESTORE_TEXTS)
    assert ask(fresh, "eval", input=line)["text"] == want


def test_restore_counts_like_the_live_session():
    live = Dispatcher()
    texts = [ask(live, "eval", input=line)["text"] for line in ["1", "27.10.2026 [longdate]", "2"]]
    fresh = Dispatcher()
    ask(fresh, "restore_results", texts=texts)
    assert ask(live, "eval", input="$3")["text"] == ask(fresh, "eval", input="$3")["text"] == "1"


def test_delete_result_renumbers(dispatcher):
    for line in ["10", "20", "30"]:
        ask(dispatcher, "eval", input=line)
    ask(dispatcher, "delete_result", n=2)    # the 20; now 10, 30
    assert ask(dispatcher, "eval", input="$2")["text"] == "10"    # adds 10 again
    ask(dispatcher, "delete_result", n=1)    # the newest; now 10, 30
    assert ask(dispatcher, "eval", input="answer")["text"] == "30"    # answer stays $1
    assert ask(dispatcher, "delete_result", n=9)["ok"] is False


def test_delete_last_result_keeps_answer(dispatcher):
    ask(dispatcher, "eval", input="7")
    ask(dispatcher, "delete_result", n=1)
    assert ask(dispatcher, "eval", input="$1")["kind"] == "error"
    assert ask(dispatcher, "eval", input="answer")["text"] == "7"


def test_restore_results_rejects_bad_payload(dispatcher):
    assert ask(dispatcher, "restore_results", texts="5")["ok"] is False
    assert ask(dispatcher, "restore_results", texts=[5])["ok"] is False


#----------------------------------------------------------------------
#  schedule errors name their field (Compound Interest window, M8)
#----------------------------------------------------------------------
@pytest.mark.parametrize("fields, prefix", [
    ({"principal": "1000+", "annual_rate": "5", "years": "10"}, "principal:"),
    ({"principal": "1000", "annual_rate": "nope", "years": "10"}, "annual_rate:"),
    ({"principal": "1000", "annual_rate": "5", "years": "0"}, "years:"),
    ({"principal": "1000", "annual_rate": "5", "years": "1000", "compounding": "daily"}, "years:"),
    ({"principal": "1000", "annual_rate": "5", "years": "1", "contribution": "i"}, "contribution:"),
])
def test_schedule_errors_name_the_field(dispatcher, fields, prefix):
    response = ask(dispatcher, "schedule", **fields)
    assert response["ok"] is False and response["error"].startswith(prefix)
