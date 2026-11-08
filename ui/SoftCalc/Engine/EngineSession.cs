// EngineSession.cs - Typed engine operations with a restart watchdog
//
// Wraps EngineClient with the operations the UI needs. If the engine
// dies or stops answering, the session starts a new one and restores
// the settings and user definitions (from the last pcf_export), then
// raises Restarted. `answer` and $n do not survive a restart. Currency
// rates come from RatesProvider when the engine asks for them (M8).

using System.Diagnostics;
using System.Text.Json.Nodes;
using SoftCalc.Logic;

namespace SoftCalc.Engine;

internal sealed record EvalReply(string Kind, string Input, string DisplayInput, string Text, string Note = "");

internal sealed record NamedValue(string Name, string Value);

internal sealed record ScheduleRow(int Period, string Start, string Interest, string Contribution, string End);

internal sealed record ScheduleReply(
	string FutureValue, string TotalContributions, string TotalInterest,
	IReadOnlyList<ScheduleRow> Rows, double Principal, IReadOnlyList<double> Contributions,
	IReadOnlyList<double> Interest, int PeriodsPerYear);

internal sealed record UserFunction(string Name, IReadOnlyList<string> Parameters, string Head, string Body);

internal sealed record EngineState(
	IReadOnlyList<NamedValue> Constants,
	IReadOnlyList<NamedValue> Variables,
	IReadOnlyList<UserFunction> Functions);

internal sealed record Catalog(
	IReadOnlyList<KeyValuePair<string, IReadOnlyList<string>>> FunctionGroups,
	IReadOnlyList<KeyValuePair<string, IReadOnlyList<string>>> ConversionGroups,
	IReadOnlyList<string> Formats,
	IReadOnlyList<string> Currencies);

internal sealed class EngineSession : IDisposable
{
	private static readonly TimeSpan EvalTimeout = TimeSpan.FromSeconds(30);
	private static readonly TimeSpan QuickTimeout = TimeSpan.FromSeconds(10);

	private readonly ProcessStartInfo _startInfo;
	private readonly SemaphoreSlim _gate = new(1, 1);
	private EngineClient? _client;
	private JsonObject _settings = new();
	private string _definitions = "";
	private JsonObject? _rates;

	public event EventHandler<string>? Restarted;

	// Raised with the full definitions text after it changed (definition, delete, import)
	public event EventHandler<string>? DefinitionsChanged;

	public string Definitions => _definitions;

	// Fetches currency rates (a set_rates payload) when an eval needs them
	public Func<Task<JsonObject>>? RatesProvider { get; set; }

	public EngineSession(ProcessStartInfo startInfo)
	{
		_startInfo = startInfo;
	}

	internal EngineClient? Client => _client;

	public bool IsStarted => _client != null;

	public async Task StartAsync()
	{
		_client = await EngineClient.StartAsync(_startInfo);
		_settings = await RequestAsync("settings", null, QuickTimeout);
		_settings.Remove("id");
		_settings.Remove("ok");
		_definitions = (string?)(await RequestAsync("pcf_export", null, QuickTimeout))["text"] ?? "";
	}

	//==================================================================
	//  Operations
	//==================================================================
	public async Task<EvalReply> EvalAsync(string input)
	{
		JsonObject reply;
		try
		{
			reply = await RequestAsync("eval", new JsonObject { ["input"] = input }, EvalTimeout);
		}
		catch (EngineLostException ex)
		{
			await RestartAsync(ex.Message);
			return new EvalReply("error", input, input, "ERROR:The engine stopped and was restarted; try again");
		}
		if ((string?)reply["kind"] == "rates_needed")
		{
			reply = await EvalWithRatesAsync(input);
		}
		var result = new EvalReply(
			(string?)reply["kind"] ?? "error", (string?)reply["input"] ?? input,
			(string?)reply["display_input"] ?? input, (string?)reply["text"] ?? "", (string?)reply["note"] ?? "");
		if (result.Kind == "definition")
		{
			await SnapshotDefinitionsAsync();
		}
		return result;
	}

	// Fetch once, hand the rates over, evaluate again; never loops
	private async Task<JsonObject> EvalWithRatesAsync(string input)
	{
		if (RatesProvider == null)
		{
			return new JsonObject { ["kind"] = "error", ["input"] = input, ["text"] = "ERROR:Currency rates unavailable" };
		}
		await SetRatesAsync(await RatesProvider());
		var reply = await RequestAsync("eval", new JsonObject { ["input"] = input }, EvalTimeout);
		if ((string?)reply["kind"] == "rates_needed")
		{
			return new JsonObject { ["kind"] = "error", ["input"] = input, ["text"] = "ERROR:Currency rates unavailable" };
		}
		return reply;
	}

	public async Task SetRatesAsync(JsonObject payload)
	{
		_rates = payload.DeepClone().AsObject();
		await GuardedAsync("set_rates", payload);
	}

