using System.Text;

namespace PortfolioCalc.App.Tests.Application.Import.T212;

/// <summary>A fully synthetic Trading 212 "Transaction history" CSV export, used by every
/// T212 import test instead of a real personal export file — no real account, security, or
/// transaction data belongs in this repository (see doc/decisions.md and CLAUDE.md). Covers
/// the same shapes the real importer/mapper handle: a deposit, interest on cash, a
/// USD-priced buy (with a currency-conversion fee) and sell on the same security, a
/// GBX(pence)-priced buy and sell on another security, a dividend with withholding tax on
/// the same row, and an action this importer doesn't recognize.</summary>
internal static class T212SampleFixture
{
    public const string Csv = """
        Action,Time (UTC),ISIN,Ticker,Name,Notes,ID,No. of shares,Price / share,Currency (Price / share),Exchange rate,Result,Currency (Result),Total,Currency (Total),Withholding tax,Currency (Withholding tax),Currency conversion fee,Currency (Currency conversion fee)
        Deposit,2026-01-10 10:00:00+00:00,,,,"Deposit",T212ID01,,,,,,,1000.00,GBP,,,,
        Interest on cash,2026-01-15 01:00:00+00:00,,,,Interest on cash,T212ID02,,,,,,,5.00,GBP,,,,
        Market buy,2026-02-01 09:30:00+00:00,TESTISIN01,TSTA,"Test A, Inc.",,ORDER001,10.0000000000,100.0000000000,USD,1.25000000,,,800.00,GBP,,,1.50,GBP
        Market sell,2026-03-01 09:30:00+00:00,TESTISIN01,TSTA,"Test A, Inc.",,ORDER002,4.0000000000,110.0000000000,USD,1.30000000,40.00,GBP,352.00,GBP,,,,
        Limit buy,2026-04-01 11:00:00+00:00,TESTISIN02,TSTB,"Test B",,ORDER003,50.0000000000,200.0000000000,GBX,1.00000000,,,100.00,GBP,,,,
        Limit sell,2026-05-01 11:00:00+00:00,TESTISIN02,TSTB,"Test B",,ORDER004,20.0000000000,250.0000000000,GBX,1.00000000,10.00,GBP,50.00,GBP,,,,
        Dividend (Dividend),2026-06-01 00:00:00+00:00,TESTISIN01,TSTA,"Test A, Inc.",,,6.0000000000,0.500000,USD,1.28000000,,,2.34,GBP,0.90,USD,,
        Withdrawal,2026-07-01 00:00:00+00:00,,,,"Withdrawal to bank",T212ID03,,,,,,,50.00,GBP,,,,
        """;

    public static Stream OpenStream() => new MemoryStream(Encoding.UTF8.GetBytes(Csv));
}
