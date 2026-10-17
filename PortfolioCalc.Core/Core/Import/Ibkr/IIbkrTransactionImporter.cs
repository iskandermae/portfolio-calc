namespace PortfolioCalc.Core.Import.Ibkr;

/// <summary>Parses an IBKR Flex Query XML export into raw rows. Implementation lives in
/// Data/. Mapping the raw rows onto the domain model (account/security resolution,
/// dividend aggregation, dedup) is Application-layer logic. Async because the GUI hands
/// this a browser/WebView file stream that only supports asynchronous reads (see
/// doc/decisions.md) — never fall back to a synchronous parse.</summary>
public interface IIbkrTransactionImporter
{
    Task<IbkrFlexQueryImportData> ParseAsync(Stream xmlStream, CancellationToken cancellationToken = default);
}