	public async Task<JsonObject> ChangeSettingsAsync(JsonObject changes)
	{
		var reply = await GuardedAsync("settings", changes);
		foreach (var (key, value) in reply)
		{
			if (key != "id" && key != "ok")
			{
				_settings[key] = value?.DeepClone();
			}
		}
		return _settings;
	}

	public JsonObject Settings => _settings;

	public async Task ClearHistoryAsync()
	{
		await GuardedAsync("clear_history", null);
	}

	public async Task<EngineState> StateAsync()
	{
		var reply = await GuardedAsync("state", null);
		return new EngineState(
			NamedValues(reply["constants"]),
			NamedValues(reply["variables"]),
			reply["functions"]?.AsArray().Select(f => new UserFunction(
				(string?)f?["name"] ?? "",
				f?["params"]?.AsArray().Select(p => (string?)p ?? "").ToList() ?? new List<string>(),
				(string?)f?["head"] ?? "",
				(string?)f?["body"] ?? "")).ToList() ?? new List<UserFunction>());
	}

	public async Task<Catalog> CatalogAsync()
	{
		var reply = await GuardedAsync("catalog", null);
		return new Catalog(Groups(reply["functions"]), Groups(reply["conversions"]),
			reply["formats"]?.AsArray().Select(f => (string?)f ?? "").ToList() ?? new List<string>(),
			reply["currencies"]?.AsArray().Select(f => (string?)f ?? "").ToList() ?? new List<string>());
	}

	public async Task<(string Text, int Caret)> MenuInsertAsync(JsonObject arguments)
	{
		var reply = await GuardedAsync("menu_insert", arguments);
		return ((string?)reply["text"] ?? "", (int?)reply["caret"] ?? 0);
	}

	// Compound Interest table; an input error comes back as EngineRequestException
	// whose message starts with the field name ("years: must be positive")
	public async Task<ScheduleReply> ScheduleAsync(JsonObject arguments)
	{
		var reply = await GuardedAsync("schedule", arguments);
		var rows = reply["rows"]?.AsArray().Select(r => new ScheduleRow(
			(int?)r?["period"] ?? 0, (string?)r?["start"] ?? "", (string?)r?["interest"] ?? "",
			(string?)r?["contribution"] ?? "", (string?)r?["end"] ?? "")).ToList() ?? new List<ScheduleRow>();
		var series = reply["series"];
		return new ScheduleReply(
			(string?)reply["future_value"] ?? "", (string?)reply["total_contributions"] ?? "",
			(string?)reply["total_interest"] ?? "", rows, (double?)series?["principal"] ?? 0,
			Numbers(series?["contributions"]), Numbers(series?["interest"]), (int?)reply["periods_per_year"] ?? 1);
	}

	private static List<double> Numbers(JsonNode? node)
	{
		return node?.AsArray().Select(n => (double?)n ?? 0).ToList() ?? new List<double>();
	}

	// Removes a user variable or function; returns false if there was none
	public async Task<bool> DeleteAsync(string name)
	{
		try
		{
			await GuardedAsync("delete", new JsonObject { ["name"] = name });
		}
		catch (EngineRequestException)
		{
			return false;
		}
		await SnapshotDefinitionsAsync();
		return true;
	}

	// Samples a one-parameter function; null entries are gaps (undefined or complex)
	public async Task<IReadOnlyList<(double X, double Y)?>> PlotAsync(
		string function, string mode, double start, double end, int samples)
	{
		var reply = await GuardedAsync("plot", new JsonObject
		{
			["function"] = function, ["mode"] = mode, ["start"] = start, ["end"] = end, ["samples"] = samples,
		});
		var points = new List<(double X, double Y)?>();
		foreach (var node in reply["points"]?.AsArray() ?? new JsonArray())
		{
			if (node is JsonArray pair && pair.Count == 2)
			{
				points.Add(((double)pair[0]!, (double)pair[1]!));
			}
			else
			{
				points.Add(null);
			}
		}
		return points;
	}

	// Graph trace: full-precision text and a float; nulls where undefined (G2)
	public async Task<(string? Text, double? Value)> ValueAtAsync(string function, double x)
	{
		var reply = await GuardedAsync("value_at", new JsonObject { ["function"] = function, ["x"] = x });
		return ((string?)reply["text"], (double?)reply["value"]);
	}

	// Equation in x and y: line segments (x1, y1, x2, y2) in the rectangle (graph2_plan G5)
	public async Task<IReadOnlyList<(double X1, double Y1, double X2, double Y2)>> PlotEquationAsync(
		string equation, WorldRect view, int cols, int rows)
	{
		var reply = await GuardedAsync("plot_equation", new JsonObject
		{
			["equation"] = equation, ["x_min"] = view.MinX, ["x_max"] = view.MaxX, ["y_min"] = view.MinY, ["y_max"] = view.MaxY,
			["cols"] = cols, ["rows"] = rows,
		});
		var segments = new List<(double, double, double, double)>();
		foreach (var node in reply["segments"]?.AsArray() ?? new JsonArray())
		{
			if (node is JsonArray s && s.Count == 4)
			{
				segments.Add(((double)s[0]!, (double)s[1]!, (double)s[2]!, (double)s[3]!));
			}
		}
		return segments;
	}

