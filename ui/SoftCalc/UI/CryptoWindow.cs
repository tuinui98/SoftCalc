// CryptoWindow.cs - Tools > Crypto: address checks, balances, watched addresses and fee sums (design/crypto_plan.md C0-C2a)
//
// Owner 2026-11-15: SoftCalc only checks addresses and (later, C1-C2)
// balances: no seed, no private key, no signing. The Address tab checks
// a Bitcoin, Ethereum or Tron address as you type and refuses text that
// looks like a secret (the box is emptied at once). The Fees tab works
// out a Bitcoin or Ethereum fee and can put the line in Input. Those two
// are offline; the Balance tab (CryptoWindow.Balance.cs) asks a service
// only when Check is pressed; the Watch tab (CryptoWindow.Watch.cs) shows
// the addresses CryptoWatcher checks on a timer.

using System.Globalization;
using SoftCalc.Engine;
using SoftCalc.Logic;
using SoftCalc.Persistence;

namespace SoftCalc.UI;

internal sealed partial class CryptoWindow : Form
{
	private static readonly string[] TabIds = { "address", "balance", "watch", "fees" };
	private readonly CryptoPrefs _prefs;
	private readonly CryptoWatcher _watcher;
	private readonly Action<string> _insert;
	private readonly Func<Task<CoinPrices?>> _prices;
	private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
	private readonly TextBox _address = new() { Dock = DockStyle.Top, Font = new Font("Consolas", 11f) };
	private readonly Label _verdict = new() { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 10, 0, 6) };
	private readonly Label _details = new() { AutoSize = true, Dock = DockStyle.Top, MaximumSize = new Size(560, 0) };
	private readonly Label _warnings = new() { AutoSize = true, Dock = DockStyle.Top, MaximumSize = new Size(560, 0), Padding = new Padding(0, 6, 0, 0) };
	private readonly TextBox _normal = new() { ReadOnly = true, Width = 440, Font = new Font("Consolas", 10f) };
	private readonly FlowLayoutPanel _normalRow = new() { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 8, 0, 0), Visible = false };
	private readonly NumericUpDown _vbytes = Number(1, 1_000_000, 0, 141);
	private readonly NumericUpDown _satPerVb = Number(0.1m, 100_000, 1, 10);
	private readonly NumericUpDown _gas = Number(1, 30_000_000, 0, 21_000);
	private readonly NumericUpDown _gwei = Number(0.001m, 100_000, 3, 1);
	private readonly Label _btcFee = new() { AutoSize = true, Margin = new Padding(3, 8, 3, 3) };
	private readonly Label _ethFee = new() { AutoSize = true, Margin = new Padding(3, 8, 3, 3) };

	public CryptoWindow(CryptoPrefs prefs, CryptoWatcher watcher, Action<string> insert, Func<Task<CoinPrices?>>? prices = null)
	{
		_prefs = prefs;
		_watcher = watcher;
		_insert = insert;
		_prices = prices ?? (() => Task.FromResult<CoinPrices?>(null));
		Text = "Crypto";
		Icon = SvgIcon.ProgramIcon();
		ShowInTaskbar = false;
		MinimizeBox = false;
		AutoScaleMode = AutoScaleMode.Dpi;
		Size = new Size(720, 620);
		MinimumSize = new Size(520, 380);
		StartPosition = FormStartPosition.CenterParent;
		if (prefs.Window is { HasPosition: true } w)
		{
			StartPosition = FormStartPosition.Manual;
			Bounds = WindowPlacement.FitOnScreen(new Rectangle(w.X, w.Y, w.Width, w.Height),
				Screen.AllScreens.Select(s => s.WorkingArea).ToList());
		}
		_tabs.TabPages.Add(AddressTab());
		_tabs.TabPages.Add(BalanceTab());
		_tabs.TabPages.Add(WatchTab());
		_tabs.TabPages.Add(FeesTab());
		_tabs.SelectedIndex = Math.Max(0, Array.IndexOf(TabIds, prefs.Tab));
		Controls.Add(_tabs);
		var note = new Label
		{
			Dock = DockStyle.Bottom, AutoSize = true, MaximumSize = new Size(660, 0), ForeColor = SystemColors.GrayText,
			Padding = new Padding(8, 4, 8, 6),
			Text = "Address and Fees are offline; Balance asks a service only when you press Check, Watch every few minutes. "
				+ "SoftCalc never asks for seed words or private keys.",
		};
		Controls.Add(note);
		FormClosing += (_, _) => SavePrefs();
		FormClosed += (_, _) =>
		{
			_checking?.Cancel();
			_address.Clear();
			_normal.Clear();
			_balanceInput.Clear();
		};
		ShowCheck();
		ShowFees();
	}

	//==================================================================
	//  Address
	//==================================================================
	private TabPage AddressTab()
	{
		var page = new TabPage("Address") { Padding = new Padding(10) };
		var caption = new Label
		{
			Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 0, 0, 4),
			Text = "&Address (Bitcoin, Ethereum or Tron); it is checked as you type:",
		};
		_address.PlaceholderText = "bc1q...   0x...   T...";
		_address.TextChanged += (_, _) => ShowCheck();
		_verdict.Font = new Font(Font.FontFamily, 11f, FontStyle.Bold);
		var copy = new Button { Text = "&Copy", AutoSize = true };
		copy.Click += (_, _) =>
		{
			if (_normal.Text.Length > 0)
			{
				Clipboard.SetText(_normal.Text);
			}
		};
		_normalRow.Controls.AddRange(new Control[] { new Label { Text = "As wallets write it", AutoSize = true, Margin = new Padding(3, 7, 6, 3) },
			_normal, copy });
		page.Controls.Add(_warnings);
		page.Controls.Add(_normalRow);
		page.Controls.Add(_details);
		page.Controls.Add(_verdict);
		page.Controls.Add(_address);
		page.Controls.Add(caption);
		return page;
	}

	private void ShowCheck()
	{
		if (_address.Text.Trim().Length == 0)
		{
			_verdict.Text = "";
			_details.Text = "";
			_warnings.Text = "";
			_normalRow.Visible = false;
			return;
		}
		var check = CryptoAddress.Check(_address.Text);
		if (check.Secret)
		{
			_address.Clear();    // a seed or key is never left on screen; Clear calls ShowCheck again with nothing
			_verdict.ForeColor = Theme.Error;
			_verdict.Text = "Not kept";
			_details.Text = check.Error;
			return;
		}
		_verdict.ForeColor = check.Valid ? Theme.Series(2) : Theme.Error;
		_verdict.Text = check.Valid ? $"✔ Valid {check.Network} address" : "✖ Not a valid address";
		_details.Text = check.Valid ? string.Join(Environment.NewLine, new[] { "Type: " + check.Kind }.Concat(check.Facts)) : check.Error;
		_warnings.ForeColor = Theme.Error;
		_warnings.Text = string.Join(Environment.NewLine, check.Warnings.Select(w => "! " + w));
		_normal.Text = check.Normal ?? "";
		_normalRow.Visible = check.Valid && check.Normal != null;
	}

	//==================================================================
	//  Fees
	//==================================================================
	private TabPage FeesTab()
	{
		var page = new TabPage("Fees") { Padding = new Padding(10) };
		var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 5 };
		var insertBtc = new Button { Text = "Insert", AutoSize = true };
		insertBtc.Click += (_, _) => _insert($"{Plain(_vbytes)}*{Plain(_satPerVb)} [sat->btc]");
		var insertEth = new Button { Text = "Insert", AutoSize = true };
		insertEth.Click += (_, _) => _insert($"{Plain(_gas)}*{Plain(_gwei)} [gwei->eth]");
		Row(table, 0, "&Bitcoin: size (vB)", _vbytes, "sat/vB", _satPerVb);
		table.Controls.Add(_btcFee, 1, 1);
		table.SetColumnSpan(_btcFee, 3);
		table.Controls.Add(insertBtc, 4, 1);
		Row(table, 2, "&Ethereum: gas", _gas, "gwei", _gwei);
		table.Controls.Add(_ethFee, 1, 3);
		table.SetColumnSpan(_ethFee, 3);
		table.Controls.Add(insertEth, 4, 3);
		foreach (var box in new[] { _vbytes, _satPerVb, _gas, _gwei })
		{
			box.ValueChanged += (_, _) => ShowFees();
		}
		var notes = new Label
		{
			Dock = DockStyle.Top, AutoSize = true, MaximumSize = new Size(560, 0), ForeColor = SystemColors.GrayText,
			Padding = new Padding(0, 12, 0, 0),
			Text = "Bitcoin: a payment from one SegWit input to two outputs is about 141 vB. "
				+ "Ethereum: sending ETH takes 21,000 gas; sending a token such as USDT about 65,000. "
				+ "Tron fees depend on energy and bandwidth and are not worked out here. "
				+ "Insert puts the sum in Input, such as 141*10 [sat->btc].",
		};
		page.Controls.Add(notes);
		page.Controls.Add(table);
		return page;
	}

	private static void Row(TableLayoutPanel table, int row, string first, Control a, string second, Control b)
	{
		table.Controls.Add(new Label { Text = first, AutoSize = true, Margin = new Padding(3, 7, 6, 3) }, 0, row);
		table.Controls.Add(a, 1, row);
		table.Controls.Add(new Label { Text = second, AutoSize = true, Margin = new Padding(3, 7, 6, 3) }, 2, row);
		table.Controls.Add(b, 3, row);
	}

	private void ShowFees()
	{
		decimal sats = Math.Ceiling(_vbytes.Value * _satPerVb.Value);
		_btcFee.Text = $"= {sats:#,0} sat = {sats / 100_000_000m:0.########} BTC";
		decimal eth = _gas.Value * _gwei.Value / 1_000_000_000m;
		_ethFee.Text = $"= {_gas.Value * _gwei.Value:#,0.###} gwei = {eth:0.##################} ETH";
	}

	private static NumericUpDown Number(decimal min, decimal max, int places, decimal value)
	{
		return new NumericUpDown { Minimum = min, Maximum = max, DecimalPlaces = places, Value = value, Width = 110, ThousandsSeparator = true };
	}

	// A box's value as SoftCalc reads it: a point, no thousands separator, no trailing zeros
	private static string Plain(NumericUpDown box)
	{
		return box.Value.ToString("0.############", CultureInfo.InvariantCulture);
	}

	public void ShowTab(string id)
	{
		_tabs.SelectedIndex = Math.Max(0, Array.IndexOf(TabIds, id));
	}

	private void SavePrefs()
	{
		_prefs.Tab = TabIds[Math.Clamp(_tabs.SelectedIndex, 0, TabIds.Length - 1)];
		var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
		_prefs.Window = new WindowPlacement { X = bounds.X, Y = bounds.Y, Width = bounds.Width, Height = bounds.Height };
	}
}
