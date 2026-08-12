using PortfolioCalc.Core.Import.Ibkr;

namespace PortfolioCalc.Core.Import;

/// <summary>Parses a broker export file into raw, uninterpreted rows. Implementations
/// live in Data/ (one per broker/format). Mapping the raw rows onto the domain model
/// (account/security resolution, aggregation, dedup) is Application-layer logic.
/// Async because the GUI hands this a browser/WebView file stream that only supports
/// asynchronous reads (see doc/decisions.md) — never fall back to a synchronous parse.</summary>
public interface ITransactionImporter
{
    Task<IbkrFlexQueryImportData> ParseAsync(Stream xmlStream, CancellationToken cancellationToken = default);
}