	// Trace on an equation: fix x (or y) at the exact text value, solve the other near guess (G6)
	public async Task<(string? X, string? Y, double? XValue, double? YValue)> EquationAtAsync(
		string equation, string axis, string value, double guess, double window)
	{
		var reply = await GuardedAsync("equation_at", new JsonObject
		{
			["equation"] = equation, ["axis"] = axis, ["value"] = value, ["guess"] = guess, ["window"] = window,
		});
		return ((string?)reply["x"], (string?)reply["y"], (double?)reply["x_value"], (double?)reply["y_value"]);
	}

	// Deleting a History result line removes `$n` (1 = newest) from the engine too
	public async Task DeleteResultAsync(int n)
	{
		await GuardedAsync("delete_result", new JsonObject { ["n"] = n });
	}

	// One number literal in the output base for the hint line, or null (protocol `literal`)
	public async Task<string?> LiteralAsync(string text)
	{
		var reply = await GuardedAsync("literal", new JsonObject { ["text"] = text });
		return (string?)reply["text"];
	}

	//==================================================================
	//  Plumbing
	//==================================================================
	private async Task<JsonObject> GuardedAsync(string op, JsonObject? arguments)
	{
		try
		{
			return await RequestAsync(op, arguments, QuickTimeout);
		}
		catch (EngineLostException ex)
		{
			await RestartAsync(ex.Message);
			return await RequestAsync(op, arguments, QuickTimeout);
		}
	}

	private async Task<JsonObject> RequestAsync(string op, JsonObject? arguments, TimeSpan timeout)
	{
		// Not started yet is not a lost engine: no restart for it
		var client = _client ?? throw new EngineNotStartedException();
		// One request at a time keeps restarts and snapshots consistent
		await _gate.WaitAsync();
		try
		{
			return await client.RequestAsync(op, arguments, timeout);
		}
		finally
		{
			_gate.Release();
		}
	}

	private async Task SnapshotDefinitionsAsync()
	{
		_definitions = (string?)(await GuardedAsync("pcf_export", null))["text"] ?? _definitions;
		DefinitionsChanged?.Invoke(this, _definitions);
	}

	// Replaces all user definitions; returns the skipped lines as "line N: text (reason)"
	public async Task<IReadOnlyList<string>> ImportDefinitionsAsync(string text)
	{
		var reply = await GuardedAsync("pcf_import", new JsonObject { ["text"] = text });
		await SnapshotDefinitionsAsync();
		return reply["skipped"]?.AsArray()
			.Select(s => $"line {(int?)s?["line"]}: {(string?)s?["text"]} ({(string?)s?["reason"]})")
			.ToList() ?? new List<string>();
	}

	// Rebuilds answer and $n from saved History results (oldest first)
	public async Task<int> RestoreResultsAsync(IEnumerable<string> texts)
	{
		var array = new JsonArray(texts.Select(t => (JsonNode?)JsonValue.Create(t)).ToArray());
		var reply = await GuardedAsync("restore_results", new JsonObject { ["texts"] = array });
		return (int?)reply["restored"] ?? 0;
	}

	public async Task RefreshSnapshotAsync()
	{
		await SnapshotDefinitionsAsync();
	}

	private async Task RestartAsync(string reason)
	{
		_client?.Dispose();
		_client = await EngineClient.StartAsync(_startInfo);
		var restore = _settings.DeepClone().AsObject();
		await RequestAsync("settings", restore, QuickTimeout);
		await RequestAsync("pcf_import", new JsonObject { ["text"] = _definitions }, QuickTimeout);
		if (_rates != null)
		{
			await RequestAsync("set_rates", _rates.DeepClone().AsObject(), QuickTimeout);
		}
		Restarted?.Invoke(this, reason);
	}

	private static List<NamedValue> NamedValues(JsonNode? node)
	{
		return node?.AsArray().Select(v => new NamedValue((string?)v?["name"] ?? "", (string?)v?["value"] ?? "")).ToList()
			?? new List<NamedValue>();
	}

	private static List<KeyValuePair<string, IReadOnlyList<string>>> Groups(JsonNode? node)
	{
		var groups = new List<KeyValuePair<string, IReadOnlyList<string>>>();
		if (node is JsonObject obj)
		{
			foreach (var (name, items) in obj)
			{
				IReadOnlyList<string> list = items?.AsArray().Select(i => (string?)i ?? "").ToList() ?? new List<string>();
				groups.Add(new KeyValuePair<string, IReadOnlyList<string>>(name, list));
			}
		}
		return groups;
	}

	public void Dispose()
	{
		_client?.Dispose();
		_gate.Dispose();
	}
}
