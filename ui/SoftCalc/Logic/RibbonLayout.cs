// RibbonLayout.cs - The ribbon as data: groups of command ids (Customize Ribbon, design/ribbon_plan.md)
//
// A command id is the menu path of the command, without `&` and a
// trailing "...": `View/Hexadecimal Output`, `Conversions/Length/ft/in`.
// The default layout is B3 (owner 2026-11-06); settings.json keeps a
// changed layout under "ribbon" and null for the default.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace SoftCalc.Logic;

internal sealed class RibbonItem
{
	public string Id { get; set; } = "";

	// Shown text instead of the command's name; null = the name
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Label { get; set; }

	// "icon", "text" or "both"; null = icon and text when the command has an icon
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? Display { get; set; }
}

internal sealed class RibbonGroup
{
	// `{decimals}` in the caption shows the decimal places sample
	public string Caption { get; set; } = "";
	public int Columns { get; set; } = 2;
	public List<RibbonItem> Items { get; set; } = new();
}

// A place in the layout: a group (Item = -1) or a button in it
internal readonly record struct RibbonPos(int Group, int Item)
{
	public bool IsGroup => Item < 0;
}

internal static class RibbonLayout
{
	public const string DecimalsToken = "{decimals}";
	public const string PrecisionId = "Ribbon/Precision";    // the only command not taken from a menu
	public const int MaxColumns = 12;
	public static readonly string[] Displays = { "icon", "text", "both" };

	private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

	//==================================================================
	//  Default layout (B3) and copies
	//==================================================================
	public static List<RibbonGroup> Default()
	{
		return new List<RibbonGroup>
		{
			Group("Angle", 3, Item("View/Advanced Options/Degrees", "Deg"), Item("View/Advanced Options/Radians", "Rad"),
				Item("View/Advanced Options/Grads", "Grad")),
			Group("Output", 5, Item("View/Decimal Output", "Dec"), Item("View/Hexadecimal Output", "Hex"),
				Item("View/Binary Output", "Bin"), Item("View/Octal Output", "Oct"), Item("View/Word Size")),
			Group("Decimals  " + DecimalsToken, 3, Item("View/Decimal Places/More", display: "icon"),
				Item("View/Decimal Places/Fewer", display: "icon"), Item("View/Decimal Places/All Digits", "All")),
			Group("Mode", 2, Item("View/Advanced Options/RPN Mode", "RPN"), Item(PrecisionId)),
			Group("History", 2, Item("View/Clear History", "Clear"), Item("File/Save History", "Save")),
			Group("Tools", 4, Item("Tools/Graph"), Item("Tools/Compound Interest", "Interest"),
				Item("View/Always on Top", "On top"), Item("Conversions")),
		};
	}

	private static RibbonGroup Group(string caption, int columns, params RibbonItem[] items)
	{
		return new RibbonGroup { Caption = caption, Columns = columns, Items = items.ToList() };
	}

	private static RibbonItem Item(string id, string? label = null, string? display = null)
	{
		return new RibbonItem { Id = id, Label = label, Display = display };
	}

	public static List<RibbonGroup> Clone(IEnumerable<RibbonGroup> layout)
	{
		return layout.Select(g => new RibbonGroup
		{
			Caption = g.Caption, Columns = g.Columns,
			Items = g.Items.Select(i => new RibbonItem { Id = i.Id, Label = i.Label, Display = i.Display }).ToList(),
		}).ToList();
	}

