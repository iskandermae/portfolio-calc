using Microsoft.EntityFrameworkCore;
using PortfolioCalc.App.Application.Import.Ibkr;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Data.Import.Ibkr;
using PortfolioCalc.Core.Data.Repositories;
using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.App.Tests.Application.Import.Ibkr;

public class IbkrImportServiceTests
{
    private static PortfolioDbContext CreateOpenInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<PortfolioDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var context = new PortfolioDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    private static IbkrImportService CreateService(PortfolioDbContext context) =>
        new(new IbkrFlexQueryImporter(),
            new AccountRepository(context),
            new SecurityRepository(context),
            new PositionRepository(context),
            new CashTransactionRepository(context),
            new SecurityTransactionRepository(context));

    private static string SamplePath => Path.Combine(AppContext.BaseDirectory, "samples", "all-total-export-2026.xml");

    [Fact]
    public async Task ImportAsync_imports_real_buy_and_sell_trades_from_the_sample_export()
    {
        using var context = CreateOpenInMemoryContext();
        var service = CreateService(context);

        using var stream = File.OpenRead(SamplePath);
        var result = await service.ImportAsync(stream);

        // Sample has 10 ExchTrade rows with a non-empty securityID (AVSG, SWDA, XMWX buys;
        // QQQ sell; SGLD x2 sells + 1 buy; VWRA x3 buys), plus a run of EUR.GBP/GBP.USD/
        // EUR.USD FX-conversion rows which are not yet handled by this slice.
        Assert.True(result.ImportedCount >= 10);

        var tradeTransactions = context.SecurityTransactions
            .Where(t => t.Type == SecurityTransactionType.Buy || t.Type == SecurityTransactionType.Sell)
            .ToList();
        Assert.Equal(10, tradeTransactions.Count);
    }

    [Fact]
    public async Task ImportAsync_creates_a_security_from_a_traded_symbol()
    {
        using var context = CreateOpenInMemoryContext();
        var service = CreateService(context);

        using var stream = File.OpenRead(SamplePath);
        await service.ImportAsync(stream);

        var security = context.Securities.Single(s => s.Symbol == "AVSG");
        Assert.Equal("GBP", security.Currency);
        Assert.Equal("AVSG", security.Name);
    }

    [Fact]
    public async Task ImportAsync_resolves_the_account_by_alias()
    {
        using var context = CreateOpenInMemoryContext();
        var service = CreateService(context);

        using var stream = File.OpenRead(SamplePath);
        await service.ImportAsync(stream);

        var account = Assert.Single(context.Accounts);
        Assert.Equal("my_ibkr_acc", account.Name);
    }

    [Fact]
    public async Task ImportAsync_maps_a_buy_trade_with_fee_and_absolute_quantity()
    {
        using var context = CreateOpenInMemoryContext();
        var service = CreateService(context);

        using var stream = File.OpenRead(SamplePath);
        await service.ImportAsync(stream);

        var avsgBuy = context.SecurityTransactions
            .Include(t => t.Position!.Security)
            .Single(t => t.Position!.Security!.Symbol == "AVSG");

        Assert.Equal(SecurityTransactionType.Buy, avsgBuy.Type);
        Assert.Equal(150m, avsgBuy.Quantity);
        Assert.Equal(-(150m * 21.335m), avsgBuy.Amount);
        Assert.Equal("GBP", avsgBuy.Currency);
        Assert.Equal(-1.660125m, avsgBuy.FeeAmount);
        Assert.Equal("GBP", avsgBuy.FeeCurrency);
        Assert.Equal(new DateOnly(2026, 7, 6), avsgBuy.Date);
    }

    [Fact]
    public async Task ImportAsync_maps_a_sell_trade_with_absolute_quantity()
    {
        using var context = CreateOpenInMemoryContext();
        var service = CreateService(context);

        using var stream = File.OpenRead(SamplePath);
        await service.ImportAsync(stream);

        var qqqSell = context.SecurityTransactions
            .Include(t => t.Position!.Security)
            .Single(t => t.Position!.Security!.Symbol == "QQQ");

        Assert.Equal(SecurityTransactionType.Sell, qqqSell.Type);
        Assert.Equal(15m, qqqSell.Quantity);
        Assert.Equal(15m * 622.07m, qqqSell.Amount);
    }

    [Fact]
    public async Task ImportAsync_imports_deposits_and_broker_interest_as_cash_transactions()
    {
        using var context = CreateOpenInMemoryContext();
        var service = CreateService(context);

        using var stream = File.OpenRead(SamplePath);
        await service.ImportAsync(stream);

        // Sample has 8 "Deposits/Withdrawals" rows (all positive, so all Deposit — no
        // negative/withdrawal row exists in this sample) and 11 "Broker Interest
        // Received" rows.
        var deposits = context.CashTransactions.Where(t => t.Type == CashTransactionType.Deposit).ToList();
        var interest = context.CashTransactions.Where(t => t.Type == CashTransactionType.Interest).ToList();
        Assert.Equal(8, deposits.Count);
        Assert.Equal(11, interest.Count);
        Assert.All(deposits, d => Assert.True(d.Amount > 0));
        Assert.All(interest, i => Assert.True(i.Amount > 0));
        Assert.Equal(0, context.CashTransactions.Count(t => t.Type == CashTransactionType.Withdrawal));
    }

