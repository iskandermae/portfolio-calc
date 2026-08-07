# Story: Local Transaction Data Storage

## Description
Model and persist cash transactions and security transactions locally, so all other
features (import, reports) have a durable source of truth.

## Acceptance Criteria
- [ ] Domain model exists in `Core/` for `CashTransaction` and `SecurityTransaction`
      (and a shared `Security` reference: ticker, name, trading currency).
- [ ] Repository interfaces exist in `Core/` (e.g. `ITransactionRepository`) with CRUD +
      basic query operations (by date range, by security).
- [ ] EF Core implementation in `Data/` persists both transaction types to SQLite.
- [ ] Transactions can be created, edited, and deleted through the repository layer.
- [ ] Unit tests cover the domain model; integration tests cover the SQLite repository
      round-tripping both transaction types.

## Technical Notes
- Keep `SecurityTransaction` fields generic enough to cover buy/sell/dividend/fee
  transaction types (an enum `TransactionType`), since IBKR imports (story 02) will map
  onto this model.
- This is the foundational schema — expect stories 02–12 to extend it (e.g. adding
  FX rate references, inflation data) rather than replace it.

## Dependencies / Open Questions
- Depends on [00-solution-scaffolding](00-solution-scaffolding.md).
