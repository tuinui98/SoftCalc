// CryptoWatcher.cs - Checks the watched addresses on a timer and tells of changes from the tray (design/crypto_plan.md C2a)
//
// Owner 2026-11-16: every 10 minutes by default (2-60), every minute while
// an incoming payment waits for its confirmations; one address at a time,
// a second apart. Changes show as a notification from SoftCalc's tray
// icon (a toast on Windows 10/11, kept in the notification centre), with
// Windows' own sound. The icon is there while addresses are watched.
// Watching runs while SoftCalc is open, or, with "keep watching in the
// tray", after its window is closed too.

using System.Text.Json;
using SoftCalc.Engine;
using SoftCalc.Logic;
using SoftCalc.Persistence;

namespace SoftCalc.UI;

internal sealed class CryptoWatcher : IDisposable
{
	private static readonly TimeSpan FirstLook = TimeSpan.FromSeconds(15);
	private static readonly TimeSpan WhileWaiting = TimeSpan.FromMinutes(1);
	private static readonly TimeSpan BetweenAddresses = TimeSpan.FromSeconds(1);
	private readonly CryptoWatchStore _store;
	private readonly CryptoPrefs _prefs;
	private readonly Func<Task<CoinPrices?>> _prices;
	private readonly NotifyIcon _tray = new();
	private readonly System.Windows.Forms.Timer _timer = new();
	private CancellationTokenSource? _round;
	private bool _toldAboutTray;
	private bool _disposed;

	public event EventHandler? Changed;

	public Action? OpenMain { get; set; }
	public Action? OpenWatch { get; set; }
	public Action? ExitApp { get; set; }
	public DateTime? NextUtc { get; private set; }
	public bool Busy => _round != null;
	public bool Active => _store.File.Entries.Count > 0;
	public IReadOnlyList<WatchEntry> Entries => _store.File.Entries;
	public IReadOnlyList<WatchEvent> Events => _store.File.Events;
	public string? LoadWarning => _store.Warning;

	public CryptoWatcher(DataFolder? folder, CryptoPrefs prefs, Func<Task<CoinPrices?>> prices)
	{
		_store = new CryptoWatchStore(folder);
		_prefs = prefs;
		_prices = prices;
		_tray.Icon = SvgIcon.ProgramIcon();
		var menu = new ContextMenuStrip();
		menu.Items.Add("&Open SoftCalc", null, (_, _) => OpenMain?.Invoke());
		menu.Items.Add("Crypto &watch...", null, (_, _) => OpenWatch?.Invoke());
		menu.Items.Add("&Check now", null, async (_, _) => await CheckNowAsync());
		menu.Items.Add(new ToolStripSeparator());
		menu.Items.Add("E&xit SoftCalc", null, (_, _) => ExitApp?.Invoke());
		_tray.ContextMenuStrip = menu;
		_tray.DoubleClick += (_, _) => OpenMain?.Invoke();
		_tray.BalloonTipClicked += (_, _) => OpenWatch?.Invoke();
		_timer.Tick += async (_, _) => await CheckNowAsync();
		ShowTray(false);
		if (Active)
		{
			Schedule(FirstLook);
		}
	}

	//==================================================================
	//  The list
	//==================================================================
	public string? Add(string label, string address)
	{
		var check = CryptoAddress.Check(address);
		if (check.Secret)
		{
			return check.Error;
		}
		if (!check.Valid || check.Normal == null)
		{
			return check.Error ?? "Not a valid address.";
		}
		if (CryptoWatchStore.IsWallet(check))
		{
			return "An xpub / ypub / zpub is checked in the Balance tab; watching a whole wallet comes later (with your own node).";
		}
		if (_store.File.Entries.Any(e => string.Equals(e.Address, check.Normal, StringComparison.OrdinalIgnoreCase)))
		{
			return "This address is watched already.";
		}
		if (_store.File.Entries.Count >= CryptoWatchStore.MaxEntries)
		{
			return $"At most {CryptoWatchStore.MaxEntries} addresses are watched.";
		}
		string name = label.Trim().Length > 0 ? label.Trim() : $"{check.Network} {CryptoWatch.Short(check.Normal)}";
		_store.File.Entries.Add(new WatchEntry { Label = name, Address = check.Normal, Status = "New: looked at in a moment" });
		Saved();
		Schedule(TimeSpan.FromSeconds(2));
		return null;
	}

	public void Rename(WatchEntry entry, string label)
	{
		if (label.Trim().Length > 0)
		{
			entry.Label = label.Trim();
			Saved();
		}
	}

	public void Remove(WatchEntry entry)
	{
		_store.File.Entries.Remove(entry);
		Saved();
		if (!Active)
		{
			_timer.Stop();
			NextUtc = null;
		}
	}

