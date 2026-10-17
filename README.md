# portfolio-calc
Personal portfolio calculator

## Run
`dotnet run --project PortfolioCalc.App`


If a local database gets into a bad state, reset it with:
`dotnet run --project PortfolioCalc.App -- --reset-db`
(deletes the local database file so it's recreated fresh — this is data loss).

## IBKR import format
Requires an **IBKR Flex Query XML** export (not Activity Statement CSV) with these
sections/fields:
- `FlexStatement`: `accountId`
- `Trades`: `transactionType`, `securityID`, `symbol`, `currency`, `buySell`, `quantity`,
  `tradePrice`, `ibCommission`, `ibCommissionCurrency`, `dateTime`
- `CashTransactions`: `type`, `currency`, `symbol`, `amount`, `dateTime`
- `Transfers`: `type`, `direction`, `currency`, `symbol`, `quantity`, `dateTime`

## Trading 212 import format
Requires a Trading 212 **"Transaction history" CSV** export with these columns: `Action`,
`Time (UTC)`, `Ticker`, `No. of shares`, `Price / share`, `Currency (Price / share)`,
`Total`, `Currency (Total)`, `Withholding tax`, `Currency (Withholding tax)`, `Currency
conversion fee`, `Currency (Currency conversion fee)`. The file carries no account identity
of its own, so the Import page asks you to pick (or create) the target account. Recognized
`Action` values: `Deposit`, `Interest on cash`, `Market buy`/`Limit buy`, `Market
sell`/`Limit sell`, `Dividend (Dividend)` — any other action is reported as unrecognized.
