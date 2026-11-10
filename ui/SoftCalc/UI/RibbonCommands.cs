// RibbonCommands.cs - Every menu command, for the ribbon and the Customize Ribbon window
//
// Built by walking the menu bar: a menu item without a submenu is a
// command that clicks that menu item, a submenu is a drop-down that
// copies the submenu when it opens. So every menu command can go on the
// ribbon and always does what the menu does (design/ribbon_plan.md R1).

namespace SoftCalc.UI;

internal sealed class RibbonCommand
{
	public required string Id { get; init; }
	public required string Text { get; init; }    // the menu text without & and "..."
	public required string Path { get; init; }    // "View > Decimal Places > More", the tooltip
	public string? Icon { get; init; }

	// Leaf: the menu item to click. Drop-down: Children gives its items.
	public ToolStripMenuItem? Source { get; init; }
	public Func<IEnumerable<ToolStripItem>>? Children { get; init; }

	// Text that follows the settings ("Prec 32"); a label set by the owner wins
	public Func<string>? DynamicText { get; init; }

	public bool IsDropDown => Children != null;

	// Space or separator: drawn by the ribbon, runs nothing
	public bool IsGap => Logic.RibbonLayout.IsGap(Id);
}

// One line in the Available commands tree
internal sealed class CommandNode
{
	public required string Text { get; init; }
	public RibbonCommand? Command { get; init; }
	public List<CommandNode> Children { get; } = new();
}

internal sealed class RibbonCommands
{
	// Embedded IconMCP icons; the rest show text
	private static readonly Dictionary<string, string> Icons = new()
	{
		["View/Clear History"] = "trash",
		["File/Save History"] = "save",
		["Tools/Graph"] = "line-chart",
		["Tools/Compound Interest"] = "compound-interest",
		["View/Always on Top"] = "pushpin",
		["View/Decimal Places/More"] = "decimal-increase",
		["View/Decimal Places/Fewer"] = "decimal-decrease",
		["Conversions"] = "swap-horizontal",
	};

	private readonly Dictionary<string, RibbonCommand> _byId = new();
	private IReadOnlyDictionary<string, Func<string>> _dynamicTexts = new Dictionary<string, Func<string>>();

	public List<CommandNode> Tree { get; } = new();

	public RibbonCommand? Find(string id)
	{
		return _byId.GetValueOrDefault(id);
	}

	public IEnumerable<RibbonCommand> All => _byId.Values;

	// submenus: menu items that are submenus even while empty (User Functions)
	// dynamicTexts: menu command id -> text showing its current value ("Word 32")
	public static RibbonCommands FromMenu(MenuStrip menu, IEnumerable<RibbonCommand> extras, ISet<ToolStripMenuItem> submenus,
		IReadOnlyDictionary<string, Func<string>>? dynamicTexts = null)
	{
		var commands = new RibbonCommands { _dynamicTexts = dynamicTexts ?? new Dictionary<string, Func<string>>() };
		foreach (var item in menu.Items.OfType<ToolStripMenuItem>())
		{
			commands.Tree.Add(commands.Walk(item, new List<string>(), new List<string>(), submenus));
		}
		var ribbonNode = new CommandNode { Text = "Ribbon" };
		var gaps = new[]
		{
			new RibbonCommand { Id = Logic.RibbonLayout.SpaceId, Text = "Space", Path = "Space (a gap between buttons)" },
			new RibbonCommand { Id = Logic.RibbonLayout.SeparatorId, Text = "Separator", Path = "Separator (a line between buttons)" },
		};
		foreach (var extra in gaps.Concat(extras))
		{
			commands._byId[extra.Id] = extra;
			ribbonNode.Children.Add(new CommandNode { Text = extra.Text, Command = extra });
		}
		if (ribbonNode.Children.Count > 0)
		{
			commands.Tree.Add(ribbonNode);
		}
		return commands;
	}

	private CommandNode Walk(ToolStripMenuItem item, List<string> idPath, List<string> textPath, ISet<ToolStripMenuItem> submenus)
	{
		string text = Logic.RibbonLayout.CleanText(item.Text ?? "");
		var ids = new List<string>(idPath) { item.Tag as string ?? text };    // a user function's Tag is its name
		var texts = new List<string>(textPath) { text };
		string id = Logic.RibbonLayout.CommandId(ids);
		bool isSubmenu = item.HasDropDownItems || submenus.Contains(item) || idPath.Count == 0;    // menu bar items, even while empty
		var command = new RibbonCommand
		{
			Id = id, Text = text, Path = string.Join(" > ", texts), Icon = Icons.GetValueOrDefault(id), Source = item,
			Children = isSubmenu ? () => item.DropDownItems.Cast<ToolStripItem>() : null,
			DynamicText = _dynamicTexts.GetValueOrDefault(id),
		};
		_byId.TryAdd(id, command);
		var node = new CommandNode { Text = text, Command = command };
		if (isSubmenu)
		{
			foreach (var child in item.DropDownItems.OfType<ToolStripMenuItem>())
			{
				node.Children.Add(Walk(child, ids, texts, submenus));
			}
		}
		return node;
	}

	//==================================================================
	//  Copies of menu items for a ribbon drop-down
	//==================================================================
	// One level at a time: a submenu copies its own items when it opens
	public static void FillCopies(ToolStripItemCollection target, IEnumerable<ToolStripItem> source)
	{
		target.Clear();
		foreach (var item in source)
		{
			switch (item)
			{
				case ToolStripSeparator:
					target.Add(new ToolStripSeparator());
					break;
				case ToolStripMenuItem menuItem:
					target.Add(Copy(menuItem));
					break;
			}
		}
	}

	private static ToolStripMenuItem Copy(ToolStripMenuItem source)
	{
		var copy = new ToolStripMenuItem(source.Text) { Checked = source.Checked, Enabled = source.Enabled };
		if (source.HasDropDownItems)
		{
			copy.DropDownItems.Add(new ToolStripMenuItem("..."));    // placeholder so the arrow shows
			copy.DropDownOpening += (_, _) => FillCopies(copy.DropDownItems, source.DropDownItems.Cast<ToolStripItem>().ToList());
		}
		else
		{
			copy.Click += (_, _) => source.PerformClick();
		}
		return copy;
	}
}
