// HistoryView.cs - History pane: one two-line item per entry, click to recall
//
// Line 1: the input as typed on the left; when values were substituted
// for variables, answer or $n, the numeric form right-aligned in a
// fainter colour (extensions.md E10). Line 2: the result, indented.
// A currency line shows its rate date faint at the right of line 2 (M8).
// Click raises EntryClicked (recall the expression), Shift+click raises
// ResultClicked (insert the result); a balloon tip over the list says so.
// Ctrl+C copies the selected entry; the context menu also recalls,
// inserts, and copies the result, the entry, or everything (E9).

using System.Text;

namespace SoftCalc.UI;

internal sealed record HistoryEntry(string Kind, string Input, string DisplayInput, string Text)
{
	// Set for entries that are saved to history.jsonl (notices are not)
	public string Id { get; init; } = "";
	public DateTime Time { get; init; }

	// ECB rate date of a currency line, else empty
	public string Note { get; init; } = "";

	// The right-hand numeric form is shown only when it differs from the input
	public bool HasSubstitution => Kind == "result" && DisplayInput != Input;
}

internal sealed class HistoryView : ListBox
{
	private const int Gap = 16;
	private const int Indent = 40;

	private readonly List<HistoryEntry> _entries = new();

	public const string HoverHint = "Click: use the expression\r\nShift+Click: insert the result\r\nRight-click: more";

	private readonly ToolTip _hint = new() { IsBalloon = true, InitialDelay = 700, AutoPopDelay = 6000, ToolTipTitle = "History" };

	public event EventHandler<HistoryEntry>? EntryClicked;
	public event EventHandler<HistoryEntry>? ResultClicked;

	public IReadOnlyList<HistoryEntry> Entries => _entries;

