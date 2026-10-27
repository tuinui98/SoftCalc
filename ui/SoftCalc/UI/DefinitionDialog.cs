// DefinitionDialog.cs - Add/Edit Function and Add/Edit Variable dialogs (SPEC 4)
//
// Add: one box for the whole definition (`f(x)=x^2`, `a=5`). Edit: a
// read-only head (`g(x)=`, `a=`) and a box for the body or value only.
// OK runs the submit callback; an error keeps the dialog open.

namespace SoftCalc.UI;

internal sealed class DefinitionDialog : Form
{
	private readonly TextBox _body = new() { Dock = DockStyle.Fill };
	private readonly Label _error = new()
	{
		Dock = DockStyle.Fill, ForeColor = Color.Firebrick, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft,
	};
	private readonly Func<string, Task<string?>> _submit;
	private readonly string _head;

	public DefinitionDialog(string title, string head, string body, Func<string, Task<string?>> submit)
	{
		_head = head;
		_submit = submit;
		Text = title;
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		ShowInTaskbar = false;
		StartPosition = FormStartPosition.CenterParent;
		ClientSize = new Size(460, 110);

		var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(8) };
		layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
		layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
		layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
		layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		if (head.Length > 0)
		{
			layout.Controls.Add(new Label { Text = head, AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
		}
		_body.Text = body;
		layout.Controls.Add(_body, head.Length > 0 ? 1 : 0, 0);
		if (head.Length == 0)
		{
			layout.SetColumnSpan(_body, 2);
		}
		layout.Controls.Add(_error, 0, 1);
		layout.SetColumnSpan(_error, 2);

		var ok = new Button { Text = "OK", Width = 90 };
		var cancel = new Button { Text = "Cancel", Width = 90, DialogResult = DialogResult.Cancel };
		var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true };
		buttons.Controls.Add(cancel);
		buttons.Controls.Add(ok);
		layout.Controls.Add(buttons, 0, 2);
		layout.SetColumnSpan(buttons, 2);
		Controls.Add(layout);

		AcceptButton = ok;
		CancelButton = cancel;
		ok.Click += async (_, _) => await SubmitAsync(ok);
		Shown += (_, _) =>
		{
			_body.Focus();
			_body.SelectionStart = _body.TextLength;
		};
	}

	private async Task SubmitAsync(Button ok)
	{
		ok.Enabled = false;
		try
		{
			string? error = await _submit(_head + _body.Text);
			if (error == null)
			{
				DialogResult = DialogResult.OK;
				Close();
				return;
			}
			_error.Text = error;
			_body.Focus();
		}
		finally
		{
			ok.Enabled = true;
		}
	}
}
