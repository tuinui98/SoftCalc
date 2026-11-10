// CustomizeRibbonDialog.cs - Customize Ribbon window: add, remove, edit and order ribbon buttons
//
// Modelled on ImageGlass Settings > Layout > Toolbar (owner 2026-11-08),
// with groups because the ribbon has them. Left: every menu command in
// menu order, with a menu filter and a search box. Right: the ribbon's
// groups and buttons; the selected row has Up / Down / Edit / Remove
// buttons at its right end. Apply rebuilds the real ribbon at once.
// Plan: design/ribbon_plan.md R2.

using SoftCalc.Logic;

namespace SoftCalc.UI;

internal sealed class CustomizeRibbonDialog : Form
{
	private readonly RibbonCommands _commands;
	private readonly Action<List<RibbonGroup>> _apply;
	private List<RibbonGroup> _layout;
	private readonly TreeView _available = new() { Dock = DockStyle.Fill, HideSelection = false };
	private readonly RowTree _ribbon = new() { Dock = DockStyle.Fill, HideSelection = false, ShowRootLines = false, ShowNodeToolTips = true };
	private readonly ComboBox _filter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
	private readonly TextBox _search = new() { Dock = DockStyle.Fill, PlaceholderText = "Search" };
	private readonly FlowLayoutPanel _rowButtons = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = Padding.Empty, Padding = Padding.Empty };
	private readonly Button _up;
	private readonly Button _down;
	private readonly Button _edit;
	private readonly Button _remove;

	public CustomizeRibbonDialog(RibbonCommands commands, IEnumerable<RibbonGroup> layout, Action<List<RibbonGroup>> apply)
	{
		_commands = commands;
		_apply = apply;
		_layout = RibbonLayout.Clone(layout);
		Text = "Customize Ribbon";
		Icon = SvgIcon.ProgramIcon();
		ShowInTaskbar = false;
		MinimizeBox = false;
		StartPosition = FormStartPosition.CenterParent;
		ClientSize = new Size(760, 520);
		MinimumSize = new Size(600, 400);

		//==================================================================
		//  BLOCK 1 - Left: Available commands
		//==================================================================
		_filter.Items.Add("All menus");
		foreach (var node in commands.Tree)
		{
			_filter.Items.Add(node.Text);
		}
		_filter.SelectedIndex = 0;
		_filter.SelectedIndexChanged += (_, _) => FillAvailable();
		_search.TextChanged += (_, _) => FillAvailable();
		_available.NodeMouseDoubleClick += (_, e) =>
		{
			if (e.Node?.Nodes.Count == 0)
			{
				AddSelected();    // a submenu node opens on double-click; Add puts it on as a drop-down
			}
		};
		_available.KeyDown += (_, e) =>
		{
			if (e.KeyCode == Keys.Enter)
			{
				AddSelected();
				e.Handled = true;
			}
		};
		var leftHead = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
		leftHead.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
		leftHead.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
		leftHead.Controls.Add(_filter, 0, 0);
		leftHead.Controls.Add(_search, 1, 0);
		// end BLOCK 1

		//==================================================================
		//  BLOCK 2 - Middle: Add and Remove
		//==================================================================
		var add = new Button { Text = "Add  ›", Size = new Size(90, 28), Margin = new Padding(3, 3, 3, 8) };
		add.Click += (_, _) => AddSelected();
		var removeMiddle = new Button { Text = "‹  Remove", Size = new Size(90, 28) };
		removeMiddle.Click += (_, _) => RemoveSelected();
		var middle = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
		middle.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
		middle.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		middle.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		middle.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
		add.Anchor = removeMiddle.Anchor = AnchorStyles.Left | AnchorStyles.Right;
		middle.Controls.Add(add, 0, 1);
		middle.Controls.Add(removeMiddle, 0, 2);
		// end BLOCK 2

		//==================================================================
		//  BLOCK 3 - Right: the ribbon tree and its row buttons
		//==================================================================
		var newGroup = new Button { Text = "New group", AutoSize = true };
		newGroup.Click += (_, _) => Select(RibbonLayout.NewGroup(_layout, Selected()), refill: true);
		var reset = new Button { Text = "Reset to default", AutoSize = true };
		reset.Click += (_, _) =>
		{
			_layout = RibbonLayout.Default();
			FillRibbon(null);
		};
		var rightHead = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, Margin = Padding.Empty };
		rightHead.Controls.AddRange(new Control[] { newGroup, reset });

		_up = RowButton("▲", "Move up (Ctrl+Up)", () => MoveSelected(-1));
		_down = RowButton("▼", "Move down (Ctrl+Down)", () => MoveSelected(+1));
		_edit = RowButton("✎", "Edit (F2)", EditSelected);
		_remove = RowButton("✕", "Remove (Delete)", RemoveSelected);
		_rowButtons.Controls.AddRange(new Control[] { _up, _down, _edit, _remove });
		_rowButtons.Visible = false;
		_ribbon.Controls.Add(_rowButtons);
		_ribbon.ItemHeight = Math.Max(_ribbon.ItemHeight, _up.Height + 2);
		_ribbon.AfterSelect += (_, _) => PlaceRowButtons();
		_ribbon.Scrolled += (_, _) => PlaceRowButtons();
		_ribbon.Resize += (_, _) => PlaceRowButtons();
		_ribbon.AfterExpand += (_, _) => PlaceRowButtons();
		_ribbon.AfterCollapse += (_, _) => PlaceRowButtons();
		_ribbon.NodeMouseDoubleClick += (_, _) => EditSelected();
		_ribbon.KeyDown += OnRibbonKey;
		HookDragAndDrop();
		// end BLOCK 3

		//==================================================================
		//  BLOCK 4 - Frame and OK / Cancel / Apply
		//==================================================================
		var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3, Padding = new Padding(8) };
		grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
		grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
		grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
		grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
		grid.Controls.Add(new Label { Text = "&Available commands:", AutoSize = true, Margin = new Padding(3, 3, 3, 4) }, 0, 0);
		grid.Controls.Add(new Label { Text = "&Ribbon:", AutoSize = true, Margin = new Padding(3, 3, 3, 4) }, 2, 0);
		grid.Controls.Add(leftHead, 0, 1);
		grid.Controls.Add(rightHead, 2, 1);
		grid.Controls.Add(_available, 0, 2);
		grid.Controls.Add(middle, 1, 2);
		grid.Controls.Add(_ribbon, 2, 2);

		var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(80, 0) };
		ok.Click += (_, _) => _apply(RibbonLayout.Clone(_layout));
		var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(80, 0) };
		var applyButton = new Button { Text = "Apply", AutoSize = true, MinimumSize = new Size(80, 0) };
		applyButton.Click += (_, _) => _apply(RibbonLayout.Clone(_layout));
		var buttons = new FlowLayoutPanel
		{
			Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8, 0, 8, 8),
		};
		buttons.Controls.AddRange(new Control[] { applyButton, cancel, ok });
		Controls.Add(grid);
		Controls.Add(buttons);
		AcceptButton = ok;
		CancelButton = cancel;

		FillAvailable();
		FillRibbon(_layout.Count > 0 ? new RibbonPos(0, -1) : null);
		// end BLOCK 4
	}

	//==================================================================
	//  Available commands
	//==================================================================
	private void FillAvailable()
	{
		_available.BeginUpdate();
		_available.Nodes.Clear();
		var roots = _filter.SelectedIndex <= 0 ? _commands.Tree : _commands.Tree.Where(n => n.Text == (string)_filter.SelectedItem!).ToList();
		string search = _search.Text.Trim();
		if (search.Length == 0)
		{
			foreach (var root in roots)
			{
				_available.Nodes.Add(TreeNodeFor(root));
			}
		}
		else
		{
			foreach (var node in roots.SelectMany(Flatten).Where(n => n.Command != null
				&& n.Text.Contains(search, StringComparison.OrdinalIgnoreCase)).Take(500))
			{
				_available.Nodes.Add(new TreeNode(node.Command!.Path) { Tag = node.Command });
			}
		}
		_available.EndUpdate();
	}

	private static TreeNode TreeNodeFor(CommandNode node)
	{
		var treeNode = new TreeNode(node.Text) { Tag = node.Command };
		foreach (var child in node.Children)
		{
			treeNode.Nodes.Add(TreeNodeFor(child));
		}
		return treeNode;
	}

	private static IEnumerable<CommandNode> Flatten(CommandNode node)
	{
		yield return node;
		foreach (var child in node.Children.SelectMany(Flatten))
		{
			yield return child;
		}
	}

	//==================================================================
	//  Ribbon tree
	//==================================================================
	private void FillRibbon(RibbonPos? select)
	{
		int topRow = RowIndex(_ribbon.TopNode);    // the rebuilt list keeps its scroll position
		_refilling = true;    // rows and layout disagree until the end
		_rowButtons.Visible = false;
		_ribbon.BeginUpdate();
		_ribbon.Nodes.Clear();
		for (int g = 0; g < _layout.Count; g++)
		{
			var group = _layout[g];
			string caption = group.Caption.Length == 0 ? "(no caption)" : group.Caption;
			var groupNode = new TreeNode($"{caption}    [{group.Columns} col]") { Tag = new RibbonPos(g, -1) };
			for (int i = 0; i < group.Items.Count; i++)
			{
				groupNode.Nodes.Add(ItemNode(group.Items[i], new RibbonPos(g, i)));
			}
			_ribbon.Nodes.Add(groupNode);
		}
		_ribbon.ExpandAll();
		_ribbon.EndUpdate();
		if (RowAt(topRow) is { } top)
		{
			_ribbon.TopNode = top;
		}
		_refilling = false;
		if (select is { } pos)
		{
			Select(pos, refill: false);
		}
		else
		{
			PlaceRowButtons();
		}
	}

	// Rows counted from the top, groups and buttons alike (all expanded)
	private int RowIndex(TreeNode? node)
	{
		int row = 0;
		foreach (TreeNode group in _ribbon.Nodes)
		{
			if (group == node)
			{
				return row;
			}
			row++;
			foreach (TreeNode child in group.Nodes)
			{
				if (child == node)
				{
					return row;
				}
				row++;
			}
		}
		return -1;
	}

	private TreeNode? RowAt(int row)
	{
		if (row < 0)
		{
			return null;
		}
		foreach (TreeNode group in _ribbon.Nodes)
		{
			if (row-- == 0)
			{
				return group;
			}
			foreach (TreeNode child in group.Nodes)
			{
				if (row-- == 0)
				{
					return child;
				}
			}
		}
		return null;
	}

	private TreeNode ItemNode(RibbonItem item, RibbonPos pos)
	{
		var command = _commands.Find(item.Id);
		if (command == null)
		{
			return new TreeNode($"(missing) {item.Label ?? item.Id}") { Tag = pos, ForeColor = SystemColors.GrayText };
		}
		if (command.IsGap)
		{
			return new TreeNode(command.Id == RibbonLayout.SeparatorId ? "(separator)" : "(space)")
			{
				Tag = pos, ToolTipText = command.Path, ForeColor = SystemColors.GrayText,
			};
		}
		string text = item.Label ?? command.Text;
		string kind = command.IsDropDown ? " ▾" : "";
		string detail = item.Label != null ? $"    ({command.Text})" : "";
		if (item.IconSvg != null)
		{
			detail += "    [own icon]";
		}
		return new TreeNode(text + kind + detail) { Tag = pos, ToolTipText = command.Path };
	}

	private RibbonPos? Selected()
	{
		return _ribbon.SelectedNode?.Tag is RibbonPos pos && IsValid(pos) ? pos : null;
	}

	private void Select(RibbonPos pos, bool refill)
	{
		if (refill)
		{
			FillRibbon(null);
		}
		if (pos.Group < 0 || pos.Group >= _ribbon.Nodes.Count)
		{
			return;
		}
		var node = _ribbon.Nodes[pos.Group];
		if (!pos.IsGroup && pos.Item < node.Nodes.Count)
		{
			node = node.Nodes[pos.Item];
		}
		_ribbon.SelectedNode = node;
		node.EnsureVisible();
		PlaceRowButtons();
		_ribbon.Focus();
	}

	//==================================================================
	//  Row buttons and keys
	//==================================================================
	private static Button RowButton(string glyph, string tip, Action run)
	{
		var button = new Button
		{
			Text = glyph, Size = new Size(24, 22), Margin = new Padding(1, 0, 0, 0), Padding = Padding.Empty,
			Font = new Font("Segoe UI Symbol", 8f), TabStop = false, FlatStyle = FlatStyle.System,
		};
		new ToolTip().SetToolTip(button, tip);
		button.Click += (_, _) => run();
		return button;
	}

	private bool _refilling;

	private void PlaceRowButtons()
	{
		var node = _ribbon.SelectedNode;
		if (_refilling || node?.Tag is not RibbonPos pos || !IsValid(pos)
			|| node.Bounds.Height == 0 || node.Bounds.Bottom > _ribbon.ClientSize.Height || node.Bounds.Top < 0)
		{
			_rowButtons.Visible = false;
			return;
		}
		_up.Enabled = CanMove(pos, -1);
		_down.Enabled = CanMove(pos, +1);
		_edit.Enabled = pos.IsGroup || _commands.Find(_layout[pos.Group].Items[pos.Item].Id) is { IsGap: false };
		_rowButtons.Location = new Point(
			Math.Max(0, _ribbon.ClientSize.Width - _rowButtons.Width - 2),
			node.Bounds.Top + (node.Bounds.Height - _rowButtons.Height) / 2);
		_rowButtons.Visible = true;
	}

	private bool IsValid(RibbonPos pos)
	{
		return pos.Group >= 0 && pos.Group < _layout.Count && (pos.IsGroup || pos.Item < _layout[pos.Group].Items.Count);
	}

	private bool CanMove(RibbonPos pos, int step)
	{
		var copy = RibbonLayout.Clone(_layout);
		return RibbonLayout.Move(copy, pos, step) != pos;
	}

	//==================================================================
	//  Drag and drop (owner 2026-11-09)
	//==================================================================
	// Right list: move buttons and groups. Left to right: add at the drop
	// place. Right to left: remove. The native insert mark shows where.
	private const int EdgePixels = 16;    // near the top or bottom edge a drag scrolls the list
	private long _lastScroll;

	private void HookDragAndDrop()
	{
		_ribbon.AllowDrop = true;
		_available.AllowDrop = true;
		_ribbon.ItemDrag += (_, e) =>
		{
			if (e.Item is TreeNode { Tag: RibbonPos pos } node)
			{
				_ribbon.SelectedNode = node;
				_rowButtons.Visible = false;
				_ribbon.DoDragDrop(new RibbonDrag { From = pos }, DragDropEffects.Move);
				PlaceRowButtons();
			}
		};
		_available.ItemDrag += (_, e) =>
		{
			if (e.Item is TreeNode { Tag: RibbonCommand command })
			{
				_available.DoDragDrop(new RibbonDrag { Command = command }, DragDropEffects.Copy);
			}
		};
		_ribbon.DragEnter += (_, e) => OnRibbonDragOver(e);
		_ribbon.DragOver += (_, e) => OnRibbonDragOver(e);
		_ribbon.DragLeave += (_, _) => _ribbon.SetInsertMark(null, false);
		_ribbon.DragDrop += (_, e) => OnRibbonDrop(e);
		_available.DragEnter += (_, e) => e.Effect = Dragged(e)?.From != null ? DragDropEffects.Move : DragDropEffects.None;
		_available.DragOver += (_, e) => e.Effect = Dragged(e)?.From != null ? DragDropEffects.Move : DragDropEffects.None;
		_available.DragDrop += (_, e) =>
		{
			if (Dragged(e)?.From is { } from)
			{
				FillRibbon(RibbonLayout.Remove(_layout, from));    // dragged back: removed
			}
		};
	}

	private static RibbonDrag? Dragged(DragEventArgs e)
	{
		return e.Data?.GetData(typeof(RibbonDrag)) as RibbonDrag;
	}

	private void OnRibbonDragOver(DragEventArgs e)
	{
		var drag = Dragged(e);
		var at = _ribbon.PointToClient(new Point(e.X, e.Y));
		if (drag == null || DropTarget(at, drag.From is { IsGroup: true }) is not { } target)
		{
			e.Effect = DragDropEffects.None;
			_ribbon.SetInsertMark(null, false);
			return;
		}
		e.Effect = drag.From != null ? DragDropEffects.Move : DragDropEffects.Copy;
		_ribbon.SetInsertMark(target.Mark, target.After);
		if ((at.Y < EdgePixels || at.Y > _ribbon.ClientSize.Height - EdgePixels) && Environment.TickCount64 - _lastScroll > 80)
		{
			_lastScroll = Environment.TickCount64;
			_ribbon.ScrollLine(down: at.Y > EdgePixels);
		}
	}

	private void OnRibbonDrop(DragEventArgs e)
	{
		_ribbon.SetInsertMark(null, false);
		var drag = Dragged(e);
		var at = _ribbon.PointToClient(new Point(e.X, e.Y));
		if (drag == null || DropTarget(at, drag.From is { IsGroup: true }) is not { } target)
		{
			return;
		}
		if (drag.From is { } from)
		{
			Select(RibbonLayout.MoveTo(_layout, from, target.Group, target.Index), refill: true);
		}
		else if (drag.Command is { } command)
		{
			Select(RibbonLayout.InsertAt(_layout, target.Group, target.Index, command.Id), refill: true);
		}
	}

	// Where a drop at a point goes, and where the insert mark is drawn.
	// A button: before or after the row (by its upper or lower half); on a
	// group's row, lower half = first in that group, upper half = last in
	// the group above. A group: before its own row's upper half, else
	// after the group the point is in.
	private (int Group, int Index, TreeNode? Mark, bool After)? DropTarget(Point at, bool movingGroup)
	{
		if (_layout.Count == 0)
		{
			return movingGroup ? null : (0, 0, null, false);
		}
		var node = _ribbon.GetNodeAt(at);
		if (node?.Tag is not RibbonPos pos)
		{
			int last = _layout.Count - 1;    // empty space below the rows: the end
			return movingGroup ? (_layout.Count, -1, LastNodeOf(last), true) : (last, _layout[last].Items.Count, LastNodeOf(last), true);
		}
		bool upper = at.Y < node.Bounds.Top + node.Bounds.Height / 2;
		if (movingGroup)
		{
			return pos.IsGroup && upper ? (pos.Group, -1, node, false) : (pos.Group + 1, -1, LastNodeOf(pos.Group), true);
		}
		if (!pos.IsGroup)
		{
			return upper ? (pos.Group, pos.Item, node, false) : (pos.Group, pos.Item + 1, node, true);
		}
		if (upper && pos.Group > 0)
		{
			int above = pos.Group - 1;
			return (above, _layout[above].Items.Count, LastNodeOf(above), true);
		}
		return (pos.Group, 0, node, true);
	}

	private TreeNode? LastNodeOf(int group)
	{
		if (group < 0 || group >= _ribbon.Nodes.Count)
		{
			return null;
		}
		var node = _ribbon.Nodes[group];
		return node.Nodes.Count > 0 ? node.Nodes[node.Nodes.Count - 1] : node;
	}

	private void OnRibbonKey(object? sender, KeyEventArgs e)
	{
		switch (e.KeyData)
		{
			case Keys.Delete:
				RemoveSelected();
				break;
			case Keys.F2:
				EditSelected();
				break;
			case Keys.Control | Keys.Up:
				MoveSelected(-1);
				break;
			case Keys.Control | Keys.Down:
				MoveSelected(+1);
				break;
			default:
				return;
		}
		e.Handled = true;
		e.SuppressKeyPress = true;
	}

	//==================================================================
	//  Edits
	//==================================================================
	private void AddSelected()
	{
		if (_available.SelectedNode?.Tag is RibbonCommand command)
		{
			Select(RibbonLayout.Add(_layout, Selected(), command.Id), refill: true);
		}
	}

	private void RemoveSelected()
	{
		if (Selected() is { } pos)
		{
			var next = RibbonLayout.Remove(_layout, pos);
			FillRibbon(next);
		}
	}

	private void MoveSelected(int step)
	{
		if (Selected() is { } pos)
		{
			Select(RibbonLayout.Move(_layout, pos, step), refill: true);
		}
	}

	private void EditSelected()
	{
		if (Selected() is not { } pos)
		{
			return;
		}
		bool changed;
		if (pos.IsGroup)
		{
			changed = EditGroup(_layout[pos.Group]);
		}
		else
		{
			var item = _layout[pos.Group].Items[pos.Item];
			var command = _commands.Find(item.Id);
			changed = command is { IsGap: false } && EditItem(item, command);
		}
		if (changed)
		{
			Select(pos, refill: true);
		}
	}

	private bool EditGroup(RibbonGroup group)
	{
		using var form = SmallForm("Edit Group");
		var caption = new TextBox { Text = group.Caption, Width = 220 };
		var columns = new NumericUpDown { Minimum = 1, Maximum = RibbonLayout.MaxColumns, Value = Math.Clamp(group.Columns, 1, RibbonLayout.MaxColumns), Width = 60 };
		var fields = Fields(form);
		AddField(fields, "&Caption", caption);
		AddField(fields, "C&olumns", columns);
		fields.Controls.Add(new Label
		{
			Text = $"{RibbonLayout.DecimalsToken} in the caption shows the decimal places sample.", AutoSize = true,
			ForeColor = SystemColors.GrayText,
		}, 1, fields.RowCount++);
		FitSmall(form, fields);
		if (form.ShowDialog(this) != DialogResult.OK)
		{
			return false;
		}
		group.Caption = caption.Text.Trim();
		group.Columns = (int)columns.Value;
		return true;
	}

	private bool EditItem(RibbonItem item, RibbonCommand command)
	{
		using var form = SmallForm("Edit Button");
		var label = new TextBox { Text = item.Label ?? "", Width = 220, PlaceholderText = command.DynamicText?.Invoke() ?? command.Text };
		var display = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
		display.Items.AddRange(new object[] { "Icon and text", "Icon only", "Text only" });
		string? svg = item.IconSvg;
		bool HasIcon() => svg != null || command.Icon != null;
		string current = item.Display ?? (HasIcon() ? "both" : "text");
		display.SelectedIndex = current switch
		{
			"icon" => 1,
			"text" => 2,
			_ => 0,
		};

		// Icon: a 20 px preview, what it is, and Choose... (owner 2026-11-11)
		var preview = new PictureBox { Size = new Size(24, 24), SizeMode = PictureBoxSizeMode.CenterImage, Margin = new Padding(3, 3, 6, 3) };
		var source = new Label { AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = SystemColors.GrayText };
		var choose = new Button { Text = "&Choose...", AutoSize = true, Margin = new Padding(12, 3, 3, 3) };
		var iconRow = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty, WrapContents = false };
		iconRow.Controls.AddRange(new Control[] { preview, source, choose });
		void ShowIcon()
		{
			int size = (int)Math.Round(20 * form.DeviceDpi / 96.0);
			preview.Image = svg != null ? SvgIcon.FromText(svg, size, SystemColors.ControlText)
				: command.Icon != null ? SvgIcon.Get(command.Icon, size, SystemColors.ControlText) : null;
			source.Text = svg != null ? "own icon" : command.Icon != null ? "the command's icon" : "none";
			display.Enabled = HasIcon();    // no icon: text only
			if (!HasIcon())
			{
				display.SelectedIndex = 2;
			}
		}
		choose.Click += (_, _) =>
		{
			bool hadIcon = HasIcon();
			if (IconPicker.Ask(form, svg, command.Icon, out var picked))
			{
				svg = picked;
				if (!hadIcon && HasIcon())
				{
					display.SelectedIndex = 0;    // a text-only button that just got an icon shows it
				}
				ShowIcon();
			}
		};
		ShowIcon();

		var fields = Fields(form);
		fields.Controls.Add(new Label { Text = command.Path, AutoSize = true, ForeColor = SystemColors.GrayText }, 1, fields.RowCount++);
		AddField(fields, "&Label", label);
		AddField(fields, "&Icon", iconRow);
		AddField(fields, "&Show", display);
		FitSmall(form, fields);
		if (form.ShowDialog(this) != DialogResult.OK)
		{
			return false;
		}
		string text = label.Text.Trim();
		item.Label = text.Length == 0 ? null : text;
		item.IconSvg = svg;
		item.Display = !HasIcon() ? null : display.SelectedIndex switch
		{
			1 => "icon",
			2 => "text",
			_ => null,
		};
		return true;
	}

	private static Form SmallForm(string title)
	{
		var form = new Form
		{
			Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false,
			ShowInTaskbar = false, StartPosition = FormStartPosition.CenterParent, Padding = new Padding(8),
		};
		var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(80, 0) };
		var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(80, 0) };
		var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
		buttons.Controls.AddRange(new Control[] { cancel, ok });
		form.Controls.Add(buttons);
		form.AcceptButton = ok;
		form.CancelButton = cancel;
		return form;
	}

	// Sized from its content (an AutoSize form with docked children collapses)
	private static void FitSmall(Form form, TableLayoutPanel fields)
	{
		var buttons = form.Controls.OfType<FlowLayoutPanel>().First().GetPreferredSize(Size.Empty);
		var size = fields.GetPreferredSize(Size.Empty);
		form.ClientSize = new Size(Math.Max(size.Width, buttons.Width) + form.Padding.Horizontal,
			size.Height + buttons.Height + form.Padding.Vertical + 8);
	}

	private static TableLayoutPanel Fields(Form form)
	{
		var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
		fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
		fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
		form.Controls.Add(fields);
		return fields;
	}

	private static void AddField(TableLayoutPanel fields, string caption, Control control)
	{
		fields.Controls.Add(new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 6, 3) }, 0, fields.RowCount);
		fields.Controls.Add(control, 1, fields.RowCount++);
	}
}

