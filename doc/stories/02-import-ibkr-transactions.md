# Story: Import Transactions from IBKR Export

## Description
Allow importing cash and security transactions from a file exported by Interactive
Brokers (IBKR), mapping rows onto the local transaction model (story 01).

## Acceptance Criteria
- [x] A file picker in the GUI lets the user select an IBKR export file.
- [x] The parser reads the file and produces `CashTransaction`/`SecurityTransaction`
      records via the repository from story 01.
- [x] Duplicate imports (re-importing the same file/overlapping date range) do not create
      duplicate transactions.
- [x] Malformed/unrecognized rows are reported to the user rather than silently skipped
      or crashing the import.
- [x] Integration tests parse a sample export file (a fully synthetic fixture, not real data).

## Technical Notes
- Parser lives in `Data/` behind an `Core`-defined import interface (e.g.
  `ITransactionImporter`), so a second broker's format could be added later without
  touching application/UI code.
- Get a real sample export before finalizing the parser — see open question below.

## Implementation Notes (as built)
Initially built and validated against the IBKR export sample. See `doc/decisions.md`
for the full rationale of each call below:
- **Format**: IBKR Flex Query XML specifically (not Activity Statement CSV).
- `ITransactionImporter` (Data/`IbkrFlexQueryImporter`) parses XML into raw attribute
  rows only (`IbkrRawRow`/`IbkrFlexQueryImportData` in `Core/Import/`); all mapping logic
  (account/security resolution, dividend aggregation, dedup, FX-skip recognition) lives
  in `App/Application/Import/IbkrImportService`, returning an `ImportResult`
  (imported/recognized-but-skipped/unrecognized).
- `Security` is keyed by `Symbol` + `Currency`. ISIN is deliberately not imported or
  stored for now.
- New enum values: `SecurityTransactionType.Tax`, `SecurityTransactionType.TransferIn`,
  `CashTransactionType.Interest` — each backed by a real row shape in the sample file.
  `TransferOut` deliberately not added (no sample case).
- Sign convention: `CashTransaction`/`SecurityTransaction.Validate()` changed from a
  blanket "Amount must be positive" to a type-dependent rule (Withdrawal/Tax negative,
  everything else positive); this changed one pre-existing story-01 test's fixture data
  (`Withdrawal` amount) to stay valid, with no behavior change for `Deposit`/`Buy`/`Sell`.
- Dedup key: `Account + Security + Amount + Date + Currency`, applied to the transaction
  *after* mapping (not raw rows), no new schema.
- GUI: `/import` page (file picker + results view), linked from `NavMenu.razor`.
- `PortfolioCalc.App.Tests` (new xUnit project) compiles `PortfolioCalc.App/Application/**`
  sources directly rather than via `ProjectReference`, to avoid a MAUI
  `Microsoft.Maui.Resizetizer` build conflict — see `doc/decisions.md`.

## Dependencies / Open Questions
- Depends on [01-transaction-data-storage](01-transaction-data-storage.md).
