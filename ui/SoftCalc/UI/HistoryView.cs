// HistoryView.cs - History pane: one two-line item per entry, click to recall
//
// Line 1: the input as typed on the left; when values were substituted
// for variables, answer or $n, the numeric form right-aligned in a
// fainter colour (extensions.md E10). Line 2: the result, indented.
// Clicking an entry raises EntryClicked; Ctrl+C copies the selected one.

namespace SoftCalc.UI;

internal sealed record HistoryEntry(string Kind, string Input, string DisplayInput, string Text)
{
	// Set for entries that are saved to history.jsonl (notices are not)
	public string Id { get; init; } = "";
	public DateTime Time { get; init; }

	// The right-hand numeric form is shown only when it differs from the input
	public bool HasSubstitution => Kind == "result" && DisplayInput != Input;
}

internal sealed class HistoryView : ListBox
{
	private const int Gap = 16;
	private const int Indent = 40;

	private readonly List<HistoryEntry> _entries = new();

	public event EventHandler<HistoryEntry>? EntryClicked;

	public IReadOnlyList<HistoryEntry> Entries => _entries;

	public HistoryView()
	{
		DrawMode = DrawMode.OwnerDrawFixed;
		BorderStyle = BorderStyle.None;
		IntegralHeight = false;
		HorizontalScrollbar = false;
		ItemHeight = (Font.Height + 2) * 2 + 4;
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
		TextRenderer.DrawText(e.Graphics, entry.Text, Font, line2, resultColor, flags | TextFormatFlags.EndEllipsis);
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		base.OnMouseClick(e);
		int index = IndexFromPoint(e.Location);
		if (index >= 0 && index < _entries.Count)
		{
			EntryClicked?.Invoke(this, _entries[index]);
		}
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);
		if (e.Control && e.KeyCode == Keys.C && SelectedIndex >= 0 && SelectedIndex < _entries.Count)
		{
			Clipboard.SetText(CopyText(_entries[SelectedIndex]));
			e.Handled = true;
		}
	}

	internal static string CopyText(HistoryEntry entry)
	{
		return entry.Input + "\r\n\t" + entry.Text;
	}
}
