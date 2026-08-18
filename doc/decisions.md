# Decisions

Why the domain model looks the way it does. Short entries, why only — the what is in the
code. Add an entry here whenever a non-obvious design choice is made; don't let it drift.

- **`Position` (Account × Security) exists, not just `Security`.** The user holds the same
  security across multiple brokerage accounts and needs them tracked separately, not
  aggregated by ticker alone. Any "by security" query/report must aggregate across all of
  a security's `Position`s, not assume one row per security.
- **`SecurityTransaction` references `Position`, not `Security` directly.** A buy/sell/
  dividend always happens within one account's holding of a security, never against the
  security in the abstract.
- **`CashTransaction` has no link to `Position`, only `Account`.** Cash movements are
  account-level, not tied to any specific holding.
- **Dividends/coupons are `SecurityTransaction`s, not `CashTransaction`s.** They're tied
  to a specific position and occur on distinct dates (e.g. quarterly), not generic
  account cash flow.
- **Fee is a field on each transaction, not its own transaction type.** A fee is
  incidental to another economic event (a buy, a withdrawal) and can be in a different
  currency than that event.
- **Transaction-type enums are intentionally minimal.** Extend only when a real case
  shows up — don't pre-model kinds that aren't needed yet.
- **Repository interfaces are split per aggregate, not one combined interface.** Keeps
  each interface cohesive as query methods accumulate over time.
- **Import target format is IBKR Flex Query XML, not Activity Statement CSV.** Flex Query
  has a stable, flat per-row schema designed for automated consumption; Activity
  Statement is a multi-section human report with repeating headers, harder to parse
  reliably. Confirmed against a real sample export.
- **`Security` is keyed by `Symbol` + `Currency`, not by ISIN.** The same symbol can be
  quoted in more than one currency (e.g. a dual-listed ETF), and each currency-denominated
  listing is a distinct tradeable instrument. ISIN is deliberately not imported or stored
  for now — reintroduce only if a real case shows up that `Symbol` + `Currency` can't
  disambiguate.
- **Account is resolved/auto-created by the export's account alias, falling back to the
  raw account id.** The alias is the human-meaningful, stable label; the raw id is a
  fallback only for exports that omit it. No GUI account-picker step for this story.
- **Sign convention: incoming/profit amounts are positive, outgoing/cost amounts are
  negative** (e.g. a Buy's cash impact is negative, a Sell's is positive, a fee is always
  negative). This lets amounts be summed directly for cash-flow totals without branching
  on transaction type. The importer preserves whatever sign the source file already
  reports (IBKR reports commission as negative) rather than normalizing to a magnitude —
  a broker's own sign is authoritative and shouldn't be second-guessed or discarded.
- **`SecurityTransaction.TaxAmount` is a field separate from `FeeAmount`, not a reuse of
  it.** A dividend's withholding tax and a trade's broker commission are unrelated costs
  that happen to both reduce cash; conflating them would make it impossible to later tell
  which cost was which when reading a transaction back.
- **Dedup key is `Account + Security + Amount + Date + Currency`, applied after mapping
  to the domain model, not to raw import rows.** Some domain transactions (e.g. a
  dividend) are themselves aggregated from multiple raw rows, so dedup can only run once
  that aggregation has happened. No dedicated dedup schema (external id/source column) —
  the natural key is enough to make re-importing the same export a no-op.
- **A standalone `Tax`-type transaction exists for withholding tax with no dividend to
  attach to** (e.g. a tax-rate correction on a prior period, reported alone). Without it,
  such tax events would have nowhere to go.
- **`TransferIn` exists for in-kind security transfers into an account (no cash impact).**
  `TransferOut` is deliberately not added — no real case for it has shown up yet (YAGNI).
- **`Interest` (cash transaction type) exists for broker-paid interest income.** It's
  account-level income with no security/position, and conceptually distinct from a
  `Deposit` (an external cash movement, not income earned by the account).
