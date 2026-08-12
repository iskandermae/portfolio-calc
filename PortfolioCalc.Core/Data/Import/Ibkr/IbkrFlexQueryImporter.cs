using System.Xml;
using System.Xml.Linq;
using PortfolioCalc.Core.Import;
using PortfolioCalc.Core.Import.Ibkr;

namespace PortfolioCalc.Core.Data.Import.Ibkr;

/// <summary>Parses an IBKR Flex Query XML export into raw rows. Only the first
/// FlexStatement in the file is read (a file with more than one is not supported yet —
/// no real case for it has come up).</summary>
public class IbkrFlexQueryImporter : ITransactionImporter
{
    public async Task<IbkrFlexQueryImportData> ParseAsync(Stream xmlStream, CancellationToken cancellationToken = default)
    {
        // The GUI hands this a browser/WebView file stream whose synchronous Read()
        // throws "Synchronous reads are not supported." — XDocument.Load(Stream) reads
        // synchronously internally, so it must go through an async XmlReader instead.
        using var xmlReader = XmlReader.Create(xmlStream, new XmlReaderSettings { Async = true });
        var document = await XDocument.LoadAsync(xmlReader, LoadOptions.None, cancellationToken);
        var statement = document.Root?.Element("FlexStatements")?.Element("FlexStatement")
            ?? throw new InvalidDataException("Not a recognizable IBKR Flex Query export: missing FlexStatement.");

        return new IbkrFlexQueryImportData
        {
            StatementAccountId = statement.Attribute("accountId")?.Value ?? "",
            Trades = ReadRows(statement, "Trades", "Trade"),
            CashTransactions = ReadRows(statement, "CashTransactions", "CashTransaction"),
            Transfers = ReadRows(statement, "Transfers", "Transfer"),
            UnhandledSectionRows =
            [
                .. ReadRows(statement, "TransactionTaxes", "TransactionTax"),
                .. ReadRows(statement, "TradeTransfers", "TradeTransfer"),
                .. ReadRows(statement, "CorporateActions", "CorporateAction"),
            ],
        };
    }

    private static List<IbkrRawRow> ReadRows(XElement statement, string sectionName, string rowElementName)
    {
        var section = statement.Element(sectionName);
        if (section is null)
            return [];

        return section.Elements(rowElementName)
            .Select(row => new IbkrRawRow(
                rowElementName,
                row.Attributes().ToDictionary(a => a.Name.LocalName, a => a.Value)))
            .ToList();
    }
}
