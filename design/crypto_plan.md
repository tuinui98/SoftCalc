# Crypto watch - plan (owner 2026-11-15, not started)

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

## Open questions for the owner

1. Window name and place: Tools > Crypto...? Separate window like
   Random?
2. Notifications: Windows toast, or only inside the window?
3. Should balances or prices also be usable in lines (`[btc->thb]`)?
4. Which public Ethereum RPC provider to start with (some need keys)?
