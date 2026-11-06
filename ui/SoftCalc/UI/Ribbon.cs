// Ribbon.cs - Button groups under the menu bar (owner's layout B3, 2026-11-06)
//
// Angle, Output, Decimals (caption shows a sample of the format), Mode,
// History (Clear, Save) and Tools (Graph, Interest, On top, Conversions).
// Every button repeats a menu command; check marks follow the engine
// settings through ShowSettings. Built from stock ToolStrips that wrap
// onto more rows when the window is narrow.

using System.Text.Json.Nodes;
using SoftCalc.Logic;

namespace SoftCalc.UI;

internal sealed record RibbonActions(
	Func<string, object, Task> ChangeSetting,
	Func<int, Task> StepDecimals,
	Func<Task> AllDecimals,
	Func<Task> ClearHistory,
	Action SaveHistory,
	Action OpenGraph,
	Action OpenInterest,
	Action ToggleOnTop);

internal sealed class Ribbon : FlowLayoutPanel
{
	private const int IconSize = 20;

	private readonly RibbonActions _actions;
	private readonly Dictionary<(string Key, string Value), ToolStripItem> _choices = new();
	private readonly ToolStripDropDownButton _precision = new("Prec 32") { DisplayStyle = ToolStripItemDisplayStyle.Text };
	private readonly ToolStripButton _all;
	private readonly ToolStripButton _onTop;
	private readonly Label _decimalsCaption = new();

	// Filled by the main window with the same items as the Conversions menu
	public ToolStripDropDownButton Conversions { get; }

	public Ribbon(RibbonActions actions)
	{
		_actions = actions;
		Dock = DockStyle.Top;
		AutoSize = true;
		WrapContents = true;
		Padding = new Padding(2);
		BackColor = SystemColors.ControlLight;

		_all = Command("All digits", null, async () => await _actions.AllDecimals(), text: "All");
		_onTop = Command("On top", "pushpin", () =>
		{
			_actions.ToggleOnTop();
			return Task.CompletedTask;
		});
		Conversions = new ToolStripDropDownButton("Conversions", Icon("swap-horizontal"))
		{
			DisplayStyle = ToolStripItemDisplayStyle.ImageAndText, ImageScaling = ToolStripItemImageScaling.None,
		};
		foreach (int precision in new[] { 32, 64, 128, 512 })
		{
			var item = new ToolStripMenuItem(precision.ToString(), null, async (_, _) => await _actions.ChangeSetting("precision", precision));
			_choices[("precision", precision.ToString())] = item;
			_precision.DropDownItems.Add(item);
		}

		Controls.Add(Group("Angle", 3, null, Choice("angle", "deg", "Deg"), Choice("angle", "rad", "Rad"), Choice("angle", "grad", "Grad")));
		Controls.Add(Group("Output", 4, null, Choice("base", "dec", "Dec"), Choice("base", "hex", "Hex"),
			Choice("base", "bin", "Bin"), Choice("base", "oct", "Oct")));
		Controls.Add(Group("Decimals", 3, _decimalsCaption,
			Command("More decimal places", "decimal-increase", async () => await _actions.StepDecimals(+1), iconOnly: true),
			Command("Fewer decimal places", "decimal-decrease", async () => await _actions.StepDecimals(-1), iconOnly: true),
			_all));
		Controls.Add(Group("Mode", 2, null, Choice("rpn", "true", "RPN"), _precision));
		Controls.Add(Group("History", 2, null,
			Command("Clear", "trash", async () => await _actions.ClearHistory()),
			Command("Save", "save", () =>
			{
				_actions.SaveHistory();
				return Task.CompletedTask;
			})));
		Controls.Add(Group("Tools", 4, null,
			Command("Graph", "line-chart", () =>
			{
				_actions.OpenGraph();
				return Task.CompletedTask;
			}),
			Command("Interest", "compound-interest", () =>
			{
				_actions.OpenInterest();
				return Task.CompletedTask;
			}),
			_onTop, Conversions));
		ShowDecimals(null);
	}

	//==================================================================
	//  State from the engine settings
	//==================================================================
	public void ShowSettings(JsonObject settings)
	{
		foreach (var ((key, value), item) in _choices)
		{
			bool on = settings[key]?.ToJsonString().Trim('"') == value;
			switch (item)
			{
				case ToolStripButton button:
					button.Checked = on;
					break;
				case ToolStripMenuItem menuItem:
					menuItem.Checked = on;
					break;
			}
		}
		_precision.Text = $"Prec {(int?)settings["precision"] ?? 32}";
		ShowDecimals((int?)settings["decimals"]);
	}

	public void ShowOnTop(bool onTop)
	{
		_onTop.Checked = onTop;
	}

	private void ShowDecimals(int? decimals)
	{
		_decimalsCaption.Text = "Decimals  " + DecimalSteps.Sample(decimals);
		_all.Checked = decimals == null;
	}

	//==================================================================
	//  Building blocks
	//==================================================================
	private ToolStripButton Choice(string key, string value, string text)
	{
		var button = new ToolStripButton(text) { DisplayStyle = ToolStripItemDisplayStyle.Text };
		object setting = key switch
		{
			"rpn" => true,    // the handler flips RPN
			"precision" => int.Parse(value),
			_ => value,
		};
		button.Click += async (_, _) => await _actions.ChangeSetting(key, setting);
		_choices[(key, value)] = button;
		return button;
	}

	private ToolStripButton Command(string name, string? icon, Func<Task> run, bool iconOnly = false, string? text = null)
	{
		var button = new ToolStripButton(text ?? name)
		{
			ToolTipText = name, ImageScaling = ToolStripItemImageScaling.None,
			DisplayStyle = icon == null ? ToolStripItemDisplayStyle.Text
				: iconOnly ? ToolStripItemDisplayStyle.Image : ToolStripItemDisplayStyle.ImageAndText,
		};
		if (icon != null)
		{
			button.Image = Icon(icon);
		}
		button.Click += async (_, _) => await run();
		return button;
	}

	private Bitmap Icon(string name)
	{
		int size = (int)Math.Round(IconSize * DeviceDpi / 96.0);
		return SvgIcon.Get(name, size, SystemColors.ControlText);
	}

	private static Control Group(string caption, int columns, Label? captionLabel, params ToolStripItem[] items)
	{
		var strip = new ToolStrip
		{
			GripStyle = ToolStripGripStyle.Hidden, LayoutStyle = ToolStripLayoutStyle.Table, Dock = DockStyle.None,
			AutoSize = true, BackColor = SystemColors.Control, RenderMode = ToolStripRenderMode.System,
		};
		if (strip.LayoutSettings is TableLayoutSettings table)
		{
			table.ColumnCount = columns;
		}
		strip.Items.AddRange(items);
		var label = captionLabel ?? new Label();
		label.Text = captionLabel == null ? caption : label.Text;
		label.AutoSize = true;    // a long Decimals sample widens its group
		label.Anchor = AnchorStyles.None;
		label.ForeColor = SystemColors.GrayText;
		var box = new TableLayoutPanel
		{
			AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 2,
			Margin = new Padding(2), BorderStyle = BorderStyle.FixedSingle, BackColor = SystemColors.Control,
		};
		box.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		box.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
		box.Controls.Add(strip, 0, 0);
		box.Controls.Add(label, 0, 1);
		return box;
	}
}
