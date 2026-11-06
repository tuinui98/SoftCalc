// MainForm.Persistence.cs - Settings, definitions and History files for the main window (M7)
//
// Start: settings -> window and view -> engine settings -> definitions ->
// History (and answer/$n). Close: settings and History merge. Definitions
// are also written after every change. Plan: design/m7_plan.md.

using System.Diagnostics;
using System.Text.Json.Nodes;
using SoftCalc.Engine;
using SoftCalc.Logic;
using SoftCalc.Persistence;

namespace SoftCalc.UI;

internal sealed partial class MainForm
{
	private readonly DataFolder _folder;
	private readonly AppSettings _prefs;
	private readonly string? _settingsWarning;
	private readonly List<HistoryEntry> _sessionEntries = new();
	private readonly DateTime _processStart = Process.GetCurrentProcess().StartTime;
	private DateTime? _clearedAt;
	private int _sequence;
	private bool _engineReady;

	//==================================================================
	//  Start-up
	//==================================================================
	private void ApplySavedWindow()
	{
		var placement = _prefs.Window;
		Size = new Size(placement.Width, placement.Height);
		if (placement.HasPosition)
		{
			StartPosition = FormStartPosition.Manual;
			var areas = Screen.AllScreens.Select(s => s.WorkingArea).ToList();
			Bounds = WindowPlacement.FitOnScreen(new Rectangle(placement.X, placement.Y, placement.Width, placement.Height), areas);
		}
		if (placement.Maximized)
		{
			WindowState = FormWindowState.Maximized;
		}
		_showSamplesItem.Checked = _prefs.ShowSamples;
		SetAlwaysOnTop(_prefs.AlwaysOnTop);
		_showRibbonItem.Checked = _prefs.ShowRibbon;
		_showHintsItem.Checked = _prefs.ShowHints;
		SetTextFont(_prefs.TextFontFamily, _prefs.TextFontBase, _prefs.TextScale);
		ApplyGraphPrefs(_prefs.Graph);
	}

	private async Task RestoreSessionAsync()
	{
		await _session.SetRatesAsync(_rates.LoadCached());    // cached rates; fetched only when a conversion needs them
		await _session.ChangeSettingsAsync(new JsonObject
		{
			["precision"] = _prefs.Precision, ["angle"] = _prefs.Angle, ["base"] = _prefs.Base,
			["rpn"] = _prefs.Rpn, ["decimals"] = _prefs.Decimals, ["pad_zeros"] = _prefs.PadZeros,
		});
		if (File.Exists(_folder.DefinitionsPath))
		{
			var skipped = await _session.ImportDefinitionsAsync(PcfFile.Read(_folder.DefinitionsPath));
			if (skipped.Count > 0)
			{
				AddNotice("(some saved definitions were skipped)", string.Join("; ", skipped));
			}
		}
		var saved = HistoryStore.Visible(HistoryStore.Load(_folder.HistoryPath), _prefs.HistoryCap);
		foreach (var stored in saved)
		{
			_history.Append(new HistoryEntry(stored.Kind, stored.Input, stored.DisplayInput, stored.Text)
			{
				Id = stored.Id, Time = stored.Time, Note = stored.Note ?? "",
			});
		}
		_lastWasResult = saved.Count > 0 && saved[^1].Kind == "result";
		await _session.RestoreResultsAsync(saved.Where(s => s.Kind == "result").Select(s => s.Text));
		if (_settingsWarning != null)
		{
			AddNotice("(settings reset)", _settingsWarning);
		}
		_engineReady = true;
	}

	private void AddNotice(string title, string detail)
	{
		_history.Append(new HistoryEntry("notice", "", title, detail));
	}

	// Stamps an entry so it can be merged into history.jsonl at close
	private HistoryEntry Stamp(HistoryEntry entry)
	{
		var stamped = entry with
		{
			Id = HistoryStore.MakeId(_processStart, Environment.ProcessId, ++_sequence),
			Time = DateTime.UtcNow,
		};
		_sessionEntries.Add(stamped);
		return stamped;
	}

	private void OnDefinitionsChanged(string text)
	{
		if (!_engineReady)
		{
			return;    // the start-up import is not a change
		}
		try
		{
			_folder.WithLock(() => PcfFile.Write(_folder.DefinitionsPath, text));    // last writer wins
		}
		catch (IOException ex)
		{
			AddNotice("(definitions not saved)", ex.Message);
		}
	}

