// CryptoWindow.Watch.cs - The Watch tab: watched addresses, their settings and what happened (design/crypto_plan.md C2a)
//
// The list and the checks live in CryptoWatcher (owned by the main
// window), so watching goes on with this window closed; this tab shows
// them and edits the list and the settings.

using System.Globalization;
using SoftCalc.Logic;

namespace SoftCalc.UI;

internal sealed partial class CryptoWindow
{
	private readonly ListView _watchList = Table(("Label", 140), ("Address", 150), ("Balance", 170), ("Last change", 120), ("Status", 200));
	private readonly ListView _eventList = Table(("Time", 120), ("Label", 130), ("Event", 480));
	private readonly Label _watchState = new() { AutoSize = true, Margin = new Padding(12, 8, 3, 3), ForeColor = SystemColors.GrayText };
	private readonly Label _watchService = new() { AutoSize = true, Dock = DockStyle.Top, ForeColor = SystemColors.GrayText, Padding = new Padding(0, 2, 0, 6),
		MaximumSize = new Size(640, 0) };

	private TabPage WatchTab()
	{
		var page = new TabPage("Watch") { Padding = new Padding(10) };
		var row = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true };
		var add = new Button { Text = "&Add...", AutoSize = true };
		add.Click += (_, _) => AddWatched("");
		var rename = new Button { Text = "Re&name...", AutoSize = true };
		rename.Click += (_, _) => RenameWatched();
		var remove = new Button { Text = "&Remove", AutoSize = true };
		remove.Click += (_, _) => RemoveWatched();
		var now = new Button { Text = "Check &now", AutoSize = true };
		now.Click += async (_, _) => await _watcher.CheckNowAsync();
		row.Controls.AddRange(new Control[] { add, rename, remove, now, _watchState });
		_watchList.Dock = DockStyle.Top;
		_watchList.Height = 130;
		_watchList.DoubleClick += (_, _) => RenameWatched();