// What is being dragged: a ribbon row (From) or an available command (Command)
internal sealed class RibbonDrag
{
	public RibbonPos? From { get; init; }
	public RibbonCommand? Command { get; init; }
}

// A TreeView that tells when it scrolls, so the row buttons can follow,
// and shows the native insert mark line while dragging
internal sealed class RowTree : TreeView
{
	private const int WmVScroll = 0x0115;
	private const int WmMouseWheel = 0x020A;
	private const int TvmSetInsertMark = 0x111A;
	private const int ScrollLineUp = 0;
	private const int ScrollLineDown = 1;

	public event EventHandler? Scrolled;

	protected override void WndProc(ref Message m)
	{
		base.WndProc(ref m);
		if (m.Msg is WmVScroll or WmMouseWheel)
		{
			Scrolled?.Invoke(this, EventArgs.Empty);
		}
	}

	// A line above (after: below) the node; null clears it
	public void SetInsertMark(TreeNode? node, bool after)
	{
		SendMessage(Handle, TvmSetInsertMark, after ? 1 : 0, node?.Handle ?? IntPtr.Zero);
	}

	// One line up or down while a drag is near the top or bottom edge
	public void ScrollLine(bool down)
	{
		SendMessage(Handle, WmVScroll, down ? ScrollLineDown : ScrollLineUp, IntPtr.Zero);
	}

	[System.Runtime.InteropServices.DllImport("user32.dll")]
	private static extern IntPtr SendMessage(IntPtr hwnd, int msg, int wParam, IntPtr lParam);
}
