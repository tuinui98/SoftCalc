// RandomWindow.Entropy.cs - The Entropy tab: try entropy sources side by side (design/random_plan.md R5)
//
// Owner 2026-11-12: get a value from a source (system, TPM, CPU jitter,
// clock, GUID, mouse, keys, dice, NIST beacon, drand), see it raw and as
// a 128-bit seed, and compare ten pulls: how much the raw value changes
// and how many bits of the seed change. A row can become the Seed, or
// be mixed into the System generator for every tab (Password too).

using System.Diagnostics;
using SoftCalc.Engine;
using SoftCalc.Logic;
using SoftCalc.Persistence;

namespace SoftCalc.UI;

internal sealed partial class RandomWindow
{
	private sealed record EntropyRow(string Source, EntropySample Sample, byte[] Seed);

	private readonly ComboBox _entropySource = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
	private readonly Label _entropyAbout = new() { AutoSize = true, MaximumSize = new Size(500, 0), Dock = DockStyle.Top, Padding = new Padding(3, 4, 3, 4) };
	private readonly Button _entropyGet = new() { Text = "&Get", AutoSize = true, MinimumSize = new Size(70, 0) };
	private readonly Button _entropyGetTen = new() { Text = "Get x&10", AutoSize = true };
	private readonly Label _mousePad = new()
	{
		Height = 80, Dock = DockStyle.Top, BorderStyle = BorderStyle.FixedSingle, TextAlign = ContentAlignment.MiddleCenter,
		BackColor = SystemColors.Window, Text = "Move the mouse here",
	};
	private readonly TextBox _keyBox = new() { Dock = DockStyle.Top, PlaceholderText = "Type anything here" };
	private readonly TextBox _diceBox = new() { Dock = DockStyle.Top, PlaceholderText = "Dice results, e.g. 3 6 1 4 2 5 ..." };
	private readonly ListView _entropyRows = new()
	{
		Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, Margin = new Padding(0, 6, 0, 0),
	};
	private readonly Label _entropyNote = Note();
	private readonly List<EntropyRow> _entropyList = new();
	private readonly List<long> _mouseData = new(), _keyData = new();
	private int _mouseMoves, _keyPresses;
	private Point _lastMouse;

	private TabPage EntropyTab()
	{
		var page = new TabPage("Entropy") { Padding = new Padding(8) };
		_entropyNote.MaximumSize = new Size(500, 0);    // wraps
		_entropySource.Items.AddRange(Entropy.Sources.Select(s => (object)s.Name).ToArray());
		_entropySource.SelectedIndexChanged += (_, _) => EntropySourceChanged();
		_entropyGet.Click += async (_, _) => await GetEntropy(1);
		_entropyGetTen.Click += async (_, _) => await GetEntropy(10);
		var top = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = false };
		top.Controls.Add(new Label { Text = "S&ource", AutoSize = true, Margin = new Padding(3, 7, 6, 3) });
		top.Controls.AddRange(new Control[] { _entropySource, _entropyGet, _entropyGetTen });

		// what the user gives: mouse moves, keys, dice
		_mousePad.MouseMove += (_, e) =>
		{
			if (e.Location == _lastMouse)
			{
				return;
			}
			_lastMouse = e.Location;
			_mouseData.AddRange(new long[] { e.X, e.Y, Stopwatch.GetTimestamp() });
			_mouseMoves++;
			_mousePad.Text = $"{_mouseMoves} moves (about {Math.Min(Entropy.MaxBits, _mouseMoves * MouseBitsEach):0} bits)";
		};
		_keyBox.KeyPress += (_, e) =>
		{
			_keyData.AddRange(new long[] { e.KeyChar, Stopwatch.GetTimestamp() });
			_keyPresses++;
		};

		foreach (var (text, width) in new[] { ("#", 30), ("Raw", 160), ("Seed (128 bits)", 130), ("Change", 60),
			("Raw bits changed", 95), ("Seed bits changed", 100), ("Entropy", 60) })
		{
			_entropyRows.Columns.Add(text, width);
		}