	public void ClearEvents()
	{
		_store.File.Events.Clear();
		Saved();
	}

	// The settings changed in the Watch tab
	public void Reschedule()
	{
		if (Active && !Busy)
		{
			Schedule(TimeSpan.FromMinutes(_prefs.WatchMinutes));
		}
	}

	private void Saved()
	{
		_store.Save();
		ShowTray(false);
		Changed?.Invoke(this, EventArgs.Empty);
	}

	//==================================================================
	//  A round of checks
	//==================================================================
	public async Task CheckNowAsync()
	{
		if (Busy || !Active)
		{
			return;
		}
		_timer.Stop();
		NextUtc = null;
		_round = new CancellationTokenSource();
		var events = new List<WatchEvent>();
		Changed?.Invoke(this, EventArgs.Empty);
		try
		{
			var prices = await _prices();
			var rules = new WatchRules(_prefs.BtcConfirmations, _prefs.TronConfirmations, _prefs.DustUsd);
			var endpoints = new CryptoEndpoints(_prefs.Esplora, _prefs.EthereumRpc, _prefs.TronGrid);
			var known = _store.File.Entries.Select(e => e.Address).ToList();
			foreach (var entry in _store.File.Entries.ToList())
			{
				try
				{
					var report = await CryptoBalance.CheckAsync(CryptoAddress.Check(entry.Address), 0, endpoints, CryptoHttp.FetchAsync, null,
						_round.Token);
					events.AddRange(CryptoWatch.Update(entry, report, rules, asset => Usd(prices, asset), known, DateTime.UtcNow));
				}
				catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or JsonException or FormatException
					or OverflowException or InvalidOperationException or KeyNotFoundException)
				{
					entry.Status = "Could not check: " + ex.Message;
				}
				Changed?.Invoke(this, EventArgs.Empty);
				await Task.Delay(BetweenAddresses, _round.Token);
			}
		}
		catch (OperationCanceledException)
		{
			// SoftCalc is closing
		}
		finally
		{
			_round.Dispose();
			_round = null;
		}
		if (_disposed)
		{
			return;
		}
		_store.AddEvents(events);
		_store.Save();
		Tell(events);
		if (Active)
		{
			Schedule(_store.File.Entries.Any(e => e.Waiting) ? WhileWaiting : TimeSpan.FromMinutes(_prefs.WatchMinutes));
		}
		Changed?.Invoke(this, EventArgs.Empty);
	}

	private static decimal? Usd(CoinPrices? prices, string asset)
	{
		string coin = asset.Split(' ')[0].ToLowerInvariant();    // "USDT (ERC-20)" -> usdt
		return prices?.Price(coin, "usd");
	}

	private void Schedule(TimeSpan wait)
	{
		_timer.Stop();
		_timer.Interval = (int)Math.Max(1000, wait.TotalMilliseconds);
		NextUtc = DateTime.UtcNow + wait;
		_timer.Start();
	}

	//==================================================================
	//  The tray icon and its notifications
	//==================================================================
	private void Tell(IReadOnlyList<WatchEvent> events)
	{
		if (events.Count == 0)
		{
			return;
		}
		ShowTray(false);
		bool warn = events.Any(e => e.Kind == WatchKind.Suspicious);
		string title = events.Count == 1 ? events[0].Label : $"SoftCalc watch: {events.Count} changes";
		string text = string.Join(Environment.NewLine, events.Take(3).Select(e => events.Count == 1 ? e.Text : $"{e.Label}: {e.Text}"));
		if (events.Count > 3)
		{
			text += Environment.NewLine + $"... and {events.Count - 3} more (Tools > Crypto > Watch).";
		}
		_tray.ShowBalloonTip(10_000, title.Length > 60 ? title[..60] : title, text.Length > 250 ? text[..250] : text,
			warn ? ToolTipIcon.Warning : ToolTipIcon.Info);
	}

	// Shown while addresses are watched, or while SoftCalc's window is closed to the tray
	public void ShowTray(bool hidden)
	{
		_tray.Text = Active ? $"SoftCalc: watching {Entries.Count} address{(Entries.Count == 1 ? "" : "es")}" : "SoftCalc";
		_tray.Visible = Active || hidden;
		if (hidden && !_toldAboutTray)
		{
			_toldAboutTray = true;
			_tray.ShowBalloonTip(5_000, "SoftCalc keeps watching", "Double-click the icon to open SoftCalc; right-click to exit.",
				ToolTipIcon.Info);
		}
	}

	public void Dispose()
	{
		_disposed = true;
		_round?.Cancel();
		_timer.Dispose();
		_tray.Visible = false;
		_tray.Dispose();
	}
}
