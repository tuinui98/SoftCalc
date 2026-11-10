// HelpTopics.cs - The help/*.md topics and pictures embedded in the exe (owner 2026-11-10)
//
// SoftCalc.csproj embeds help/NN-name.md as `help/NN-name.md` and the
// pictures as `help/img/name.light.png` / `name.dark.png`.

using System.Reflection;

namespace SoftCalc.Logic;

internal sealed record HelpTopic(string Id, string Title, string Markdown);

internal static class HelpTopics
{
	private const string Prefix = "help/";
	private static List<HelpTopic>? _all;

	// In reading order; Id is the file name without .md ("06-conversions")
	public static IReadOnlyList<HelpTopic> All => _all ??= Load();

	private static List<HelpTopic> Load()
	{
		var assembly = Assembly.GetExecutingAssembly();
		var topics = new List<HelpTopic>();
		foreach (string name in assembly.GetManifestResourceNames()
			.Where(name => name.StartsWith(Prefix) && name.EndsWith(".md") && !name.Contains("/img/"))
			.Order(StringComparer.Ordinal))
		{
			using var stream = assembly.GetManifestResourceStream(name)!;
			using var reader = new StreamReader(stream);
			string markdown = reader.ReadToEnd();
			string id = name[Prefix.Length..^3];
			string title = markdown.Split('\n').Select(line => line.Trim())
				.FirstOrDefault(line => line.StartsWith("# "))?[2..] ?? id;
			topics.Add(new HelpTopic(id, title, markdown));
		}
		return topics;
	}

	// The topic whose id ends with `-name` ("graph" finds "11-graph"), or the first
	public static HelpTopic Find(string name)
	{
		return All.FirstOrDefault(topic => topic.Id == name || topic.Id.EndsWith("-" + name)) ?? All[0];
	}

	// A topic's id from its title, for *Title* cross-references
	public static string? IdOfTitle(string title)
	{
		return All.FirstOrDefault(topic => string.Equals(topic.Title, title, StringComparison.OrdinalIgnoreCase))?.Id;
	}

	// Topics holding every word of the search, case-insensitive; an empty search keeps all
	public static List<HelpTopic> Search(string text)
	{
		var words = SearchWords(text);
		return All.Where(topic => words.All(word => topic.Markdown.Contains(word, StringComparison.OrdinalIgnoreCase))).ToList();
	}

	public static string[] SearchWords(string text)
	{
		return text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
	}

	// A picture as a data URI: `img/x.light.png` swaps to `.dark.png` on dark; null when not embedded
	public static string? Picture(string path, bool dark)
	{
		string name = Prefix + path.TrimStart('.', '/');
		if (dark)
		{
			string darkName = name.Replace(".light.png", ".dark.png");
			if (Exists(darkName))
			{
				name = darkName;
			}
		}
		using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
		if (stream == null)
		{
			return null;
		}
		using var bytes = new MemoryStream();
		stream.CopyTo(bytes);
		return "data:image/png;base64," + Convert.ToBase64String(bytes.ToArray());
	}

	private static bool Exists(string name)
	{
		return Assembly.GetExecutingAssembly().GetManifestResourceInfo(name) != null;
	}
}
