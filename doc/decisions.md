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
- **`ITransactionImporter.ParseAsync` parses via an async `XmlReader` +
  `XDocument.LoadAsync`, never `XDocument.Load(Stream)`.** The GUI hands the importer a
  Blazor `InputFile` stream over the browser/WebView bridge, whose synchronous `Read()`
  throws `"Synchronous reads are not supported."` — `XDocument.Load(Stream)` reads
  synchronously internally, so it works against `File.OpenRead()` in a test but breaks
  against the real GUI file stream. Caught by a test using a stream that mimics the
  browser stream's read behavior (throws on sync `Read`, only `ReadAsync` works) — a
  fully-seekable, sync-capable `FileStream` in a test can't surface this class of bug at
  all, no matter how thoroughly it's otherwise exercised.
