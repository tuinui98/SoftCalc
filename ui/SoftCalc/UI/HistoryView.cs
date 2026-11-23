// HistoryView.cs - History pane: one two-line item per entry, click to recall
//
// Line 1: the input as typed on the left; when values were substituted
// for variables, answer or $n, the numeric form right-aligned in a
// fainter colour (extensions.md E10). Line 2: the result, indented.
// A currency line shows its rate date faint at the right of line 2 (M8).
// Click raises EntryClicked (recall the expression), Shift+click raises
// ResultClicked (insert the result).
// Ctrl+C copies the selected entry; the context menu also recalls,
// inserts, and copies the result, the entry, or everything (E9), and
// deletes the entry or clears History, without asking (owner 2026-11-07).

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

	// The faint right-hand text: when it is the typed line plus an added
	// ` = ...` (rai-ngan-wa `= 5-3-50`, twos `= 0xFF`), only that part, so
	// a narrow window does not cut it off (owner 2026-11-09)
	public string RightText => DisplayInput.StartsWith(Input + " = ", StringComparison.Ordinal)
		? DisplayInput[(Input.Length + 1)..]
		: DisplayInput;

	// A matrix result as a table (matrix round 2 D); null for every other result
	public Logic.MatrixTable? Table { get; } = Kind == "result" ? Logic.MatrixTable.Parse(Text) : null;
}

internal sealed class HistoryView : ListBox
{
	private const int Gap = 16;
	private const int Indent = 40;

	private readonly List<HistoryEntry> _entries = new();

	public event EventHandler<HistoryEntry>? EntryClicked;
	public event EventHandler<HistoryEntry>? ResultClicked;
	public event EventHandler<HistoryEntry>? DeleteClicked;
	public event EventHandler? ClearClicked;
	public event EventHandler<bool>? ArrowPressed;    // Up (true) or Down with the list focused: the Input walk does it

	public IReadOnlyList<HistoryEntry> Entries => _entries;

	public HistoryView()
	{
		DrawMode = DrawMode.OwnerDrawVariable;    // a matrix result takes a line per row
		BorderStyle = BorderStyle.None;
		IntegralHeight = false;
		HorizontalScrollbar = false;
		ItemHeight = (Font.Height + 2) * 2 + 4;
		ContextMenuStrip = BuildContextMenu();
	}

