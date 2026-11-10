// DefinitionCommands.cs - Add / Edit / Delete for user functions and variables
//
// Shared by the Graph and Variables toolbars in the sidebar and in the
// Graph window; Add and Edit open the DefinitionEditor. Definitions go
// straight to the engine and do not add History lines [DEF]; Delete
// needs no confirmation [DEF].

using System.Text.RegularExpressions;
using SoftCalc.Engine;

namespace SoftCalc.UI;

internal sealed partial class DefinitionCommands
{
	private readonly EngineSession _session;
	private readonly IWin32Window _owner;
	private readonly Func<Task> _refresh;

	[GeneratedRegex(@"^\s*[A-Za-z_][A-Za-z0-9_]*\s*\([^)]*\)\s*=")]
	private static partial Regex FunctionHead();

	[GeneratedRegex(@"^\s*[A-Za-z_][A-Za-z0-9_]*\s*=")]
	private static partial Regex VariableHead();

	private readonly Func<EngineState> _state;

	public DefinitionCommands(EngineSession session, IWin32Window owner, Func<Task> refresh, Func<EngineState> state)
	{
		_session = session;
		_owner = owner;
		_refresh = refresh;
		_state = state;
	}

	// A function renamed in the editor (old, new): the graph keeps its colour and tick
	public event Action<string, string>? Renamed;

	public void NotifyRenamed(string oldName, string newName)
	{
		Renamed?.Invoke(oldName, newName);
	}

	public EngineState State()
	{
		return _state();
	}

	public Task RefreshAsync()
	{
		return _refresh();
	}

	public Task AddFunctionAsync()
	{
		return OpenAsync(isFunction: true, select: null);
	}

	// A new function filled in from an example; the editor's OK adds it
	public async Task AddFunctionAsync(string name, string body)
	{
		using var editor = new DefinitionEditor(true, this, null, (name, "x", body));
		editor.ShowDialog(_owner);
		await _refresh();
	}

	public Task EditFunctionAsync(UserFunction function)
	{
		return OpenAsync(isFunction: true, function.Name);
	}

	public Task AddVariableAsync()
	{
		return OpenAsync(isFunction: false, select: null);
	}

	public Task EditVariableAsync(NamedValue variable)
	{
		return OpenAsync(isFunction: false, variable.Name);
	}

	public async Task DeleteAsync(string name)
	{
		await _session.DeleteAsync(name);
		await _refresh();
	}

	// Defines one line; returns the error to show, or null
	public async Task<string?> DefineAsync(string definition, bool isFunction)
	{
		string? shapeError = CheckShape(definition, isFunction);
		if (shapeError != null)
		{
			return shapeError;
		}
		var reply = await _session.EvalAsync(definition);
		if (reply.Kind == "definition")
		{
			return null;
		}
		return reply.Text.StartsWith("ERROR:") ? reply.Text["ERROR:".Length..] : reply.Text;
	}

	private async Task OpenAsync(bool isFunction, string? select)
	{
		using var editor = new DefinitionEditor(isFunction, this, select);
		editor.ShowDialog(_owner);
		await _refresh();
	}

	// Shape check before anything reaches the engine, so a wrong kind defines nothing
	internal static string? CheckShape(string definition, bool isFunction)
	{
		if (isFunction && !FunctionHead().IsMatch(definition))
		{
			return "Enter a function such as f(x)=x^2";
		}
		if (!isFunction && (!VariableHead().IsMatch(definition) || FunctionHead().IsMatch(definition)))
		{
			return "Enter a variable such as a=5";
		}
		return null;
	}
}
