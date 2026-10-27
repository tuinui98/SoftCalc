// HistoryView.cs - History pane: two lines per entry, click to recall the input
//
// Each entry shows its display expression, then its result indented by
// a tab (SPEC 6.3, extensions.md E10). Clicking either line raises
// EntryClicked with the entry, whose Input is the text as typed.

namespace SoftCalc.UI;

internal sealed record HistoryEntry(string Kind, string Input, string DisplayInput, string Text);

internal sealed class HistoryView : RichTextBox
{
	private readonly List<HistoryEntry> _entries = new();

	public event EventHandler<HistoryEntry>? EntryClicked;

	public IReadOnlyList<HistoryEntry> Entries => _entries;

	public HistoryView()
	{
		ReadOnly = true;
		WordWrap = false;    // keeps one text line per History line for click mapping
		BorderStyle = BorderStyle.None;
		BackColor = SystemColors.Window;
		DetectUrls = false;
		HideSelection = false;
		ScrollBars = RichTextBoxScrollBars.Both;
	}

	public void Append(HistoryEntry entry)
	{
		_entries.Add(entry);
		AppendText(entry.DisplayInput + "\n\t" + entry.Text + "\n");
		SelectionStart = TextLength;
		ScrollToCaret();
	}

	public void ClearEntries()
	{
		_entries.Clear();
		Clear();
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		base.OnMouseClick(e);
		if (SelectionLength > 0)
		{
			return;    // the user is selecting text to copy
		}
		int line = GetLineFromCharIndex(GetCharIndexFromPosition(e.Location));
		int index = line / 2;
		if (index >= 0 && index < _entries.Count)
		{
			EntryClicked?.Invoke(this, _entries[index]);
		}
	}
}
