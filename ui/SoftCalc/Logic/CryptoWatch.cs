// CryptoWatch.cs - What changed since the last look at a watched address (design/crypto_plan.md C2a)
//
// Owner 2026-11-16: the addresses watched today are exchange deposit
// addresses, so C2a watches single addresses (xpubs wait for C2b, with an
// own node). Each look compares the service's answer with what was seen
// before: a new payment in or out, an incoming payment reaching the
// confirmations asked for, a balance that moved (Ethereum: no history),
// and payments that look like address poisoning (0 or below the dust
// value, or from an address that copies the first and last characters of
// one already known). The first look only learns what is there.

using System.Text.Json.Serialization;

namespace SoftCalc.Logic;

internal sealed class WatchEntry
{
	public string Label { get; set; } = "";
	public string Address { get; set; } = "";
	public Dictionary<string, decimal> Balances { get; set; } = new();    // asset -> amount (confirmed + pending)
	public Dictionary<string, int> Seen { get; set; } = new();    // transaction id -> confirmations last seen
	public List<string> Counterparties { get; set; } = new();    // addresses that paid or were paid (newest last)
	public DateTime? CheckedUtc { get; set; }
	public DateTime? ChangedUtc { get; set; }
	public string Status { get; set; } = "";
	public bool Waiting { get; set; }    // an incoming payment has fewer confirmations than asked for

	[JsonIgnore]
	public bool Learned => CheckedUtc != null;
}

internal enum WatchKind
{
	Incoming,
	Confirmed,
	Outgoing,
	Changed,
	Suspicious,
}

internal sealed record WatchEvent(DateTime TimeUtc, string Label, WatchKind Kind, string Text, string? TxId);

internal sealed record WatchRules(int BtcConfirmations, int TronConfirmations, decimal DustUsd);

internal static class CryptoWatch
{
	public const int MaxCounterparties = 50;

	// Compare an answer with the entry, update the entry, and say what is new.
	// `known` is every watched address (a look-alike of one is suspicious too).
	public static List<WatchEvent> Update(WatchEntry entry, BalanceReport report, WatchRules rules, Func<string, decimal?> usdPrice,
		IReadOnlyCollection<string> known, DateTime nowUtc)
	{
		var events = new List<WatchEvent>();
		bool learning = !entry.Learned;
		int needed = report.Network switch
		{
			"Bitcoin" => rules.BtcConfirmations,
			"Tron" => rules.TronConfirmations,
			_ => 1,
		};

		// Payments: new ones, confirmations reached, poisoning
		var seen = new Dictionary<string, int>();
		bool waiting = false;
		foreach (var payment in report.Payments.Where(p => p.Id.Length > 0).Reverse())    // oldest first
		{
			int confirmations = payment.Confirmations ?? needed;
			bool incoming = payment.Amount >= 0;
			waiting |= incoming && payment.Amount > 0 && confirmations < needed;
			seen[payment.Id] = confirmations;
			if (learning)
			{
				Remember(entry, payment.Counterparty);
				continue;
			}
			if (entry.Seen.TryGetValue(payment.Id, out int before))
			{
				if (incoming && payment.Amount > 0 && before < needed && confirmations >= needed)
				{
					events.Add(new WatchEvent(nowUtc, entry.Label, WatchKind.Confirmed,
						$"{Signed(payment.Amount)} {payment.Asset} confirmed ({Count(confirmations)}).", payment.Id));
				}
				continue;
			}
			string? warning = Suspicion(payment, usdPrice, rules.DustUsd, entry, known);
			if (warning != null)
			{
				events.Add(new WatchEvent(nowUtc, entry.Label, WatchKind.Suspicious, warning, payment.Id));
				continue;    // a poisoner is not remembered as someone one deals with
			}
			Remember(entry, payment.Counterparty);
			string text = incoming
				? $"{Signed(payment.Amount)} {payment.Asset} arrived ({(confirmations == 0 ? "pending" : Count(confirmations))})."
				: $"{Signed(payment.Amount)} {payment.Asset} sent{(payment.Counterparty != null ? " to " + Short(payment.Counterparty) : "")}.";
			events.Add(new WatchEvent(nowUtc, entry.Label, incoming ? WatchKind.Incoming : WatchKind.Outgoing, text, payment.Id));
		}
		entry.Seen = seen;
		entry.Waiting = waiting;

		// Balances: moves without a payment list (Ethereum)
		var balances = report.Assets.ToDictionary(a => a.Asset, a => a.Amount + a.Pending);
		if (!learning && report.Payments.Count == 0 && report.Network == "Ethereum")
		{
			foreach (var (asset, amount) in balances)
			{
				decimal before = entry.Balances.TryGetValue(asset, out decimal b) ? b : 0;
				if (amount != before)
				{
					events.Add(new WatchEvent(nowUtc, entry.Label, amount > before ? WatchKind.Incoming : WatchKind.Changed,
						$"{asset} balance {Signed(amount - before)} (now {Plain(amount)}).", null));
				}
			}
		}
		if (!balances.SequenceEqual(entry.Balances) || events.Count > 0)
		{
			entry.ChangedUtc = learning ? entry.ChangedUtc : nowUtc;
		}
		entry.Balances = balances;
		entry.CheckedUtc = nowUtc;
		entry.Status = "OK";
		return events;
	}

