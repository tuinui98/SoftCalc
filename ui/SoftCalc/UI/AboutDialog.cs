// AboutDialog.cs - File > About SoftCalc: version, data folder, third-party notices (M9)
//
// A short note first; the notices box stays hidden until the link under
// the data folder opens it (owner 2026-11-07).
//
// The notices (THIRD-PARTY-NOTICES.txt, written by tools/make_notices.py)
// are embedded because the published exe carries .NET, Python, mpmath,
// the PyInstaller bootloader and IconMCP icons, whose licences ask for
// their notices to go with the binary.

using System.Diagnostics;
using System.Reflection;

namespace SoftCalc.UI;

internal sealed class AboutDialog : Form
{
	public AboutDialog(string dataFolder)
	{
		Text = "About SoftCalc";
		Icon = SvgIcon.ProgramIcon();
		ShowInTaskbar = false;
		MinimizeBox = false;
		MaximizeBox = false;
		StartPosition = FormStartPosition.CenterParent;
		FormBorderStyle = FormBorderStyle.FixedDialog;

		string version = Assembly.GetExecutingAssembly()
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "?";
		var picture = new PictureBox { Size = new Size(48, 48), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 0, 12, 0) };
		using (var icon = new Icon(SvgIcon.ProgramIcon(), 48, 48))
		{
			picture.Image = icon.ToBitmap();
		}
		var title = new Label { Text = $"SoftCalc {version}", AutoSize = true, Font = new Font(Font.FontFamily, 13f, FontStyle.Bold) };
		var subtitle = new Label { Text = "Calculator with history, graphs, units, dates and currency. Personal use.", AutoSize = true, ForeColor = SystemColors.GrayText };
		var heading = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(12, 12, 12, 6) };
		heading.Controls.Add(picture, 0, 0);
		heading.SetRowSpan(picture, 2);
		heading.Controls.Add(title, 1, 0);
		heading.Controls.Add(subtitle, 1, 1);

		//==================================================================
		//  Data folder, with a button that opens it in Explorer
		//==================================================================
		var path = new TextBox { Text = dataFolder, ReadOnly = true, Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
		var open = new Button { Text = "Open Folder", AutoSize = true, Anchor = AnchorStyles.Right };
		open.Click += (_, _) => Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dataFolder}\"") { UseShellExecute = true });
		var data = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Padding = new Padding(12, 0, 12, 6) };
		data.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
		data.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
		data.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
		data.Controls.Add(new Label { Text = "Data folder", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
		data.Controls.Add(path, 1, 0);
		data.Controls.Add(open, 2, 0);

		//==================================================================
		//  Third-party notices
		//==================================================================
		var notices = new TextBox
		{
			Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill,
			Font = new Font(FontFamily.GenericMonospace, 8.5f), Text = Notices(),
		};
		var noticeBox = new GroupBox { Text = "Third-party software", Dock = DockStyle.Fill, Padding = new Padding(8) };
		noticeBox.Controls.Add(notices);
		var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 0), Visible = false };
		body.Controls.Add(noticeBox);
		var link = new LinkLabel { Text = "Show third-party licences", AutoSize = true, Location = new Point(12, 2) };
		var linkRow = new Panel { Dock = DockStyle.Top, Height = link.PreferredHeight + 8 };
		linkRow.Controls.Add(link);
		link.LinkClicked += (_, _) => ShowNotices(body, linkRow);

		var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(80, 0) };
		var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(8) };
		buttons.Controls.Add(ok);
		AcceptButton = ok;
		CancelButton = ok;
		Shown += (_, _) =>
		{
			ActiveControl = ok;    // not the notices box, which would show all its text selected
			notices.Select(0, 0);
		};

		Controls.Add(body);
		Controls.Add(buttons);
		Controls.Add(linkRow);
		Controls.Add(data);
		Controls.Add(heading);
		ClientSize = new Size(480, heading.PreferredSize.Height + data.PreferredSize.Height
			+ linkRow.Height + buttons.PreferredSize.Height);
	}

	// Opens the notices box once: the dialog grows and becomes resizable
	private void ShowNotices(Panel body, Panel linkRow)
	{
		int height = ClientSize.Height - linkRow.Height;
		linkRow.Visible = false;
		FormBorderStyle = FormBorderStyle.Sizable;
		MinimumSize = new Size(420, 320);
		body.Visible = true;
		ClientSize = new Size(Math.Max(ClientSize.Width, 560), height + 300);
	}

	internal static string Notices()
	{
		using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("THIRD-PARTY-NOTICES.txt");
		if (stream == null)
		{
			return "(notices not embedded)";
		}
		using var reader = new StreamReader(stream);
		return reader.ReadToEnd();
	}
}
