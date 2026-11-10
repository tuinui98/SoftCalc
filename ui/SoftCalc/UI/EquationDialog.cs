// EquationDialog.cs - Type an equation in x and y for the graph (graph2_plan G5)
//
// x and y are the axes; other names are variables (`x^2+y^2=r^2` with r
// defined). OK asks the engine to read it, so a mistake shows here and
// not as an empty graph. From Graph > Add > Examples a grey hint under the
// input shows the example's form and what its constants change.

using SoftCalc.Engine;
using SoftCalc.Logic;

namespace SoftCalc.UI;

internal static class EquationDialog
{
	// The checked text, or null when cancelled; hint: lines shown under the input
	public static async Task<string?> AskAsync(IWin32Window? owner, EngineSession session, string title, string text,
		IReadOnlyList<string> taken, string? hint = null)
	{
		using var form = new Form
		{
			Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false,
			ShowInTaskbar = false, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(380, 150),
			Padding = new Padding(10),
		};
		var help = new Label
		{
			Text = "Equation in x and y, for example x^2+y^2=r^2 or y^2=x^3-x.\nOther names are variables.",
			AutoSize = true, Dock = DockStyle.Top, ForeColor = SystemColors.GrayText,
		};
		var box = new TextBox { Text = text, Dock = DockStyle.Top };
		var error = new Label { AutoSize = false, Height = 36, Dock = DockStyle.Top, ForeColor = Theme.Error, Padding = new Padding(0, 4, 0, 0) };
		var example = new Label
		{
			Text = hint ?? "", AutoSize = false, Height = 64, Dock = DockStyle.Top, ForeColor = SystemColors.GrayText,
			Padding = new Padding(0, 6, 0, 0), UseMnemonic = false, Visible = hint != null,
		};
		if (hint != null)
		{
			form.ClientSize = new Size(form.ClientSize.Width, form.ClientSize.Height + example.Height);
		}
		var ok = new Button { Text = "OK", AutoSize = true, MinimumSize = new Size(80, 0) };
		var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(80, 0) };
		var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
		buttons.Controls.AddRange(new Control[] { cancel, ok });
		form.Controls.Add(error);
		form.Controls.Add(example);
		form.Controls.Add(box);
		form.Controls.Add(help);
		form.Controls.Add(buttons);
		form.AcceptButton = ok;
		form.CancelButton = cancel;
		string? result = null;
		ok.Click += async (_, _) =>
		{
			string typed = box.Text.Trim();
			string? problem = Check(typed, taken) ?? await EngineProblemAsync(session, typed);
			if (problem != null)
			{
				error.Text = problem;
				box.Focus();
				return;
			}
			result = typed;
			form.DialogResult = DialogResult.OK;
		};
		form.Shown += (_, _) => box.SelectAll();
		form.ShowDialog(owner);
		return result;
	}

	// Checks that need no engine; null when fine
	internal static string? Check(string text, IReadOnlyList<string> taken)
	{
		if (!GraphSettings.IsEquation(text))
		{
			return "An equation needs '=' (for a function use Add > Function)";
		}
		if (taken.Any(t => string.Equals(t, text, StringComparison.OrdinalIgnoreCase)))
		{
			return "This equation is already in the list";
		}
		return null;
	}

	private static async Task<string?> EngineProblemAsync(EngineSession session, string text)
	{
		try
		{
			await session.PlotEquationAsync(text, new WorldRect(-1, 1, -1, 1), 1, 1);
			return null;
		}
		catch (EngineException ex)
		{
			return ex.Message;
		}
	}
}