		var useSeed = new Button { Text = "&Use as Seed", AutoSize = true };
		useSeed.Click += (_, _) => UseAsSeed();
		var mix = new Button { Text = "Mi&x into System", AutoSize = true };
		new ToolTip().SetToolTip(mix, "Mix the chosen rows (all when none is chosen) into the system generator, for every tab");
		mix.Click += (_, _) => MixIntoSystem();
		var unmix = new Button { Text = "Clear mi&x", AutoSize = true };
		unmix.Click += (_, _) =>
		{
			_secure.Clear();
			ShowMix();
			EntropyNote("The system generator is used alone again.", false);
		};
		var clear = new Button { Text = "Clear &list", AutoSize = true };
		clear.Click += (_, _) => ClearEntropy();
		var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Bottom, Margin = new Padding(0, 6, 0, 0) };
		buttons.Controls.AddRange(new Control[] { useSeed, mix, unmix, clear });

		page.Controls.Add(_entropyRows);
		page.Controls.Add(_entropyNote);
		page.Controls.Add(_diceBox);
		page.Controls.Add(_keyBox);
		page.Controls.Add(_mousePad);
		page.Controls.Add(_entropyAbout);
		page.Controls.Add(top);
		page.Controls.Add(buttons);
		return page;
	}

	private const double MouseBitsEach = 1, KeyBitsEach = 1;    // cautious: one unpredictable bit per move or key

	private string EntropyId => Entropy.Sources[Math.Max(0, _entropySource.SelectedIndex)].Id;

	private void EntropySourceChanged()
	{
		string id = EntropyId;
		_entropyAbout.Text = Entropy.Sources.First(s => s.Id == id).About;
		_mousePad.Visible = id == "mouse";
		_keyBox.Visible = id == "keys";
		_diceBox.Visible = id == "dice";
		_entropyGetTen.Enabled = !Entropy.TakesInput(id);    // what you gave is one sample
	}

	//==================================================================
	//  Getting samples
	//==================================================================
	private async Task GetEntropy(int count)
	{
		if (_drawing)
		{
			return;
		}
		string id = EntropyId;
		var (samples, error) = await SamplesAsync(id, count);
		if (IsDisposed)
		{
			return;
		}
		if (samples == null)
		{
			EntropyNote(error ?? "", true);
			return;
		}
		var added = new List<EntropyRow>();
		_entropyRows.BeginUpdate();
		foreach (var sample in samples)
		{
			var row = new EntropyRow(id, sample, Entropy.Condition(sample.Bytes));
			var before = _entropyList.LastOrDefault();
			_entropyList.Add(row);
			added.Add(row);
			_entropyRows.Items.Add(new ListViewItem(Cells(row, before?.Source == id ? before : null)));
		}
		_entropyRows.EndUpdate();
		_entropyRows.EnsureVisible(_entropyRows.Items.Count - 1);
		EntropyNote(Summary(added), false);
	}

	private async Task<(List<EntropySample>? Samples, string? Error)> SamplesAsync(string id, int count)
	{
		switch (id)
		{
			case "nist" or "drand":
				_drawing = true;
				UseWaitCursor = true;
				EntropyNote("Asking the beacon...", false);
				try
				{
					return await RandomBeacons.GetAsync(id, count);
				}
				finally
				{
					_drawing = false;
					UseWaitCursor = false;
				}
			case "tpm":
				var list = new List<EntropySample>();
				for (int i = 0; i < count; i++)
				{
					var (bytes, error) = TpmRandom.Get(Entropy.SeedBytes);
					if (bytes == null)
					{
						return (null, error);
					}
					list.Add(new EntropySample(Convert.ToHexString(bytes), bytes, bytes.Length * 8));
				}
				return (list, null);
			case "mouse":
				var moves = Entropy.FromEvents("moves", _mouseData, _mouseMoves, MouseBitsEach);
				return moves == null ? (null, "Move the mouse in the box first.") : (new List<EntropySample> { moves }, null);
			case "keys":
				var keys = Entropy.FromEvents("keys", _keyData, _keyPresses, KeyBitsEach);
				return keys == null ? (null, "Type in the box first.") : (new List<EntropySample> { keys }, null);
			case "dice":
				var (dice, diceError) = Entropy.FromDice(_diceBox.Text);
				return dice == null ? (null, diceError) : (new List<EntropySample> { dice }, null);
			default:
				Func<EntropySample> get = id switch
				{
					"guid" => Entropy.FromGuid,
					"clock" => Entropy.FromClock,
					"jitter" => () => Entropy.FromJitter(),
					_ => Entropy.FromSystem,
				};
				return (Enumerable.Range(0, count).Select(_ => get()).ToList(), null);
		}
	}

	// One row; the change columns compare with the row before from the same source
	private static string[] Cells(EntropyRow row, EntropyRow? before)
	{
		string change = "", rawBits = "", seedBits = "";
		if (before != null)
		{
			if (row.Sample.Number is { } now && before.Sample.Number is { } then)
			{
				change = (now - then).ToString("+#;-#;0", System.Globalization.CultureInfo.InvariantCulture);
			}
			if (Entropy.BitsChanged(row.Sample.Bytes, before.Sample.Bytes) is { } raw)
			{
				rawBits = $"{raw} of {row.Sample.Bytes.Length * 8}";
			}
			seedBits = $"{Entropy.BitsChanged(row.Seed, before.Seed)} of {Entropy.SeedBytes * 8}";
		}
		string bits = Entropy.IsPublic(row.Source) ? "public" : $"{row.Sample.Bits:0} bits";
		return new[] { "", row.Sample.Raw, Entropy.SeedText(row.Seed), change, rawBits, seedBits, bits };
	}

	private string Summary(List<EntropyRow> added)
	{
		for (int i = 0; i < _entropyRows.Items.Count; i++)
		{
			_entropyRows.Items[i].Text = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
		}
		if (added.Count < 2)
		{
			return !Entropy.IsPublic(added[0].Source)
				? $"About {added[0].Sample.Bits:0} unpredictable bits; the seed column is SHA-256 of the raw bytes, cut to 128 bits."
				: "A public value: anyone can look it up, so it adds no secret.";
		}
		var pairs = added.Zip(added.Skip(1)).ToList();
		double seedMean = pairs.Average(p => (double)Entropy.BitsChanged(p.First.Seed, p.Second.Seed)!);
		string text = $"Between pulls the seed changed {seedMean:0.0} of 128 bits on average (64 is what chance gives)";
		var raws = pairs.Select(p => Entropy.BitsChanged(p.First.Sample.Bytes, p.Second.Sample.Bytes)).Where(b => b != null).ToList();
		if (raws.Count > 0)
		{
			text += $", the raw value {raws.Average(b => (double)b!):0.0} of {added[0].Sample.Bytes.Length * 8}";
		}
		return text + ".";
	}

	//==================================================================
	//  Using samples
	//==================================================================
	private List<EntropyRow> ChosenRows(bool allWhenNone)
	{
		var chosen = _entropyRows.SelectedIndices.Cast<int>().Select(i => _entropyList[i]).ToList();
		return chosen.Count > 0 || !allWhenNone ? chosen : _entropyList.ToList();
	}

	private void UseAsSeed()
	{
		var row = ChosenRows(false).LastOrDefault() ?? _entropyList.LastOrDefault();
		if (row == null)
		{
			EntropyNote("Get a value first.", true);
			return;
		}
		_seed.Text = Entropy.SeedText(row.Seed);    // picks Seed too
		EntropyNote("Seed set: Simple and Advanced now draw repeatably from it.", false);
	}

	private void MixIntoSystem()
	{
		var rows = ChosenRows(true);
		if (rows.Count == 0)
		{
			EntropyNote("Get a value first.", true);
			return;
		}
		double before = _secure.Bits;
		foreach (var row in rows)
		{
			_secure.Mix(row.Sample.Bytes, Entropy.IsPublic(row.Source) ? 0 : row.Sample.Bits);
		}
		ShowMix();
		EntropyNote($"Mixed {rows.Count} into the system generator: +{_secure.Bits - before:0} secret bits"
			+ " (public values add none). Every tab uses it until Clear mix or the window closes.", false);
	}

	private void ClearEntropy()
	{
		_entropyList.Clear();
		_entropyRows.Items.Clear();
		_mouseData.Clear();
		_keyData.Clear();
		(_mouseMoves, _keyPresses) = (0, 0);
		_mousePad.Text = "Move the mouse here";
		_keyBox.Clear();
		_diceBox.Clear();
		EntropyNote("", false);
	}

	private void EntropyNote(string text, bool error)
	{
		_entropyNote.ForeColor = error ? Theme.Error : SystemColors.GrayText;
		_entropyNote.Text = text;
	}

	private void LoadEntropyPrefs(RandomPrefs p)
	{
		_entropySource.SelectedIndex = Math.Max(0, Array.FindIndex(Entropy.Sources, s => s.Id == p.EntropySource));
		EntropySourceChanged();
	}

	private void SaveEntropyPrefs(RandomPrefs p)
	{
		p.EntropySource = EntropyId;
	}
}