	//==================================================================
	//  Close
	//==================================================================
	private void SaveOnClose()
	{
		try
		{
			CaptureSettings();
			_folder.WithLock(() => SettingsStore.Save(_folder.SettingsPath, _prefs));
			HistoryStore.MergeAndSave(_folder, _sessionEntries.Select(ToStored), _clearedAt, _prefs.HistoryCap);
			if (_engineReady)
			{
				_folder.WithLock(() => PcfFile.Write(_folder.DefinitionsPath, _session.Definitions));
			}
		}
		catch (IOException ex)
		{
			MessageBox.Show(this, "SoftCalc could not save its files.\r\n\r\n" + ex.Message, "SoftCalc",
				MessageBoxButtons.OK, MessageBoxIcon.Warning);
		}
	}

	private static StoredEntry ToStored(HistoryEntry entry)
	{
		return new StoredEntry(entry.Id, entry.Time, entry.Kind, entry.Input, entry.DisplayInput, entry.Text, entry.Note);
	}

	private void CaptureSettings()
	{
		var settings = _session.Settings;
		_prefs.Precision = (int?)settings["precision"] ?? _prefs.Precision;
		_prefs.Angle = (string?)settings["angle"] ?? _prefs.Angle;
		_prefs.Base = (string?)settings["base"] ?? _prefs.Base;
		_prefs.Rpn = (bool?)settings["rpn"] ?? _prefs.Rpn;
		_prefs.Decimals = (int?)settings["decimals"];
		_prefs.PadZeros = (bool?)settings["pad_zeros"] ?? _prefs.PadZeros;
		_prefs.View = _view.ToString();
		_prefs.ShowSamples = _showSamplesItem.Checked;
		_prefs.AlwaysOnTop = _onTopItem.Checked;
		_prefs.ShowRibbon = _showRibbonItem.Checked;
		_prefs.ShowHints = _showHintsItem.Checked;
		var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
		_prefs.Window = new WindowPlacement
		{
			X = bounds.X, Y = bounds.Y, Width = bounds.Width, Height = bounds.Height,
			Maximized = WindowState == FormWindowState.Maximized,
		};
		_prefs.Graph = CaptureGraphPrefs();
	}

	private void ApplyGraphPrefs(GraphPrefs g)
	{
		var s = _graphSettings;
		s.Mode = g.Mode == "polar" ? GraphMode.Polar : GraphMode.XY;
		s.ShowGrid = g.ShowGrid;
		s.Function = g.Function;
		(s.MinX, s.MaxX, s.GridX, s.MinY, s.MaxY, s.GridY) = (g.MinX, g.MaxX, g.GridX, g.MinY, g.MaxY, g.GridY);
		(s.MinT, s.MaxT, s.MaxR, s.GridR) = (g.MinT, g.MaxT, g.MaxR, g.GridR);
	}

	private GraphPrefs CaptureGraphPrefs()
	{
		var s = _graphSettings;
		return new GraphPrefs
		{
			Mode = s.Mode == GraphMode.Polar ? "polar" : "xy", ShowGrid = s.ShowGrid, Function = s.Function,
			MinX = s.MinX, MaxX = s.MaxX, GridX = s.GridX, MinY = s.MinY, MaxY = s.MaxY, GridY = s.GridY,
			MinT = s.MinT, MaxT = s.MaxT, MaxR = s.MaxR, GridR = s.GridR,
		};
	}

	//==================================================================
	//  File menu
	//==================================================================
	private void SaveHistoryAs()
	{
		using var dialog = new SaveFileDialog { Filter = "Text file (*.txt)|*.txt", DefaultExt = "txt", FileName = "history.txt" };
		if (dialog.ShowDialog(this) != DialogResult.OK)
		{
			return;
		}
		DataFolder.WriteAtomic(dialog.FileName, _history.AllText());
	}

	private void SaveDefinitionsAs()
	{
		using var dialog = new SaveFileDialog { Filter = "Functions & Variables (*.pcf)|*.pcf", DefaultExt = "pcf" };
		if (dialog.ShowDialog(this) == DialogResult.OK)
		{
			PcfFile.Write(dialog.FileName, _session.Definitions);
		}
	}

	private async Task LoadDefinitionsAsync()
	{
		using var dialog = new OpenFileDialog { Filter = "Functions & Variables (*.pcf)|*.pcf|All files (*.*)|*.*" };
		if (dialog.ShowDialog(this) != DialogResult.OK)
		{
			return;
		}
		var skipped = await _session.ImportDefinitionsAsync(PcfFile.Read(dialog.FileName));
		await RefreshStateAsync();
		if (skipped.Count > 0)
		{
			MessageBox.Show(this, "These lines were skipped:\r\n\r\n" + string.Join("\r\n", skipped.Take(20)),
				"Load Functions & Variables", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}
	}
}
