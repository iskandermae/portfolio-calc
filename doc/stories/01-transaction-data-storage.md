# Story: Local Transaction Data Storage

## Description
Model and persist cash transactions and security transactions locally, so all other
features (import, reports) have a durable source of truth.

## Acceptance Criteria
- [x] Domain model exists in `Core/` for `CashTransaction` and `SecurityTransaction`
      (and a shared `Security` reference: ticker, name, trading currency).
- [x] Repository interfaces exist in `Core/` (e.g. `ITransactionRepository`) with CRUD +
      basic query operations (by date range, by security).
- [x] EF Core implementation in `Data/` persists both transaction types to SQLite.
- [x] Transactions can be created, edited, and deleted through the repository layer.
- [x] Unit tests cover the domain model; integration tests cover the SQLite repository
      round-tripping both transaction types.

## Technical Notes
- Keep `SecurityTransaction` fields generic enough to cover buy/sell/dividend/fee
  transaction types (an enum `TransactionType`), since IBKR imports (story 02) will map
  onto this model.
- This is the foundational schema — expect stories 02–12 to extend it (e.g. adding
  FX rate references, inflation data) rather than replace it.

## Implementation Notes (as built)
Clarified with the user beyond the original story text — the model supports multiple
brokerage accounts holding the same security separately:
- Added `Account` (id, name) and `Position` (`Account` + `Security` pair, unique) entities,
  neither of which was in the original story text but is required to support multiple
  accounts holding the same security.
- `CashTransaction` — pure account-level cash movement (`Deposit`/`Withdrawal` for now,
  enum is extensible). References `AccountId` only, no `Position` link.
- `SecurityTransaction` — references `PositionId` (not `Security` directly), so the same
  ticker held at two accounts is tracked separately. `Type` enum is `Buy`/`Sell`/`Dividend`
  for now (extensible; e.g. bond coupons can be added as a new value later). `Quantity`
  is required for `Buy`/`Sell` and must be absent for `Dividend`.
- Fee is not a separate transaction — both transaction types carry an optional
  `FeeAmount`/`FeeCurrency` pair (fee currency can differ from the transaction currency).
- Repository interfaces split per aggregate: `IAccountRepository`, `ISecurityRepository`,
  `IPositionRepository`, `ICashTransactionRepository`, `ISecurityTransactionRepository`
  (not a single combined `ITransactionRepository`).
- `IAccountRepository`/`ISecurityRepository`/`IPositionRepository` expose only
  `AddAsync`/`GetByIdAsync`/`DeleteAsync` — no `Update`, `GetAll`, `GetByTicker`, or
  `GetByAccountAndSecurity`, since nothing calls them yet. Add methods back only when a
  later story actually needs them.
- Explicit dedup fields (external id/source) for story 02's re-import detection were
  deliberately left out of this story, per the user's decision — story 02 will add
  whatever it needs.
- Removed the `AppMetadata` placeholder table from story 00, as its own comment said to
  do once real domain entities landed.

## Dependencies / Open Questions
- Depends on [00-solution-scaffolding](00-solution-scaffolding.md).
