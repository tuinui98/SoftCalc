// HelpWindow.cs - File > Help and F1: topic list, search and the topic text (owner 2026-11-10)
//
// One Help window for the process, not owned by a calculator window so
// it can sit beside it. The text is help/*.md (Logic/HelpTopics.cs)
// turned into HTML (Logic/HelpMarkdown.cs) in the stock WebBrowser
// control; a click on an example puts its line in the Input of the
// calculator window Help was opened from. F1 opens the topic for the
// active window (HelpKey below).

using System.Runtime.InteropServices;
using System.Text;
using SoftCalc.Logic;

namespace SoftCalc.UI;

internal sealed class HelpWindow : Form
{
	private static HelpWindow? _open;

	private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "Search" };
	private readonly ListBox _topics = new() { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None };
	private readonly WebBrowser _page = new()
	{
		Dock = DockStyle.Fill, IsWebBrowserContextMenuEnabled = false, AllowWebBrowserDrop = false,
		ScriptErrorsSuppressed = true,
	};
	private List<HelpTopic> _shown = new();
	private HelpTopic? _current;
	private MainForm? _target;

	// Show `topic` ("graph", "06-conversions"); examples go to `target`'s Input
	public static void Open(string topic, MainForm? target)
	{
		// A window made before a modal dialog opened is disabled by it; a new one is not
		if (_open != null && !_open.IsDisposed && !NativeMethods.IsWindowEnabled(_open.Handle))
		{
			_open.Close();
		}
		if (_open == null || _open.IsDisposed)
		{
			_open = new HelpWindow();
			_open.Show();
		}
		_open._target = target ?? _open._target;
		_open.ShowTopic(HelpTopics.Find(topic));
		if (_open.WindowState == FormWindowState.Minimized)
		{
			_open.WindowState = FormWindowState.Normal;
		}
		_open.Activate();
	}

	private HelpWindow()
	{
		Text = "SoftCalc Help";
		Icon = SvgIcon.ProgramIcon();
		StartPosition = FormStartPosition.CenterScreen;
		Size = LogicalToDeviceUnits(new Size(900, 640));
		MinimumSize = LogicalToDeviceUnits(new Size(480, 320));
		KeyPreview = true;

		var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1 };
		var left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
		left.Controls.Add(_topics);
		left.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 4 });
		left.Controls.Add(_search);
		split.Panel1.Controls.Add(left);
		split.Panel2.Controls.Add(_page);
		Controls.Add(split);
		Load += (_, _) => split.SplitterDistance = LogicalToDeviceUnits(210);

		_page.ObjectForScripting = new HelpBridge(this);
		_page.Navigating += (_, e) =>
		{
			if (e.Url?.Scheme is not ("about" or "data"))
			{
				e.Cancel = true;    // the help has no web links; nothing leaves the window
			}
		};
		_search.TextChanged += (_, _) => FilterTopics();
		_search.KeyDown += (_, e) =>
		{
			if (e.KeyCode is Keys.Enter or Keys.Down && _topics.Items.Count > 0)
			{
				_topics.Focus();
				e.SuppressKeyPress = true;
			}
		};
		_topics.SelectedIndexChanged += (_, _) =>
		{
			if (_topics.SelectedIndex >= 0 && _shown[_topics.SelectedIndex] != _current)
			{
				ShowTopic(_shown[_topics.SelectedIndex]);
			}
		};
		KeyDown += (_, e) =>
		{
			if (e.KeyCode == Keys.Escape)
			{
				Close();
			}
		};
		FilterTopics();
	}

	//==================================================================
	//  Topic list and search
	//==================================================================
	private void FilterTopics()
	{
		_shown = HelpTopics.Search(_search.Text);
		_topics.BeginUpdate();
		_topics.Items.Clear();
		foreach (var topic in _shown)
		{
			_topics.Items.Add(topic.Title);
		}
		_topics.EndUpdate();
		if (_current != null && _shown.Contains(_current))
		{
			_topics.SelectedIndex = _shown.IndexOf(_current);
			ShowTopic(_current);    // marks the new search words
		}
		else if (_shown.Count > 0 && _search.Text.Trim().Length > 0)
		{
			_topics.SelectedIndex = 0;
		}
	}

	internal void ShowTopic(HelpTopic topic)
	{
		if (!_shown.Contains(topic))
		{
			_search.Text = "";    // refills the list with every topic
		}
		_current = topic;
		int index = _shown.IndexOf(topic);
		if (index >= 0 && _topics.SelectedIndex != index)
		{
			_topics.SelectedIndex = index;
		}
		_page.DocumentText = Page(topic, HelpTopics.SearchWords(_search.Text));
	}

	//==================================================================
	//  The page: theme colours from SystemColors (remapped in dark mode)
	//==================================================================
	private static string Page(HelpTopic topic, string[] marks)
	{
		bool dark = Theme.IsDark;
		string body = HelpMarkdown.ToHtml(topic.Markdown, path => HelpTopics.Picture(path, dark), HelpTopics.IdOfTitle);
		string text = Css(SystemColors.WindowText);
		string back = Css(SystemColors.Window);
		string faint = Css(SystemColors.GrayText);
		string link = Css(dark ? Color.FromArgb(110, 170, 255) : Color.FromArgb(0, 102, 204));
		string box = Css(Blend(SystemColors.Window, SystemColors.WindowText, 0.06));
		string hover = Css(Blend(SystemColors.Window, SystemColors.Highlight, 0.25));
		string line = Css(Blend(SystemColors.Window, SystemColors.WindowText, 0.18));
		string mark = Css(dark ? Color.FromArgb(110, 90, 20) : Color.FromArgb(255, 236, 140));
		string font = SystemFonts.MessageBoxFont?.Name ?? "Segoe UI";
		var words = new StringBuilder();
		foreach (string word in marks)
		{
			words.Append('"').Append(Escape(word.ToLowerInvariant())).Append("\",");
		}
		return $$"""
			<!DOCTYPE html>
			<html><head><meta http-equiv="X-UA-Compatible" content="IE=edge"><meta charset="utf-8">
			<style>
			html { scrollbar-base-color: {{box}}; scrollbar-face-color: {{line}}; scrollbar-track-color: {{back}}; scrollbar-arrow-color: {{faint}}; }
			body { font-family: "{{font}}", sans-serif; font-size: 10pt; color: {{text}}; background: {{back}}; margin: 12px 18px; line-height: 1.4; }
			h1 { font-size: 16pt; font-weight: 600; margin: 4px 0 10px 0; }
			h2 { font-size: 12.5pt; font-weight: 600; margin: 18px 0 6px 0; }
			h3 { font-size: 11pt; font-weight: 600; margin: 14px 0 4px 0; }
			p, ul, ol { margin: 6px 0; }
			li { margin: 2px 0; }
			code { font-family: Consolas, monospace; font-size: 10pt; background: {{box}}; padding: 0 3px; }
			pre { font-family: Consolas, monospace; background: {{box}}; padding: 6px 8px; }
			a { color: {{link}}; text-decoration: none; }
			a:hover { text-decoration: underline; }
			table { border-collapse: collapse; margin: 6px 0; }
			th, td { border: 1px solid {{line}}; padding: 3px 8px; text-align: left; vertical-align: top; }
			th { background: {{box}}; font-weight: 600; }
			.examples { background: {{box}}; padding: 4px 0; margin: 8px 0; }
			.example { padding: 1px 10px; cursor: pointer; }
			.example:hover { background: {{hover}}; }
			.example code { background: transparent; padding: 0; }
			.result { color: {{faint}}; margin-left: 2em; font-family: Consolas, monospace; }
			.picture { margin: 10px 0; }
			.picture img { max-width: 100%; border: 1px solid {{line}}; }
			.caption { color: {{faint}}; font-size: 9pt; }
			.mark { background: {{mark}}; }
			</style></head>
			<body>
			{{body}}
			<script>
			function up(node, test) { while (node && node.nodeType == 1) { if (test(node)) return node; node = node.parentNode; } return null; }
			document.onclick = function (e) {
				e = e || window.event;
				var target = e.target || e.srcElement;
				var example = up(target, function (n) { return n.className == "example"; });
				if (example) { window.external.Example(example.getAttribute("data-line")); return false; }
				var topic = up(target, function (n) { return n.getAttribute && n.getAttribute("data-topic"); });
				if (topic) { window.external.Topic(topic.getAttribute("data-topic")); return false; }
				return true;
			};
			var words = [{{words}}];
			function markText(node) {
				if (node.nodeType == 3) {
					var low = node.nodeValue.toLowerCase(), best = -1, size = 0;
					for (var w = 0; w < words.length; w++) {
						var at = low.indexOf(words[w]);
						if (at >= 0 && (best < 0 || at < best)) { best = at; size = words[w].length; }
					}
					if (best < 0) return;
					var hit = node.splitText(best);
					hit.splitText(size);
					var span = document.createElement("span");
					span.className = "mark";
					hit.parentNode.replaceChild(span, hit);
					span.appendChild(hit);
					if (span.nextSibling) markText(span.nextSibling);
					return;
				}
				if (node.nodeType == 1 && node.tagName != "SCRIPT" && node.tagName != "STYLE") {
					var kids = [];
					for (var k = 0; k < node.childNodes.length; k++) kids.push(node.childNodes[k]);
					for (var k = 0; k < kids.length; k++) markText(kids[k]);
				}
			}
			if (words.length > 0) {
				markText(document.body);
				var spans = document.getElementsByTagName("span");
				for (var s = 0; s < spans.length; s++) { if (spans[s].className == "mark") { spans[s].scrollIntoView(); window.scrollBy(0, -40); break; } }
			}
			</script>
			</body></html>
			""";
	}

	private static string Css(Color colour)
	{
		return $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}";
	}

	private static Color Blend(Color from, Color to, double amount)
	{
		return Color.FromArgb(
			(int)(from.R + (to.R - from.R) * amount),
			(int)(from.G + (to.G - from.G) * amount),
			(int)(from.B + (to.B - from.B) * amount));
	}

	private static string Escape(string text)
	{
		return text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("<", "\\x3c");
	}

	//==================================================================
	//  Page clicks (window.external in the page script)
	//==================================================================
	internal void PutInInput(string line)
	{
		var target = _target != null && !_target.IsDisposed
			? _target
			: Application.OpenForms.OfType<MainForm>().FirstOrDefault();
		target?.PutInInput(line);
	}

	//==================================================================
	//  F1 anywhere in SoftCalc: the topic for the active window
	//==================================================================
	public sealed class HelpKey : IMessageFilter
	{
		private const int WmKeyDown = 0x0100;

		public bool PreFilterMessage(ref Message m)
		{
			if (m.Msg != WmKeyDown || (Keys)(int)m.WParam != Keys.F1 || Control.ModifierKeys != Keys.None)
			{
				return false;
			}
			var form = Form.ActiveForm;
			if (form == null || form is HelpWindow)
			{
				return false;
			}
			Open(TopicFor(form), MainOf(form));
			return true;
		}
	}

	// A plain dialog names its topic in its Tag: "help:graph"
	public const string TopicTag = "help:";

	// The window or the nearest owner with its own topic
	internal static string TopicFor(Form form)
	{
		for (Form? at = form; at != null; at = at.Owner)
		{
			string? topic = at switch
			{
				GraphWindow or GraphGallery => "graph",    // also opened from the sidebar
				{ Tag: string tag } when tag.StartsWith(TopicTag, StringComparison.Ordinal) => tag[TopicTag.Length..],
				CompoundWindow => "finance",
				DefinitionEditor => "variables-functions",
				CustomizeRibbonDialog => "ribbon",
				MainForm => "getting-started",
				_ => null,
			};
			if (topic != null)
			{
				return topic;
			}
		}
		return "getting-started";
	}

	internal static MainForm? MainOf(Form form)
	{
		for (Form? at = form; at != null; at = at.Owner)
		{
			if (at is MainForm main)
			{
				return main;
			}
		}
		return null;
	}

	private static class NativeMethods
	{
		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool IsWindowEnabled(IntPtr hwnd);
	}
}

// window.external in the Help page: must be a public COM-visible type
[ComVisible(true)]
public sealed class HelpBridge
{
	private readonly HelpWindow _window;

	internal HelpBridge(HelpWindow window)
	{
		_window = window;
	}

	public void Example(string line)
	{
		_window.BeginInvoke(() => _window.PutInInput(line));    // after the page's click handler returns
	}

	public void Topic(string id)
	{
		_window.BeginInvoke(() => _window.ShowTopic(HelpTopics.Find(id)));
	}
}
