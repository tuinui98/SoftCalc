// Ribbon.cs - Button groups under the menu bar, built from a layout (Customize Ribbon)
//
// The default layout is B3 (owner 2026-11-06): Angle, Output, Decimals
// (caption shows a sample of the format), Mode, History, Tools. Each
// button runs a menu command (RibbonCommands); check marks follow the
// menu items. Built from stock ToolStrips that wrap onto more rows when
// the window is narrow; a group can start a new row (RibbonLayout.Rows).
// Plan: design/ribbon_plan.md.

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
	private readonly List<bool> _newRows = new();    // per group control: starts a new row
	private bool _placing;

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
		_newRows.Clear();
		var old = Controls.Cast<Control>().ToList();
		Controls.Clear();
		foreach (var control in old)
		{
			control.Dispose();
		}
		bool first = true;
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
				_newRows.Add(!first && group.NewRow);
				first = false;
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
		if (command.IsGap)
		{
			return new RibbonGap(command.Id == RibbonLayout.SeparatorId, IconPixels / 2);
		}
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

	//==================================================================
	//  Rows: groups that start a new row (owner 2026-11-22)
	//==================================================================
	private bool HasNewRows => _newRows.Any(n => n) && _newRows.Count == Controls.Count;

	private int[] RowsAt(int available, out bool[] forced)
	{
		var widths = Controls.Cast<Control>().Select(c => c.GetPreferredSize(Size.Empty).Width + c.Margin.Horizontal).ToList();
		return RibbonLayout.Rows(widths, _newRows, available, out forced);
	}

	// The flow breaks for the current width: after the group before each one
	// that starts a row by itself (running on is left to the panel)
	protected override void OnLayout(LayoutEventArgs levent)
	{
		if (!_placing && _newRows.Count == Controls.Count)
		{
			_placing = true;
			try
			{
				RowsAt(ClientSize.Width - Padding.Horizontal, out var forced);
				for (int i = 0; i < Controls.Count; i++)
				{
					bool breakAfter = i + 1 < Controls.Count && forced[i + 1];
					if (GetFlowBreak(Controls[i]) != breakAfter)
					{
						SetFlowBreak(Controls[i], breakAfter);
					}
				}
			}
			finally
			{
				_placing = false;
			}
		}
		base.OnLayout(levent);
	}

	// Height for a width with the rows above; the panel's own wrapping otherwise
	public override Size GetPreferredSize(Size proposedSize)
	{
		if (!HasNewRows)
		{
			return base.GetPreferredSize(proposedSize);
		}
		int available = proposedSize.Width > 0 ? proposedSize.Width - Padding.Horizontal : int.MaxValue;
		var rows = RowsAt(available, out _);
		var sizes = Controls.Cast<Control>().Select(c => c.GetPreferredSize(Size.Empty) + c.Margin.Size).ToList();
		int height = 0, width = 0;
		foreach (var row in rows.Distinct())
		{
			var inRow = Enumerable.Range(0, rows.Length).Where(i => rows[i] == row).ToList();
			height += inRow.Max(i => sizes[i].Height);
			width = Math.Max(width, inRow.Sum(i => sizes[i].Width));
		}
		return new Size(width + Padding.Horizontal, height + Padding.Vertical);
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

// A space or a separator line in a ribbon group: half an icon wide, as
// tall as the row (owner 2026-11-11)
internal sealed class RibbonGap : ToolStripItem
{
	private readonly bool _line;

	public RibbonGap(bool line, int width)
	{
		_line = line;
		AutoSize = false;
		Size = new Size(width, 1);
		Anchor = AnchorStyles.Top | AnchorStyles.Bottom;
		Margin = Padding.Empty;
		AccessibleName = line ? "Separator" : "Space";
	}

	public bool IsLine => _line;

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		if (!_line)
		{
			return;
		}
		int x = Width / 2;
		int inset = Math.Min(4, Height / 4);
		using var pen = new Pen(SystemColors.ControlDark);
		e.Graphics.DrawLine(pen, x, inset, x, Height - 1 - inset);
	}
}
