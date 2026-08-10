# Decisions

Why the domain model looks the way it does. Short entries, why only — the what is in the
code. Add an entry here whenever a non-obvious design choice is made; don't let it drift.

## Story 01 — Transaction data storage

- **`Position` (Account × Security) exists, not just `Security`.** User holds the same
  security across multiple brokerage accounts and needs them tracked separately, not
  aggregated by ticker alone. Any "by security" query/report must aggregate across all
  of a security's `Position`s, not assume one row per security.
- **`SecurityTransaction` references `Position`, not `Security` directly.** Follows from
  the above — a buy/sell/dividend always happens within one account's holding of a
  security.
- **`CashTransaction` has no link to `Position`, only `Account`.** Cash movements
  (deposit/withdrawal) are account-level, not tied to any specific holding.
- **Dividends/coupons are `SecurityTransaction`s, not `CashTransaction`s.** They occur on
  distinct dates tied to a specific position (e.g. quarterly), not as generic account
  cash flow.
- **Fee is a field on each transaction (`FeeAmount`/`FeeCurrency`), not its own
  transaction type.** A fee is incidental to another economic event (a buy, a
  withdrawal) and can be in a different currency than that event.
- **`SecurityTransactionType`/`CashTransactionType` enums are intentionally minimal**
  (Buy/Sell/Dividend, Deposit/Withdrawal). Extend only when a real case shows up (e.g.
  bond coupons) — don't pre-model transaction kinds that aren't needed yet.
- **Repository interfaces are split per aggregate** (`IAccountRepository`,
  `ISecurityRepository`, `IPositionRepository`, `ICashTransactionRepository`,
  `ISecurityTransactionRepository`), not one combined `ITransactionRepository`. Keeps
  each interface cohesive as query methods are added by later stories.
- **No import-dedup fields (external id/source) yet.** Deferred to story 02, which owns
  the import/dedup concern — avoids speculative schema for a strategy not yet decided.
