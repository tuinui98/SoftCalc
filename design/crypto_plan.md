# Crypto watch - plan (owner 2026-11-15; C0 and C1 built 2026-11-15)

Scope (owner 2026-11-15): **check balances and check addresses**. No
seeds, no private keys, no signing in SoftCalc. Networks: **BTC, ETH,
USDT on Ethereum (ERC-20) and on Tron (TRC-20)**; others later. Prices
in baht from **CoinGecko**. Public APIs to start, with endpoints the
user can point at their own node.

Items marked *(check)* are from memory and must be confirmed while
building.

## C0 - offline (no network)

A **Tools > Crypto...** window (name to confirm), tab **Address**:

| input | checks | shows |
|---|---|---|
| `1...`, `3...` | Base58Check, version 0x00 / 0x05 (testnet 0x6F / 0xC4) | Bitcoin P2PKH / P2SH, mainnet or testnet |
| `bc1q...`, `tb1q...` | Bech32 (BIP173), witness v0, 20 or 32 bytes | P2WPKH / P2WSH |
| `bc1p...` | Bech32m (BIP350), witness v1, 32 bytes | Taproot |
| `0x` + 40 hex | EIP-55 mixed-case checksum (Keccak-256, not NIST SHA3-256) | Ethereum; all lower or all upper case: valid but no checksum to check (warn) |
| `T...` | Base58Check, version 0x41, 21 bytes | Tron; also its hex form `41...` |

- Says which networks can receive at the address: an Ethereum address
  takes ETH and USDT-ERC20, a Tron address takes TRX and USDT-TRC20.
  Warns that sending USDT on the wrong network loses it.
- Refuses anything that looks like a secret: 12/15/18/21/24 BIP39
  words, a WIF key (`5...`, `K...`, `L...`), 64 hex digits, an
  `xprv`/`zprv`. It says why and does not keep the text.
- Units in lines (engine unit table): `[sat->btc]`, `[wei->eth]`,
  `[gwei->eth]`, `[sun->trx]`; USDT has 6 decimals on both networks.
- Fee helper: BTC `vbytes x sat/vB`, ETH `gas x gwei`.

Libraries: **NBitcoin** (MIT) for Base58Check, Bech32/Bech32m and xpub
derivation; a Keccak-256 from a tested library (BouncyCastle, MIT
licence, or Nethereum.Util, MIT) *(check sizes; add to
THIRD-PARTY-NOTICES)*. Tests: the BIP173/BIP350 vectors, the EIP-55
examples, Tron examples.

## C1 - balances on demand

Tab **Balance**: an address (or a Bitcoin xpub/zpub, watch-only) and
**Check**. Nothing goes out until Check is pressed; the line under the
button names the service that will see the address.

| network | default endpoint | calls |
|---|---|---|
| Bitcoin | Esplora API `https://mempool.space/api` | `/address/{a}` (chain and mempool stats), `/address/{a}/txs` |
| Ethereum | public JSON-RPC URL *(choose a provider)* | `eth_getBalance`; `eth_call` `balanceOf(addr)` (selector `0x70a08231`) on the USDT contract `0xdAC17F958D2ee523a2206206994597C13D831ec7` *(check)* |
| Tron | TronGrid `https://api.trongrid.io` | `/v1/accounts/{a}` (TRX and TRC-20 balances; USDT contract `TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t` *(check)*), `/v1/accounts/{a}/transactions/trc20` |
| prices | CoinGecko `/simple/price?ids=bitcoin,ethereum,tether,tron&vs_currencies=thb,usd` | baht and dollar value; time of the price shown *(check whether a free demo key is now needed)* |

- xpub/zpub (BIP84 `zpub` for `bc1q`): derive receive and change
  addresses, scan until 20 unused in a row (gap limit), add the
  balances. Public derivation only: no private key exists here.
- History: Bitcoin and Tron from their APIs. Plain Ethereum JSON-RPC
  has no history by address: balance only, or an Etherscan API key in
  settings for a transaction list.
- Pending: Bitcoin mempool totals shown apart from confirmed ones.
- Prices may also feed lines: `0.05 [btc->thb]`, through the engine's
  rates path *(design with the existing ECB rates code)*.

## C2 - monitor

Tab **Watch**: a list of addresses with labels ("cold wallet",
"rent"), saved in `crypto.json` (public data, but it says which
addresses are yours: noted in help). Refresh every N minutes (5 to
start, 1 at least) while the window is open, or on demand.

- Alerts (Windows notification and a line in the list): money in;
  money in reached N confirmations; **money out** (worrying on a cold
  wallet); balance changed; dust (a tiny unexpected amount, often used
  to track); an address used again.
- Totals per network and in baht; a small chart of the value over time
  from what was saved.

## Settings

- Endpoints per network (Esplora URL, Ethereum RPC URL, TronGrid URL),
  so a user with their own node (e.g. Umbrel with the mempool app at
  `http://umbrel.local:3006/api`) points there. Electrum protocol
  (electrs/Fulcrum) later if wanted.
- Optional API keys (TronGrid, Etherscan, CoinGecko) kept with Windows
  DPAPI in the data folder, never in git.

## Privacy and safety

- Network only when asked (Check, or Watch running), like the Entropy
  beacons; each call names the service.
- No field takes a seed or key; pasted secrets are refused (C0).
- Help: what a public API learns, how to use an own node, the wrong
  network warning, that Watch keeps addresses on disk.

## Order

