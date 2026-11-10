// Ribbon.cs - Button groups under the menu bar, built from a layout (Customize Ribbon)
//
// The default layout is B3 (owner 2026-11-06): Angle, Output, Decimals
// (caption shows a sample of the format), Mode, History, Tools. Each
// button runs a menu command (RibbonCommands); check marks follow the
// menu items. Built from stock ToolStrips that wrap onto more rows when
// the window is narrow. Plan: design/ribbon_plan.md.

using System.Text.Json.Nodes;
using SoftCalc.Logic;

namespace SoftCalc.UI;

internal sealed class Ribbon : FlowLayoutPanel
{
	private const int IconSize = 20;

	private readonly List<(Label Label, string Caption)> _captions = new();
	private readonly List<(ToolStripItem Item, Func<string> Text)> _dynamic = new();
	private readonly List<Action> _unhook = new();
	private readonly ContextMenuStrip _menu = new();
	private int? _decimals;
	private List<RibbonGroup>? _builtLayout;
	private List<object?> _builtSources = new();

	// Times the controls were made again (tests: no rebuild when nothing changed)
	public int BuildCount { get; private set; }

	public event EventHandler? CustomizeRequested;
	public event EventHandler? HideRequested;

	public Ribbon()
	{
		Dock = DockStyle.Top;
		AutoSize = true;
		WrapContents = true;
		Padding = new Padding(2);
		BackColor = SystemColors.ControlLight;
		_menu.Items.Add(new ToolStripMenuItem("&Customize Ribbon...", null, (_, _) => CustomizeRequested?.Invoke(this, EventArgs.Empty)));
		_menu.Items.Add(new ToolStripMenuItem("&Hide Ribbon", null, (_, _) => HideRequested?.Invoke(this, EventArgs.Empty)));
		ContextMenuStrip = _menu;
	}

	//==================================================================
	//  Build from a layout
	//==================================================================
	// Buttons whose command is missing are left out; so are empty groups.
	// Nothing is rebuilt when the layout and every button's menu item are
	// the same as last time (the menus are refilled after start-up).
	public void Build(IReadOnlyList<RibbonGroup> layout, RibbonCommands commands)
	{
		var sources = layout.SelectMany(g => g.Items).Select(i => commands.Find(i.Id))
			.Select(c => c == null ? null : (object?)c.Source ?? c).ToList();
		if (_builtLayout != null && RibbonLayout.SameAs(layout, _builtLayout)
			&& sources.Count == _builtSources.Count && sources.Zip(_builtSources).All(p => ReferenceEquals(p.First, p.Second) || IsSameExtra(p.First, p.Second)))
		{
			return;
		}
		_builtLayout = RibbonLayout.Clone(layout);
		_builtSources = sources;
		BuildCount++;
		bool drawn = IsHandleCreated;
		if (drawn)
		{
			SendMessage(Handle, WmSetRedraw, IntPtr.Zero, IntPtr.Zero);    // no flicker while the groups are replaced
		}
		SuspendLayout();
		foreach (var unhook in _unhook)
		{
			unhook();
		}
		_unhook.Clear();
		_captions.Clear();
		_dynamic.Clear();
		var old = Controls.Cast<Control>().ToList();
		Controls.Clear();
		foreach (var control in old)
		{
			control.Dispose();
		}
		foreach (var group in layout)
		{
			var items = group.Items
				.Select(item => (item, command: commands.Find(item.Id)))
				.Where(pair => pair.command != null)
				.Select(pair => MakeItem(pair.item, pair.command!))
				.ToArray();
			if (items.Length > 0)
			{
				Controls.Add(Group(group.Caption, group.Columns, items));
			}
		}
		ShowDecimals(_decimals, force: true);
		ResumeLayout();
		if (drawn)
		{
			SendMessage(Handle, WmSetRedraw, (IntPtr)1, IntPtr.Zero);
			Invalidate(true);
		}
	}

	// Extra commands (Precision) are made anew each time; the same id is the same command
	private static bool IsSameExtra(object? a, object? b)
	{
		return a is RibbonCommand x && b is RibbonCommand y && x.Id == y.Id;
	}

	private const int WmSetRedraw = 0x000B;

	[System.Runtime.InteropServices.DllImport("user32.dll")]
	private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

