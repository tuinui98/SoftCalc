// DefinitionCommands.cs - Add / Edit / Delete for user functions and variables
//
// Shared by the Graph and Variables toolbars in the sidebar and in the
// Graph window. Dialog definitions go straight to the engine and do not
// add History lines [DEF]; Delete needs no confirmation [DEF].

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

	public DefinitionCommands(EngineSession session, IWin32Window owner, Func<Task> refresh)
	{
		_session = session;
		_owner = owner;
		_refresh = refresh;
	}

	public Task AddFunctionAsync()
	{
		return ShowAsync("Add Function", "", "", isFunction: true);
	}

	public Task EditFunctionAsync(UserFunction function)
	{
		return ShowAsync("Edit Function", function.Head, function.Body, isFunction: true);
	}

	public Task AddVariableAsync()
	{
		return ShowAsync("Add Variable", "", "", isFunction: false);
	}

	public Task EditVariableAsync(NamedValue variable)
	{
		return ShowAsync("Edit Variable", variable.Name + "=", variable.Value, isFunction: false);
	}

	public async Task DeleteAsync(string name)
	{
		await _session.DeleteAsync(name);
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

	private async Task ShowAsync(string title, string head, string body, bool isFunction)
	{
		using var dialog = new DefinitionDialog(title, head, body, async definition =>
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
		});
		if (dialog.ShowDialog(_owner) == DialogResult.OK)
		{
			await _refresh();
		}
	}
}
