// GraphExamples.cs - Example functions and equations for Graph > Add > Examples (owner 2026-11-10)
//
// Classic 2D curves, each with the range its thumbnail shows. Each has a
// form with constants (`(x-a)^2+(y-b)^2=r^2`), a note on what they change
// and their defaults; Use puts the form with the defaults written in as
// numbers (`(x-0)^2+(y-0)^2=2^2`), so no variables are needed. The list is
// data/graph-examples.json, shared with the web app (owner 2026-11-19);
// how it was chosen: design/graph_examples_constants.md. Constant names
// avoid the engine constants e, i, j and pi. Every one was drawn through
// the engine before it went in the list.

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SoftCalc.Logic;

internal sealed record GraphExample(string Title, string Name, string Form, string Note,
	IReadOnlyList<(string Name, string Value)> Defaults, WorldRect View)
{
	public bool IsEquation => Name.Length == 0;

	// The form with each constant replaced by its default
	public string Text => GraphExamples.WithDefaults(Form, Defaults);

	// The line put in the editor: `name(x)=body` or the equation
	public string Definition => IsEquation ? Text : $"{Name}(x)={Text}";

	// The form as a definition, for the gallery
	public string FormLine => IsEquation ? Form : $"{Name}(x)={Form}";

	// Shown under the input after Use: the form, then the note one clause a line
	public string Hint => "Form: " + Form + Environment.NewLine + Note.Replace("; ", Environment.NewLine);

	// sin, cos or tan in it: drawn differently outside radians
	public bool UsesAngles => Regex.IsMatch(Form, @"\b(sin|cos|tan)\(");
}

internal static class GraphExamples
{
	// Thumbnails are 4:3; equations get a 4:3 view around the origin so circles stay round
	private static WorldRect Square(double half)
	{
		return new WorldRect(-half * 4 / 3, half * 4 / 3, -half, half);
	}

	// "a=1 h=0" as (name, value) pairs in that order
	private static IReadOnlyList<(string Name, string Value)> Constants(string defaults)
	{
		return defaults.Split(' ', StringSplitOptions.RemoveEmptyEntries)
			.Select(pair => pair.Split('='))
			.Select(parts => (parts[0], parts[1]))
			.ToList();
	}

	// data/graph-examples.json (shared with the web app), embedded at build
	private static readonly Lazy<(GraphExample[] Functions, GraphExample[] Equations)> All = new(Load);

	public static IReadOnlyList<GraphExample> Functions => All.Value.Functions;

	public static IReadOnlyList<GraphExample> Equations => All.Value.Equations;

	private static (GraphExample[], GraphExample[]) Load()
	{
		using var stream = typeof(GraphExamples).Assembly.GetManifestResourceStream("graph-examples.json")!;
		var root = JsonNode.Parse(stream)!;
		var functions = root["functions"]!.AsArray().Select(item =>
		{
			var view = item!["view"]!.AsArray().Select(v => (double)v!).ToArray();
			return new GraphExample((string)item["title"]!, (string)item["name"]!, (string)item["form"]!, (string)item["note"]!,
				Constants((string)item["defaults"]!), new WorldRect(view[0], view[1], view[2], view[3]));
		}).ToArray();
		var equations = root["equations"]!.AsArray().Select(item => new GraphExample((string)item!["title"]!, "",
			(string)item["form"]!, (string)item["note"]!, Constants((string)item["defaults"]!), Square((double)item["half"]!))).ToArray();
		return (functions, equations);
	}

	// The form with each constant (a whole name) replaced by its value; a
	// negative value or a fraction goes in brackets so `a*x` stays `(-1)*x`
	public static string WithDefaults(string form, IReadOnlyList<(string Name, string Value)> defaults)
	{
		return Regex.Replace(form, @"[A-Za-z_][A-Za-z_0-9]*", word =>
		{
			foreach (var (name, value) in defaults)
			{
				if (name == word.Value)
				{
					return value.StartsWith('-') || value.Contains('/') ? "(" + value + ")" : value;
				}
			}
			return word.Value;
		});
	}

	// The example's function name, or name2, name3... when the name is taken
	public static string FreeName(string name, IEnumerable<string> taken)
	{
		var used = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);
		if (!used.Contains(name))
		{
			return name;
		}
		int n = 2;
		while (used.Contains(name + n))
		{
			n++;
		}
		return name + n;
	}
}