	private ContextMenuStrip BuildContextMenu()
	{
		var menu = new ContextMenuStrip();
		var useExpression = new ToolStripMenuItem("Use E&xpression", null, (_, _) => Raise(EntryClicked));
		var insertResult = new ToolStripMenuItem("&Insert Result", null, (_, _) => Raise(ResultClicked)) { ShortcutKeyDisplayString = "Shift+Click" };
		var copyResult = new ToolStripMenuItem("Copy &Result", null, (_, _) => CopySelected(e => e.Text));
		var copyEntry = new ToolStripMenuItem("Copy &Expression and Result", null, (_, _) => CopySelected(CopyText));
		var copyTable = new ToolStripMenuItem("Copy as &Table", null, (_, _) => CopySelected(e => e.Table?.Tsv() ?? ""));
		var copyAll = new ToolStripMenuItem("Copy &All", null, (_, _) => SetClipboard(AllText()));
		var delete = new ToolStripMenuItem("&Delete", null, (_, _) =>
		{
			if (Selected is { } entry)
			{
				DeleteClicked?.Invoke(this, entry);
			}
		});
		var clear = new ToolStripMenuItem("C&lear History", null, (_, _) => ClearClicked?.Invoke(this, EventArgs.Empty));
		menu.Items.AddRange(new ToolStripItem[]
		{
			useExpression, insertResult, new ToolStripSeparator(),
			copyResult, copyEntry, copyTable, new ToolStripSeparator(), copyAll,
			new ToolStripSeparator(), delete, clear,
		});
		menu.Opening += (_, _) =>
		{
			bool entry = Selected is { Kind: not "notice" };
			useExpression.Enabled = entry;
			insertResult.Enabled = Selected is { Kind: "result" };
			copyResult.Enabled = entry;
			copyEntry.Enabled = entry;
			copyTable.Visible = Selected?.Table != null;
			copyAll.Enabled = _entries.Any(e => e.Kind != "notice");
			delete.Enabled = Selected != null;
			clear.Enabled = _entries.Count > 0;
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

	// `$n` of a result entry: 1 for the newest result line, counting result lines only
	public int ResultNumber(HistoryEntry entry)
	{
		int index = _entries.IndexOf(entry);
		return index < 0 || entry.Kind != "result" ? 0 : _entries.Skip(index).Count(e => e.Kind == "result");
	}

	public void RemoveEntry(HistoryEntry entry)
	{
		int index = _entries.IndexOf(entry);
		if (index < 0)
		{
			return;
		}
		_entries.RemoveAt(index);
		Items.RemoveAt(index);
		if (Items.Count > 0)
		{
			SelectedIndex = Math.Min(index, Items.Count - 1);
		}
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
		// Variable heights are measured once per item: add them again
		BeginUpdate();
		Items.Clear();
		Items.AddRange(_entries.Cast<object>().ToArray());
		EndUpdate();
	}

	// Decimal Places "All": table cells are shortened; a set number of places is shown as given
	private bool _shortCells = true;

	[System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
	public bool ShortCells
	{
		get => _shortCells;
		set
		{
			if (_shortCells != value)
			{
				_shortCells = value;
				Invalidate();
			}
		}
	}

	private int LineHeight => Font.Height + 2;

	// Table rows shown: at most 10, and as many as fit the 255-pixel item limit
	private int ShownRows(Logic.MatrixTable table)
	{
		int fit = Math.Max(1, (255 - 4) / LineHeight - 2);
		return Math.Min(table.Rows, Math.Min(Logic.MatrixTable.MaxShown, fit));
	}

	protected override void OnMeasureItem(MeasureItemEventArgs e)
	{
		base.OnMeasureItem(e);
		if (e.Index < 0 || e.Index >= _entries.Count || _entries[e.Index].Table is not { } table)
		{
			e.ItemHeight = ItemHeight;
			return;
		}
		int lines = 1 + ShownRows(table) + (table.Rows > ShownRows(table) ? 1 : 0);
		e.ItemHeight = Math.Min(255, LineHeight * lines + 4);
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
				TextRenderer.DrawText(e.Graphics, entry.RightText, Font, right, faint,
					flags | TextFormatFlags.Right | TextFormatFlags.EndEllipsis);
			}
		}
		Color resultColor = entry.Kind == "error" && !selected ? Theme.Error : main;
		if (entry.Table is { } table)
		{
			DrawTable(e.Graphics, table, entry.Note, line2, main, faint);
			return;
		}
		int resultWidth = TextRenderer.MeasureText(e.Graphics, entry.Text, Font, Size.Empty, flags).Width;
		var resultArea = line2;
		if (entry.Note.Length > 0)
		{
			// A long result (a matrix) gives up room so its note still shows (matrix round 2, owner 2026-11-24)
			int noteWidth = TextRenderer.MeasureText(e.Graphics, entry.Note, Font, Size.Empty, flags).Width;
			int room = line2.Width - Math.Min(noteWidth, line2.Width / 2) - Gap;
			if (resultWidth > room)
			{
				resultArea = new Rectangle(line2.Left, line2.Top, Math.Max(room, 0), line2.Height);
				resultWidth = resultArea.Width;
			}
		}
		TextRenderer.DrawText(e.Graphics, entry.Text, Font, resultArea, resultColor, flags | TextFormatFlags.EndEllipsis);
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

	//==================================================================
	//  A matrix result: right-aligned columns between brackets, the size beside
	//==================================================================
	private void DrawTable(Graphics g, Logic.MatrixTable table, string note, Rectangle first, Color main, Color faint)
	{
		const TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
		int rows = ShownRows(table);
		int cols = Math.Min(table.Cols, Logic.MatrixTable.MaxShown);
		bool moreRows = table.Rows > rows;
		var text = new string[rows, cols];
		var full = new int[cols];
		for (int c = 0; c < cols; c++)
		{
			for (int r = 0; r < rows; r++)
			{
				text[r, c] = _shortCells ? Logic.MatrixTable.Short(table.Cells[r][c]) : table.Cells[r][c];
				full[c] = Math.Max(full[c], TextRenderer.MeasureText(g, text[r, c], Font, Size.Empty, flags).Width);
			}
		}
		int bracketWidth = Math.Max(4, Font.Height / 4);
		int gapWidth = Math.Max(8, Font.Height / 2);
		int dotsWidth = TextRenderer.MeasureText(g, "…", Font, Size.Empty, flags).Width;
		int sizeWidth = TextRenderer.MeasureText(g, table.Size, Font, Size.Empty, flags).Width;

		// As many columns as the width holds, keeping room for "…", the bracket and the size
		int fit = 1;
		int used = bracketWidth * 2 + full[0] + gapWidth;
		while (fit < cols)
		{
			bool last = fit + 1 == cols && table.Cols == cols;
			int after = used + full[fit] + gapWidth + (last ? 0 : dotsWidth + gapWidth);
			if (first.Left + after + Gap + sizeWidth > first.Right)
			{
				break;
			}
			used += full[fit] + gapWidth;
			fit++;
		}
		bool moreCols = table.Cols > fit;
		int columns = fit + (moreCols ? 1 : 0);
		var cells = new string[rows, columns];
		var widths = new int[columns];
		for (int c = 0; c < columns; c++)
		{
			widths[c] = c < fit ? full[c] : dotsWidth;
			for (int r = 0; r < rows; r++)
			{
				cells[r, c] = c < fit ? text[r, c] : "…";
			}
		}
		int left = first.Left;
		int top = first.Top;
		int height = LineHeight * (rows + (moreRows ? 1 : 0));
		int x = left + bracketWidth + gapWidth / 2;
		using var pen = new Pen(main);
		for (int c = 0; c < columns; c++)
		{
			for (int r = 0; r < rows; r++)
			{
				var cell = new Rectangle(x, top + r * LineHeight, widths[c], LineHeight);
				TextRenderer.DrawText(g, cells[r, c], Font, cell, main, flags | TextFormatFlags.Right);
			}
			if (moreRows)
			{
				var dots = new Rectangle(x, top + rows * LineHeight, widths[c], LineHeight);
				TextRenderer.DrawText(g, "⋮", Font, dots, faint, flags | TextFormatFlags.HorizontalCenter);
			}
			x += widths[c] + gapWidth;
		}
		int right = x - gapWidth / 2 + bracketWidth;
		int y0 = top + 2;
		int y1 = top + height - 2;
		g.DrawLines(pen, new[] { new Point(left + bracketWidth, y0), new Point(left, y0), new Point(left, y1), new Point(left + bracketWidth, y1) });
		g.DrawLines(pen, new[] { new Point(right - bracketWidth, y0), new Point(right, y0), new Point(right, y1), new Point(right - bracketWidth, y1) });

		// The size beside the first row; a note (digits that may be off) beside the second
		var lines = rows > 1 && note.Length > 0 ? new[] { table.Size, note } : new[] { note.Length > 0 ? $"{table.Size}   {note}" : table.Size };
		for (int i = 0; i < lines.Length; i++)
		{
			var beside = Rectangle.FromLTRB(right + Gap, top + i * LineHeight, first.Right, top + (i + 1) * LineHeight);
			if (beside.Width > 20)
			{
				TextRenderer.DrawText(g, lines[i], Font, beside, faint, flags | TextFormatFlags.EndEllipsis);
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
		if (index < 0 || index >= _entries.Count)
		{
			return;
		}
		var handler = (ModifierKeys & Keys.Shift) != 0 ? ResultClicked : EntryClicked;
		handler?.Invoke(this, _entries[index]);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);
		if (!e.Control && !e.Alt && !e.Shift && e.KeyCode is Keys.Up or Keys.Down)
		{
			e.Handled = true;    // not the list's own move: the line goes to Input as well (owner 2026-11-16)
			ArrowPressed?.Invoke(this, e.KeyCode == Keys.Up);
			return;
		}
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
