// RandomWindow.Seed.cs - The Random window's Source row: System or Seed (design/random_plan.md R4)
//
// System: the system's secure generator (the default). Seed: repeatable
// draws through an engine of the window's own (SeedEngine), the same
// values as `rands(seed)` and the tab's Insert Command line typed in
// Input. The Password tab always uses System (owner 2026-11-12: Seed is
// switched off there). Neither the seed nor the choice is saved.

using System.Text.RegularExpressions;
using SoftCalc.Engine;
using SoftCalc.Logic;

namespace SoftCalc.UI;

internal sealed partial class RandomWindow
{
	private static readonly Regex SeedNumber = new(@"^-?[0-9]{1,40}(\.[0-9]{1,40})?$");
	private readonly RadioButton _system = new() { Text = "S&ystem", AutoSize = true, Checked = true, Margin = new Padding(3, 5, 6, 3) };
	private readonly RadioButton _seeded = new() { Text = "&Seed", AutoSize = true, Margin = new Padding(3, 5, 3, 3) };
	private readonly TextBox _seed = new() { Width = 120, PlaceholderText = "e.g. 42" };
	private readonly Button _newSeed = new() { Text = "&New seed", AutoSize = true, Margin = new Padding(6, 2, 3, 3) };
	private readonly Label _mixed = new() { AutoSize = true, Margin = new Padding(12, 7, 3, 3), Visible = false };
	private readonly SeedEngine? _seeds;
	private bool _seededBeforePassword;
	private bool _drawing;

	private Control SourceRow()
	{
		var row = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false, Padding = new Padding(6, 6, 6, 0) };
		row.Controls.Add(new Label { Text = "Source", AutoSize = true, Margin = new Padding(3, 7, 6, 3) });
		row.Controls.AddRange(new Control[] { _system, _seeded, _seed, _newSeed, _mixed });
		_newSeed.Click += (_, _) => _seed.Text = Entropy.NewSeed();    // 128 bits from the system generator
		var tips = new ToolTip();
		tips.SetToolTip(_system, "The system's secure random generator: different every time");
		tips.SetToolTip(_seeded, "The same seed gives the same values, like rands(seed) in Input");
		tips.SetToolTip(_newSeed, "A 128-bit seed from the system generator; note it down to draw the same values again");
		_seed.TextChanged += (_, _) =>
		{
			if (_seed.Text.Length > 0)
			{
				_seeded.Checked = true;
			}
		};
		if (_seeds == null)
		{
			_seeded.Enabled = false;    // no engine to draw with (tests)
			_seed.Enabled = false;
			_newSeed.Enabled = false;
		}
		_tabs.SelectedIndexChanged += (_, _) => SourceForTab();
		return row;
	}

	// Password: System only, Seed switched off; back on the other tabs the choice returns
	private void SourceForTab()
	{
		bool password = _tabs.SelectedIndex == Array.IndexOf(TabIds, "password");
		if (password && _seeded.Enabled)
		{
			_seededBeforePassword = _seeded.Checked;
			_system.Checked = true;
		}
		_seeded.Enabled = !password && _seeds != null;
		_seed.Enabled = _seeded.Enabled;
		_newSeed.Enabled = _seeded.Enabled;
		if (!password && _seeded.Enabled && _seededBeforePassword)
		{
			_seeded.Checked = true;
			_seededBeforePassword = false;
		}
	}

	// The seed in use, or null for System
	private string? SeedInUse => _seeded.Checked && _seeds != null ? _seed.Text.Trim() : null;

	// Draws line count times after rands(seed); shows progress and errors in note
	private async Task<List<string>?> SeedDrawsAsync(string seed, string line, int count, bool unique, Label note)
	{
		if (!SeedNumber.IsMatch(seed))
		{
			note.ForeColor = Theme.Error;
			note.Text = "Seed must be a number, such as 42 or -7.5.";
			return null;
		}
		_drawing = true;
		UseWaitCursor = true;
		note.ForeColor = SystemColors.GrayText;
		note.Text = _seeds!.IsStarted ? $"Drawing with seed {seed}..." : "Starting the engine for Seed...";
		try
		{
			var (texts, error) = await _seeds.DrawsAsync(seed, line, count, unique);
			if (IsDisposed)
			{
				return null;
			}
			if (texts == null)
			{
				note.ForeColor = Theme.Error;
				note.Text = error;
			}
			return texts;
		}
		finally
		{
			_drawing = false;
			UseWaitCursor = false;
		}
	}

	// "+ entropy" beside the source while something is mixed into System
	private void ShowMix()
	{
		_mixed.Visible = _secure.Mixes > 0;
		_mixed.Text = $"System + entropy ({_secure.Bits:0} bits)";
	}

	private static string SeedNote(string seed)
	{
		return $"Seed {seed}: the same as rands({seed}) and Insert Command's line typed that many times in Input.";
	}
}
