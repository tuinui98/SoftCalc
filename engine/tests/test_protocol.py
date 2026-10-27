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
    assert (response["kind"], response["display_input"], response["text"]) == ("result", "2*5+1", "11")
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


@pytest.mark.parametrize("arguments", [
    {"principal": "1000", "annual_rate": "5", "years": "0"},
    {"principal": "abc", "annual_rate": "5", "years": "1"},
    {"principal": "1000", "annual_rate": "5", "years": "1", "compounding": "hourly"},
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


FROZEN_EXE = ENGINE_DIR / "dist" / "softcalc-engine.exe"


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
