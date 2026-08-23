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
- Account is resolved/auto-created from the export's alias, falling back to the raw account id.
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
- `ITransactionImporter` parses via async `XmlReader`/`XDocument.LoadAsync`, never
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
- `Security.Exchange` stores the broker's raw listing-exchange code, backfilled on first
  non-empty observation and never overwritten after.
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
