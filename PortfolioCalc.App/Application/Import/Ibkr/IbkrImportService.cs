using System.Globalization;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Import;
using PortfolioCalc.Core.Import.Ibkr;
using PortfolioCalc.Core.Repositories;
using static PortfolioCalc.App.Application.Import.Ibkr.IbkrConstants;

namespace PortfolioCalc.App.Application.Import.Ibkr;

/// <summary>Maps raw rows from an <see cref="ITransactionImporter"/> onto the local
/// domain model: resolves/auto-creates Account, Security (keyed by Symbol + Currency) and
/// Position, and persists the resulting transactions. See doc/decisions.md for the
/// account/security matching rules.</summary>
public class IbkrImportService(
    ITransactionImporter importer,
    IAccountRepository accountRepository,
    ISecurityRepository securityRepository,
    IVocabularyRepository vocabularyRepository,
    IPositionRepository positionRepository,
    ICashTransactionRepository cashTransactionRepository,
    ISecurityTransactionRepository securityTransactionRepository)
{
    public async Task<ImportResult> ImportAsync(Stream xmlStream)
    {
        var data = await importer.ParseAsync(xmlStream);
        var result = new ImportResult();
        var account = await ResolveAccountAsync(data);
        var exchangeSuffixes = await GetExchangeSuffixesAsync();

        foreach (var row in data.Trades)
            await ProcessTradeAsync(row, account, result, exchangeSuffixes);

        foreach (var row in data.CashTransactions)
            await ProcessCashTransactionAsync(row, account, result, exchangeSuffixes);

        await ProcessDividendsAsync(data, account, result, exchangeSuffixes);

        foreach (var row in data.Transfers)
            await ProcessTransferAsync(row, account, result, exchangeSuffixes);

        foreach (var row in data.UnhandledSectionRows)
            result.Unrecognized.Add(new SkippedRow(row.ElementName,
                "Row is from a section this importer doesn't process (TransactionTaxes/TradeTransfers/CorporateActions).",
                row.Attributes));

        return result;
    }

    /// <summary>Groups dividend/payment-in-lieu/withholding-tax cash rows by
    /// (Security, Date) and emits one Dividend (or, if there's tax with no matching
    /// dividend, one Tax) SecurityTransaction per group. See doc/decisions.md.</summary>
    private async Task ProcessDividendsAsync(
        IbkrFlexQueryImportData data,
        Account account,
        ImportResult result,
        IReadOnlyCollection<string> exchangeSuffixes)
    {
        var groups = data.CashTransactions
            .Where(r => DividendGroupTypes.Contains(r.Get("type") ?? ""))
            .GroupBy(r => (
                SecuritySymbol: r.Get("symbol") ?? "",
                Currency: r.Get("currency") ?? "",
                Date: ParseDate(r.Get("dateTime"))));

        foreach (var group in groups)
        {
            var rows = group.ToList();
            var grossDividend = rows
                .Where(r => r.Get("type") is CashTypeDividends or CashTypePaymentInLieuOfDividends)
                .Sum(r => ParseDecimal(r.Get("amount")));
            var taxSigned = rows
                .Where(r => r.Get("type") == CashTypeWithholdingTax)
                .Sum(r => ParseDecimal(r.Get("amount")));

            // All rows in a (Security, Date) group are the same security; the first
            // non-empty listingExchange seen among them is authoritative (see
            // ResolveSecurityAsync).
            var exchange = rows.Select(r => r.Get("listingExchange")).FirstOrDefault(e => !string.IsNullOrEmpty(e));
            var securitySymbolWithoutExchange = NormalizeSecuritySymbol(group.Key.SecuritySymbol, exchangeSuffixes);
            var security = await ResolveSecurityAsync(securitySymbolWithoutExchange, group.Key.Currency, exchange);
            var position = await ResolvePositionAsync(account, security);

            if (grossDividend != 0)
            {
                if (await AlreadyImportedAsync(position.Id, grossDividend, group.Key.Date, group.Key.Currency))
                    continue;

                // Tax is kept separate from FeeAmount (a broker commission) — see
                // doc/decisions.md. Sign is preserved as-is from the file.
                var transaction = new SecurityTransaction
                {
                    PositionId = position.Id,
                    Type = SecurityTransactionType.Dividend,
                    Date = group.Key.Date,
                    Amount = grossDividend,
                    Currency = group.Key.Currency,
                    TaxAmount = taxSigned != 0 ? taxSigned : null,
                };
                await securityTransactionRepository.AddAsync(transaction);
                result.Imported.Add($"Dividend {grossDividend} {group.Key.Currency} ({group.Key.SecuritySymbol} as {securitySymbolWithoutExchange}) on {group.Key.Date:yyyy-MM-dd}");
            }
            else if (taxSigned != 0)
            {
                if (await AlreadyImportedAsync(position.Id, taxSigned, group.Key.Date, group.Key.Currency))
                    continue;

                var transaction = new SecurityTransaction
                {
                    PositionId = position.Id,
                    Type = SecurityTransactionType.Tax,
                    Date = group.Key.Date,
                    Amount = taxSigned,
                    Currency = group.Key.Currency,
                };
                await securityTransactionRepository.AddAsync(transaction);
                result.Imported.Add($"Tax {taxSigned} {group.Key.Currency} ({group.Key.SecuritySymbol}) on {group.Key.Date:yyyy-MM-dd}");
            }
        }
    }

    private async Task<Account> ResolveAccountAsync(IbkrFlexQueryImportData data)
    {
        var alias = data.Trades.Concat(data.CashTransactions).Concat(data.Transfers)
            .Select(r => r.Get("acctAlias"))
            .FirstOrDefault(a => !string.IsNullOrWhiteSpace(a));
        var name = !string.IsNullOrWhiteSpace(alias) ? alias! : data.StatementAccountId;

        return await accountRepository.GetByNameAsync(name)
            ?? await accountRepository.AddAsync(new Account { Name = name });
    }

    private async Task ProcessTradeAsync(
        IbkrRawRow row,
        Account account,
        ImportResult result,
        IReadOnlyCollection<string> exchangeSuffixes)
    {
        var transactionType = row.Get("transactionType") ?? "";
        var securityId = row.Get("securityID") ?? "";
        var securitySymbolFull = row.Get("symbol") ?? "";

        if (string.IsNullOrEmpty(securityId) && CurrencyPairSymbol.IsMatch(securitySymbolFull))
        {
            result.RecognizedButSkipped.Add(new SkippedRow(row.ElementName,
                "IBKR currency-conversion trade, not a security trade.", row.Attributes));
            return;
        }

        if (transactionType != TradeTransactionTypeExchTrade || string.IsNullOrEmpty(securityId))
        {
            result.Unrecognized.Add(new SkippedRow(row.ElementName,
                $"Unrecognized trade row (transactionType='{transactionType}', securityID='{securityId}').", row.Attributes));
            return;
        }

        var currency = row.Get("currency") ?? "";

        var buySell = row.Get("buySell");
        var type = buySell switch
        {
            BuySellBuy => SecurityTransactionType.Buy,
            BuySellSell => SecurityTransactionType.Sell,
            _ => (SecurityTransactionType?)null,
        };
        if (type is null)
        {
            result.Unrecognized.Add(new SkippedRow(row.ElementName,
                $"Unrecognized buySell value '{buySell}'.", row.Attributes));
            return;
        }
        var securitySymbolWithoutExchange = NormalizeSecuritySymbol(securitySymbolFull, exchangeSuffixes);
        var security = await ResolveSecurityAsync(securitySymbolWithoutExchange, currency, row.Get("listingExchange"));
        var position = await ResolvePositionAsync(account, security);

        var quantity = Math.Abs(ParseDecimal(row.Get("quantity")));
        var price = ParseDecimal(row.Get("tradePrice"));
        // Preserve the sign IBKR reports for commission as-is (it's already negative in
        // the export) rather than taking Math.Abs — see doc/decisions.md sign convention.
        var commission = ParseDecimal(row.Get("ibCommission"));
        var commissionCurrency = row.Get("ibCommissionCurrency");
        var date = ParseDate(row.Get("dateTime"));
        var principal = quantity * price;
        var amount = type.Value == SecurityTransactionType.Buy ? -principal : principal;

        if (await AlreadyImportedAsync(position.Id, amount, date, currency))
            return;

        var transaction = new SecurityTransaction
        {
            PositionId = position.Id,
            Type = type.Value,
            Date = date,
            Quantity = quantity,
            Amount = amount,
            Currency = currency,
            FeeAmount = commission != 0 ? commission : null,
            FeeCurrency = commission != 0 ? commissionCurrency : null,
        };
        await securityTransactionRepository.AddAsync(transaction);
        result.Imported.Add($"{type.Value} {quantity} {securitySymbolFull} (as {securitySymbolWithoutExchange}) ({currency}) @ {price} on {date:yyyy-MM-dd}");
    }

    private async Task ProcessCashTransactionAsync(
        IbkrRawRow row,
        Account account,
        ImportResult result,
        IReadOnlyCollection<string> exchangeSuffixes)
    {
        var type = row.Get("type") ?? "";
        var currency = row.Get("currency") ?? "";
        var amount = ParseDecimal(row.Get("amount"));
        var date = ParseDate(row.Get("dateTime"));

        switch (type)
        {
            case CashTypeDepositsWithdrawals:
            {
                // IBKR already encodes direction via the amount's sign for this type.
                var cashType = amount >= 0 ? CashTransactionType.Deposit : CashTransactionType.Withdrawal;
                if (await AlreadyImportedAsync(account, amount, date, currency))
                    break;
                var transaction = new CashTransaction
                {
                    AccountId = account.Id,
                    Type = cashType,
                    Date = date,
                    Amount = amount,
                    Currency = currency,
                };
                await cashTransactionRepository.AddAsync(transaction);
                result.Imported.Add($"{cashType} {amount} {currency} on {date:yyyy-MM-dd}");
                break;
            }
            case CashTypeBrokerInterest:
            {
                if (await AlreadyImportedAsync(account, amount, date, currency))
                    break;
                var transaction = new CashTransaction
                {
                    AccountId = account.Id,
                    Type = CashTransactionType.Interest,
                    Date = date,
                    Amount = amount,
                    Currency = currency,
                };
                await cashTransactionRepository.AddAsync(transaction);
                result.Imported.Add($"Interest {amount} {currency} on {date:yyyy-MM-dd}");
                break;
            }
            case CashTypeDividends:
            case CashTypePaymentInLieuOfDividends:
            case CashTypeWithholdingTax:
                // Handled by grouping (see the dividend-aggregation pass), not row-by-row.
                break;
            default:
                result.Unrecognized.Add(new SkippedRow(row.ElementName,
                    $"Unrecognized cash transaction type '{type}'.", row.Attributes));
                break;
        }
    }

    private async Task ProcessTransferAsync(
        IbkrRawRow row,
        Account account,
        ImportResult result,
        IReadOnlyCollection<string> exchangeSuffixes)
    {
        var transferType = row.Get("type") ?? "";
        var direction = row.Get("direction") ?? "";

        // Only in-kind "FOP IN" transfers are handled — no real case for "OUT" (or any
        // other type) has come up yet (YAGNI); reported as unrecognized until one does.
        if (transferType != TransferTypeFop || direction != TransferDirectionIn)
        {
            result.Unrecognized.Add(new SkippedRow(row.ElementName,
                $"Unrecognized transfer row (type='{transferType}', direction='{direction}').", row.Attributes));
            return;
        }

        var currency = row.Get("currency") ?? "";
        var securitySymbol = row.Get("symbol") ?? "";
        var quantity = Math.Abs(ParseDecimal(row.Get("quantity")));
        var date = ParseDate(row.Get("dateTime"));

        var securitySymbolWithoutExchange = NormalizeSecuritySymbol(securitySymbol, exchangeSuffixes);
        var security = await ResolveSecurityAsync(securitySymbolWithoutExchange, currency, row.Get("listingExchange"));
        var position = await ResolvePositionAsync(account, security);

        // TransferIn always has Amount = 0, so the dedup key (Position/Amount/Date/
        // Currency) alone can't tell two different-quantity transfers on the same day
        // apart — no such case exists in the sample; accepted as a known limitation.
        if (await AlreadyImportedAsync(position.Id, 0m, date, currency))
            return;

        var transaction = new SecurityTransaction
        {
            PositionId = position.Id,
            Type = SecurityTransactionType.TransferIn,
            Date = date,
            Quantity = quantity,
            Amount = 0m,
            Currency = currency,
        };
        await securityTransactionRepository.AddAsync(transaction);
        result.Imported.Add($"TransferIn {quantity} {securitySymbol} (as {securitySymbolWithoutExchange}) ({currency}) on {date:yyyy-MM-dd}");
    }

    /// <summary>Dedup key: Account + Security + Amount + Date + Currency, applied to the
    /// mapped transaction (not raw rows) — see doc/decisions.md. For SecurityTransaction,
    /// Position already encodes Account + Security.</summary>
    private async Task<bool> AlreadyImportedAsync(int positionId, decimal amount, DateOnly date, string currency)
    {
        var existing = await securityTransactionRepository.GetByPositionAsync(positionId);
        return existing.Any(t => t.Amount == amount && t.Date == date && t.Currency == currency);
    }

    private async Task<bool> AlreadyImportedAsync(Account account, decimal amount, DateOnly date, string currency)
    {
        var existing = await cashTransactionRepository.GetByAccountAsync(account.Id);
        return existing.Any(t => t.Amount == amount && t.Date == date && t.Currency == currency);
    }

    /// <summary>Resolves/auto-creates a Security, threading through the row's raw
    /// listingExchange code (if present). A newly-created Security gets it directly; an
    /// existing Security with no Exchange recorded yet (e.g. imported before this field
    /// existed) is backfilled in place. An existing Security that already has an
    /// Exchange is left untouched — the first non-empty value ever seen is authoritative,
    /// so a later row can't flip-flop it. See doc/decisions.md.</summary>
    private async Task<Security> ResolveSecurityAsync(string securitySymbol, string currency, string? exchange)
    {
        var existing = await securityRepository.GetBySymbolAndCurrencyAsync(securitySymbol, currency);
        if (existing is null)
        {
            return await securityRepository.AddAsync(new Security
            {
                Symbol = securitySymbol,
                Currency = currency,
                Name = securitySymbol,
                Exchange = string.IsNullOrEmpty(exchange) ? null : exchange,
            });
        }

        if (string.IsNullOrEmpty(existing.Exchange) && !string.IsNullOrEmpty(exchange))
        {
            existing.Exchange = exchange;
            await securityRepository.UpdateAsync(existing);
        }

        return existing;
    }

    private async Task<Position> ResolvePositionAsync(Account account, Security security) =>
        await positionRepository.GetByAccountAndSecurityAsync(account.Id, security.Id)
        ?? await positionRepository.AddAsync(new Position { AccountId = account.Id, SecurityId = security.Id });

    private async Task<IReadOnlyCollection<string>> GetExchangeSuffixesAsync()
    {
        var entries = await vocabularyRepository.GetByTypeAsync(VocabularyTypes.ExchangeYahooSuffix);
        return entries
            .Select(entry => entry.Value)
            .Where(value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeSecuritySymbol(string securitySymbol, IReadOnlyCollection<string> exchangeSuffixes)
    {
        if (string.IsNullOrEmpty(securitySymbol))
            return securitySymbol;

        if (!securitySymbol.Contains('.'))
            return securitySymbol;

        foreach (var suffix in exchangeSuffixes)
        {
            if (securitySymbol.EndsWith(suffix, StringComparison.Ordinal) && securitySymbol.Length > suffix.Length)
                return securitySymbol[..^suffix.Length];
        }

        return securitySymbol;
    }

    private static decimal ParseDecimal(string? value) =>
        decimal.Parse(value ?? "0", CultureInfo.InvariantCulture);

    private static DateOnly ParseDate(string? dateTime)
    {
        var datePart = (dateTime ?? "").Split(';')[0];
        return DateOnly.ParseExact(datePart, "yyyyMMdd", CultureInfo.InvariantCulture);
    }
}