    [Fact]
    public async Task ImportAsync_maps_a_deposit_with_currency_and_date()
    {
        using var context = CreateOpenInMemoryContext();
        var service = CreateService(context);

        using var stream = File.OpenRead(SamplePath);
        await service.ImportAsync(stream);

        var deposit = context.CashTransactions.Single(t => t.Type == CashTransactionType.Deposit && t.Date == new DateOnly(2026, 1, 22));
        Assert.Equal("EUR", deposit.Currency);
        Assert.Equal(5000m, deposit.Amount);
    }

    [Fact]
    public async Task ImportAsync_aggregates_a_dividend_with_its_withholding_tax_into_one_transaction()
    {
        using var context = CreateOpenInMemoryContext();
        var service = CreateService(context);

        using var stream = File.OpenRead(SamplePath);
        await service.ImportAsync(stream);

        // DB1(DE0005810055), 2026-05-19: one Dividends row (147 EUR) + one Withholding
        // Tax row (-38.77 EUR) on the same date -> one Dividend transaction.
        var dividend = context.SecurityTransactions
            .Include(t => t.Position!.Security)
            .Single(t => t.Position!.Security!.Symbol == "DB1" && t.Date == new DateOnly(2026, 5, 19));

        Assert.Equal(SecurityTransactionType.Dividend, dividend.Type);
        Assert.Equal(147m, dividend.Amount);
        Assert.Equal(-38.77m, dividend.TaxAmount);
        Assert.Null(dividend.FeeAmount);
        Assert.Null(dividend.FeeCurrency);
    }

    [Fact]
    public async Task ImportAsync_emits_a_standalone_tax_transaction_when_a_group_has_no_dividend_row()
    {
        using var context = CreateOpenInMemoryContext();
        var service = CreateService(context);

        using var stream = File.OpenRead(SamplePath);
        await service.ImportAsync(stream);

        // AGG(US4642872265), 2025-02-06: two Withholding Tax rows (+6.43, -0.44) and no
        // Dividends/Payment-in-lieu row on that date -> one standalone Tax transaction.
        var tax = context.SecurityTransactions
            .Include(t => t.Position!.Security)
            .Single(t => t.Position!.Security!.Symbol == "AGG" && t.Date == new DateOnly(2025, 2, 6));

        Assert.Equal(SecurityTransactionType.Tax, tax.Type);
        Assert.Equal(6.43m - 0.44m, tax.Amount);
        Assert.Null(tax.FeeAmount);
    }

    [Fact]
    public async Task ImportAsync_maps_an_in_kind_transfer_in_with_zero_amount()
    {
        using var context = CreateOpenInMemoryContext();
        var service = CreateService(context);

        using var stream = File.OpenRead(SamplePath);
        await service.ImportAsync(stream);

        // <Transfer type="FOP" direction="IN" quantity="35" isin="DE0005810055" .../>
        var transferIn = context.SecurityTransactions
            .Include(t => t.Position!.Security)
            .Single(t => t.Type == SecurityTransactionType.TransferIn);

        Assert.Equal("DB1", transferIn.Position!.Security!.Symbol);
        Assert.Equal(35m, transferIn.Quantity);
        Assert.Equal(0m, transferIn.Amount);
        Assert.Equal(new DateOnly(2026, 4, 14), transferIn.Date);
    }

    [Fact]
    public async Task ImportAsync_re_importing_the_same_file_does_not_create_duplicate_transactions()
    {
        using var context = CreateOpenInMemoryContext();
        var service = CreateService(context);

        using (var stream = File.OpenRead(SamplePath))
            await service.ImportAsync(stream);

        var securityTransactionCountAfterFirstImport = context.SecurityTransactions.Count();
        var cashTransactionCountAfterFirstImport = context.CashTransactions.Count();
        var accountCountAfterFirstImport = context.Accounts.Count();

        using (var stream = File.OpenRead(SamplePath))
            await service.ImportAsync(stream);

        Assert.Equal(securityTransactionCountAfterFirstImport, context.SecurityTransactions.Count());
        Assert.Equal(cashTransactionCountAfterFirstImport, context.CashTransactions.Count());
        Assert.Equal(accountCountAfterFirstImport, context.Accounts.Count());
    }

    [Fact]
    public async Task ImportAsync_reports_fx_conversion_trades_as_recognized_but_skipped()
    {
        using var context = CreateOpenInMemoryContext();
        var service = CreateService(context);

        using var stream = File.OpenRead(SamplePath);
        var result = await service.ImportAsync(stream);

        // EUR.GBP (x6), EUR.USD (x6), GBP.USD (x10) FX-conversion rows in the sample.
        Assert.Equal(22, result.RecognizedButSkipped.Count);
        Assert.All(result.RecognizedButSkipped, s => Assert.Equal("Trade", s.ElementName));
        Assert.Empty(result.Unrecognized);
    }
}
