# M8 plan: tools (agreed 2026-11-06)

Branch `m8-tools`. Covers extensions.md E8 (currency), E9 (copy menu,
always on top) and E11 (Compound Interest window).

## 1. Currency conversion (E8)
- Syntax as units: `100 [usd->thb]`, chainable, case-insensitive; the
  ECB codes plus EUR, cross rates through EUR.
- Engine has no network. Op `set_rates {date, rates, stale, next_check}`
  gives it the rates; `next_check` is Unix seconds after which the
  rates are due again.
- A conversion with no rates, or with due rates, makes `eval` answer
  `kind: "rates_needed"`. The UI fetches `eurofxref-daily.xml`
  (timeout 5 s), sends `set_rates`, and evaluates the line once more.
- `next_check` (UI policy): after a fetch that got the latest
  publication, the next publication time (about 16:00 CET on TARGET
  working days; weekends and fixed TARGET holidays skipped); after a
  failed fetch or an old publication, now + 1 hour.
- Cache `%APPDATA%\SoftCalc\rates.xml` (raw ECB file), loaded at start
  without fetching; replayed to the engine after a restart.
- Offline with a cache: cached rates, line marked offline. No cache:
  `ERROR:Currency rates unavailable`.
- Owner 2026-11-06:
  - result shown at full precision, like units;
  - every currency line shows the rate date (faint, on the result line),
    e.g. `ECB 2026-11-05`, plus `offline` when the fetch failed;
  - Conversions > Currency: the 10 common codes (THB USD EUR JPY GBP CNY
    SGD HKD AUD KRW) as source > target submenus; below a separator a
    *From* group (all codes, inserts ` [THB`) and a *To* group (all
    codes, inserts `->USD]`).
- Only network access in the program; generic User-Agent.

## 2. Compound Interest window (E11)
- Tools > Compound Interest, modeless, like the graph window.
- Inputs: principal, annual rate %, years, contribution (expressions
  allowed), compounding and contribution frequency combos, "at start
  of period" check box. Owner 2026-11-06: recalculates automatically
  while typing (300 ms delay).
- Outputs: future value, total contributions, total interest; stacked
  area graph (contributions below, interest above, own GDI drawing);
  period table (virtual DataGridView), Ctrl+C copies selected rows as
  tab-separated text, Copy all, Export CSV (UTF-8).
- A bad input shows its message next to the field; the last good result
  stays. Inputs are remembered in settings.json. Engine `schedule`
  errors start with the field name; at most 100000 rows.

## 3. Copy menu and always on top (E9)
- History context menu: Copy result, Copy expression and result,
  Copy all. Ctrl+C unchanged.
- View > Always on top, remembered; owned windows follow.

## 4. Tests
- Engine: currency with fixed rates (chain, case, unknown code,
  rates_needed, due, offline note), `set_rates` protocol.
- UI: ECB XML parse (fixture), publication calendar, next_check policy,
  rates cache, CSV escaping, copy texts, settings round trip.
- Manual: one real ECB fetch, the Compound window, scratch data folder.
