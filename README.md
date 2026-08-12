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
- `CashTransactions`: `acctAlias`, `type`, `currency`, `symbol`, `amount`, `dateTime`
- `Transfers`: `type`, `direction`, `currency`, `symbol`, `quantity`, `dateTime`
