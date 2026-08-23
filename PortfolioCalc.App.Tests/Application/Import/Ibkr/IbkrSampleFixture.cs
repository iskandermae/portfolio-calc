using System.Text;

namespace PortfolioCalc.App.Tests.Application.Import.Ibkr;

/// <summary>A fully synthetic IBKR Flex Query XML export, used by every IBKR import test
/// instead of a real personal export file — no real account, security, or transaction data
/// belongs in this repository (see doc/decisions.md and CLAUDE.md). Covers the same shapes
/// the real importer/mapper handle: ordinary buy/sell trades (one with a fee), IBKR's
/// currency-conversion "trades" (symbol like "EUR.GBP", no securityID), deposits, broker
/// interest, a dividend aggregated with its withholding tax, a standalone withholding tax
/// with no dividend in its group, and an in-kind transfer-in.</summary>
internal static class IbkrSampleFixture
{
    public const string AccountAlias = "test_account";

    public const string Xml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <FlexQueryResponse>
          <FlexStatements>
            <FlexStatement accountId="TESTACCT001">
              <Trades>
                <Trade acctAlias="test_account" currency="GBP" symbol="TSTA" securityID="TESTISIN01" listingExchange="LSEETF" transactionType="ExchTrade" buySell="BUY" quantity="100" tradePrice="20" ibCommission="-2.5" ibCommissionCurrency="GBP" dateTime="20260706" />
                <Trade acctAlias="test_account" currency="USD" symbol="TSTB" securityID="TESTISIN02" listingExchange="ARCA" transactionType="ExchTrade" buySell="SELL" quantity="15" tradePrice="600" ibCommission="0" ibCommissionCurrency="USD" dateTime="20260710" />
                <Trade acctAlias="test_account" currency="USD" symbol="TSTC" securityID="TESTISIN03" listingExchange="NASDAQ" transactionType="ExchTrade" buySell="BUY" quantity="10" tradePrice="50" ibCommission="0" ibCommissionCurrency="USD" dateTime="20260105" />
                <Trade acctAlias="test_account" currency="USD" symbol="TSTC" securityID="TESTISIN03" listingExchange="NASDAQ" transactionType="ExchTrade" buySell="BUY" quantity="10" tradePrice="55" ibCommission="0" ibCommissionCurrency="USD" dateTime="20260205" />
                <Trade acctAlias="test_account" currency="EUR" symbol="TSTD" securityID="TESTISIN04" listingExchange="IBIS" transactionType="ExchTrade" buySell="BUY" quantity="20" tradePrice="30" ibCommission="0" ibCommissionCurrency="EUR" dateTime="20260305" />
                <Trade acctAlias="test_account" currency="EUR" symbol="TSTD" securityID="TESTISIN04" listingExchange="IBIS" transactionType="ExchTrade" buySell="SELL" quantity="5" tradePrice="35" ibCommission="0" ibCommissionCurrency="EUR" dateTime="20260310" />
                <Trade acctAlias="test_account" currency="EUR" symbol="TSTD" securityID="TESTISIN04" listingExchange="IBIS" transactionType="ExchTrade" buySell="SELL" quantity="5" tradePrice="36" ibCommission="0" ibCommissionCurrency="EUR" dateTime="20260315" />
                <Trade acctAlias="test_account" currency="USD" symbol="TSTE" securityID="TESTISIN05" listingExchange="NYSE" transactionType="ExchTrade" buySell="BUY" quantity="8" tradePrice="12" ibCommission="0" ibCommissionCurrency="USD" dateTime="20260401" />
                <Trade acctAlias="test_account" currency="USD" symbol="TSTE" securityID="TESTISIN05" listingExchange="NYSE" transactionType="ExchTrade" buySell="BUY" quantity="8" tradePrice="12.5" ibCommission="0" ibCommissionCurrency="USD" dateTime="20260402" />
                <Trade acctAlias="test_account" currency="GBP" symbol="TSTF" securityID="TESTISIN06" listingExchange="LSE" transactionType="ExchTrade" buySell="SELL" quantity="3" tradePrice="99" ibCommission="0" ibCommissionCurrency="GBP" dateTime="20260403" />
                <Trade acctAlias="test_account" currency="GBP" symbol="EUR.GBP" securityID="" listingExchange="" transactionType="ExchTrade" buySell="BUY" quantity="1" tradePrice="1" ibCommission="0" ibCommissionCurrency="GBP" dateTime="20260101" />
                <Trade acctAlias="test_account" currency="GBP" symbol="EUR.GBP" securityID="" listingExchange="" transactionType="ExchTrade" buySell="SELL" quantity="1" tradePrice="1" ibCommission="0" ibCommissionCurrency="GBP" dateTime="20260102" />
                <Trade acctAlias="test_account" currency="USD" symbol="EUR.USD" securityID="" listingExchange="" transactionType="ExchTrade" buySell="BUY" quantity="1" tradePrice="1" ibCommission="0" ibCommissionCurrency="USD" dateTime="20260103" />
                <Trade acctAlias="test_account" currency="USD" symbol="EUR.USD" securityID="" listingExchange="" transactionType="ExchTrade" buySell="SELL" quantity="1" tradePrice="1" ibCommission="0" ibCommissionCurrency="USD" dateTime="20260104" />
                <Trade acctAlias="test_account" currency="USD" symbol="GBP.USD" securityID="" listingExchange="" transactionType="ExchTrade" buySell="BUY" quantity="1" tradePrice="1" ibCommission="0" ibCommissionCurrency="USD" dateTime="20260106" />
                <Trade acctAlias="test_account" currency="USD" symbol="GBP.USD" securityID="" listingExchange="" transactionType="ExchTrade" buySell="SELL" quantity="1" tradePrice="1" ibCommission="0" ibCommissionCurrency="USD" dateTime="20260107" />
              </Trades>
              <CashTransactions>
                <CashTransaction acctAlias="test_account" currency="EUR" symbol="" dateTime="20260110" amount="1000" type="Deposits/Withdrawals" />
                <CashTransaction acctAlias="test_account" currency="USD" symbol="" dateTime="20260210" amount="2000" type="Deposits/Withdrawals" />
                <CashTransaction acctAlias="test_account" currency="GBP" symbol="" dateTime="20260310" amount="500" type="Deposits/Withdrawals" />
                <CashTransaction acctAlias="test_account" currency="USD" symbol="" dateTime="20260131" amount="10" type="Broker Interest Received" />
                <CashTransaction acctAlias="test_account" currency="EUR" symbol="" dateTime="20260228" amount="12" type="Broker Interest Received" />
                <CashTransaction acctAlias="test_account" currency="EUR" symbol="TSTG" securityID="TESTISIN07" listingExchange="IBIS" dateTime="20260519" amount="100" type="Dividends" />
                <CashTransaction acctAlias="test_account" currency="EUR" symbol="TSTG" securityID="TESTISIN07" listingExchange="IBIS" dateTime="20260519" amount="-20" type="Withholding Tax" />
                <CashTransaction acctAlias="test_account" currency="USD" symbol="TSTH" securityID="TESTISIN08" listingExchange="NYSE" dateTime="20241115" amount="5" type="Withholding Tax" />
                <CashTransaction acctAlias="test_account" currency="USD" symbol="TSTH" securityID="TESTISIN08" listingExchange="NYSE" dateTime="20241115" amount="-0.5" type="Withholding Tax" />
              </CashTransactions>
              <Transfers>
                <Transfer acctAlias="test_account" currency="EUR" symbol="TSTG" securityID="TESTISIN07" listingExchange="IBIS" dateTime="20260301" quantity="40" type="FOP" direction="IN" />
              </Transfers>
            </FlexStatement>
          </FlexStatements>
        </FlexQueryResponse>
        """;

    public static Stream OpenStream() => new MemoryStream(Encoding.UTF8.GetBytes(Xml));
}