- **Dividend/withholding-tax rows are grouped and summed by (Security, Date) rather than
  matched one-to-one.** A dividend and its tax can be reported as more than one row each
  (e.g. an original entry plus a same-day rate-correction restatement); summing with sign
  preserved avoids having to guess which row is "the real one."
- **IBKR's currency-conversion "trades" are recognized and explicitly skipped, not
  imported, and kept distinct from "unrecognized" rows.** They're a real, understood
  shape (mechanics of settling a multi-currency trade), not a security trade and not a
  parsing failure — conflating them with genuinely unrecognized rows would hide actual
  parsing problems.
- **The point-in-time position snapshot in an export is never read.** It's not a
  transaction log; importing it would duplicate information already derivable from the
  transaction history and risks contradicting it.
- **The import parser produces raw rows only — no enum mapping, aggregation, or
  account/security resolution.** That's real domain logic and belongs in the Application
  layer per the project's Core/Data/Application boundary, not in the broker-specific
  parser.
- **The import integration test project links the Application layer's source files
  directly instead of referencing the App project.** The App project's MAUI build
  tooling fails when referenced from a plain test project; the Application layer itself
  has no MAUI dependency, so this sidesteps a build-tool conflict without weakening the
  architecture boundary (the Gui layer stays untested by this project).
- **Broker-specific import code lives in its own `Ibkr/` subfolder under each layer's
  `Import/` folder** (`Core/Import/Ibkr/`, `Data/Import/Ibkr/`, `Application/Import/Ibkr/`,
  mirrored in the test project), with IBKR's own literal strings centralized in one
  `IbkrConstants` file rather than repeated across the mapping code. Only the
  broker-agnostic pieces (`ITransactionImporter`, `ImportResult`/`SkippedRow`) stay at the
  `Import/` root. A second broker's importer should follow the same shape — its own
  sibling subfolder and constants file — so brokers never bleed into each other's code.
- **The app uses EF Core migrations (`Database.Migrate()`), not `EnsureCreated()`, to
  bring up its local database.** `EnsureCreated()` only creates tables when none exist —
  it silently leaves an already-existing database's schema untouched, so any schema
  change (e.g. the `Symbol` rename, `TaxAmount`) never reaches a user's existing local
  database and the next write fails with a raw SQLite column error. Every model change
  from now on needs a matching migration (`dotnet ef migrations add <Name> --project
  PortfolioCalc.Core --output-dir Data/Migrations`) — `PortfolioDbContextFactory` exists
  purely so that command can run against the Core class library without needing the MAUI
  app as the EF startup project. `EnsureCreated()` is still fine (and used) in tests
  against ephemeral in-memory databases, where there's no prior schema to diverge from.
- **A `--reset-db` command-line flag deletes the local database before startup**, as an
  escape hatch for a database that's beyond what a migration can fix. It's a flag on the
  app itself, not a "go delete this file by hand" instruction, so recovering doesn't
  require knowing where the file lives or using a shell.
