# Story: Import Transactions from IBKR Export

## Description
Allow importing cash and security transactions from a file exported by Interactive
Brokers (IBKR), mapping rows onto the local transaction model (story 01).

## Acceptance Criteria
- [ ] A file picker in the GUI lets the user select an IBKR export file.
- [ ] The parser reads the file and produces `CashTransaction`/`SecurityTransaction`
      records via the repository from story 01.
- [ ] Duplicate imports (re-importing the same file/overlapping date range) do not create
      duplicate transactions.
- [ ] Malformed/unrecognized rows are reported to the user rather than silently skipped
      or crashing the import.
- [ ] Integration tests parse at least one real (anonymized) sample export file.

## Technical Notes
- Parser lives in `Data/` behind an `Core`-defined import interface (e.g.
  `ITransactionImporter`), so a second broker's format could be added later without
  touching application/UI code.
- Get a real sample export before finalizing the parser — see open question below.

## Dependencies / Open Questions
- Depends on [01-transaction-data-storage](01-transaction-data-storage.md).
- **Open question:** exact IBKR export format (Activity Statement CSV vs. Flex Query
  XML/CSV) was not decided during planning — confirm against an actual sample file
  before/while implementing this story.
