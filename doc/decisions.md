# Decisions

Non-obvious "why" behind the domain model and design. The "what" lives in the code — don't
duplicate it here. Keep entries to one line. Add one only for a genuinely non-obvious call;
remove/update an entry once it's superseded rather than layering a correction on top.

- `Position` (Account × Security) exists because the same security can be held in multiple
  accounts and must stay separate, not aggregated by symbol alone.
- `SecurityTransaction` references `Position`, not `Security` — a trade happens within one
  account's holding.
- `CashTransaction` links only to `Account` — cash movements aren't tied to a holding.
- Dividends/coupons are `SecurityTransaction`s, not `CashTransaction`s — they're tied to a
  position and a date.
- Fee is a field on each transaction (own currency), not its own transaction type.
- Transaction-type enums are intentionally minimal — extend only when a real case needs it.
- Repository interfaces are split per aggregate, not combined.
- Import target is IBKR Flex Query XML, not Activity Statement CSV — a stable flat schema.
- `Security` is keyed by `Symbol` + `Currency`, not ISIN — the same symbol can be dual-listed
  in different currencies.
- Account is resolved/auto-created from the export's `FlexStatement.accountId` only — not
  the per-row `acctAlias` (changed after existing accounts were renamed to match
  `accountId` directly; avoids the alias and the id resolving to two different accounts).
- Sign convention: incoming/profit amounts positive, outgoing/cost amounts negative — lets
  amounts sum directly; the importer preserves the broker's own sign rather than normalizing it.
- `SecurityTransaction.TaxAmount` is separate from `FeeAmount` — withholding tax and broker
  commission are unrelated costs.
- Dedup key is Account+Security+Amount+Date+Currency, applied after mapping to the domain
  model (not on raw import rows), since some transactions are aggregated from multiple rows.
- A standalone `Tax` transaction type exists for withholding tax with no dividend to attach to.
- `TransferIn` models in-kind transfers (no cash impact); `TransferOut` is omitted (YAGNI).
- `Interest` (cash type) is broker-paid income, distinct from `Deposit` (external movement).
- Dividend/withholding-tax rows are grouped and summed by (Security, Date), not matched 1:1 —
  brokers can report duplicate/correction rows.
- IBKR currency-conversion "trades" are recognized and skipped, kept distinct from genuinely
  unrecognized rows.
- The export's point-in-time position snapshot is never imported — it's derivable from
  transaction history and could contradict it.
- The import parser produces raw rows only; enum mapping/aggregation/resolution is Application
  layer, not parser, logic.
- Import integration tests link the Application layer's source files directly instead of
  referencing the App project, to avoid its MAUI build tooling.
- Broker-specific import code lives under its own `Ibkr/` subfolder per layer, with IBKR's
  literals centralized in `IbkrConstants` — a second broker follows the same shape.
- The app uses EF Core migrations (`Database.Migrate()`), not `EnsureCreated()`, so schema
  changes reach an existing local database; `EnsureCreated()` is still used for in-memory tests.
- `--reset-db` deletes the local database before startup, as an in-app recovery escape hatch.
- `IFxRateProvider`/`ISecurityPriceProvider` fetch one pair/security for one date — no batch
  API; multiple sources can later be composed behind the same interface.
- `FrankfurterFxRateProvider` (ECB rates) is the only `IFxRateProvider` — free, no key.
  `YahooFinanceSecurityPriceProvider` (its undocumented chart endpoint) is the only
  `ISecurityPriceProvider` — needs a browser-like `User-Agent` or Yahoo 429s the request.
- `FxRateResult`/`PriceResult`/`InflationRateResult` carry a status enum instead of
  throwing/nullable — a network failure and an unsupported currency/security need different
  caller reactions.
- `IIbkrTransactionImporter` parses via async `XmlReader`/`XDocument.LoadAsync`, never
  `XDocument.Load(Stream)` — the Blazor `InputFile` stream doesn't support synchronous reads.