- **`IFxRateProvider` fetches one currency pair for one date; no batch/range method.**
  Calling code only ever needs "this pair, this date" (a position valuation), so a range API
  would be speculative. Different sources per currency (e.g. a different provider for a
  currency Frankfurter doesn't cover) can be added later behind a composite that also
  implements `IFxRateProvider` — callers stay unaware either way, since they only ever
  depend on the interface.
- **`FrankfurterFxRateProvider` (frankfurter.dev, ECB reference rates) is the first/only
  `IFxRateProvider` implementation.** Free, no API key, no rate limit tier to manage — good
  enough for a single-user local app. It doesn't cover every currency a broker export might
  report; that surfaces as `FxRateStatus.UnsupportedCurrency` per call rather than being
  worked around preemptively with a second provider before a real currency needs one.
- **`FxRateResult` carries a `FxRateStatus` (`Success`/`UnsupportedCurrency`/`NetworkError`)
  instead of throwing or returning a nullable rate.** A network failure and an unsupported
  currency are both real, distinguishable outcomes a caller needs to react to differently
  (retry vs. skip), not exceptions to catch or a silent gap to notice later.
- **`ITransactionImporter.ParseAsync` parses via an async `XmlReader` +
  `XDocument.LoadAsync`, never `XDocument.Load(Stream)`.** The GUI hands the importer a
  Blazor `InputFile` stream over the browser/WebView bridge, whose synchronous `Read()`
  throws `"Synchronous reads are not supported."` — `XDocument.Load(Stream)` reads
  synchronously internally, so it works against `File.OpenRead()` in a test but breaks
  against the real GUI file stream. Caught by a test using a stream that mimics the
  browser stream's read behavior (throws on sync `Read`, only `ReadAsync` works) — a
  fully-seekable, sync-capable `FileStream` in a test can't surface this class of bug at
  all, no matter how thoroughly it's otherwise exercised.
- **`FxRate` and `SecurityPrice` are separate entities/repositories, not one combined
  table.** Their shapes match (date, value) but their keys don't (currency pair vs.
  security id) — splitting per aggregate follows the existing repository-interface
  convention rather than forcing a shared schema with unused columns for either side.
  The *caching* logic is what's actually shared: `FxRateService`/`SecurityPriceService`
  (Application layer) both do check-repository → fetch-from-provider-if-missing →
  store-if-successful, as parallel implementations rather than a generic base — two
  call sites didn't justify the complexity of a shared generic abstraction.
- **`ISecurityPriceProvider` (+ `PriceResult`/`PriceStatus`) exists with no concrete
  `Data/` implementation yet.** No price data source has been chosen (unlike FX, which
  has Frankfurter from story 03); the interface and its `SecurityPriceService` caching
  wrapper were still built now, mirroring `IFxRateProvider`, so the storage schema and
  caching shape are already in place for whichever provider a future story adds — that
  story only needs to supply the `Data/` implementation and DI wiring, tested here
  against a fake provider instead of a real API.
- **The base-currency setting is a single-row `AppSettings` table (fixed `Id = 1`,
  upserted), not a local config file.** Keeps it in the same SQLite database and behind
  the same repository pattern as everything else, instead of introducing a second
  persistence mechanism for one value.
- **Base-currency conversion (story 05) is a narrow `BaseCurrencyConversionService`
  Application-layer method, not wired into any report/portfolio-total screen.** Stories
  08–11 (the actual report screens) don't exist yet, so there's nothing real to wire it
  into; the service reads the setting and stored FX history fresh on every call — proving
  no migration is needed when the setting changes — and is the interface those future
  screens will call.
- **`SupportedCurrencies.Codes` is a small static list (USD, EUR, GBP, CHF, JPY, AUD,
  CAD), not a full ISO-4217 table.** No currency enum/list existed before story 05; these
  are major currencies also covered by the Frankfurter provider (story 03/04). Extend only
  when a real currency need shows up.
