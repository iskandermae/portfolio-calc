# portfolio-calc
Personal portfolio calculator

## Run
`dotnet run --project PortfolioCalc.App`


If a local database gets into a bad state, reset it with:
`dotnet run --project PortfolioCalc.App -- --reset-db`
(deletes the local database file so it's recreated fresh — this is data loss).