- `FxRate`/`SecurityPrice` are separate tables (different keys) but share the same
  check-store→fetch→store caching shape as parallel implementations, not a generic base.
- The base-currency setting is a single-row `AppSettings` table, not a config file.
  `BaseCurrencyConversionService` re-reads the setting and FX history on every call, so
  changing it needs no migration.
- `SupportedCurrencies.Codes` is a small static list of major currencies, not full ISO-4217.
- Inflation data source is the World Bank API (free, queryable on demand), not manual import.
  `InflationRate.Period` is a `DateOnly` normalized to Jan 1 (annual data only).
- `IInflationRateProvider` takes a base currency, not a country code — the currency→region
  mapping is an internal detail of the World Bank implementation.
- `FxRate`/`SecurityPrice` carry a `ValidationStatus` (Valid/PendingValidation/Rejected)
  instead of a separate table; pre-existing rows default to Valid on migration.
- `AnomalyDetector` is a pure stddev-based outlier check, independent of the DB/service layer,
  for direct unit testing; classification only considers already-Valid rows in its window.
- A pending/rejected row for the exact requested date is never returned or duplicated — the
  live provider is queried instead, leaving the stored row for manual review.
- Correcting/rejecting a pending value reuses the same row/status field — no separate audit
  trail (not needed yet).
- `ValidationReview.razor`/`Vocabularies.razor`'s prices/rates tabs call repositories directly
  (plain CRUD) — the actual anomaly/classification logic lives in the services.
- Saved screen layouts live in their own `UiLayoutSetting` table (ScreenKey+LayoutJson), not
  in the `AppSettings` singleton.
- `GetAllAsync` on the transaction repositories eager-loads Account/Security — needed for the
  transactions report's display columns.
- Non-US/unrecognized symbols surface as `PriceStatus.UnsupportedSecurity` — no pre-built
  exchange mapping. Yahoo quotes London securities in pence (`GBp`, case-sensitive, unlike
  `GBP`) — a GBP-currency security's price is converted ÷100.
