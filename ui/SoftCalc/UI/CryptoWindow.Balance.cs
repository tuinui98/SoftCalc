// CryptoWindow.Balance.cs - The Balance tab: balances and recent payments on demand (design/crypto_plan.md C1)
//
// Owner 2026-11-15: nothing is sent until Check is pressed; the line
// under the box names the service that will see the address. Values in
// baht and dollars come from CoinGecko prices (the same as `[btc->thb]`
// in lines). Services can be pointed at one's own node.

using System.Globalization;
using SoftCalc.Engine;
using SoftCalc.Logic;
using SoftCalc.Persistence;

namespace SoftCalc.UI;

internal sealed partial class CryptoWindow
{
	private static readonly HttpClient Http = MakeHttp();
	private readonly TextBox _balanceInput = new() { Font = new Font("Consolas", 10.5f), Width = 380 };
	private readonly ComboBox _xpubType = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170, Visible = false };
	private readonly Button _check = new() { Text = "Chec&k", AutoSize = true };
	private readonly Button _stop = new() { Text = "Stop", AutoSize = true, Enabled = false };
	private readonly Label _service = new() { AutoSize = true, Dock = DockStyle.Top, ForeColor = SystemColors.GrayText, Padding = new Padding(0, 2, 0, 6) };
	private readonly ListView _assets = Table(("Asset", 120), ("Balance", 130), ("Pending", 100), ("THB", 110), ("USD", 100));
	private readonly ListView _payments = Table(("Date", 130), ("Asset", 60), ("Amount", 120), ("Status", 120), ("Transaction", 200));
	private readonly Label _balanceNote = new() { AutoSize = true, Dock = DockStyle.Bottom, MaximumSize = new Size(600, 0), Padding = new Padding(0, 4, 0, 4) };
	private CancellationTokenSource? _checking;

	private TabPage BalanceTab()
	{
		var page = new TabPage("Balance") { Padding = new Padding(10) };
		var row = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true };
		_balanceInput.PlaceholderText = "address, or a Bitcoin xpub / zpub";
		_xpubType.Items.AddRange(CryptoBalance.XpubTypes);
		_xpubType.SelectedIndex = 0;
		var services = new Button { Text = "&Services...", AutoSize = true };
		services.Click += (_, _) => EditServices();
		row.Controls.AddRange(new Control[] { _balanceInput, _xpubType, _check, _stop, services });
		_balanceInput.TextChanged += (_, _) => ShowService();
		_check.Click += async (_, _) => await CheckBalanceAsync();
		_stop.Click += (_, _) => _checking?.Cancel();
		_balanceInput.KeyDown += async (_, e) =>
		{
			if (e.KeyCode == Keys.Enter)
			{
				e.SuppressKeyPress = true;
				await CheckBalanceAsync();
			}
		};
		_payments.DoubleClick += (_, _) =>
		{
			if (_payments.SelectedItems.Count == 1 && _payments.SelectedItems[0].Tag is string id && id.Length > 0)
			{
				Clipboard.SetText(id);
				Say("Transaction id copied.", error: false);
			}
		};
		_assets.Dock = DockStyle.Top;
		_assets.Height = 110;
		var history = new Label { Text = "Recent payments (double-click to copy the transaction id)", AutoSize = true, Dock = DockStyle.Top,
			Padding = new Padding(0, 8, 0, 2) };
		page.Controls.Add(_payments);
		page.Controls.Add(history);
		page.Controls.Add(_assets);
		page.Controls.Add(_service);
		page.Controls.Add(row);
		page.Controls.Add(_balanceNote);
		ShowService();
		return page;
	}

	private CryptoEndpoints Endpoints => new(_prefs.Esplora, _prefs.EthereumRpc, _prefs.TronGrid);

	private void ShowService()
	{
		string text = _balanceInput.Text.Trim();
		var check = text.Length == 0 ? null : CryptoAddress.Check(text);
		if (check?.Secret == true)
		{
			_balanceInput.Clear();
			Say(check.Error ?? "", error: true);
			return;
		}
		_xpubType.Visible = check?.Kind?.Contains("extended public key") == true;
		_service.Text = check is { Valid: true }
			? $"Check asks {CryptoBalance.Service(check, Endpoints)}: it will see this {(_xpubType.Visible ? "wallet's addresses" : "address")} and your IP address."
			: "Nothing is sent until you press Check.";
	}

	private async Task CheckBalanceAsync()
	{
		var check = CryptoAddress.Check(_balanceInput.Text);
		if (!check.Valid)
		{
			Say(check.Error ?? "Not a valid address.", error: true);
			return;
		}
		if (check.Network == "Bitcoin" && check.Facts.Any(f => f.Contains("testnet") || f.Contains("regtest")))
		{
			Say("A test-network address: point Services at a testnet Esplora API (e.g. https://mempool.space/testnet/api) first.",
				error: true);
			if (!_prefs.Esplora.Contains("test", StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
		}
		_checking = new CancellationTokenSource();
		(_check.Enabled, _stop.Enabled) = (false, true);
		_assets.Items.Clear();
		_payments.Items.Clear();
		Say("Asking " + CryptoBalance.Service(check, Endpoints) + "...", error: false);
		try
		{
			var progress = new Progress<string>(text => Say(text, error: false));
			var report = await CryptoBalance.CheckAsync(check, _xpubType.SelectedIndex, Endpoints, FetchAsync, progress, _checking.Token);
			var prices = await _prices();
			ShowReport(report, prices);
		}
		catch (OperationCanceledException)
		{
			Say("Stopped.", error: false);
		}
		catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or System.Text.Json.JsonException or FormatException
			or OverflowException or InvalidOperationException or KeyNotFoundException)
		{
			Say("Could not check: " + ex.Message, error: true);
		}
		finally
		{
			(_check.Enabled, _stop.Enabled) = (true, false);
			_checking.Dispose();
			_checking = null;
		}
	}

	private void ShowReport(BalanceReport report, CoinPrices? prices)
	{
		_assets.BeginUpdate();
		_assets.Items.Clear();
		foreach (var asset in report.Assets)
		{
			var item = new ListViewItem(asset.Asset);
			item.SubItems.Add(Amount(asset.Amount));
			item.SubItems.Add(asset.Pending == 0 ? "" : Amount(asset.Pending));
			foreach (string currency in new[] { "thb", "usd" })
			{
				decimal? price = prices?.Price(asset.Coin, currency);
				item.SubItems.Add(price is decimal p ? (asset.Amount * p).ToString("#,0.00", CultureInfo.InvariantCulture) : "");
			}
			_assets.Items.Add(item);
		}
		_assets.EndUpdate();
		_payments.BeginUpdate();
		_payments.Items.Clear();
		foreach (var payment in report.Payments)
		{
			var item = new ListViewItem(payment.TimeUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "(waiting)")
			{
				Tag = payment.Id,
			};
			item.SubItems.Add(payment.Asset);
			item.SubItems.Add((payment.Amount > 0 ? "+" : "") + Amount(payment.Amount));
			item.SubItems.Add(payment.Status);
			item.SubItems.Add(payment.Id.Length > 20 ? payment.Id[..10] + "…" + payment.Id[^8..] : payment.Id);
			_payments.Items.Add(item);
		}
		_payments.EndUpdate();
		string priced = prices == null ? "No prices (CoinGecko could not be reached)."
			: $"Prices: CoinGecko {prices.FetchedUtc.ToLocalTime():HH:mm}.";
		Say(string.Join(" ", report.Notes.Append(priced)), error: false);
	}

	private static string Amount(decimal value)
	{
		return value.ToString("#,0.########", CultureInfo.InvariantCulture);
	}

	private void Say(string text, bool error)
	{
		_balanceNote.ForeColor = error ? Theme.Error : SystemColors.GrayText;
		_balanceNote.Text = text;
	}

	//==================================================================
	//  Services (own node) and the network
	//==================================================================
	private void EditServices()
	{
		using var dialog = new Form
		{
			Text = "Services", Icon = SvgIcon.ProgramIcon(), ShowInTaskbar = false, MinimizeBox = false, MaximizeBox = false,
			StartPosition = FormStartPosition.CenterParent, AutoScaleMode = AutoScaleMode.Dpi, Padding = new Padding(10),
			FormBorderStyle = FormBorderStyle.Sizable, SizeGripStyle = SizeGripStyle.Show,
		};
		// One table fills the dialog: the address boxes stretch with its width, the note wraps to it
		var table = new TableLayoutPanel { ColumnCount = 2, RowCount = 5, Dock = DockStyle.Fill };
		table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
		table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
		var boxes = new[] { new TextBox { Width = 360, Text = _prefs.Esplora }, new TextBox { Width = 360, Text = _prefs.EthereumRpc },
			new TextBox { Width = 360, Text = _prefs.TronGrid } };
		string[] labels = { "Bitcoin (Esplora API)", "Ethereum (JSON-RPC)", "Tron (TronGrid API)" };
		for (int i = 0; i < 3; i++)
		{
			table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			table.Controls.Add(new Label { Text = labels[i], AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 3, 8, 3) }, 0, i);
			boxes[i].Anchor = AnchorStyles.Left | AnchorStyles.Right;
			table.Controls.Add(boxes[i], 1, i);
		}
		var note = new Label
		{
			AutoSize = true, ForeColor = SystemColors.GrayText, Anchor = AnchorStyles.Left | AnchorStyles.Right, Padding = new Padding(0, 6, 0, 6),
			Text = "A public service sees every address you check. With your own node (e.g. Umbrel with the mempool app: "
				+ "http://umbrel.local:3006/api) nobody else does.",
		};
		var buttons = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Right, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
		var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(80, 0) };
		var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(80, 0) };
		var reset = new Button { Text = "&Defaults", AutoSize = true };
		reset.Click += (_, _) =>
		{
			boxes[0].Text = CryptoEndpoints.Default.Esplora;
			boxes[1].Text = CryptoEndpoints.Default.EthereumRpc;
			boxes[2].Text = CryptoEndpoints.Default.TronGrid;
		};
		buttons.Controls.AddRange(new Control[] { cancel, ok, reset });
		table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		table.Controls.Add(note, 0, 3);
		table.SetColumnSpan(note, 2);
		table.Controls.Add(buttons, 0, 4);
		table.SetColumnSpan(buttons, 2);
		dialog.Controls.Add(table);
		// Start at the size the rows want (after DPI scaling), the note wrapped to the boxes' width;
		// the dialog then grows sideways only
		dialog.Load += (_, _) =>
		{
			int labelWidth = table.Controls.OfType<Label>().Where(l => l != note).Max(l => l.PreferredSize.Width + l.Margin.Horizontal);
			int rowWidth = labelWidth + dialog.LogicalToDeviceUnits(360) + boxes[0].Margin.Horizontal;
			note.MaximumSize = new Size(rowWidth, 0);
			var want = table.GetPreferredSize(Size.Empty);
			note.MaximumSize = Size.Empty;
			dialog.ClientSize = new Size(want.Width + dialog.Padding.Horizontal, want.Height + dialog.Padding.Vertical);
			dialog.MinimumSize = dialog.Size;
			dialog.MaximumSize = new Size(int.MaxValue, dialog.Height);
		};
		dialog.AcceptButton = ok;
		dialog.CancelButton = cancel;
		if (dialog.ShowDialog(this) != DialogResult.OK)
		{
			return;
		}
		string[] urls = boxes.Select(b => b.Text.Trim().TrimEnd('/')).ToArray();
		if (urls.Any(u => !Uri.TryCreate(u, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
		{
			Say("Each service needs a full http:// or https:// address; nothing was changed.", error: true);
			return;
		}
		(_prefs.Esplora, _prefs.EthereumRpc, _prefs.TronGrid) = (urls[0], urls[1], urls[2]);
		ShowService();
	}

	private static async Task<string> FetchAsync(string url, string? jsonBody, CancellationToken cancel)
	{
		using var request = new HttpRequestMessage(jsonBody == null ? HttpMethod.Get : HttpMethod.Post, url);
		if (jsonBody != null)
		{
			request.Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
		}
		using var response = await Http.SendAsync(request, cancel);
		string text = await response.Content.ReadAsStringAsync(cancel);
		if (!response.IsSuccessStatusCode)
		{
			throw new HttpRequestException($"{(int)response.StatusCode} {response.ReasonPhrase} from {new Uri(url).Host}"
				+ (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ? " (too many requests: wait a minute)" : ""));
		}
		return text;
	}

	private static HttpClient MakeHttp()
	{
		var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
		http.DefaultRequestHeaders.UserAgent.ParseAdd("SoftCalc/1.0");
		return http;
	}

	private static ListView Table(params (string Name, int Width)[] columns)
	{
		var list = new ListView
		{
			Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable,
		};
		foreach (var (name, width) in columns)
		{
			list.Columns.Add(name, width, name is "Asset" or "Date" or "Status" or "Transaction" ? HorizontalAlignment.Left : HorizontalAlignment.Right);
		}
		return list;
	}
}
