"""softcalc_engine/server.py - JSON-lines server for the UI (design/protocol.md).

Reads one request per line from stdin, answers one response per line on
stdout, in order. Calculation failures are normal responses
(`kind: "error"`); only malformed requests get `ok: false`.
"""

import json
import re
import sys

import mpmath

from . import __version__, currency, finance, menu, units
from .dates import ZONES
from .engine import Engine, Settings
from .errors import CalcError

PROTOCOL_VERSION = 1
SEED = re.compile(r"-?[0-9]{1,40}(\.[0-9]{1,40})?")
MAX_DRAWS = 10000
SETTING_TYPES = {
    "precision": int, "angle": str, "base": str, "rpn": bool,
    "decimals": (int, type(None)), "pad_zeros": bool, "word_size": int,
}
SETTING_CHOICES = {
    "precision": (32, 64, 128, 512),
    "word_size": (8, 16, 32, 64),
    "angle": ("rad", "deg", "grad"),
    "base": ("dec", "hex", "bin", "oct"),
}
CATALOG = {
    "functions": {
        "Basic Operations": ["+", "-", "*", "/", "^", "sqrt", "abs", "mod", "!", "%"],
        "Trig Functions": ["cos", "cosh", "acos", "acosh", "sin", "sinh", "asin", "asinh",
                           "tan", "tanh", "atan", "atanh"],
        "Log Functions": ["exp", "log10", "ln"],
        "Date Functions": ["epoch", "fromepoch", "longdate", "longtime", "longdatetime"],
        "Finance Functions": ["fv", "pv", "pmt", "nper"],
        "Two's Complement": ["twos", "signed"],
        "Rounding": ["round", "ceil", "floor"],
        "Random": ["rand", "rands", "randbeta", "randtri", "randexp", "randpeaks"],
    },
    "conversions": {**units.MENU, "Time zone": list(ZONES), "Currency": currency.COMMON},
    "currencies": currency.CODES,
    "formats": ["longdatetime", "longdate", "longtime", "epoch", "fromepoch"],
}


class RequestError(Exception):
    """The request itself is malformed; answered with ok: false."""


def argument(request, name, kind, default=None, required=True):
    """Fetch and type-check one request argument."""
    if name not in request:
        if required:
            raise RequestError(f"missing argument '{name}'")
        return default
    value = request[name]
    if kind is int and isinstance(value, bool):
        raise RequestError(f"'{name}' must be {kind.__name__}")
    if not isinstance(value, kind):
        expected = kind.__name__ if isinstance(kind, type) else "/".join(k.__name__ for k in kind)
        raise RequestError(f"'{name}' must be {expected}")
    return value