- `PositionValuationService.GetCurrentPositionsAsync` (net qty = Buy/TransferIn − Sell) is
  exposed separately from valuation since later reports need holdings without a valuation; a
  fully-sold position (qty 0) is dropped. "Current" price/FX resolves via a 7-day backward
  lookback (first success) — duplicated in `PositionValueChartService` rather than extracted
  (two call sites don't justify a shared abstraction).
- `Security.Exchange` stores the broker's raw listing-exchange code, set only when the
  Security is first created by an import and never updated after.
- A generic `VocabularyEntry` table (Type+Key+Value+Description) backs exchange→Yahoo-suffix
  and other lookups, user-editable via a Gui CRUD page instead of hard-coded mappings; a
  missing key and an empty value are distinct "no data" outcomes.
- Transaction-type filtering uses a `TransactionCategory` (Primary/Secondary) classification
  (one central classifier that throws on an unclassified type) rather than per-type checkboxes;
  Tax/Interest/Dividend are Secondary (hidden by default).
- App-wide logging is a small custom file logger (`FileLoggerProvider` → `log.txt`), not a
  third-party library, registered at Warning level to avoid EF Core's Information-level noise.
  `LoggingErrorBoundary` wraps every routed page so a render exception logs and shows in-page
  instead of MAUI's generic error banner; `LogActivityTracker` flags unseen Warning+ lines for
  a nav badge.
- Report pages that call external-API services catch/log/show a friendly error on load failure;
  pages that only read simple repository state don't need the same wrapper.
- The position chart's security picker builds its list from every distinct transacted
  `Security` plus manual free-text entry, since Favorites (story 12) doesn't exist yet.
- Chart date-sampling coarsens dates older than one year to month-1st only, using strict `<`
  so the cutoff date itself keeps full sampling.
- CSPX.L (the chart's comparison security) is USD-priced by Yahoo despite being LSE-listed —
  not pence-quoted like VOD.L; its `Exchange` is left null since `Symbol` already carries
  Yahoo's own `.L` suffix.
- A sample date whose price/FX/inflation rate can't be resolved just drops that point (never
  throws or defaults to zero); an unresolvable start date leaves that series empty.
- Missing inflation rates are logged as a warning and can be backfilled via an
  `InflationRateOverride` vocabulary entry (keyed `baseCurrency:year`), consulted only when the
  real provider fails. `InflationRate.Rate` is stored as a percentage (e.g. 4.7), not a
  fraction — divide by 100 before feeding it into `InflationAdjustmentCalculator`.
- `YahooFinanceSecurityPriceProvider` parses the price JSON defensively (`TryGetProperty`) —
  some historical dates return a response missing expected fields entirely.
- `PositionValueChartService` catches/logs unexpected exceptions per sample-date lookup so one
  bad date degrades a single chart point instead of aborting the whole build.
- `VocabularyOverrideInflationRateProvider` does a case-insensitive key lookup and tolerates
  common hand-typed value formats (whitespace, "%", comma decimal, but not thousands
  separators) — an exact-match lookup silently behaved as "no override" on any typo. It's the
  first Core/Data class with an `ILogger` dependency, since only it can explain why a manual
  override didn't apply.
- A position whose price/FX can't be resolved is excluded from the portfolio grand total and
  flagged, not shown with a caveat — there's nothing left to fall back on by that point.
- `GrowthRateCalculator` (`Core/Analytics/`) is a pure standalone class for direct unit
  testing; its floor on days-elapsed for young transactions is a fixed business rule.
- `PositionValuation` carries `PositionId`, needed by the Position TAB's analytics lookup.
- `InflationRateService.GetForwardFactorAsync` centralizes the per-year-rate/forward-factor
  pattern for the position/transaction performance services; `PositionValueChartService`'s own
  inlined version is left as-is rather than touching an already-shipped feature.
- A `TransferIn` with no cash amount is valued, for performance figures, as a synthetic cost
  (quantity × price on the transfer date); an unresolvable price excludes it and flags that
  position's figures incomplete.
- The Position TAB's cash-flow result sums every linked transaction (amount + fee/tax, each
  inflation-adjusted from its own date) plus the position's current market value, so an open
  position's result reflects unrealized value too, not just realized cash flows. `FeeAmount`
  converts via its own `FeeCurrency`; `TaxAmount` via the transaction's `Currency`.
- The Transaction TAB's CAGR/cash-flow result only inflation-adjusts the buy's initial
  investment — its current value is already expressed in today's prices by construction.
- `TaxBaseCurrency` (story 12) is a second field on the `AppSettings` singleton, defaulted
  (not `required`) so existing call sites that only set `BaseCurrency` keep compiling.
- `CompositeFxRateProvider` (story 12) takes both inner providers by `IFxRateProvider`
  interface, not concrete type, so its UAH-vs-default routing is unit-testable against fakes;
  DI still wires it to the concrete `FrankfurterFxRateProvider`/`NbuFxRateProvider`.
- The tax-estimation report's "FX rate" columns are blended totals (base-currency sum ÷
  security-currency sum), on both the buy side (average cost spans multiple buy dates/rates)
  and the sell side (an actual sell and a proposed sell for the same security can have
  different dates too) — kept symmetric rather than treating only one side as multi-date.
- The tax-estimation report distinguishes historical dates (a buy, an actual sell, a
  TransferIn — no lookback, missing data stops the whole report via
  `TaxEstimationException`) from "today" lookups (a proposed sell's price/FX — 5-day
  lookback, logged on fallback) — a deliberately different rule from every other report's
  single 7-day lookback, per explicit business decision.