C0 (offline, smallest risk), then C1 for BTC, ETH + USDT-ERC20, Tron +
USDT-TRC20, then C2. Each phase ends with tests, help and the owner
trying it.

## Owner's answers (2026-11-15)

1. Tools > Crypto..., a window of its own like Random.
2. Notifications: Windows toast.
3. Prices in lines too: `0.05 [btc->thb]`.
4. Ethereum RPC: find a public one that needs no key (to do in C1).

## C0 as built (2026-11-15)

- Engine units: `btc`/`sat` (`sats`, `satoshi`), `eth`/`gwei`/`wei`,
  `trx`/`sun`, one category per coin; Conversions menu groups Bitcoin,
  Ethereum, Tron (tests/test_crypto_units.py).
- `Logic/CryptoAddress.cs`: Bitcoin through NBitcoin 10.0.19 (a version
  some weeks old on purpose); EIP-55 with an own Keccak-256 (checked
  against the empty-string and "abc" hashes and the EIP-55 examples);
  Tron Base58Check and its 41... hex form; payment URIs
  (`bitcoin:...?amount=`); xpub/ypub/zpub recognised as watch-only;
  recovery phrases, WIF keys, 64-hex keys and xprv refused.
- `UI/CryptoWindow.cs`: Tools > Crypto... with Address (checked as you
  type; a secret empties the box at once; the wallet form with Copy)
  and Fees (BTC vB x sat/vB, ETH gas x gwei, Insert puts the sum in
  Input). Remembers its tab and place, never an address.
- NBitcoin, Newtonsoft.Json (its dependency) and
  Microsoft.Extensions.Logging.Abstractions credited in
  THIRD-PARTY-NOTICES. Help waits until the crypto topic is done.

## C1 as built (2026-11-15)

Services checked live on 2026-11-15 with public addresses:
mempool.space (Esplora) and api.trongrid.io answer without a key;
CoinGecko's simple/price answers without a key; for Ethereum,
ethereum-rpc.publicnode.com (default) and eth.drpc.org answer without a
key, while eth.llamarpc.com, cloudflare-eth.com and rpc.ankr.com/eth did
not. The USDT contracts were confirmed: Ethereum
0xdAC17F958D2ee523a2206206994597C13D831ec7 answers symbol USDT and 6
decimals; Tron TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t is named TetherToken.

- Engine `crypto.py`: `set_prices` (coin -> currency -> price, time,
  stale, next_check) and `prices_needed`, like the ECB rates;
  `[btc->thb]`, `[sat->thb]`, `[thb->btc]`, `[usdt->thb]`,
  `[gwei->usd]`, coin to coin through usd; `[btc->kg]` is an error
  without a fetch. The note shows `CoinGecko <time>`.
- `Engine/PriceSource.cs`: one CoinGecko request for the four coins in
  the 30 ECB currencies; prices.json cache; fresh 5 minutes, retry in 1
  after a failure (cached prices shown `, offline`). The request has no
  address in it.
- `Logic/CryptoBalance.cs` and the Balance tab: Check sends the
  address (or every address of an xpub/ypub/zpub, scanned to 20 unused
  in a row on receive and change; the type can be chosen) to the
  service named under the box. BTC confirmed and pending with recent
  payments and confirmations; ETH and USDT-ERC20 balances (no history:
  plain RPC has none by address); TRX and USDT-TRC20 with their recent
  transfers. Values in THB and USD from the same prices. Services... sets
  the three URLs (own node), saved in settings. Testnet addresses need a
  testnet Esplora URL. Huge spam-token amounts saturate instead of
  overflowing.

## C2a as built (2026-11-16)

Owner 2026-11-16: today's addresses are exchange deposit addresses
(Bitkub, Binance; money only passes through), so C2 is split: C2a
watches single addresses now; C2b (xpub / descriptor watching, through
an own node) waits for the hardware-wallet project. Owner took every
proposal: every 10 minutes (2-60), every minute while an incoming
payment waits; watching while SoftCalc runs, or in the tray after the
window closes (option); BTC 2 and Tron 1 confirmations; dust below
1 USD; Windows' own sound.

- `Logic/CryptoWatch.cs`: compares a look with the last one. The first
  look only learns. Events: incoming (pending or with its
  confirmations), confirmed (reached the number asked for, told once),
  outgoing, an Ethereum balance move (no history), and suspicious:
  a 0 transfer, an incoming payment under the dust value, or a party
  whose first and last 4 characters copy a watched address or an
  earlier party (address poisoning; outgoing to such a twin too).
- `Persistence/CryptoWatchStore.cs`: crypto.json (at most 50 addresses,
  200 events); xpubs and invalid addresses are dropped on load.
- `UI/CryptoWatcher.cs`, owned by the main window: timer, one address at
  a time a second apart (plus the 1 s gap per service), tray icon while
  addresses are watched, a balloon (toast) per round of changes,
  warning icon for suspicious ones; tray menu Open, Crypto watch, Check
  now, Exit. File > Exit and the tray's Exit really close.
- Watch tab: Add / Rename / Remove / Check now, the settings, and the
  event list (double-click copies the transaction id). Balance tab has
  Watch... to add the address in its box.
- Payments now carry the counterparty and the confirmations; TronGrid's
  TRC-20 list asks for confirmed transfers only.
- Checked live 2026-11-16 with the genesis address (blockstream.info),
  the EIP-55 sample and a Tron sample: a forgotten transaction came back
  as a dust warning (546 sat) and an outgoing USDT transfer.
