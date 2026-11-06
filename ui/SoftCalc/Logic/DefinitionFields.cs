// DefinitionFields.cs - Name / parameters / body fields of the definition editor
//
// The editor (owner 2026-11-07, spec/ideas.md) splits `g(x, y)=x*y` into
// separate fields. This builds the definition text the engine takes and
// reports the first field that is wrong, so the editor can mark it.

using System.Text.RegularExpressions;

namespace SoftCalc.Logic;

internal enum DefinitionField
{
	Name,
	Parameters,
	Body,
}

internal static partial class DefinitionFields
{
	[GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
	private static partial Regex Identifier();

	[GeneratedRegex(@"^\s*([A-Za-z_][A-Za-z0-9_]*)\s*\(([^)]*)\)\s*=\s*$")]
	private static partial Regex FunctionHead();

	// `g(x)=` and `sin(x)*x` from the engine's state -> name "g", parameters "x"
	public static (string Name, string Parameters) SplitHead(string head)
	{
		var match = FunctionHead().Match(head);
		if (!match.Success)
		{
			return (head.TrimEnd('=', ' '), "");
		}
		var parameters = match.Groups[2].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
		return (match.Groups[1].Value, string.Join(", ", parameters));
	}

	// The definition to send, or the field to fix and why
	public static (string? Definition, DefinitionField? Field, string? Error) Compose(
		bool isFunction, string name, string parameters, string body)
	{
		name = name.Trim();
		if (!Identifier().IsMatch(name))
		{
			return (null, DefinitionField.Name, name.Length == 0
				? "Enter a name"
				: "A name starts with a letter or _ and has only letters, digits and _");
		}
		var list = new List<string>();
		if (isFunction)
		{
			foreach (string raw in parameters.Split(','))
			{
				string p = raw.Trim();
				if (p.Length == 0 && parameters.Trim().Length == 0)
				{
					break;    // no parameters at all
				}
				if (!Identifier().IsMatch(p))
				{
					return (null, DefinitionField.Parameters, "Parameters are names separated by commas, e.g. x, y");
				}
				if (list.Contains(p, StringComparer.OrdinalIgnoreCase))
				{
					return (null, DefinitionField.Parameters, $"'{p}' is listed twice");
				}
				list.Add(p);
			}
		}
		if (body.Trim().Length == 0)
		{
			return (null, DefinitionField.Body, isFunction ? "Enter what the function computes" : "Enter a value");
		}
		string head = isFunction ? $"{name}({string.Join(", ", list)})" : name;
		return ($"{head}={body.Trim()}", null, null);
	}
}