	// Why a new payment looks like address poisoning, or null
	internal static string? Suspicion(CryptoPayment payment, Func<string, decimal?> usdPrice, decimal dustUsd, WatchEntry entry,
		IReadOnlyCollection<string> known)
	{
		string? party = payment.Counterparty;
		string? twin = party == null ? null : known.Concat(entry.Counterparties).FirstOrDefault(k => LookAlike(k, party));
		if (twin != null)
		{
			return $"{Signed(payment.Amount)} {payment.Asset} {(payment.Amount >= 0 ? "from" : "to")} {Short(party!)}, which imitates "
				+ $"{Short(twin)}: likely address poisoning. Never copy an address from the history.";
		}
		if (payment.Amount == 0)
		{
			return $"A 0 {payment.Asset} transfer {(party != null ? "with " + Short(party) : "")}: likely address poisoning. "
				+ "Never copy an address from the history.";
		}
		decimal? usd = usdPrice(payment.Asset) is decimal price ? Math.Abs(payment.Amount) * price : null;
		if (payment.Amount > 0 && usd < dustUsd)
		{
			return $"{Signed(payment.Amount)} {payment.Asset} (under {dustUsd} USD) from {(party != null ? Short(party) : "someone")}: "
				+ "dust, often sent to trace a wallet or to plant a look-alike address. Leave it unspent.";
		}
		return null;
	}

	// Same first and last 4 characters after the prefix, but not the same address
	internal static bool LookAlike(string a, string b)
	{
		if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		string x = Body(a), y = Body(b);
		return x.Length >= 8 && y.Length >= 8
			&& string.Equals(x[..4], y[..4], StringComparison.OrdinalIgnoreCase)
			&& string.Equals(x[^4..], y[^4..], StringComparison.OrdinalIgnoreCase);
	}

	private static string Body(string address)
	{
		if (address.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
		{
			return address[2..];
		}
		if (address.StartsWith("bc1", StringComparison.OrdinalIgnoreCase) || address.StartsWith("tb1", StringComparison.OrdinalIgnoreCase))
		{
			return address.Length > 4 ? address[4..] : "";
		}
		return address.Length > 1 ? address[1..] : "";
	}

	private static void Remember(WatchEntry entry, string? party)
	{
		if (party == null)
		{
			return;
		}
		entry.Counterparties.Remove(party);
		entry.Counterparties.Add(party);
		if (entry.Counterparties.Count > MaxCounterparties)
		{
			entry.Counterparties.RemoveAt(0);
		}
	}

	public static string Short(string address)
	{
		return address.Length > 16 ? address[..8] + "…" + address[^6..] : address;
	}

	private static string Count(int confirmations)
	{
		return confirmations >= 19 ? "confirmed" : $"{confirmations} confirmation{(confirmations == 1 ? "" : "s")}";
	}

	private static string Signed(decimal amount)
	{
		return (amount > 0 ? "+" : "") + Plain(amount);
	}

	private static string Plain(decimal amount)
	{
		return amount.ToString("#,0.########", System.Globalization.CultureInfo.InvariantCulture);
	}
}