class Dispatcher:
    """Maps protocol operations onto one Engine."""

    def __init__(self, engine=None):
        self.engine = engine or Engine()
        self.handlers = {
            "eval": self.op_eval,
            "clear_history": self.op_clear_history,
            "settings": self.op_settings,
            "state": lambda request: self.engine.state(),
            "delete": self.op_delete,
            "catalog": lambda request: CATALOG,
            "menu_insert": self.op_menu_insert,
            "plot": self.op_plot,
            "value_at": self.op_value_at,
            "plot_equation": self.op_plot_equation,
            "equation_at": self.op_equation_at,
            "literal": self.op_literal,
            "delete_result": self.op_delete_result,
            "pcf_export": lambda request: {"text": self.engine.export_pcf()},
            "pcf_import": self.op_pcf_import,
            "restore_results": self.op_restore_results,
            "schedule": self.op_schedule,
            "set_rates": self.op_set_rates,
            "draws": self.op_draws,
            "shutdown": lambda request: {},
        }

    def handle(self, request):
        """Return the response object for one request object."""
        request_id = request.get("id") if isinstance(request, dict) else None
        try:
            if not isinstance(request, dict):
                raise RequestError("request must be a JSON object")
            op = request.get("op")
            handler = self.handlers.get(op)
            if handler is None:
                raise RequestError(f"unknown op '{op}'")
            result = handler(request)
        except (RequestError, CalcError) as exc:
            return {"id": request_id, "ok": False, "error": str(exc)}
        return {"id": request_id, "ok": True, **result}

    #------------------------------------------------------------------
    #  Operations
    #------------------------------------------------------------------
    def op_eval(self, request):
        result = self.engine.evaluate(argument(request, "input", str))
        return {"kind": result.kind, "input": result.input,
                "display_input": result.display_input, "text": result.text, "note": result.note}

    def op_clear_history(self, request):
        self.engine.clear_history()
        return {}

    def op_settings(self, request):
        settings = self.engine.settings
        for name, kind in SETTING_TYPES.items():
            if name not in request:
                continue
            value = argument(request, name, kind)
            if name in SETTING_CHOICES and value not in SETTING_CHOICES[name]:
                raise RequestError(f"'{name}' must be one of {SETTING_CHOICES[name]}")
            if name == "decimals" and value is not None and not 0 <= value <= 512:
                raise RequestError("'decimals' must be between 0 and 512")
            setattr(settings, name, value)
        return {name: getattr(settings, name) for name in Settings.__dataclass_fields__}

    def op_delete(self, request):
        return {"deleted": self.engine.delete(argument(request, "name", str))}

    def op_menu_insert(self, request):
        kind = argument(request, "kind", str)
        text = argument(request, "text", str)
        caret = argument(request, "caret", int, len(text), required=False)
        if not 0 <= caret <= len(text):
            raise RequestError("'caret' is outside the text")
        if kind == "operator":
            new_text, new_caret = menu.insert_operator(text, caret, argument(request, "symbol", str))
        elif kind == "function":
            new_text, new_caret = menu.insert_function(text, argument(request, "name", str))
        elif kind == "format":
            new_text, new_caret = menu.insert_format(text, argument(request, "name", str))
        elif kind == "currency_from":
            new_text, new_caret = menu.insert_currency_from(text, argument(request, "code", str))
        elif kind == "currency_to":
            new_text, new_caret = menu.insert_currency_to(text, argument(request, "code", str))
        elif kind == "conversion":
            new_text, new_caret = menu.insert_conversion(
                text, argument(request, "source", str), argument(request, "target", str))
        else:
            raise RequestError(f"unknown menu_insert kind '{kind}'")
        return {"text": new_text, "caret": new_caret}

    def op_plot(self, request):
        mode = argument(request, "mode", str, "xy", required=False)
        if mode not in ("xy", "polar"):
            raise RequestError("'mode' must be xy or polar")
        start = argument(request, "start", (int, float))
        end = argument(request, "end", (int, float))
        samples = argument(request, "samples", int, 400, required=False)
        if samples > 100000:
            raise RequestError("'samples' is too large")
        points = self.engine.plot(argument(request, "function", str), mode, start, end, samples)
        return {"points": points}

    def op_value_at(self, request):
        t = argument(request, "x", (int, float, str))    # text: the x as the status line shows it
        text, value = self.engine.value_at(argument(request, "function", str), t)
        return {"text": text, "value": value}

    def op_plot_equation(self, request):
        cols = argument(request, "cols", int, 60, required=False)
        rows = argument(request, "rows", int, 45, required=False)
        angle = argument(request, "angle", (str, type(None)), None, required=False)
        if angle is not None and angle not in SETTING_CHOICES["angle"]:
            raise RequestError(f"'angle' must be one of {SETTING_CHOICES['angle']}")
        segments = self.engine.plot_equation(
            argument(request, "equation", str),
            argument(request, "x_min", (int, float)), argument(request, "x_max", (int, float)),
            argument(request, "y_min", (int, float)), argument(request, "y_max", (int, float)), cols, rows, angle)
        return {"segments": segments}

    def op_equation_at(self, request):
        axis = argument(request, "axis", str)
        if axis not in ("x", "y"):
            raise RequestError("'axis' must be x or y")
        found = self.engine.equation_at(
            argument(request, "equation", str), axis, argument(request, "value", str),
            argument(request, "guess", (int, float)), argument(request, "window", (int, float)))
        if found is None:
            return {"x": None, "y": None, "x_value": None, "y_value": None}
        x_text, y_text, x_value, y_value = found
        return {"x": x_text, "y": y_text, "x_value": x_value, "y_value": y_value}

    def op_literal(self, request):
        return {"text": self.engine.literal_hint(argument(request, "text", str))}

    def op_delete_result(self, request):
        self.engine.delete_result(argument(request, "n", int))
        return {}

    def op_restore_results(self, request):
        texts = argument(request, "texts", list)
        if not all(isinstance(text, str) for text in texts):
            raise RequestError("'texts' must be a list of strings")
        return {"restored": self.engine.restore_results(texts)}

    def op_draws(self, request):
        """The Random window's Seed (owner 2026-11-12): rands(seed), then
        `line` count times, exactly as if typed in Input in that order.
        unique: a text already drawn is drawn again (No repeats).
        """
        seed = argument(request, "seed", str)
        line = argument(request, "line", str)
        count = argument(request, "count", int)
        unique = argument(request, "unique", bool, False, required=False)
        if not SEED.fullmatch(seed):
            raise RequestError("'seed' must be a number such as 42 or -7.5")
        if not 1 <= count <= MAX_DRAWS:
            raise RequestError(f"'count' must be 1 to {MAX_DRAWS}")
        self.engine.evaluate(f"rands({seed})")
        texts, seen = [], set()
        tries = 0
        while len(texts) < count:
            tries += 1
            if unique and tries > count * 50 + 1000:
                raise RequestError("not enough different values")
            result = self.engine.evaluate(line)
            if result.kind != "result" or result.text.startswith("ERROR:"):
                raise RequestError(result.text.removeprefix("ERROR:") or "the line gives no value")
            if unique and result.text in seen:
                continue
            seen.add(result.text)
            texts.append(result.text)
        self.engine.clear_history()    # the draws are not $n results
        return {"texts": texts}

    def op_set_rates(self, request):
        date = argument(request, "date", (str, type(None)), None, required=False)
        rates = argument(request, "rates", dict, {}, required=False)
        for code, rate in rates.items():
            if not isinstance(rate, (int, float, str)) or isinstance(rate, bool) or len(code) != 3:
                raise RequestError("'rates' must map 3-letter codes to numbers")
            try:
                positive = mpmath.mpf(rate) > 0
            except ValueError:
                positive = False
            if not positive:
                raise RequestError(f"rate for '{code}' must be a positive number")
        if rates and date is None:
            raise RequestError("'date' is needed with 'rates'")
        stale = argument(request, "stale", bool, False, required=False)
        next_check = argument(request, "next_check", (int, float))
        self.engine.rates.load(date, rates, stale, float(next_check))
        return {}

    def op_pcf_import(self, request):
        skipped = self.engine.import_pcf(argument(request, "text", str))
        return {"skipped": [{"line": n, "text": line, "reason": reason} for n, line, reason in skipped]}

    def op_schedule(self, request):
        engine = self.engine

        def number(name, default=None):
            # Errors start with the field name so the UI can mark that field (M8)
            text = argument(request, name, str, default, required=default is None)
            try:
                value = engine.value_of(text)
            except CalcError as exc:
                raise CalcError(f"{name}: {exc}") from exc
            if not isinstance(value, mpmath.mpf):
                raise CalcError(f"{name}: must be a real number")
            return value

        compounding = argument(request, "compounding", str, "yearly", required=False)
        contribution_freq = argument(request, "contribution_freq", str, "yearly", required=False)
        for name, value in (("compounding", compounding), ("contribution_freq", contribution_freq)):
            allowed = list(finance.COMPOUNDING) + (["continuous"] if name == "compounding" else [])
            if value not in allowed:
                raise RequestError(f"'{name}' must be one of {allowed}")
        with engine.working_context():
            plan = finance.schedule(
                number("principal"), number("annual_rate") / 100, number("years"),
                compounding, number("contribution", "0"), contribution_freq,
                argument(request, "at_start", bool, False, required=False))
            show = engine.format_value
            principal = plan.rows[0].start if plan.rows else 0
            rows, contributions, interest = [], [], []
            paid, earned = principal, 0
            for row in plan.rows:
                paid += row.contribution
                earned += row.interest
                rows.append({"period": row.index, "start": show(row.start), "interest": show(row.interest),
                             "contribution": show(row.contribution), "end": show(row.end)})
                contributions.append(float(paid))
                interest.append(float(earned))
            return {"future_value": show(plan.future_value),
                    "total_contributions": show(plan.total_contributions),
                    "total_interest": show(plan.total_interest),
                    "rows": rows, "periods_per_year": plan.periods_per_year,
                    "series": {"principal": float(principal), "contributions": contributions,
                               "interest": interest}}


#----------------------------------------------------------------------
#  Process entry point
#----------------------------------------------------------------------
def write(stream, obj):
    stream.write(json.dumps(obj, ensure_ascii=False) + "\n")
    stream.flush()


def serve(stdin=None, stdout=None):
    """Run the request loop until stdin closes or `shutdown` arrives."""
    stdin = stdin or sys.stdin
    stdout = stdout or sys.stdout
    dispatcher = Dispatcher()
    write(stdout, {"id": 0, "op": "ready", "protocol": PROTOCOL_VERSION, "version": __version__})
    for line in stdin:
        if not line.strip():
            continue
        try:
            request = json.loads(line)
        except json.JSONDecodeError as exc:
            write(stdout, {"id": None, "ok": False, "error": f"invalid JSON: {exc.msg}"})
            continue
        response = dispatcher.handle(request)
        write(stdout, response)
        if isinstance(request, dict) and request.get("op") == "shutdown":
            break


def main():
    # Windows consoles default to a legacy code page; the protocol is UTF-8
    sys.stdin.reconfigure(encoding="utf-8")
    sys.stdout.reconfigure(encoding="utf-8", newline="\n")
    serve()
