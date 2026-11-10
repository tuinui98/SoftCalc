// GraphExamples.cs - Example functions and equations for Graph > Add > Examples (owner 2026-11-10)
//
// Classic 2D curves, each with the range its thumbnail shows. A function
// is offered as `name(x)=body`; an equation as typed in Add Equation.
// Every one was drawn through the engine before it went in the list.

namespace SoftCalc.Logic;

internal sealed record GraphExample(string Title, string Name, string Text, WorldRect View)
{
	public bool IsEquation => Name.Length == 0;

	// The line shown under the gallery and put in the editor
	public string Definition => IsEquation ? Text : $"{Name}(x)={Text}";
}

internal static class GraphExamples
{
	// Thumbnails are 4:3; equations get a 4:3 view around the origin so circles stay round
	private static WorldRect Square(double half)
	{
		return new WorldRect(-half * 4 / 3, half * 4 / 3, -half, half);
	}

	private static GraphExample Function(string title, string name, string body, double minX, double maxX, double minY, double maxY)
	{
		return new GraphExample(title, name, body, new WorldRect(minX, maxX, minY, maxY));
	}

	private static GraphExample Equation(string title, string text, double half)
	{
		return new GraphExample(title, "", text, Square(half));
	}

	public static readonly IReadOnlyList<GraphExample> Functions = new[]
	{
		Function("Parabola", "parabola", "x^2", -3, 3, -1, 8),
		Function("Cubic", "cubic", "x^3-3x", -3, 3, -4, 4),
		Function("Absolute value", "vshape", "abs(x)", -4, 4, -1, 5),
		Function("Square root", "halfroot", "sqrt(x)", -1, 9, -0.5, 3.5),
		Function("Hyperbola 1/x", "hyperbola", "1/x", -4, 4, -4, 4),
		Function("Sine", "sine", "sin(x)", -7, 7, -1.6, 1.6),
		Function("Cosine", "cosine", "cos(x)", -7, 7, -1.6, 1.6),
		Function("Tangent", "tangent", "tan(x)", -5, 5, -4, 4),
		Function("Sinc", "sinc", "sin(x)/x", -15, 15, -0.4, 1.2),
		Function("Exponential", "growth", "exp(x)", -3, 3, -1, 8),
		Function("Natural log", "logarithm", "ln(x)", -1, 8, -4, 3),
		Function("Bell curve", "bell", "exp(-x^2)", -3, 3, -0.2, 1.2),
		Function("Sigmoid", "sigmoid", "1/(1+exp(-x))", -8, 8, -0.2, 1.2),
		Function("Witch of Agnesi", "agnesi", "8/(x^2+4)", -6, 6, -0.5, 2.5),
		Function("x sin(1/x)", "wobble", "x*sin(1/x)", -0.5, 0.5, -0.4, 0.4),
		Function("Two waves", "waves", "sin(x)+sin(3x)/3", -7, 7, -1.5, 1.5),
	};

	public static readonly IReadOnlyList<GraphExample> Equations = new[]
	{
		Equation("Circle", "x^2+y^2=4", 3),
		Equation("Ellipse", "x^2/9+y^2/4=1", 4),
		Equation("Hyperbola", "x^2-y^2=1", 4),
		Equation("Sideways parabola", "y^2=x", 4),
		Equation("Heart", "(x^2+y^2-1)^3=x^2*y^3", 1.6),
		Equation("Lemniscate", "(x^2+y^2)^2=2(x^2-y^2)", 2),
		Equation("Folium of Descartes", "x^3+y^3=3x*y", 3),
		Equation("Cardioid", "(x^2+y^2-x)^2=x^2+y^2", 2.5),
		Equation("Astroid", "abs(x)^(2/3)+abs(y)^(2/3)=1", 1.4),
		Equation("Superellipse", "x^4+y^4=1", 1.5),
		Equation("Cassini oval", "(x^2+y^2)^2-2(x^2-y^2)=0.5", 2),
		Equation("Elliptic curve", "y^2=x^3-x+1", 3),
		Equation("Cissoid", "y^2*(2-x)=x^3", 3),
		Equation("Devil's curve", "y^2*(y^2-4)=x^2*(x^2-5)", 3.5),
		Equation("Egg crate", "sin(x)*sin(y)=0.3", 7),
		Equation("x^y = y^x", "x^y=y^x", 6),
	};

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
