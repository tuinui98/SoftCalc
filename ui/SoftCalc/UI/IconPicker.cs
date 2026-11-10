// IconPicker.cs - Choose Icon: an own icon for a ribbon button (Customize Ribbon > Edit Button)
//
// The SVG comes from iconmcp.com by Copy SVG and Paste (owner
// 2026-11-11: SoftCalc does not fetch it), from a downloaded .svg file,
// or from the built-in icons. Coming back to the window with new SVG on
// the clipboard takes it at once. The preview shows the icon on a light
// and a dark ribbon. Default drops the own icon (the command's own icon,
// if it has one).

using System.Diagnostics;

namespace SoftCalc.UI;

internal sealed class IconPicker : Form
{
	public const string SiteUrl = "https://iconmcp.com/";
	private const int TileSize = 64;

	private readonly PictureBox _light = Tile(Color.White);
	private readonly PictureBox _dark = Tile(Color.FromArgb(32, 32, 32));
	private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(420, 0), Margin = new Padding(3, 8, 3, 3) };
	private readonly string? _fallback;    // the command's own icon, shown for Default
	private string? _seenClipboard;

	// The chosen SVG text; null = Default
	public string? Svg { get; private set; }

	private IconPicker(string? current, string? commandIcon)
	{
		Text = "Choose Icon";
		Icon = SvgIcon.ProgramIcon();
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MinimizeBox = false;
		MaximizeBox = false;
		ShowInTaskbar = false;
		StartPosition = FormStartPosition.CenterParent;
		AutoScaleMode = AutoScaleMode.Dpi;
		Padding = new Padding(10);
		_fallback = commandIcon;
		Svg = current;

		//==================================================================
		//  BLOCK 1 - Sources: iconmcp.com, Paste, From file, built-in icons
		//==================================================================
		var layout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1 };
		layout.Controls.Add(new Label
		{
			Text = "On iconmcp.com, open an icon and click Copy SVG, then come back here: SoftCalc takes it "
				+ "from the clipboard. Or use a downloaded .svg file, or a built-in icon.",
			AutoSize = true, MaximumSize = new Size(420, 0), ForeColor = SystemColors.GrayText,
		});
		var sources = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
		var site = new Button { Text = "&Open iconmcp.com", AutoSize = true };
		site.Click += (_, _) => OpenSite();
		var paste = new Button { Text = "&Paste", AutoSize = true };
		paste.Click += (_, _) => TakeClipboard(onlyNew: false);
		var file = new Button { Text = "From &file...", AutoSize = true };
		file.Click += (_, _) => TakeFile();
		sources.Controls.AddRange(new Control[] { site, paste, file });
		layout.Controls.Add(sources);
		layout.Controls.Add(new Label { Text = "Built-in:", AutoSize = true, Margin = new Padding(3, 8, 3, 0) });
		var builtIn = new FlowLayoutPanel { AutoSize = true, MaximumSize = new Size(430, 0) };
		var tips = new ToolTip();
		foreach (var name in SvgIcon.BuiltIn())
		{
			var button = new Button
			{
				Image = SvgIcon.Get(name, 20, SystemColors.ControlText), Size = new Size(34, 34),
				Margin = new Padding(2), AccessibleName = name,
			};
			tips.SetToolTip(button, name);
			button.Click += (_, _) => Choose(SvgIcon.EmbeddedText(name).Trim(), $"Built-in: {name}");
			builtIn.Controls.Add(button);
		}
		layout.Controls.Add(builtIn);
		// end BLOCK 1

		//==================================================================
		//  BLOCK 2 - Preview, status and buttons
		//==================================================================
		var preview = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
		preview.Controls.Add(new Label { Text = "Preview:", AutoSize = true, Margin = new Padding(3, 6, 6, 3) });
		preview.Controls.AddRange(new Control[] { _light, _dark });
		layout.Controls.Add(preview);
		layout.Controls.Add(_status);

		var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(80, 0) };
		var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(80, 0) };
		var reset = new Button { Text = "&Default", AutoSize = true, MinimumSize = new Size(80, 0) };
		reset.Click += (_, _) => Choose(null, _fallback != null ? "Default: the command's own icon." : "Default: no icon.");
		var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
		buttons.Controls.AddRange(new Control[] { cancel, ok, reset });
		Controls.Add(layout);
		Controls.Add(buttons);
		AcceptButton = ok;
		CancelButton = cancel;
		// end BLOCK 2

		var size = layout.GetPreferredSize(Size.Empty);
		var under = buttons.GetPreferredSize(Size.Empty);
		ClientSize = new Size(Math.Max(size.Width, under.Width) + Padding.Horizontal, size.Height + under.Height + Padding.Vertical + 8);
		_seenClipboard = ClipboardText();    // SVG copied before opening is taken by Paste, not at once
		ShowChoice(current == null ? (_fallback != null ? "The command's own icon." : "No icon yet.") : "The button's own icon.");
	}

	// Returns false on Cancel; `svg` is the chosen text, null = Default
	public static bool Ask(IWin32Window owner, string? current, string? commandIcon, out string? svg)
	{
		using var picker = new IconPicker(current, commandIcon);
		bool ok = picker.ShowDialog(owner) == DialogResult.OK;
		svg = picker.Svg;
		return ok;
	}

	protected override void OnActivated(EventArgs e)
	{
		base.OnActivated(e);
		TakeClipboard(onlyNew: true);
	}

	//==================================================================
	//  Taking an icon
	//==================================================================
	private void OpenSite()
	{
		try
		{
			Process.Start(new ProcessStartInfo(SiteUrl) { UseShellExecute = true });
		}
		catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
		{
			ShowError($"Could not open {SiteUrl}: {ex.Message}");
		}
	}

	private void TakeClipboard(bool onlyNew)
	{
		string? text = ClipboardText();
		if (onlyNew && (text == null || text == _seenClipboard))
		{
			return;
		}
		_seenClipboard = text;
		if (text == null || !LooksLikeSvg(text))
		{
			if (!onlyNew)
			{
				ShowError("The clipboard has no SVG. On iconmcp.com click Copy SVG first.");
			}
			return;
		}
		Choose(text.Trim(), "From the clipboard.");
	}

	private void TakeFile()
	{
		using var dialog = new OpenFileDialog { Filter = "SVG files (*.svg)|*.svg|All files (*.*)|*.*", Title = "Icon File" };
		if (dialog.ShowDialog(this) != DialogResult.OK)
		{
			return;
		}
		try
		{
			if (new FileInfo(dialog.FileName).Length > SvgIcon.MaxLength * 4L)
			{
				ShowError($"{Path.GetFileName(dialog.FileName)} is too big for an icon.");
				return;
			}
			Choose(File.ReadAllText(dialog.FileName).Trim(), $"From {Path.GetFileName(dialog.FileName)}.");
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			ShowError(ex.Message);
		}
	}

	// A text that cannot be drawn leaves the choice as it was
	private void Choose(string? svg, string note)
	{
		if (svg != null && SvgIcon.Problem(svg) is { } problem)
		{
			ShowError("Can't use this icon: " + problem);
			return;
		}
		Svg = svg;
		ShowChoice(note);
	}

	private void ShowChoice(string note)
	{
		_light.Image?.Dispose();
		_dark.Image?.Dispose();
		_light.Image = Preview(Color.Black, Color.White);
		_dark.Image = Preview(Color.White, Color.FromArgb(32, 32, 32));
		_status.ForeColor = SystemColors.GrayText;
		_status.Text = note;
	}

	private void ShowError(string message)
	{
		_status.ForeColor = Color.Firebrick;
		_status.Text = message;
	}

	// The icon at ribbon size (20 px) and twice that, side by side
	private Bitmap Preview(Color ink, Color back)
	{
		var bitmap = new Bitmap(TileSize, TileSize);
		using var g = Graphics.FromImage(bitmap);
		g.Clear(back);
		var small = Draw(20, ink);
		var big = Draw(40, ink);
		if (small != null && big != null)
		{
			g.DrawImage(small, 2, (TileSize - 20) / 2);
			g.DrawImage(big, 22, (TileSize - 40) / 2);
		}
		return bitmap;
	}

	private Bitmap? Draw(int size, Color ink)
	{
		if (Svg != null)
		{
			return SvgIcon.FromText(Svg, size, ink);
		}
		return _fallback != null ? SvgIcon.Get(_fallback, size, ink) : null;
	}

	//==================================================================
	//  Helpers
	//==================================================================
	public static bool LooksLikeSvg(string text)
	{
		return text.Contains("<svg", StringComparison.OrdinalIgnoreCase) && text.Length <= SvgIcon.MaxLength;
	}

	private static string? ClipboardText()
	{
		try
		{
			return Clipboard.ContainsText() ? Clipboard.GetText() : null;
		}
		catch (System.Runtime.InteropServices.ExternalException)
		{
			return null;    // another program holds the clipboard
		}
	}

	private static PictureBox Tile(Color back)
	{
		return new PictureBox
		{
			Size = new Size(TileSize, TileSize), BackColor = back, BorderStyle = BorderStyle.FixedSingle,
			SizeMode = PictureBoxSizeMode.CenterImage, Margin = new Padding(3, 3, 8, 3),
		};
	}
}