- **Inflation data source (story 06's deferred open question) is the World Bank API, not
  manual file import.** It's free, no API key, no rate-limit tier — same reasoning as
  Frankfurter for FX (story 03) — and, unlike a manual import, a real API can genuinely be
  queried lazily on calculation demand rather than requiring the user to source and upload
  a file themselves each time a new period is needed.
- **`InflationRate.Period` is a `DateOnly` normalized to the 1st of the year (the World
  Bank's CPI indicator is annual-only), not a bespoke year/month "period" type.** Reuses
  the same comparable/range-queryable shape as `FxRate.Date`/`SecurityPrice.Date` instead
  of inventing a new period abstraction for what is just a queryable key; a future monthly
  source would normalize to the 1st of its month the same way.
- **`IInflationRateProvider` takes a base currency, not a country code.** The World Bank
  indexes data by country/region, so `WorldBankInflationRateProvider` privately maps each
  of `SupportedCurrencies.Codes` to one representative country/region code (e.g. EUR to
  the World Bank's "EMU" euro-area aggregate) — callers stay in currency terms, matching
  every other rate/price interface in this codebase, and the mapping is an implementation
  detail of this one provider rather than a general currency-to-country concept.
- **`FxRate`/`SecurityPrice` gain a `ValidationStatus` column (`Valid`/`PendingValidation`/
  `Rejected`, `Valid = 0`) rather than a separate validation table** — story 07 extends the
  existing schema per its own Technical Note. The migration defaults existing rows to
  `Valid` (the enum's zero value): pre-story-07 rows were already being used unreviewed, and
  there's no recent-history context left to judge them against retroactively, so leaving
  them trusted is the only defensible backfill.
- **The stddev anomaly check (`AnomalyDetector` in `Core/Validation/`) is a pure static
  function taking a history list and a candidate value, kept independent of the DB/service
  layer** — mirrors the general principle (story 11's day-count solver call-out) of
  isolating pure calc logic so it's directly unit-testable against synthetic series.
  Constants: `MinHistoryPoints = 5` (fewer points can't produce a meaningful stddev, so a
  new value is never flagged against too-thin a baseline — including a series' very first
  value); `ThresholdStdDevs = 3.0` (the conventional outlier cutoff, ~99.7% of normally
  distributed values fall within 3 stddevs — catches real jumps without flagging ordinary
  day-to-day volatility); `TrailingWindowDays = 90` (a few months of history is enough to
  characterize current volatility without diluting it with a stale, possibly
  different-regime past). A perfectly flat history (stddev 0) flags any change at all —
  the only sane interpretation when there's no observed variation to compare against.
- **`FxRateService`/`SecurityPriceService` classify a newly-fetched value using only the
  *valid* rows in its trailing window** (pending/rejected rows already in that window are
  excluded from the mean/stddev calculation) — an unreviewed or bad prior value shouldn't
  be allowed to normalize the baseline that judges the next one.
- **When the only stored row for an exact date is `PendingValidation`/`Rejected`,
  `GetRateAsync`/`GetPriceAsync` neither return it nor re-insert a second row for that
  date** (the unique pair/date and security/date indexes would reject a duplicate) — they
  fetch straight from the live provider and return that instead, leaving the stored row
  untouched for manual review. This keeps a calculation usable in the meantime without
  silently trusting an unreviewed value or failing outright. `GetHistoryAsync` on both
  services filters to `Valid` rows, since a stored range read (unlike the live-fetch path)
  has no live fallback to reach for.
- **Rejecting/correcting a pending value reuses the same status field and row** —
  `IFxRateRepository`/`ISecurityPriceRepository.UpdateStatusAsync(id, status,
  correctedValue?)` sets the status and optionally overwrites the stored value in one call,
  rather than modeling a correction as a new row or a separate audit table. This story only
  needs "accept as-is, correct the value, or reject" — no history of what a value used to
  be before correction, so there's nothing yet to justify a separate audit trail.
- **The validation-review page (`ValidationReview.razor`) calls `IFxRateRepository`/
  `ISecurityPriceRepository` directly, not through an Application service** — listing
  pending rows and updating a status/value is plain CRUD per CLAUDE.md's Gui/Application
  boundary; the actual logic (the stddev classification) lives in the services, not here.
- **Saved screen layouts (column order/visibility, sort) live in their own
  `UiLayoutSetting` table (`ScreenKey` + `LayoutJson`), not in the `AppSettings` singleton
  row from story 05.** `AppSettings` is deliberately a fixed `Id = 1` row for one global
  setting (base currency); a per-screen layout is keyed by screen identifier and there can
  be many screens, so it needs its own key rather than overloading the singleton row or
  adding screen-specific columns to it. `TransactionsReport.razor` (story 08) reads/writes
  it directly, same CRUD-through-Gui reasoning as `ValidationReview.razor` above — no
  Application-layer logic involved in saving a layout blob.
- **`ISecurityTransactionRepository.GetAllAsync`/`ICashTransactionRepository.GetAllAsync`
  eager-load `Position.Account`/`Position.Security` and `Account` respectively** — the
  transactions list report (story 08) needs the account name and security symbol as
  display columns for every row, and a report screen listing everything is the first real
  caller that needs "all rows," unlike the existing range/security/account-scoped
  queries.
- **`YahooFinanceSecurityPriceProvider` (Yahoo Finance's public, undocumented "chart"
  endpoint — the same one `yfinance` wraps) is the first/only `ISecurityPriceProvider`
  implementation**, closing the open question left by story 04. Chosen over Stooq's CSV
  quote endpoint: Stooq requires a real, working exchange-suffix mapping from this app's
  plain `Symbol` to a Stooq ticker (e.g. `.US`) before it can be trusted for anything beyond
  US tickers, whereas Yahoo's plain `Security.Symbol` already works unmodified for
  US-listed tickers (this app's common case, since transactions are imported from a US
  broker) with no mapping step to get wrong. The endpoint requires a browser-like
  `User-Agent` header (Yahoo's edge otherwise returns 429 on the very first request — a bot
  filter, not a rate limit) and returns a structured JSON error body even on a 404 status,
  so the response body is always parsed before checking the status code. Confirmed against
  real tickers (AAPL, SPY) and a nonexistent one before committing.
- **Non-US listings and any symbol/currency Yahoo doesn't recognize are a per-call
  `PriceStatus.UnsupportedSecurity`, not worked around with a mapping table up front** —
  mirrors the `FrankfurterFxRateProvider`/`WorldBankInflationRateProvider` precedent of
  accepting a real, known coverage gap rather than pre-building for exchanges no story
  needs priced yet.
- **One specific instance of that gap is handled rather than left unsupported: Yahoo quotes
  London-listed securities in pence (`meta.currency` = `"GBp"`), not pounds.** A request for
  a security whose `Currency` is `GBP` (already in `SupportedCurrencies.Codes`, and a pair
  `FrankfurterFxRateProvider` already prices against USD/EUR) against a pence-quoted symbol
  converts the close price ÷100 on the fly instead of being rejected as a currency mismatch
  — worth the small amount of special-casing because GBP is a currency this app already
  models end-to-end (base-currency picker, FX conversion), unlike an arbitrary unmapped
  exchange suffix. The comparison distinguishing `"GBp"` from `"GBP"` is ordinal/case-sensitive
  on purpose — Yahoo's two codes differ only by case and mean different things (pence vs.
  pounds), so a case-insensitive check would silently misprice a plain GBP-quoted security by
  100x. Confirmed for real against VOD.L before writing the conversion.
- **`PositionValuationService` (Application layer) derives current holdings and values
  them in base currency for the portfolio value report (story 09).** Position derivation
  (`GetCurrentPositionsAsync`) is exposed as its own public step, separate from valuation,
  per the story's Technical Note that later reports (10/11) will need "what's currently
  held" without necessarily wanting a valuation alongside it — net quantity per position is
  Buy/TransferIn minus Sell quantities (Dividend/Tax never carry a quantity), and a fully
  sold position (net quantity == 0) is dropped rather than shown at zero.
- **"Current price"/"latest valid FX rate" for the portfolio value report resolves by
  walking backwards up to 7 calendar days from the requested as-of date, using the first
  `Success` result.** Prices and FX rates are on-demand-fetched series with real gaps
  (weekends, holidays, a source with no data yet for "today"), and `SecurityPriceService`/
  `FxRateService` only expose exact-date lookups — a general "as-of" resolution system
  wasn't needed, just enough lookback to clear a single holiday weekend without walking
  back so far the figure stops being "current." No caller existed yet that needed this, so
  it lives in `PositionValuationService`, not as a new method on the two services.
- **A position whose price or FX conversion doesn't come back `Success` is excluded from
  the portfolio value report's grand total and visually flagged, rather than shown with a
  caveat alongside a number.** `SecurityPriceService`/`FxRateService.GetPriceAsync`/
  `GetRateAsync` already refuse to return a stored pending/rejected value (story 07) and
  fetch live instead — so by the time `PositionValuationService` sees a non-`Success`
  status, that's a real, current failure (unsupported security, network error, or the live
  refetch itself came back pending/unusable), not a stale flag to second-guess; there's
  nothing left to reuse into an inline caveat, only a hole to leave out of the sum.
