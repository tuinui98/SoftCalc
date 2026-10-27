// EngineSession.cs - Typed engine operations with a restart watchdog
//
// Wraps EngineClient with the operations the UI needs. If the engine
// dies or stops answering, the session starts a new one and restores
// the settings and user definitions (from the last pcf_export), then
// raises Restarted. `answer` and $n do not survive a restart.

using System.Diagnostics;
using System.Text.Json.Nodes;

namespace SoftCalc.Engine;

internal sealed record EvalReply(string Kind, string Input, string DisplayInput, string Text);

internal sealed record NamedValue(string Name, string Value);

internal sealed record UserFunction(string Name, IReadOnlyList<string> Parameters, string Head, string Body);

internal sealed record EngineState(
	IReadOnlyList<NamedValue> Constants,
	IReadOnlyList<NamedValue> Variables,
	IReadOnlyList<UserFunction> Functions);

internal sealed record Catalog(
	IReadOnlyList<KeyValuePair<string, IReadOnlyList<string>>> FunctionGroups,
	IReadOnlyList<KeyValuePair<string, IReadOnlyList<string>>> ConversionGroups,
	IReadOnlyList<string> Formats);

internal sealed class EngineSession : IDisposable
{
	private static readonly TimeSpan EvalTimeout = TimeSpan.FromSeconds(30);
	private static readonly TimeSpan QuickTimeout = TimeSpan.FromSeconds(10);

	private readonly ProcessStartInfo _startInfo;
	private readonly SemaphoreSlim _gate = new(1, 1);
	private EngineClient? _client;
	private JsonObject _settings = new();
	private string _definitions = "";

	public event EventHandler<string>? Restarted;

	public EngineSession(ProcessStartInfo startInfo)
	{
		_startInfo = startInfo;
	}

	internal EngineClient? Client => _client;

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
		var result = new EvalReply(
			(string?)reply["kind"] ?? "error", (string?)reply["input"] ?? input,
			(string?)reply["display_input"] ?? input, (string?)reply["text"] ?? "");
		if (result.Kind == "definition")
		{
			await SnapshotDefinitionsAsync();
		}
		return result;
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
			reply["formats"]?.AsArray().Select(f => (string?)f ?? "").ToList() ?? new List<string>());
	}

	public async Task<(string Text, int Caret)> MenuInsertAsync(JsonObject arguments)
	{
		var reply = await GuardedAsync("menu_insert", arguments);
		return ((string?)reply["text"] ?? "", (int?)reply["caret"] ?? 0);
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
		var client = _client ?? throw new EngineLostException("Engine is not running");
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