	public static bool SameAs(IEnumerable<RibbonGroup> a, IEnumerable<RibbonGroup> b)
	{
		return JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json);
	}

	// What settings.json keeps: null when it is the default
	public static List<RibbonGroup>? ToSaved(IEnumerable<RibbonGroup> layout)
	{
		return SameAs(layout, Default()) ? null : Clone(layout);
	}

	// Values from a hand-edited or older file that would break the ribbon
	public static void Sanitize(List<RibbonGroup> layout)
	{
		layout.RemoveAll(g => g == null);
		foreach (var group in layout)
		{
			group.Caption ??= "";
			group.Columns = Math.Clamp(group.Columns, 1, MaxColumns);
			group.Items ??= new List<RibbonItem>();
			group.Items.RemoveAll(i => i == null || string.IsNullOrEmpty(i.Id));
			foreach (var item in group.Items)
			{
				if (item.Display != null && !Displays.Contains(item.Display))
				{
					item.Display = null;
				}
				if (string.IsNullOrEmpty(item.Label))
				{
					item.Label = null;
				}
			}
		}
	}

	//==================================================================
	//  Command ids from menu texts
	//==================================================================
	public static string CleanText(string menuText)
	{
		string text = menuText.Replace("&&", "\u0001").Replace("&", "").Replace("\u0001", "&").Trim();
		if (text.EndsWith("...", StringComparison.Ordinal))
		{
			text = text[..^3].TrimEnd();
		}
		return text;
	}

	public static string CommandId(IEnumerable<string> path)
	{
		return string.Join("/", path);
	}

	//==================================================================
	//  Editing (Customize Ribbon window)
	//==================================================================
	// Adds after the selected button, or at the end of the selected group; returns where it went
	public static RibbonPos Add(List<RibbonGroup> layout, RibbonPos? at, string id)
	{
		if (layout.Count == 0)
		{
			layout.Add(new RibbonGroup { Caption = "New group" });
		}
		var pos = at ?? new RibbonPos(layout.Count - 1, -1);
		var items = layout[pos.Group].Items;
		int index = pos.IsGroup ? items.Count : pos.Item + 1;
		items.Insert(index, new RibbonItem { Id = id });
		return new RibbonPos(pos.Group, index);
	}

	// Removes a button or a whole group; returns what to select next, or null
	public static RibbonPos? Remove(List<RibbonGroup> layout, RibbonPos pos)
	{
		if (pos.IsGroup)
		{
			layout.RemoveAt(pos.Group);
			return layout.Count == 0 ? null : new RibbonPos(Math.Min(pos.Group, layout.Count - 1), -1);
		}
		var items = layout[pos.Group].Items;
		items.RemoveAt(pos.Item);
		return items.Count == 0 ? new RibbonPos(pos.Group, -1) : new RibbonPos(pos.Group, Math.Min(pos.Item, items.Count - 1));
	}

	public static RibbonPos NewGroup(List<RibbonGroup> layout, RibbonPos? at)
	{
		int index = at is { } pos ? pos.Group + 1 : layout.Count;
		layout.Insert(index, new RibbonGroup { Caption = "New group" });
		return new RibbonPos(index, -1);
	}

	// Moves one step up (-1) or down (+1). A button at the edge of its
	// group moves into the neighbouring group; a group swaps with its
	// neighbour. Returns the new place (the same when it cannot move).
	public static RibbonPos Move(List<RibbonGroup> layout, RibbonPos pos, int step)
	{
		if (pos.IsGroup)
		{
			int target = pos.Group + step;
			if (target < 0 || target >= layout.Count)
			{
				return pos;
			}
			(layout[pos.Group], layout[target]) = (layout[target], layout[pos.Group]);
			return new RibbonPos(target, -1);
		}
		var items = layout[pos.Group].Items;
		var item = items[pos.Item];
		int next = pos.Item + step;
		if (next >= 0 && next < items.Count)
		{
			(items[pos.Item], items[next]) = (items[next], items[pos.Item]);
			return new RibbonPos(pos.Group, next);
		}
		int group = pos.Group + step;
		if (group < 0 || group >= layout.Count)
		{
			return pos;
		}
		items.RemoveAt(pos.Item);
		var into = layout[group].Items;
		int index = step < 0 ? into.Count : 0;    // last of the group above, first of the group below
		into.Insert(index, item);
		return new RibbonPos(group, index);
	}
}
