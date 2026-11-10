// RibbonLayoutTests.cs - Customize Ribbon: layout data, edits, command ids, saving across windows

using SoftCalc.Engine;
using SoftCalc.Logic;
using SoftCalc.Persistence;
using SoftCalc.UI;

namespace SoftCalc.Tests;

public sealed class RibbonLayoutTests
{
	private static string TempFile(string name)
	{
		return Path.Combine(Path.GetTempPath(), $"softcalc-{name}-" + Guid.NewGuid().ToString("N") + ".json");
	}

	private static List<RibbonGroup> Two()
	{
		return new List<RibbonGroup>
		{
			new() { Caption = "A", Items = { new RibbonItem { Id = "a1" }, new RibbonItem { Id = "a2" } } },
			new() { Caption = "B", Items = { new RibbonItem { Id = "b1" } } },
		};
	}

	private static string Ids(List<RibbonGroup> layout)
	{
		return string.Join(" | ", layout.Select(g => g.Caption + ":" + string.Join(",", g.Items.Select(i => i.Id))));
	}

	[Theory]
	[InlineData("S&ave History...", "Save History")]
	[InlineData("&Save Functions && Variables...", "Save Functions & Variables")]
	[InlineData("R&PN Mode", "RPN Mode")]
	[InlineData("ft", "ft")]
	public void CleanTextDropsMnemonicsAndDots(string menu, string want)
	{
		Assert.Equal(want, RibbonLayout.CleanText(menu));
	}

