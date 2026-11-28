"""softcalc_engine/server.py - JSON-lines server for the UI (design/protocol.md).

Reads one request per line from stdin, answers one response per line on
stdout, in order. Calculation failures are normal responses
(`kind: "error"`); only malformed requests get `ok: false`.
"""

import dataclasses
import json
import re
import sys

import mpmath

from . import __version__, crypto, currency, finance, menu, units
from .dates import ZONES
from .cards import decks_from
from .engine import Engine, Settings
from .errors import CalcError
from .formatting import format_number

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
DESCRIPTIVE = ["sum", "prod", "min", "max", "count", "range", "mean", "median", "mode",
               "geomean", "harmean", "stdev", "stdevp", "variance", "variancep",
               "percentile", "quartile", "iqr", "zscore", "skew", "skewp", "kurt", "kurtp", "wmean"]
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
        "Random": ["rand", "rands", "randnorm", "randbinom", "randpois", "randbeta", "randtri", "randexp",
                   "randpeaks", "card"],
        # The ten used most; the rest in "subgroups" below (owner 2026-11-25)
        "Matrix": ["det", "inv", "solve", "transpose", "eig", "identity", "fill", "seq", "sum", "mean"],
        # Its own group (owner 2026-11-26), not a Matrix submenu: they take numbers as well as lists.
        # The ten used most, then Descriptive, Counting and Distributions (round 1, design/TODO.md)
        "Statistics": ["sum", "mean", "median", "mode", "stdev", "variance", "percentile", "comb", "normcdf", "norminv"],
        "Special Functions": ["gamma", "erf", "erfc"],    # owner 2026-11-26: a group of their own
    },
    # A function group's submenus, after its own items and a separator; the
    # top items stay in their submenu too, so each submenu is whole (owner 2026-11-25)
    "subgroups": {
        "Matrix": {
            "Build": ["identity", "zeros", "ones", "diag", "fill", "seq", "reshape", "randm"],
            "Size and pick": ["rows", "cols", "len", "at", "row", "col"],
            "Linear algebra": ["transpose", "det", "inv", "trace", "rank", "solve", "eig", "dot", "vdot", "cross", "norm"],
            "Arrange": ["sort", "reverse", "cumsum"],
        },
        "Statistics": {
            "Descriptive": DESCRIPTIVE,
            "Two variables": ["covar", "covarp", "correl", "slope", "intercept", "rsq", "forecast"],
            "Counting": ["comb", "perm"],
            "Distributions": ["normpdf", "normcdf", "norminv", "tpdf", "tcdf", "tinv",
                              "chi2pdf", "chi2cdf", "chi2inv", "fpdf", "fcdf", "finv",
                              "binompdf", "binomcdf", "poisspdf", "poisscdf", "geometpdf", "geometcdf", "expcdf",
                              "randnorm", "randbinom", "randpois"],
        },
    },
    "conversions": {**units.MENU, "Time zone": list(ZONES), "Currency": currency.COMMON},
    "currencies": currency.CODES,
    "crypto": units.CRYPTO_MENU,
    "crypto_targets": currency.COMMON,
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
        self.apart = None    # the web app's Seed draws (`draws` with apart), made on first use
        self.schedule_id = 0
        self.last_schedule = None    # (id, rows, cell formatter) of the last `schedule`, for schedule_rows
        self.handlers = {
            "eval": self.op_eval,
            "record": self.op_record,
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
            "hint": self.op_hint,
            "delete_result": self.op_delete_result,
            "pcf_export": lambda request: {"text": self.engine.export_pcf()},
            "pcf_import": self.op_pcf_import,
            "restore_results": self.op_restore_results,
            "schedule": self.op_schedule,
            "schedule_rows": self.op_schedule_rows,
            "set_rates": self.op_set_rates,
            "set_prices": self.op_set_prices,
            "draws": self.op_draws,
            "decks": self.op_decks,
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
        reply = {"kind": result.kind, "input": result.input,
                 "display_input": result.display_input, "text": result.text, "note": result.note}
        if result.settings_changed:
            reply["settings"] = self.current_settings()    # a `:` line (commands.py); the UI keeps them
        if result.timer is not None:
            reply["timer"] = result.timer    # a stopwatch, timer or alarm line (timers.py); the UI acts
        return reply

    def op_record(self, request):
        """A stopwatch's measure into History: {label, value, note}, kept as answer and $n;
        input is the value, display_input the label and note."""
        result = self.engine.record(argument(request, "label", str), argument(request, "value", str),
                                    argument(request, "note", str, "", required=False))
        return {"kind": result.kind, "input": result.input, "display_input": result.display_input,
                "text": result.text, "note": ""}

    def current_settings(self):
        return {name: getattr(self.engine.settings, name) for name in Settings.__dataclass_fields__}

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
        return self.current_settings()

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

    def op_hint(self, request):
        return {"text": self.engine.hint(argument(request, "text", str), argument(request, "caret", int))}

    def op_delete_result(self, request):
        self.engine.delete_result(argument(request, "n", int))
        return {}

    def op_restore_results(self, request):
        texts = argument(request, "texts", list)
        if not all(isinstance(text, str) for text in texts):
            raise RequestError("'texts' must be a list of strings")
        return {"restored": self.engine.restore_results(texts)}

    def op_decks(self, request):
        """card()'s decks (owner 2026-11-24): the UI's own and ready-made decks."""
        items = argument(request, "decks", list)
        try:
            self.engine.decks = decks_from(items)
        except ValueError as exc:
            raise RequestError(str(exc)) from exc
        if self.apart is not None:
            self.apart.decks = self.engine.decks
        return {"decks": len(items)}

    def op_draws(self, request):
        """The Random window's Seed (owner 2026-11-12): rands(seed), then
        `line` count times, exactly as if typed in Input in that order.
        unique: a text already drawn is drawn again (No repeats).
        apart (owner 2026-11-20): in an engine of its own at this one's
        settings, so the calculator's seed and $n stay as they were (the
        web app has one engine; the desktop window starts a second one).
        """
        engine = self.engine
        if argument(request, "apart", bool, False, required=False):
            if self.apart is None:
                self.apart = Engine()
            self.apart.settings = dataclasses.replace(self.engine.settings)
            self.apart.decks = self.engine.decks
            engine = self.apart
        seed = argument(request, "seed", str)
        line = argument(request, "line", str)
        count = argument(request, "count", int)
        unique = argument(request, "unique", bool, False, required=False)
        if not SEED.fullmatch(seed):
            raise RequestError("'seed' must be a number such as 42 or -7.5")
        if not 1 <= count <= MAX_DRAWS:
            raise RequestError(f"'count' must be 1 to {MAX_DRAWS}")
        engine.evaluate(f"rands({seed})")
        try:
            texts = engine.repeat(line, count, unique, count * 50 + 1000 if unique else count)
        except CalcError as exc:
            raise RequestError(str(exc)) from exc
        finally:
            engine.clear_history()    # the draws are not $n results
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

    def op_set_prices(self, request):
        when = argument(request, "time", (str, type(None)), None, required=False)
        prices = argument(request, "prices", dict, {}, required=False)
        for coin, per_currency in prices.items():
            if coin.lower() not in crypto.COINS or not isinstance(per_currency, dict):
                raise RequestError("'prices' must map btc, eth, trx or usdt to {currency: price}")
            for code, value in per_currency.items():
                if not isinstance(value, (int, float, str)) or isinstance(value, bool) or len(code) != 3:
                    raise RequestError("prices must map 3-letter codes to numbers")
                try:
                    positive = mpmath.mpf(value) > 0
                except ValueError:
                    positive = False
                if not positive:
                    raise RequestError(f"the price of {coin} in '{code}' must be a positive number")
        if prices and when is None:
            raise RequestError("'time' is needed with 'prices'")
        stale = argument(request, "stale", bool, False, required=False)
        next_check = argument(request, "next_check", (int, float))
        self.engine.prices.load(when, prices, stale, float(next_check))
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
        # owner 2026-11-28: the rate per day, week, month or year; the length in any of them
        rate_unit = argument(request, "rate_unit", str, "yearly", required=False)
        length_unit = argument(request, "length_unit", str, "years", required=False)
        for name, value, table in (("rate_unit", rate_unit, finance.RATE_UNITS),
                                   ("length_unit", length_unit, finance.LENGTH_UNITS)):
            if value not in table:
                raise RequestError(f"'{name}' must be one of {list(table)}")
        # owner 2026-11-28: the table's own decimal places, a number or "all"; left out, the View's
        table_decimals = request.get("table_decimals", "view")
        if not (table_decimals in ("view", "all") or (type(table_decimals) is int and 0 <= table_decimals <= 30)):
            raise RequestError("'table_decimals' must be 0 to 30 or \"all\"")
        mode = argument(request, "mode", str, "normal", required=False)
        if mode not in ("normal", "goal"):
            raise RequestError("'mode' must be normal or goal")
        at_start = argument(request, "at_start", bool, False, required=False)
        with engine.working_context():
            s = engine.settings
            if table_decimals == "view":
                cell = engine.format_value
            else:
                # owner 2026-11-29: the places go for the totals and the worked-out field too
                places = None if table_decimals == "all" else table_decimals

                def cell(value):
                    return format_number(value, s.precision, s.base, places, places is not None)

            def exact(value):
                return format_number(value, s.precision, s.base)    # every digit, to keep when it is set later

            solved = None
            if mode == "goal":
                # owner 2026-11-28: four fields give the fifth; the rate's unit is the compounding
                solve = argument(request, "solve", str)
                if solve not in finance.GOAL_FIELDS:
                    raise RequestError(f"'solve' must be one of {list(finance.GOAL_FIELDS)}")
                values = {name: None if name == solve else number(name, "0" if name == "contribution" else None)
                          for name in finance.GOAL_FIELDS}
                result = finance.goal(values, solve, rate_unit, length_unit, contribution_freq, at_start)
                plan = result.plan
                solved = {"field": solve, "value": cell(result.values[solve]),
                          "exact": exact(result.values[solve]),
                          "length_ymd": finance.years_text(result.years),
                          "values": {name: cell(value) for name, value in result.values.items()}}
            else:
                plan = finance.schedule(
                    number("principal"), number("annual_rate") / 100, number("years"),
                    compounding, number("contribution", "0"), contribution_freq,
                    at_start, rate_unit, length_unit)
            principal = plan.rows[0].start if plan.rows else 0
            contributions, interest = [], []
            paid, earned = principal, 0
            for row in plan.rows:
                paid += row.contribution
                earned += row.interest
                contributions.append(float(paid))
                interest.append(float(earned))
            # owner 2026-11-29: formatting every row took seconds for long tables, so with row_limit only
            # the first rows come now and schedule_rows gives the others when they are shown
            self.schedule_id += 1
            self.last_schedule = (self.schedule_id, plan.rows, cell)
            limit = request.get("row_limit")
            if limit is not None and not (type(limit) is int and limit >= 0):
                raise RequestError("'row_limit' must be a whole number 0 or more")
            shown = plan.rows if limit is None else plan.rows[:limit]
            reply = {"future_value": cell(plan.future_value),
                     "total_contributions": cell(plan.total_contributions),
                     "total_interest": cell(plan.total_interest),
                     "rows": [schedule_line(row, cell) for row in shown], "row_count": len(plan.rows),
                     "schedule_id": self.schedule_id, "periods_per_year": float(plan.periods_per_year),
                     "periods_per_unit": float(plan.periods_per_unit), "length_unit": length_unit,
                     "partial": float(plan.partial),
                     "series": {"principal": float(principal), "contributions": contributions,
                                "interest": interest}}
            if solved:
                reply["solved"] = solved
            return reply

    def op_schedule_rows(self, request):
        """Rows first .. first+count-1 (0-based) of the last schedule, in its places."""
        schedule_id = argument(request, "schedule_id", int)
        first = argument(request, "first", int)
        count = argument(request, "count", int)
        if self.last_schedule is None or self.last_schedule[0] != schedule_id:
            raise RequestError("that schedule is gone; a newer one was worked out")
        if first < 0 or count < 0:
            raise RequestError("'first' and 'count' must be 0 or more")
        _, rows, cell = self.last_schedule
        with self.engine.working_context():
            return {"schedule_id": schedule_id, "first": first,
                    "rows": [schedule_line(row, cell) for row in rows[first:first + count]]}


def schedule_line(row, cell):
    """One table row for the protocol."""
    line = {"period": row.index, "start": cell(row.start), "interest": cell(row.interest),
            "contribution": cell(row.contribution), "end": cell(row.end)}
    if row.part is not None:
        line["part"] = format_number(row.part, 4)    # a last row shorter than a period: 0.3
    return line


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