	private ToolStripItem MakeItem(RibbonItem item, RibbonCommand command)
	{
		var image = ImageFor(item, command);
		string display = item.Display ?? (image != null ? "both" : "text");
		bool hasIcon = image != null && display != "text";
		ToolStripItem button;
		if (command.IsDropDown)
		{
			var dropDown = new ToolStripDropDownButton();
			dropDown.DropDownItems.Add(new ToolStripMenuItem("..."));    // placeholder so it opens
			dropDown.DropDownOpening += (_, _) => RibbonCommands.FillCopies(dropDown.DropDownItems, command.Children!().ToList());
			button = dropDown;
		}
		else
		{
			var plain = new ToolStripButton();
			var source = command.Source!;
			plain.Click += (_, _) => source.PerformClick();
			EventHandler sync = (_, _) =>
			{
				plain.Checked = source.Checked;
				plain.Enabled = source.Enabled;
			};
			source.CheckedChanged += sync;
			source.EnabledChanged += sync;
			_unhook.Add(() =>
			{
				source.CheckedChanged -= sync;
				source.EnabledChanged -= sync;
			});
			sync(null, EventArgs.Empty);
			button = plain;
		}
		button.Text = item.Label ?? command.DynamicText?.Invoke() ?? command.Text;
		if (item.Label == null && command.DynamicText != null)
		{
			_dynamic.Add((button, command.DynamicText));
		}
		button.ToolTipText = command.Path;
		button.ImageScaling = ToolStripItemImageScaling.None;
		if (hasIcon)
		{
			button.Image = image;
		}
		button.DisplayStyle = !hasIcon ? ToolStripItemDisplayStyle.Text
			: display == "icon" ? ToolStripItemDisplayStyle.Image : ToolStripItemDisplayStyle.ImageAndText;
		return button;
	}

	// Every group frame as tall as the tallest one (owner 2026-11-06)
	private void EqualizeHeights()
	{
		var groups = Controls.OfType<TableLayoutPanel>().ToList();
		if (groups.Count == 0)
		{
			return;
		}
		foreach (var group in groups)
		{
			group.MinimumSize = Size.Empty;
		}
		int height = groups.Max(g => g.GetPreferredSize(Size.Empty).Height);
		foreach (var group in groups)
		{
			group.MinimumSize = new Size(0, height);
		}
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		EqualizeHeights();    // sizes are final once the handles exist
	}

	protected override void OnDpiChangedAfterParent(EventArgs e)
	{
		base.OnDpiChangedAfterParent(e);
		EqualizeHeights();
	}

	//==================================================================
	//  State from the engine settings
	//==================================================================
	// Check marks follow the menu items; this updates texts that show values
	public void ShowSettings(JsonObject settings)
	{
		foreach (var (item, text) in _dynamic)
		{
			string value = text();
			if (item.Text != value)
			{
				item.Text = value;
			}
		}
		ShowDecimals((int?)settings["decimals"]);
	}

	// Heights are equalized again only when a caption's text changes
	private void ShowDecimals(int? decimals, bool force = false)
	{
		_decimals = decimals;
		bool changed = force;
		foreach (var (label, caption) in _captions)
		{
			string text = caption.Replace(RibbonLayout.DecimalsToken, DecimalSteps.Sample(decimals));
			if (label.Text != text)
			{
				label.Text = text;
				changed = true;
			}
		}
		if (changed)
		{
			EqualizeHeights();
		}
	}

	//==================================================================
	//  Building blocks
	//==================================================================
	private int IconPixels => (int)Math.Round(IconSize * DeviceDpi / 96.0);

	// The own icon when it draws, else the command's icon, else none
	private Bitmap? ImageFor(RibbonItem item, RibbonCommand command)
	{
		if (item.IconSvg != null)
		{
			try
			{
				return SvgIcon.FromText(item.IconSvg, IconPixels, SystemColors.ControlText);
			}
			catch (SvgException)
			{
				// a hand-edited settings.json: fall back to the command's icon
			}
		}
		return command.Icon != null ? SvgIcon.Get(command.Icon, IconPixels, SystemColors.ControlText) : null;
	}

	private Control Group(string caption, int columns, ToolStripItem[] items)
	{
		var strip = new ToolStrip
		{
			GripStyle = ToolStripGripStyle.Hidden, LayoutStyle = ToolStripLayoutStyle.Table, Dock = DockStyle.None,
			AutoSize = true, BackColor = SystemColors.Control, RenderMode = ToolStripRenderMode.System,
			ContextMenuStrip = _menu,
		};
		if (strip.LayoutSettings is TableLayoutSettings table)
		{
			table.ColumnCount = columns;
		}
		strip.Items.AddRange(items);
		var label = new Label
		{
			Text = caption, AutoSize = true,    // a long Decimals sample widens its group
			Anchor = AnchorStyles.None, ForeColor = SystemColors.GrayText, ContextMenuStrip = _menu, UseMnemonic = false,
		};
		if (caption.Contains(RibbonLayout.DecimalsToken))
		{
			_captions.Add((label, caption));
		}
		var box = new TableLayoutPanel
		{
			AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 2,
			Margin = new Padding(2), BorderStyle = BorderStyle.FixedSingle, BackColor = SystemColors.Control,
			ContextMenuStrip = _menu,
		};
		box.RowStyles.Add(new RowStyle(SizeType.Percent, 100));    // spare height goes around the buttons
		box.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
		strip.Anchor = AnchorStyles.Left;    // buttons centred vertically, captions on one line
		box.Controls.Add(strip, 0, 0);
		box.Controls.Add(label, 0, 1);
		return box;
	}
}