	[Fact]
	public void DefaultIdsAllResolveAgainstTheRealMenus()
	{
		string root = Path.Combine(Path.GetTempPath(), "softcalc-ribbon-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(root);
		try
		{
			using var session = new EngineSession(EngineLocator.Locate(Path.Combine(Path.GetTempPath(), "softcalc-test-engine")));
			using var form = new MainForm(session, new DataFolder(root));
			var commands = form.RibbonCommandList();
			var missing = RibbonLayout.Default().SelectMany(g => g.Items).Where(i => commands.Find(i.Id) == null).Select(i => i.Id);
			Assert.Empty(missing);
			Assert.True(commands.Find("Conversions")!.IsDropDown);
			Assert.True(commands.Find("View/Theme")!.IsDropDown);
			Assert.False(commands.Find("View/Theme/Dark")!.IsDropDown);
			Assert.Equal("Prec 32", commands.Find(RibbonLayout.PrecisionId)!.DynamicText!());
			Assert.Equal("Word 32", commands.Find("View/Word Size")!.DynamicText!());
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void UserFunctionIdIsItsNameAndAnEmptySubmenuIsADropDown()
	{
		using var menu = new MenuStrip();
		var functions = new ToolStripMenuItem("F&unctions");
		var user = new ToolStripMenuItem("&User Functions");
		functions.DropDownItems.Add(user);
		menu.Items.Add(functions);
		var empty = RibbonCommands.FromMenu(menu, Array.Empty<RibbonCommand>(), new HashSet<ToolStripMenuItem> { user });
		Assert.True(empty.Find("Functions/User Functions")!.IsDropDown);
		user.DropDownItems.Add(new ToolStripMenuItem("g(x, y)") { Tag = "g" });
		var filled = RibbonCommands.FromMenu(menu, Array.Empty<RibbonCommand>(), new HashSet<ToolStripMenuItem> { user });
		var g = filled.Find("Functions/User Functions/g")!;
		Assert.Equal(("g(x, y)", "Functions > User Functions > g(x, y)"), (g.Text, g.Path));
	}

	[Fact]
	public void LeafCommandClicksItsMenuItem()
	{
		using var menu = new MenuStrip();
		int clicks = 0;
		var view = new ToolStripMenuItem("&View");
		view.DropDownItems.Add(new ToolStripMenuItem("&Clear History", null, (_, _) => clicks++));
		menu.Items.Add(view);
		var command = RibbonCommands.FromMenu(menu, Array.Empty<RibbonCommand>(), new HashSet<ToolStripMenuItem>()).Find("View/Clear History")!;
		command.Source!.PerformClick();
		Assert.Equal(1, clicks);
		Assert.Equal("trash", command.Icon);
	}

	[Fact]
	public void RibbonRebuildsOnlyWhenSomethingItShowsChanged()
	{
		using var menu = new MenuStrip();
		var view = new ToolStripMenuItem("&View");
		view.DropDownItems.Add(new ToolStripMenuItem("&Clear History"));
		var user = new ToolStripMenuItem("&User Functions");
		menu.Items.AddRange(new ToolStripItem[] { view, user });
		var subs = new HashSet<ToolStripMenuItem> { user };
		var layout = new List<RibbonGroup> { new() { Caption = "G", Items = { new RibbonItem { Id = "View/Clear History" } } } };
		using var ribbon = new Ribbon();
		ribbon.Build(layout, RibbonCommands.FromMenu(menu, Array.Empty<RibbonCommand>(), subs));
		user.DropDownItems.Add(new ToolStripMenuItem("g(x)") { Tag = "g" });    // menus refilled, nothing shown changed
		ribbon.Build(layout, RibbonCommands.FromMenu(menu, Array.Empty<RibbonCommand>(), subs));
		Assert.Equal(1, ribbon.BuildCount);
		layout[0].Items.Add(new RibbonItem { Id = "User Functions/g" });
		ribbon.Build(layout, RibbonCommands.FromMenu(menu, Array.Empty<RibbonCommand>(), subs));
		Assert.Equal(2, ribbon.BuildCount);
		user.DropDownItems.Clear();
		user.DropDownItems.Add(new ToolStripMenuItem("g(x, y)") { Tag = "g" });    // a new menu item: buttons must click it
		ribbon.Build(layout, RibbonCommands.FromMenu(menu, Array.Empty<RibbonCommand>(), subs));
		Assert.Equal(3, ribbon.BuildCount);
	}

	[Fact]
	public void SpaceAndSeparatorAreRibbonCommandsDrawnAsGaps()
	{
		using var menu = new MenuStrip();
		var view = new ToolStripMenuItem("&View");
		view.DropDownItems.Add(new ToolStripMenuItem("&Clear History"));
		menu.Items.Add(view);
		var commands = RibbonCommands.FromMenu(menu, Array.Empty<RibbonCommand>(), new HashSet<ToolStripMenuItem>());
		var ribbonNode = commands.Tree.Single(n => n.Text == "Ribbon");
		Assert.Equal(new[] { "Space", "Separator" }, ribbonNode.Children.Select(c => c.Text));
		Assert.True(commands.Find(RibbonLayout.SpaceId)!.IsGap);
		Assert.False(commands.Find("View/Clear History")!.IsGap);

		var layout = new List<RibbonGroup>
		{
			new()
			{
				Caption = "G", Columns = 5, Items =
				{
					new RibbonItem { Id = "View/Clear History" }, new RibbonItem { Id = RibbonLayout.SpaceId },
					new RibbonItem { Id = "View/Clear History" }, new RibbonItem { Id = RibbonLayout.SeparatorId, Label = "x" },
				},
			},
		};
		RibbonLayout.Sanitize(layout);
		Assert.Null(layout[0].Items[3].Label);    // a gap has no label
		using var ribbon = new Ribbon();
		ribbon.Build(layout, commands);
		var items = ribbon.Controls.Cast<Control>().SelectMany(Descendants).OfType<ToolStrip>().SelectMany(s => s.Items.Cast<ToolStripItem>()).ToList();
		Assert.Equal(4, items.Count);
		var gaps = items.OfType<RibbonGap>().ToList();
		Assert.Equal(new[] { false, true }, gaps.Select(g => g.IsLine));
		Assert.All(gaps, g => Assert.Equal(10, g.Width));    // half of a 20 px icon at 96 dpi
	}

	private static IEnumerable<Control> Descendants(Control control)
	{
		yield return control;
		foreach (var child in control.Controls.Cast<Control>().SelectMany(Descendants))
		{
			yield return child;
		}
	}

	[Fact]
	public void DropDownCopiesFollowTheMenuAndClickIt()
	{
		int clicks = 0;
		var dark = new ToolStripMenuItem("&Dark", null, (_, _) => clicks++) { Checked = true };
		var sub = new ToolStripMenuItem("&More");
		sub.DropDownItems.Add(new ToolStripMenuItem("x"));
		using var target = new ContextMenuStrip();
		RibbonCommands.FillCopies(target.Items, new ToolStripItem[] { dark, new ToolStripSeparator(), sub });
		Assert.Equal(3, target.Items.Count);
		var copy = (ToolStripMenuItem)target.Items[0];
		Assert.True(copy.Checked);
		copy.PerformClick();
		Assert.Equal(1, clicks);
		Assert.True(((ToolStripMenuItem)target.Items[2]).HasDropDownItems);    // filled when it opens
	}

	[Fact]
	public void AddGoesAfterTheSelectionOrAtTheGroupEnd()
	{
		var layout = Two();
		Assert.Equal(new RibbonPos(0, 1), RibbonLayout.Add(layout, new RibbonPos(0, 0), "x"));
		Assert.Equal(new RibbonPos(1, 1), RibbonLayout.Add(layout, new RibbonPos(1, -1), "y"));
		Assert.Equal("A:a1,x,a2 | B:b1,y", Ids(layout));
	}

	[Fact]
	public void MoveCrossesGroupEdgesAndSwapsGroups()
	{
		var layout = Two();
		Assert.Equal(new RibbonPos(1, 0), RibbonLayout.Move(layout, new RibbonPos(0, 1), +1));    // a2 to the top of B
		Assert.Equal("A:a1 | B:a2,b1", Ids(layout));
		Assert.Equal(new RibbonPos(0, 1), RibbonLayout.Move(layout, new RibbonPos(1, 0), -1));    // back to the end of A
		Assert.Equal(new RibbonPos(0, 0), RibbonLayout.Move(layout, new RibbonPos(0, 0), -1));    // top of the first group: stays
		Assert.Equal(new RibbonPos(1, -1), RibbonLayout.Move(layout, new RibbonPos(0, -1), +1));
		Assert.Equal("B:b1 | A:a1,a2", Ids(layout));
	}

	[Fact]
	public void RemoveSelectsANeighbour()
	{
		var layout = Two();
		Assert.Equal(new RibbonPos(0, 0), RibbonLayout.Remove(layout, new RibbonPos(0, 1)));
		Assert.Equal(new RibbonPos(1, -1), RibbonLayout.Remove(layout, new RibbonPos(1, 0)));    // empty group stays
		Assert.Equal(new RibbonPos(0, -1), RibbonLayout.Remove(layout, new RibbonPos(1, -1)));
		Assert.Null(RibbonLayout.Remove(layout, new RibbonPos(0, -1)));
		Assert.Empty(layout);
		Assert.Equal(new RibbonPos(0, 0), RibbonLayout.Add(layout, null, "z"));    // adding to nothing makes a group
	}

	[Fact]
	public void DefaultIsSavedAsNullAndChangesRoundTrip()
	{
		Assert.Null(RibbonLayout.ToSaved(RibbonLayout.Default()));
		string path = TempFile("layout");
		try
		{
			var layout = Two();
			layout[0].Items[0].Label = "One";
			layout[0].Items[0].Display = "text";
			SettingsStore.Save(path, new AppSettings { Ribbon = RibbonLayout.ToSaved(layout) });
			var loaded = SettingsStore.Load(path).Settings.Ribbon!;
			Assert.True(RibbonLayout.SameAs(layout, loaded));
			File.WriteAllText(path, "{\"ribbon\":[{\"caption\":null,\"columns\":99,\"items\":[{\"id\":\"a\",\"display\":\"huge\"},{\"id\":\"\"}]}]}");
			var odd = SettingsStore.Load(path).Settings.Ribbon!;
			Assert.Equal(("", 12, 1, null), (odd[0].Caption, odd[0].Columns, odd[0].Items.Count, odd[0].Items[0].Display));
			File.WriteAllText(path, "{}");
			Assert.Null(SettingsStore.Load(path).Settings.Ribbon);    // older files: default
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void ClosingWindowKeepsALayoutAnotherWindowSaved()
	{
		string path = TempFile("windows");
		try
		{
			var older = new AppSettings { Precision = 64 };    // window A, opened before the change
			SettingsStore.Save(path, new AppSettings());
			SettingsStore.SaveRibbon(path, Two());    // window B: Customize Ribbon OK
			Assert.Equal("A:a1,a2 | B:b1", Ids(SettingsStore.Load(path).Settings.Ribbon!));
			SettingsStore.SaveKeepingRibbon(path, older, ownRibbon: false);    // A closes
			var after = SettingsStore.Load(path).Settings;
			Assert.Equal((64, "A:a1,a2 | B:b1"), (after.Precision, Ids(after.Ribbon!)));
			SettingsStore.SaveKeepingRibbon(path, new AppSettings { Ribbon = null }, ownRibbon: true);    // a window that reset it
			Assert.Null(SettingsStore.Load(path).Settings.Ribbon);
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void SaveRibbonOnAFreshFolderWritesDefaultsToo()
	{
		string path = TempFile("fresh");
		try
		{
			SettingsStore.SaveRibbon(path, Two());
			var loaded = SettingsStore.Load(path);
			Assert.Null(loaded.Warning);
			Assert.Equal((32, "A:a1,a2 | B:b1"), (loaded.Settings.Precision, Ids(loaded.Settings.Ribbon!)));
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void DragMovesButtonsWithinAndAcrossGroups()
	{
		var layout = Two();    // A:a1,a2 | B:b1
		Assert.Equal(new RibbonPos(0, 1), RibbonLayout.MoveTo(layout, new RibbonPos(0, 0), 0, 2));    // a1 below a2
		Assert.Equal("A:a2,a1 | B:b1", Ids(layout));
		Assert.Equal(new RibbonPos(0, 1), RibbonLayout.MoveTo(layout, new RibbonPos(0, 1), 0, 1));    // dropped on itself
		Assert.Equal("A:a2,a1 | B:b1", Ids(layout));
		Assert.Equal(new RibbonPos(1, 1), RibbonLayout.MoveTo(layout, new RibbonPos(0, 0), 1, 1));    // a2 after b1
		Assert.Equal("A:a1 | B:b1,a2", Ids(layout));
		Assert.Equal(new RibbonPos(0, 0), RibbonLayout.MoveTo(layout, new RibbonPos(1, 0), 0, 0));
		Assert.Equal("A:b1,a1 | B:a2", Ids(layout));
	}

	[Fact]
	public void DragMovesGroupsAndInsertsCommands()
	{
		var layout = Two();
		layout.Add(new RibbonGroup { Caption = "C" });
		Assert.Equal(new RibbonPos(2, -1), RibbonLayout.MoveTo(layout, new RibbonPos(0, -1), 3, -1));    // A to the end
		Assert.Equal("B:b1 | C: | A:a1,a2", Ids(layout));
		Assert.Equal(new RibbonPos(0, -1), RibbonLayout.MoveTo(layout, new RibbonPos(2, -1), 0, -1));
		Assert.Equal(new RibbonPos(1, -1), RibbonLayout.MoveTo(layout, new RibbonPos(1, -1), 2, -1));    // B before C: stays
		Assert.Equal("A:a1,a2 | B:b1 | C:", Ids(layout));
		Assert.Equal(new RibbonPos(2, 0), RibbonLayout.InsertAt(layout, 2, 0, "n"));
		Assert.Equal(new RibbonPos(0, 1), RibbonLayout.InsertAt(layout, 0, 1, "m"));
		Assert.Equal("A:a1,m,a2 | B:b1 | C:n", Ids(layout));
		var empty = new List<RibbonGroup>();
		Assert.Equal(new RibbonPos(0, 0), RibbonLayout.InsertAt(empty, 0, 0, "z"));    // no groups: one is made
	}
}
