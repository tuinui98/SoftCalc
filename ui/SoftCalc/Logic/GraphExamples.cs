// GraphExamples.cs - Example functions and equations for Graph > Add > Examples (owner 2026-11-10)
//
// Classic 2D curves, each with the range its thumbnail shows. Each has a
// form with constants (`(x-a)^2+(y-b)^2=r^2`), a note on what they change
// and their defaults; Use puts the form with the defaults written in as
// numbers (`(x-0)^2+(y-0)^2=2^2`), so no variables are needed. Forms,
// notes and defaults: design/graph_examples_constants.md. Constant names
// avoid the engine constants e, i, j and pi. Every one was drawn through
// the engine before it went in the list.

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

	private static GraphExample Function(string title, string name, string form, string note, string defaults,
		double minX, double maxX, double minY, double maxY)
	{
		return new GraphExample(title, name, form, note, Constants(defaults), new WorldRect(minX, maxX, minY, maxY));
	}

	private static GraphExample Equation(string title, string form, string note, string defaults, double half)
	{
		return new GraphExample(title, "", form, note, Constants(defaults), Square(half));
	}

	public static readonly IReadOnlyList<GraphExample> Functions = new[]
	{
		Function("Parabola", "parabola", "a*(x-h)^2+k", "vertex at (h, k); a = width, a<0 opens down", "a=1 h=0 k=0", -3, 3, -1, 8),
		Function("Cubic", "cubic", "x^3-b*x+k", "b>0: two bumps at x=±√(b/3); b≤0: none; k = y offset", "b=3 k=0", -3, 3, -4, 4),
		Function("Absolute value", "vshape", "a*abs(x-h)+k", "corner at (h, k); a = slope of the arms", "a=1 h=0 k=0", -4, 4, -1, 5),
		Function("Square root", "halfroot", "a*sqrt(x-h)+k", "starts at (h, k); a = stretch", "a=1 h=0 k=0", -1, 9, -0.5, 3.5),
		Function("Hyperbola 1/x", "hyperbola", "a/(x-h)+k", "asymptotes x=h, y=k; a<0 flips quadrants", "a=1 h=0 k=0", -4, 4, -4, 4),
		Function("Sine", "sine", "a*sin(b*(x-c))+d", "a = amplitude; period 2π/b; c = phase shift; d = mid line", "a=1 b=1 c=0 d=0", -7, 7, -1.6, 1.6),
		Function("Cosine", "cosine", "a*cos(b*(x-c))+d", "a = amplitude; period 2π/b; c = phase shift; d = mid line", "a=1 b=1 c=0 d=0", -7, 7, -1.6, 1.6),
		Function("Tangent", "tangent", "a*tan(b*(x-c))", "period π/b; c = shift; a = stretch", "a=1 b=1 c=0", -5, 5, -4, 4),
		Function("Sinc", "sinc", "sin(b*x)/(b*x)", "zeros at multiples of π/b", "b=1", -15, 15, -0.4, 1.2),
		Function("Exponential", "growth", "a*exp(b*x)+k", "y = a+k at x=0; b<0 decays; asymptote y=k", "a=1 b=1 k=0", -3, 3, -1, 8),
		Function("Natural log", "logarithm", "a*ln(x-h)+k", "asymptote x=h; passes (h+1, k); a = stretch", "a=1 h=0 k=0", -1, 8, -4, 3),
		Function("Bell curve", "bell", "a*exp(-(x-m)^2/(2*s^2))", "a = peak; m = centre; s = width (bends at m±s)", "a=1 m=0 s=0.7071", -3, 3, -0.2, 1.2),
		Function("Sigmoid", "sigmoid", "L/(1+exp(-k*(x-m)))", "L = top level; k = steepness; m = midpoint, where y = L/2", "L=1 k=1 m=0", -8, 8, -0.2, 1.2),
		Function("Witch of Agnesi", "agnesi", "8*a^3/(x^2+4*a^2)", "a = radius of the circle that draws it; peak 2a", "a=1", -6, 6, -0.5, 2.5),
		Function("x sin(1/x)", "wobble", "x^n*sin(1/x)", "n=0: no limit at 0; n=1: squeezed to 0; n=2: smooth at 0", "n=1", -0.5, 0.5, -0.4, 0.4),
		Function("Two waves", "waves", "sin(x)+a*sin(b*x)", "a = second wave height; b = its frequency", "a=1/3 b=3", -7, 7, -1.5, 1.5),
	};

	public static readonly IReadOnlyList<GraphExample> Equations = new[]
	{
		Equation("Circle", "(x-a)^2+(y-b)^2=r^2", "centre (a, b); r = radius", "a=0 b=0 r=2", 3),
		Equation("Ellipse", "(x-h)^2/a^2+(y-k)^2/b^2=1", "centre (h, k); a = half width; b = half height", "h=0 k=0 a=3 b=2", 4),
		Equation("Hyperbola", "x^2/a^2-y^2/b^2=1", "vertices (±a, 0); asymptotes y = ±(b/a)x", "a=1 b=1", 4),
		Equation("Sideways parabola", "(y-k)^2=4*p*(x-h)", "vertex (h, k); focus p to the right; p<0 opens left", "k=0 p=0.25 h=0", 4),
		Equation("Heart", "(x^2+y^2-1)^3=a*x^2*y^3", "a = height of the lobes; a=0 is a circle", "a=1", 1.6),
		Equation("Lemniscate", "(x^2+y^2)^2=2*a^2*(x^2-y^2)", "foci (±a, 0); tips at ±a√2", "a=1", 2),
		Equation("Folium of Descartes", "x^3+y^3=3*a*x*y", "loop tip at (1.5a, 1.5a); asymptote x+y = -a", "a=1", 3),
		Equation("Cardioid", "(x^2+y^2-a*x)^2=a^2*(x^2+y^2)", "cusp at 0; reaches x = 2a", "a=1", 2.5),
		Equation("Astroid", "abs(x)^(2/3)+abs(y)^(2/3)=a^(2/3)", "cusps at ±a on both axes", "a=1", 1.4),
		Equation("Superellipse", "abs(x/a)^n+abs(y/b)^n=1", "a = half width; b = half height; n=2 ellipse, n>2 squarer, n=1 diamond, n<1 star", "a=1 b=1 n=4", 1.5),
		Equation("Cassini oval", "(x^2+y^2)^2-2*a^2*(x^2-y^2)=b^4-a^4", "foci (±a, 0); b<a: two ovals, b=a: lemniscate, b>a: one", "a=1 b=1.107", 2),
		Equation("Elliptic curve", "y^2=x^3+a*x+b", "4a³+27b² < 0: two pieces, > 0: one, = 0: pinched", "a=-1 b=1", 3),
		Equation("Cissoid of Diocles", "y^2*(2*a-x)=x^3", "asymptote x = 2a", "a=1", 3),
		Equation("Devil's curve", "y^2*(y^2-a^2)=x^2*(x^2-b^2)", "crosses the y axis at ±a, the x axis at ±b; a=b: lines y = ±x and a circle", "a=2 b=2.236", 3.5),
		Equation("Egg crate", "sin(b*x)*sin(b*y)=c", "c=0: grid of lines, 0<c<1: eggs, |c|>1: nothing; cell size π/b", "b=1 c=0.3", 7),
		Equation("x^y = y^x", "x^y=y^x", "no constant: the line y = x and a curve crossing it at (e, e); passes (2, 4)", "", 6),
	};

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
