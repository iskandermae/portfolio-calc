using System.Globalization;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Import.T212;
using PortfolioCalc.Core.Repositories;
using static PortfolioCalc.App.Application.Import.T212.T212Constants;

namespace PortfolioCalc.App.Application.Import.T212;

/// <summary>Maps raw rows from an <see cref="IT212TransactionImporter"/> onto the local
/// domain model. Unlike IBKR (story 02), the T212 export carries no account identity of
/// its own, so the target <see cref="Account"/> is chosen by the caller (the GUI's account
/// picker) rather than resolved from the file — see doc/decisions.md.</summary>
public class T212ImportService(
    IT212TransactionImporter importer,
    IAccountRepository accountRepository,
    ISecurityRepository securityRepository,
    IPositionRepository positionRepository,
    ICashTransactionRepository cashTransactionRepository,
    ISecurityTransactionRepository securityTransactionRepository)
{
    public async Task<ImportResult> ImportAsync(Stream csvStream, int accountId)
    {
        var account = await accountRepository.GetByIdAsync(accountId)
            ?? throw new InvalidOperationException($"Account {accountId} not found.");
        var data = await importer.ParseAsync(csvStream);
        var result = new ImportResult();

        foreach (var row in data.Rows)
            await ProcessRowAsync(row, account, result);

        return result;
    }

    private async Task ProcessRowAsync(T212RawRow row, Account account, ImportResult result)
    {
        var action = row.Get("Action") ?? "";

        switch (action)
        {
            case ActionDeposit:
                await ProcessCashRowAsync(row, account, result, CashTransactionType.Deposit);
                break;
            case ActionInterestOnCash:
                await ProcessCashRowAsync(row, account, result, CashTransactionType.Interest);
                break;
            case ActionDividend:
                await ProcessDividendAsync(row, account, result);
                break;
            default:
                if (BuyActions.Contains(action))
                    await ProcessTradeAsync(row, account, result, SecurityTransactionType.Buy);
                else if (SellActions.Contains(action))
                    await ProcessTradeAsync(row, account, result, SecurityTransactionType.Sell);
                else
                    result.Unrecognized.Add(new SkippedRow("Row", $"Unrecognized action '{action}'.", row.Fields));
                break;
        }
    }

    private async Task ProcessCashRowAsync(T212RawRow row, Account account, ImportResult result, CashTransactionType type)
    {
        var amount = ParseDecimal(row.Get("Total"));
        var currency = row.Get("Currency (Total)") ?? "";
        var date = ParseDate(row.Get("Time (UTC)"));

        if (await AlreadyImportedAsync(account, amount, date, currency))
            return;

        var transaction = new CashTransaction
        {
            AccountId = account.Id,
            Type = type,
            Date = date,
            Amount = amount,
            Currency = currency,
        };
        await cashTransactionRepository.AddAsync(transaction);
        result.Imported.Add($"{type} {amount} {currency} on {date:yyyy-MM-dd}");
    }

    private async Task ProcessTradeAsync(T212RawRow row, Account account, ImportResult result, SecurityTransactionType type)
    {
        var ticker = row.Get("Ticker") ?? "";
        var date = ParseDate(row.Get("Time (UTC)"));
        var quantity = ParseDecimal(row.Get("No. of shares"));
        var (price, currency) = NormalizePrice(row.Get("Price / share"), row.Get("Currency (Price / share)"));
        var principal = quantity * price;
        var amount = type == SecurityTransactionType.Buy ? -principal : principal;

        var security = await ResolveSecurityAsync(ticker, currency);
        var position = await ResolvePositionAsync(account, security);

        if (await AlreadyImportedAsync(position.Id, amount, date, currency))
            return;

        // T212 reports its conversion fee as a positive charge, always in the account's
        // own currency (GBP in the sample) regardless of the security's trading currency —
        // negated here since FeeAmount can't be positive (see doc/decisions.md).
        var fee = ParseDecimalOrNull(row.Get("Currency conversion fee"));
        var feeCurrency = row.Get("Currency (Currency conversion fee)");

        var transaction = new SecurityTransaction
        {
            PositionId = position.Id,
            Type = type,
            Date = date,
            Quantity = quantity,
            Amount = amount,
            Currency = currency,
            FeeAmount = fee is > 0 ? -fee : null,
            FeeCurrency = fee is > 0 ? feeCurrency : null,
        };
        await securityTransactionRepository.AddAsync(transaction);
        result.Imported.Add($"{type} {quantity} {ticker} ({currency}) @ {price} on {date:yyyy-MM-dd}");
    }

    /// <summary>T212 puts the gross dividend and its withholding tax on the same row,
    /// unlike IBKR's separate rows for the two (see doc/decisions.md) — no aggregation
    /// pass needed.</summary>
    private async Task ProcessDividendAsync(T212RawRow row, Account account, ImportResult result)
    {
        var ticker = row.Get("Ticker") ?? "";
        var date = ParseDate(row.Get("Time (UTC)"));
        var quantity = ParseDecimal(row.Get("No. of shares"));
        var (price, currency) = NormalizePrice(row.Get("Price / share"), row.Get("Currency (Price / share)"));
        var grossDividend = quantity * price;

        var security = await ResolveSecurityAsync(ticker, currency);
        var position = await ResolvePositionAsync(account, security);

        if (await AlreadyImportedAsync(position.Id, grossDividend, date, currency))
            return;

        var withholdingTax = ParseDecimalOrNull(row.Get("Withholding tax"));

        var transaction = new SecurityTransaction
        {
            PositionId = position.Id,
            Type = SecurityTransactionType.Dividend,
            Date = date,
            Amount = grossDividend,
            Currency = currency,
            TaxAmount = withholdingTax is > 0 ? -withholdingTax : null,
        };
        await securityTransactionRepository.AddAsync(transaction);
        result.Imported.Add($"Dividend {grossDividend} {currency} ({ticker}) on {date:yyyy-MM-dd}");
    }

    /// <summary>Dedup key: Account + Security + Amount + Date + Currency, applied to the
    /// mapped transaction (not raw rows) — same rule as IBKR (see doc/decisions.md).</summary>
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

    /// <summary>The CSV has no listing exchange, so a newly created GBP-traded security is
    /// guessed to be <see cref="ExchangeGbpGuess"/>; other currencies get none. An existing
    /// Security is returned untouched. See doc/decisions.md.</summary>
    private async Task<Security> ResolveSecurityAsync(string ticker, string currency)
    {
        var existing = await securityRepository.GetBySymbolAndCurrencyAsync(ticker, currency);
        if (existing is not null)
            return existing;

        return await securityRepository.AddAsync(new Security
        {
            Symbol = ticker,
            Currency = currency,
            Name = ticker,
            Exchange = currency == CurrencyGbp ? ExchangeGbpGuess : null,
        });
    }

    private async Task<Position> ResolvePositionAsync(Account account, Security security) =>
        await positionRepository.GetByAccountAndSecurityAsync(account.Id, security.Id)
        ?? await positionRepository.AddAsync(new Position { AccountId = account.Id, SecurityId = security.Id });

    /// <summary>T212 quotes some GBP securities' price-per-share in pence ("GBX") — the
    /// same pence/pounds split Yahoo reports as "GBp" (see doc/decisions.md) — so the price
    /// is divided by 100 and the trading currency normalized to "GBP".</summary>
    private static (decimal Price, string Currency) NormalizePrice(string? rawPrice, string? rawCurrency)
    {
        var price = ParseDecimal(rawPrice);
        var currency = rawCurrency ?? "";
        return currency == CurrencyGbx ? (price / 100m, CurrencyGbp) : (price, currency);
    }

    private static decimal ParseDecimal(string? value) =>
        decimal.Parse(string.IsNullOrWhiteSpace(value) ? "0" : value, CultureInfo.InvariantCulture);

    private static decimal? ParseDecimalOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : decimal.Parse(value, CultureInfo.InvariantCulture);

    private static DateOnly ParseDate(string? timeUtc) =>
        DateOnly.FromDateTime(DateTimeOffset.Parse(timeUtc ?? "", CultureInfo.InvariantCulture).UtcDateTime);
}
