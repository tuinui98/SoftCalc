// Keypad.cs - Numeric Calc view keypad, 4 x 5 buttons as in the original (SPEC 2)

namespace SoftCalc.UI;

internal sealed class Keypad : TableLayoutPanel
{
	private static readonly string[,] KeyLabels =
	{
		{ "M+", "7", "8", "9", "+" },
		{ "M-", "4", "5", "6", "-" },
		{ "MC", "1", "2", "3", "/" },
		{ "MR", "Clr", "0", "Ent", "*" },
	};

	public event EventHandler<string>? KeyPressedLabel;

	public Keypad()
	{
		RowCount = KeyLabels.GetLength(0);
		ColumnCount = KeyLabels.GetLength(1);
		AutoSize = true;
		AutoSizeMode = AutoSizeMode.GrowAndShrink;
		Anchor = AnchorStyles.None;
		for (int row = 0; row < RowCount; row++)
		{
			for (int col = 0; col < ColumnCount; col++)
			{
				string label = KeyLabels[row, col];
				var button = new Button
				{
					Text = label,
					Width = 48,
					Height = 32,
					Margin = new Padding(2),
					TabStop = false,    // focus stays in the Input box
				};
				button.Click += (_, _) => KeyPressedLabel?.Invoke(this, label);
				Controls.Add(button, col, row);
			}
		}
	}
}