- The tax-estimation report is grouped and computed per `Position` (Account × Security), not
  aggregated by security — unlike the portfolio value/performance reports (09–11), which
  deliberately do aggregate across a security's positions. Cost basis, held-quantity
  validation, and actual/proposed sells all stay within one account; the same security in two
  accounts gets two independent rows. `ProposedSell` carries a `PositionId`, not a
  `SecurityId`. An optional account filter scopes the whole computation the same way.
- `AppSettings.TaxBaseCurrency`'s `GetTaxBaseCurrencyAsync` treats a blank value (not just a
  missing settings row) as "unset" and logs a warning before defaulting — the migration that
  added the column backfilled existing rows with `""`, not a real default, so an
  already-existing installation silently got an empty FX "to" currency without this check.
- A second broker (Trading 212, story 14) got its own importer/service pair under a parallel
  `T212/` subfolder per layer, rather than generalizing the IBKR importer interface — it's
  typed to return `IbkrFlexQueryImportData`, so it was never actually broker-agnostic
  despite story 02's note; forcing a shared abstraction wasn't worth it for two brokers with
  unrelated raw-row shapes (XML attributes vs. a flat CSV row). Renamed
  `ITransactionImporter` → `IIbkrTransactionImporter` and moved it into `Core/Import/Ibkr/`
  (next to `IbkrFlexQueryImportData`) to make that non-generality explicit, mirroring
  `IT212TransactionImporter`'s own location under `Core/Import/T212/`.
- T212's CSV reports `Total`/`Result`/its conversion fee in the account's own currency
  regardless of the security's trading currency (unlike IBKR, which reports per-security).
  The importer computes `SecurityTransaction.Amount`/`Currency` from shares × price in the
  trading currency instead of reading `Total`, so a T212-imported security stays keyed the
  same way (`Symbol` + trading currency) as the same security imported from IBKR.
- T212's `GBX` (pence) currency code is normalized to `GBP` (price ÷ 100) before computing
  Amount — the same pence/pounds split already handled for Yahoo's `GBp` quotes.
- T212's CSV carries no account identity at all (unlike IBKR's `FlexStatement.accountId`) —
  the GUI resolves/creates the target `Account` via a picker before calling the import
  service, rather than the importer/service resolving it from file content.
- T212 puts a dividend's gross amount and its withholding tax on the same row, so (unlike
  IBKR) no cross-row aggregation pass is needed to produce one Dividend transaction.
- T212's CSV has no listing exchange, so GBP-traded T212 securities get the placeholder
  exchange `MY_T212.L` (new securities only; existing ones are never updated); the user maps it to `.L` in the
  `ExchangeYahooSuffix` vocabulary. Other currencies get none. A heuristic by design — wrong
  for a GBP-denominated security that isn't London-listed, fixable by editing its exchange.
- The Transactions page's "Details" memo shows currencies as plain codes: there is no currency
  entity/name table (see `SupportedCurrencies`), and none was added for display.
- Story 15's Account/Currency filters are matched by account name (the Portfolio Value rows
  carry no account id) and their options derive from the loaded rows, never persisted. The
  Portfolio Value grand total is recomputed over the filtered rows by `PortfolioValueGridView`
  from the already-loaded report, so a filter change never re-fetches prices/FX/inflation, and
  analytics stay keyed by position id over the full report. Its footer is one right-aligned
  total cell, not a cell under the value column, since columns are now hideable/reorderable.
- Column order/visibility/sort persistence is the shared `GridLayout` (+ `ColumnChooser`
  component); a screen with extra saved state derives from `SavedGridLayout` (Transactions
  adds `ShowSecondary`), keeping its existing JSON shape compatible with already-saved layouts.
- External FX/price provider calls (only those — never DB reads) are memoized per page by
  decorators over `IFxRateProvider`/`ISecurityPriceProvider` sharing one `ExternalCallCache`,
  cleared on every navigation by `MainLayout`; no invalidation otherwise, to stay trivially
  simple. It exists because failed/pending lookups aren't stored in the DB and would otherwise
  hit the network on every analytics toggle.
