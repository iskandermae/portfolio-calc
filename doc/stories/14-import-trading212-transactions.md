# Story: Import Transactions from Trading 212 Export

## Description
Allow importing cash and security transactions from a Trading 212 "Transaction history"
CSV export, mapping rows onto the local transaction model (story 01), as a second broker
alongside IBKR (story 02).

## Acceptance Criteria
- [x] A broker selector on the existing `/import` page switches between IBKR (.xml) and
      Trading 212 (.csv); selecting Trading 212 shows an Account picker (existing accounts
      + free-text for a new one), since the T212 export carries no account identity of its
      own (unlike IBKR's `FlexStatement.accountId`).
- [x] The parser reads a T212 CSV export and produces `CashTransaction`/`SecurityTransaction`
      records via the repositories from story 01, for the chosen Account.
- [x] Recognized row types (`Action` column): `Deposit` (Deposit), `Interest on cash`
      (Interest), `Market buy`/`Limit buy` (Buy), `Market sell`/`Limit sell` (Sell),
      `Dividend (Dividend)` (Dividend — gross amount and withholding tax are on the same
      row, folded into one transaction, unlike IBKR's separate dividend/tax rows).
- [x] Any other `Action` value (e.g. a future "Withdrawal" row — no sample exists yet) is
      reported as unrecognized rather than guessed.
- [x] Duplicate imports (re-importing the same file/overlapping date range) do not create
      duplicate transactions — same dedup key as story 02 (Account + Security + Amount +
      Date + Currency, applied after mapping).
- [x] Malformed/unrecognized rows are reported to the user rather than silently skipped or
      crashing the import.
- [x] A fully synthetic CSV fixture (not real data) covers every recognized row shape;
      integration tests parse it.
- [x] Securities traded in GBP (after GBX→GBP normalization) are assigned the placeholder
      exchange `MY_T212.L` (newly created securities only); other
      currencies get none. The user maps `MY_T212.L` → `.L` in the `ExchangeYahooSuffix`
      vocabulary.
- [x] The `/import` page shows a short note, when Trading 212 is selected, explaining the
      GBP → `MY_T212.L` exchange guess.
- [x] The Transactions page has a per-row "Details" button that toggles a read-only memo with
      every stored field of the transaction and its linked Account/Position/Security
      (including all ids). Currencies are plain codes — no currency entity, so no names.
- [x] The full existing test suite (Core + App) still passes after implementation, and the
      IBKR import path is unaffected — confirms the second broker didn't regress the first.

## Technical Notes
- New parallel vertical slice under its own `T212/` subfolder per layer, mirroring IBKR's
  (`Core/Import/T212`, `Data/Import/T212`, `App/Application/Import/T212`). The existing
  `ITransactionImporter` stays IBKR-specific — it's typed to return `IbkrFlexQueryImportData`,
  so it isn't actually broker-agnostic despite story 02's note. T212 gets its own
  importer/service pair instead of forcing a shared abstraction, consistent with
  doc/decisions.md's "a second broker follows the same shape."
- T212 reports `Total`/`Result`/`Currency conversion fee` in the account's own currency
  (GBP in the sample) regardless of the security's trading currency — unlike IBKR, which
  reports everything per-security. To keep `Security` keyed consistently with
  IBKR-imported securities (same symbol + trading currency, not fragmented by account
  currency), `SecurityTransaction.Amount`/`Currency` are computed from `No. of shares` ×
  `Price / share` in the **trading currency** (`Currency (Price / share)`), not read from
  `Total`. `Currency conversion fee` (always GBP in the sample, reported as a positive
  charge) maps to `FeeAmount` (negated — `FeeAmount` can't be positive) / `FeeCurrency =
  "GBP"`, independent of the security's trading currency, reusing the existing
  FeeAmount/FeeCurrency split.
- `Currency (Price / share)` of `GBX` (pence) is normalized to `GBP` by dividing the price
  by 100 before computing Amount — the same pence/pounds convention already used for Yahoo
  `GBp` quotes (doc/decisions.md), applied here to the import's own currency code.
- Withholding tax on a Dividend row is already in the trading currency, matching
  `SecurityTransaction.TaxAmount`'s "same currency as the dividend" rule — no separate tax
  currency to reconcile.
- ISIN is available in the CSV but, consistent with IBKR (story 02), is not imported or
  stored — `Security` stays keyed by Ticker (as `Symbol`) + trading currency. T212 has no
  listing-exchange field, so the importer guesses it from currency instead — see the follow-up note below.
- Account resolution: since nothing in the file can drive auto-resolution, the GUI's broker
  selector shows an Account dropdown (existing accounts, via a new
  `IAccountRepository.GetAllAsync()`) plus a free-text option to create one; the chosen
  Account is used for every row in that import.
- `Time (UTC)` truncates to `DateOnly` the same way IBKR's `dateTime` does.
- `Market buy`/`Limit buy` and `Market sell`/`Limit sell` both map to
  `SecurityTransactionType.Buy`/`Sell` — order type (market vs. limit) isn't part of the
  domain model.
- Regression check: after building the T212 slice, run the full test suite and re-verify
  the IBKR GUI/import path — the two importers/services must stay fully independent (no
  shared mutable state), so adding T212 must not change IBKR's behavior.

## Implementation Notes (as built)
Built as planned above; a few specifics worth recording:
- `Core/Import/T212` (`IT212TransactionImporter`, `T212RawRow`, `T212ImportData`) +
  `Data/Import/T212/T212CsvImporter` (a small hand-rolled RFC4180-quoting line parser — no
  CSV library was already a project dependency) + `App/Application/Import/T212`
  (`T212Constants`, `T212ImportService`), fully parallel to IBKR's layout, as planned.
- `T212ImportService.ImportAsync(Stream, int accountId)` takes the target `Account` as a
  parameter rather than resolving it itself — `Import.razor` resolves/creates the Account
  (via the new `IAccountRepository.GetAllAsync()` plus `AddAsync` for a typed new name)
  *before* calling in, then passes its id.
- `Import.razor` gained a `Broker` selector (IBKR/Trading 212). Trading 212 shows the
  Account dropdown + a "new account" text box (shown only when no existing account is
  selected) and switches `<InputFile accept>` to `.csv`.
- While fixing the build to get a baseline (see below), found and fixed one more pre-existing
  break: `IbkrImportServiceTests`/`AppStartupImportTests` hadn't been updated for an
  `IVocabularyRepository` constructor parameter added to `IbkrImportService` in the prior
  commit — fixed as a one-line, in-scope compile fix, not a T212 change.
- Confirmed with you that the same prior commit's change to
  `IbkrImportService.ResolveAccountAsync` (now resolving solely from
  `FlexStatement.accountId`, no longer preferring the row `acctAlias`) was intentional (an
  already-renamed database). Updated the one test that still asserted the old alias-based
  behavior, `IbkrFlexQueryImportData`'s doc comment, `README.md`, and doc/decisions.md to
  match — see the dedicated decisions.md entry.
- Tests: `T212SampleFixture` (synthetic CSV covering every recognized row shape plus one
  unrecognized `Withdrawal` row) with 10 unit tests under
  `PortfolioCalc.App.Tests/Application/Import/T212`, plus one `AppStartupImportTests` case
  exercising the real DI/migration composition root (mirroring the existing IBKR one).
- Verified green after the change: 149 `PortfolioCalc.Core.Tests` + 87
  `PortfolioCalc.App.Tests` (76 pre-existing + 1 fixed + 10 new) — the IBKR import path is
  unaffected.
- Follow-up: considered unifying `ITransactionImporter`/`IT212TransactionImporter` into one
  common interface and decided against it (no caller needs the polymorphism; the two
  brokers' raw-row shapes genuinely differ — see doc/decisions.md). Instead renamed
  `ITransactionImporter` → `IIbkrTransactionImporter` and moved it from `Core/Import/` into
  `Core/Import/Ibkr/` (next to `IbkrFlexQueryImportData`), so its name/location makes the
  non-generality explicit and mirrors `IT212TransactionImporter` under `Core/Import/T212/`.
- Not automatable in this environment: `PortfolioCalc.App` is a Windows MAUI desktop app, so
  the new broker-selector/account-picker UI could be launched (app starts, DB migrates,
  nothing logged) but not click-driven/screenshotted — manual check of `/import` in the app
  is still needed to confirm the UI itself.

## Follow-up: exchange guess + transaction details
- `T212ImportService.ResolveSecurityAsync` sets `Exchange = T212Constants.ExchangeGbpGuess`
  (`"MY_T212.L"`) on a newly created GBP-traded security. An existing security is returned
  untouched — the earlier exchange backfill (T212 and IBKR) was dropped once existing data had
  been re-imported.
- `TransactionDetailsFormatter` (`Application/Transactions`) renders the memo text; the page
  builds it when rows load from the already-eager-loaded entities, so no new repository
  methods were needed. Open rows are tracked in page state only (not saved in the layout).

## Dependencies / Open Questions
- Depends on [01-transaction-data-storage](01-transaction-data-storage.md) and follows the
  pattern established by [02-import-ibkr-transactions](02-import-ibkr-transactions.md).
- The current `import/samples/T212` files contain no Withdrawal row and only a single
  account currency (GBP) — those cases are unhandled until a real sample shows up.
- The CSV parser doesn't handle an embedded newline inside a quoted field — no sample row
  needs that.