	public HistoryView()
	{
		DrawMode = DrawMode.OwnerDrawFixed;
		BorderStyle = BorderStyle.None;
		IntegralHeight = false;
		HorizontalScrollbar = false;
		ItemHeight = (Font.Height + 2) * 2 + 4;
		ContextMenuStrip = BuildContextMenu();
		_hint.SetToolTip(this, HoverHint);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_hint.Dispose();
		}
		base.Dispose(disposing);
	}

	private ContextMenuStrip BuildContextMenu()
	{
		var menu = new ContextMenuStrip();
		var useExpression = new ToolStripMenuItem("Use E&xpression", null, (_, _) => Raise(EntryClicked));
		var insertResult = new ToolStripMenuItem("&Insert Result", null, (_, _) => Raise(ResultClicked)) { ShortcutKeyDisplayString = "Shift+Click" };
		var copyResult = new ToolStripMenuItem("Copy &Result", null, (_, _) => CopySelected(e => e.Text));
		var copyEntry = new ToolStripMenuItem("Copy &Expression and Result", null, (_, _) => CopySelected(CopyText));
		var copyAll = new ToolStripMenuItem("Copy &All", null, (_, _) => SetClipboard(AllText()));
		menu.Items.AddRange(new ToolStripItem[]
		{
			useExpression, insertResult, new ToolStripSeparator(),
			copyResult, copyEntry, new ToolStripSeparator(), copyAll,
		});
		menu.Opening += (_, _) =>
		{
			bool entry = Selected is { Kind: not "notice" };
			useExpression.Enabled = entry;
			insertResult.Enabled = Selected is { Kind: "result" };
			copyResult.Enabled = entry;
			copyEntry.Enabled = entry;
			copyAll.Enabled = _entries.Any(e => e.Kind != "notice");
		};
		return menu;
	}

	private HistoryEntry? Selected => SelectedIndex >= 0 && SelectedIndex < _entries.Count ? _entries[SelectedIndex] : null;

	private void Raise(EventHandler<HistoryEntry>? handler)
	{
		if (Selected is { Kind: not "notice" } entry)
		{
			handler?.Invoke(this, entry);
		}
	}

	private void CopySelected(Func<HistoryEntry, string> text)
	{
		if (Selected is { } entry)
		{
			SetClipboard(text(entry));
		}
	}

	private static void SetClipboard(string text)
	{
		if (text.Length > 0)
		{
			Clipboard.SetText(text);
		}
	}

	public void Append(HistoryEntry entry)
	{
		_entries.Add(entry);
		Items.Add(entry);
		TopIndex = Math.Max(0, Items.Count - 1);
	}

	public void ClearEntries()
	{
		_entries.Clear();
		Items.Clear();
	}

	protected override void OnFontChanged(EventArgs e)
	{
		base.OnFontChanged(e);
		ItemHeight = (Font.Height + 2) * 2 + 4;
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		Invalidate();    // right-aligned text moves with the width
	}

	protected override void OnDrawItem(DrawItemEventArgs e)
	{
		if (e.Index < 0 || e.Index >= _entries.Count)
		{
			return;
		}
		var entry = _entries[e.Index];
		bool selected = (e.State & DrawItemState.Selected) != 0;
		using (var back = new SolidBrush(selected ? SystemColors.Highlight : SystemColors.Window))
		{
			e.Graphics.FillRectangle(back, e.Bounds);
		}
		Color main = selected ? SystemColors.HighlightText : SystemColors.WindowText;
		Color faint = selected ? SystemColors.HighlightText : SystemColors.GrayText;
		int lineHeight = Font.Height + 2;
		var line1 = new Rectangle(e.Bounds.X + 4, e.Bounds.Y + 2, e.Bounds.Width - 8, lineHeight);
		var line2 = new Rectangle(e.Bounds.X + Indent, line1.Bottom, e.Bounds.Width - Indent - 4, lineHeight);
		const TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;

		if (entry.Kind == "notice")
		{
			using var italic = new Font(Font, FontStyle.Italic);
			TextRenderer.DrawText(e.Graphics, entry.DisplayInput, italic, line1, faint, flags | TextFormatFlags.EndEllipsis);
			TextRenderer.DrawText(e.Graphics, entry.Text, italic, line2, faint, flags | TextFormatFlags.EndEllipsis);
			return;
		}

		int inputWidth = TextRenderer.MeasureText(e.Graphics, entry.Input, Font, Size.Empty, flags).Width;
		TextRenderer.DrawText(e.Graphics, entry.Input, Font, line1, main, flags | TextFormatFlags.EndEllipsis);
		if (entry.HasSubstitution)
		{
			// Right side gets whatever width the input leaves; it shortens with an ellipsis
			var right = Rectangle.FromLTRB(line1.Left + inputWidth + Gap, line1.Top, line1.Right, line1.Bottom);
			if (right.Width > 20)
			{
				TextRenderer.DrawText(e.Graphics, entry.DisplayInput, Font, right, faint,
					flags | TextFormatFlags.Right | TextFormatFlags.EndEllipsis);
			}
		}
		Color resultColor = entry.Kind == "error" && !selected ? Color.Firebrick : main;
		int resultWidth = TextRenderer.MeasureText(e.Graphics, entry.Text, Font, Size.Empty, flags).Width;
		TextRenderer.DrawText(e.Graphics, entry.Text, Font, line2, resultColor, flags | TextFormatFlags.EndEllipsis);
		if (entry.Note.Length > 0)
		{
			var note = Rectangle.FromLTRB(line2.Left + resultWidth + Gap, line2.Top, line1.Right, line2.Bottom);
			if (note.Width > 20)
			{
				TextRenderer.DrawText(e.Graphics, entry.Note, Font, note, faint,
					flags | TextFormatFlags.Right | TextFormatFlags.EndEllipsis);
			}
		}
	}

	// Right-click selects the entry under the mouse before the menu opens
	protected override void OnMouseDown(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Right)
		{
			int index = IndexFromPoint(e.Location);
			if (index >= 0 && index < _entries.Count)
			{
				SelectedIndex = index;
			}
		}
		base.OnMouseDown(e);
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		base.OnMouseClick(e);
		int index = IndexFromPoint(e.Location);
		if (index >= 0 && index < _entries.Count)
		{
			var handler = (ModifierKeys & Keys.Shift) != 0 ? ResultClicked : EntryClicked;
			handler?.Invoke(this, _entries[index]);
		}
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);
		if (e.Control && e.KeyCode == Keys.C && Selected is { } entry)
		{
			Clipboard.SetText(CopyText(entry));
			e.Handled = true;
		}
	}

	internal static string CopyText(HistoryEntry entry)
	{
		string note = entry.Note.Length > 0 ? "\t(" + entry.Note + ")" : "";
		return entry.Input + "\r\n\t" + entry.Text + note;
	}

	// Every calculation, as Save History writes it (notices left out)
	public string AllText()
	{
		var text = new StringBuilder();
		foreach (var entry in _entries.Where(e => e.Kind != "notice"))
		{
			text.Append(CopyText(entry)).Append("\r\n");
		}
		return text.ToString();
	}
}