		var settings = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true, Padding = new Padding(0, 6, 0, 0) };
		var minutes = Setting(settings, "Check every", 2, 60, _prefs.WatchMinutes, "min", v => _prefs.WatchMinutes = v);
		Setting(settings, "BTC confirmations", 1, 6, _prefs.BtcConfirmations, "", v => _prefs.BtcConfirmations = v);
		Setting(settings, "Tron", 1, 19, _prefs.TronConfirmations, "", v => _prefs.TronConfirmations = v);
		Setting(settings, "Dust below", 0, 1000, (int)_prefs.DustUsd, "USD", v => _prefs.DustUsd = v);
		minutes.ValueChanged += (_, _) => _watcher.Reschedule();
		var tray = new CheckBox { Text = "Keep watching in the &tray after SoftCalc is closed", AutoSize = true, Checked = _prefs.KeepInTray,
			Margin = new Padding(3, 6, 3, 3) };
		tray.CheckedChanged += (_, _) => _prefs.KeepInTray = tray.Checked;
		settings.Controls.Add(tray);

		var history = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 8, 0, 0) };
		var clear = new Button { Text = "C&lear", AutoSize = true };
		clear.Click += (_, _) => _watcher.ClearEvents();
		history.Controls.Add(new Label { Text = "What happened (double-click to copy the transaction id)", AutoSize = true, Margin = new Padding(0, 8, 8, 3) });
		history.Controls.Add(clear);
		_eventList.DoubleClick += (_, _) =>
		{
			if (_eventList.SelectedItems.Count == 1 && _eventList.SelectedItems[0].Tag is string id && id.Length > 0)
			{
				Clipboard.SetText(id);
			}
		};

		page.Controls.Add(_eventList);
		page.Controls.Add(history);
		page.Controls.Add(settings);
		page.Controls.Add(_watchList);
		page.Controls.Add(_watchService);
		page.Controls.Add(row);
		_watcher.Changed += OnWatchChanged;
		FormClosed += (_, _) => _watcher.Changed -= OnWatchChanged;
		ShowWatch();
		return page;
	}

	private NumericUpDown Setting(FlowLayoutPanel panel, string caption, int min, int max, int value, string unit, Action<int> set)
	{
		var box = new NumericUpDown { Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max), Width = 60 };
		box.ValueChanged += (_, _) => set((int)box.Value);
		panel.Controls.Add(new Label { Text = caption, AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
		panel.Controls.Add(box);
		if (unit.Length > 0)
		{
			panel.Controls.Add(new Label { Text = unit, AutoSize = true, Margin = new Padding(0, 7, 12, 3) });
		}
		return box;
	}

	private void OnWatchChanged(object? sender, EventArgs e)
	{
		ShowWatch();
	}

	private void ShowWatch()
	{
		var selected = _watchList.SelectedItems.Count == 1 ? _watchList.SelectedItems[0].Tag : null;
		_watchList.BeginUpdate();
		_watchList.Items.Clear();
		foreach (var entry in _watcher.Entries)
		{
			var item = new ListViewItem(entry.Label) { Tag = entry, Selected = ReferenceEquals(entry, selected) };
			item.SubItems.Add(CryptoWatch.Short(entry.Address));
			item.SubItems.Add(string.Join(", ", entry.Balances.Select(b => $"{Amount(b.Value)} {b.Key.Split(' ')[0]}")));
			item.SubItems.Add(entry.ChangedUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "");
			item.SubItems.Add(entry.Waiting ? "Waiting for confirmations" : entry.Status);
			if (entry.Status.StartsWith("Could not", StringComparison.Ordinal))
			{
				item.ForeColor = Theme.Error;
			}
			_watchList.Items.Add(item);
		}
		_watchList.EndUpdate();

		_eventList.BeginUpdate();
		_eventList.Items.Clear();
		foreach (var e in _watcher.Events)
		{
			var item = new ListViewItem(e.TimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)) { Tag = e.TxId };
			item.SubItems.Add(e.Label);
			item.SubItems.Add(e.Text);
			if (e.Kind == WatchKind.Suspicious)
			{
				item.ForeColor = Theme.Error;
			}
			_eventList.Items.Add(item);
		}
		_eventList.EndUpdate();

		_watchState.Text = _watcher.Busy ? "Checking..."
			: _watcher.NextUtc is DateTime next ? $"Next check {next.ToLocalTime():HH:mm}" : "";
		var hosts = _watcher.Entries
			.Select(e => CryptoBalance.Service(CryptoAddress.Check(e.Address), Endpoints))
			.Distinct()
			.ToList();
		_watchService.Text = hosts.Count == 0
			? "Add an address to watch: SoftCalc then checks it every few minutes while it runs and tells you of payments in and out."
			: $"Every check asks {string.Join(", ", hosts)}: they see these addresses together with your IP address."
				+ (_watcher.LoadWarning != null ? " " + _watcher.LoadWarning : "");
	}

	//==================================================================
	//  Add, rename, remove
	//==================================================================
	private void AddWatched(string address)
	{
		_tabs.SelectedIndex = Array.IndexOf(TabIds, "watch");
		string label = "";
		while (AskWatched(ref label, ref address, editAddress: true))
		{
			string? error = _watcher.Add(label, address);
			if (error == null)
			{
				return;
			}
			if (CryptoAddress.Check(address).Secret)
			{
				address = "";    // a secret is never kept, not even in the dialog
			}
			MessageBox.Show(this, error, "Watch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
		}
	}

	private void RenameWatched()
	{
		if (_watchList.SelectedItems.Count != 1 || _watchList.SelectedItems[0].Tag is not WatchEntry entry)
		{
			return;
		}
		string label = entry.Label, address = entry.Address;
		if (AskWatched(ref label, ref address, editAddress: false))
		{
			_watcher.Rename(entry, label);
		}
	}

	private void RemoveWatched()
	{
		if (_watchList.SelectedItems.Count != 1 || _watchList.SelectedItems[0].Tag is not WatchEntry entry)
		{
			return;
		}
		if (MessageBox.Show(this, $"Stop watching \"{entry.Label}\"?", "Watch", MessageBoxButtons.OKCancel, MessageBoxIcon.Question)
			== DialogResult.OK)
		{
			_watcher.Remove(entry);
		}
	}

	private bool AskWatched(ref string label, ref string address, bool editAddress)
	{
		using var dialog = new Form
		{
			Text = editAddress ? "Watch an address" : "Rename", Icon = SvgIcon.ProgramIcon(), ShowInTaskbar = false, MinimizeBox = false,
			MaximizeBox = false, StartPosition = FormStartPosition.CenterParent, AutoScaleMode = AutoScaleMode.Dpi, Padding = new Padding(10),
			FormBorderStyle = FormBorderStyle.Sizable,
		};
		var table = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill };
		table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
		table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
		var labelBox = new TextBox { Text = label, Width = 380, Anchor = AnchorStyles.Left | AnchorStyles.Right, PlaceholderText = "e.g. Binance BTC deposit" };
		var addressBox = new TextBox { Text = address, Width = 380, Anchor = AnchorStyles.Left | AnchorStyles.Right, ReadOnly = !editAddress,
			Font = new Font("Consolas", 10f), PlaceholderText = "bc1q...   0x...   T..." };
		table.Controls.Add(new Label { Text = "&Label", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
		table.Controls.Add(labelBox, 1, 0);
		table.Controls.Add(new Label { Text = "A&ddress", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
		table.Controls.Add(addressBox, 1, 1);
		var buttons = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Right, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
		var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(80, 0) };
		var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(80, 0) };
		buttons.Controls.AddRange(new Control[] { cancel, ok });
		table.Controls.Add(buttons, 0, 2);
		table.SetColumnSpan(buttons, 2);
		dialog.Controls.Add(table);
		dialog.AcceptButton = ok;
		dialog.CancelButton = cancel;
		dialog.Load += (_, _) =>
		{
			var want = table.GetPreferredSize(Size.Empty);
			dialog.ClientSize = new Size(want.Width + dialog.Padding.Horizontal, want.Height + dialog.Padding.Vertical);
			dialog.MinimumSize = dialog.Size;
			dialog.MaximumSize = new Size(int.MaxValue, dialog.Height);
			(editAddress && addressBox.Text.Length == 0 ? addressBox : labelBox).Select();
		};
		bool done = dialog.ShowDialog(this) == DialogResult.OK;
		(label, address) = (labelBox.Text, addressBox.Text.Trim());
		addressBox.Clear();
		return done;
	}
}
