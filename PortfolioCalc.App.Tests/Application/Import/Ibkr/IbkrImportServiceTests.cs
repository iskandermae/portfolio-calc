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

    [Fact]
    public async Task ImportAsync_imports_real_buy_and_sell_trades_from_the_sample_export()
    {
        using var context = CreateOpenInMemoryContext();
        var service = CreateService(context);

        using var stream = IbkrSampleFixture.OpenStream();
        var result = await service.ImportAsync(stream);

        // The fixture has 10 ExchTrade rows with a non-empty securityID (TSTA/TSTB/TSTC x2/
        // TSTD x3/TSTE x2/TSTF), plus a run of EUR.GBP/EUR.USD/GBP.USD FX-conversion rows
        // which are not yet handled by this slice.
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

        using var stream = IbkrSampleFixture.OpenStream();
        await service.ImportAsync(stream);

        var security = context.Securities.Single(s => s.Symbol == "TSTA");
        Assert.Equal("GBP", security.Currency);
        Assert.Equal("TSTA", security.Name);
    }

    [Fact]
    public async Task ImportAsync_resolves_the_account_by_alias()
    {
        using var context = CreateOpenInMemoryContext();
        var service = CreateService(context);

        using var stream = IbkrSampleFixture.OpenStream();
        await service.ImportAsync(stream);

        var account = Assert.Single(context.Accounts);
        Assert.Equal(IbkrSampleFixture.AccountAlias, account.Name);
    }

    [Fact]
    public async Task ImportAsync_maps_a_buy_trade_with_fee_and_absolute_quantity()
    {
        using var context = CreateOpenInMemoryContext();
        var service = CreateService(context);

        using var stream = IbkrSampleFixture.OpenStream();
        await service.ImportAsync(stream);

        var buy = context.SecurityTransactions
            .Include(t => t.Position!.Security)
            .Single(t => t.Position!.Security!.Symbol == "TSTA");

        Assert.Equal(SecurityTransactionType.Buy, buy.Type);
        Assert.Equal(100m, buy.Quantity);
        Assert.Equal(-(100m * 20m), buy.Amount);
        Assert.Equal("GBP", buy.Currency);
        Assert.Equal(-2.5m, buy.FeeAmount);
        Assert.Equal("GBP", buy.FeeCurrency);
        Assert.Equal(new DateOnly(2026, 7, 6), buy.Date);
    }

    [Fact]
    public async Task ImportAsync_maps_a_sell_trade_with_absolute_quantity()
    {
        using var context = CreateOpenInMemoryContext();
        var service = CreateService(context);

        using var stream = IbkrSampleFixture.OpenStream();
        await service.ImportAsync(stream);

        var sell = context.SecurityTransactions
            .Include(t => t.Position!.Security)
            .Single(t => t.Position!.Security!.Symbol == "TSTB");

        Assert.Equal(SecurityTransactionType.Sell, sell.Type);
        Assert.Equal(15m, sell.Quantity);
        Assert.Equal(15m * 600m, sell.Amount);
    }

    [Fact]
    public async Task ImportAsync_imports_deposits_and_broker_interest_as_cash_transactions()
    {
        using var context = CreateOpenInMemoryContext();
        var service = CreateService(context);

        using var stream = IbkrSampleFixture.OpenStream();
        await service.ImportAsync(stream);

        // Fixture has 3 "Deposits/Withdrawals" rows (all positive, so all Deposit — no
        // negative/withdrawal row exists) and 2 "Broker Interest Received" rows.
        var deposits = context.CashTransactions.Where(t => t.Type == CashTransactionType.Deposit).ToList();
        var interest = context.CashTransactions.Where(t => t.Type == CashTransactionType.Interest).ToList();
        Assert.Equal(3, deposits.Count);
        Assert.Equal(2, interest.Count);
        Assert.All(deposits, d => Assert.True(d.Amount > 0));
        Assert.All(interest, i => Assert.True(i.Amount > 0));
        Assert.Equal(0, context.CashTransactions.Count(t => t.Type == CashTransactionType.Withdrawal));
    }

    [Fact]
    public async Task ImportAsync_maps_a_deposit_with_currency_and_date()
    {
        using var context = CreateOpenInMemoryContext();
        var service = CreateService(context);

        using var stream = IbkrSampleFixture.OpenStream();
        await service.ImportAsync(stream);

        var deposit = context.CashTransactions.Single(t => t.Type == CashTransactionType.Deposit && t.Date == new DateOnly(2026, 1, 10));
        Assert.Equal("EUR", deposit.Currency);
        Assert.Equal(1000m, deposit.Amount);
    }

    [Fact]
    public async Task ImportAsync_aggregates_a_dividend_with_its_withholding_tax_into_one_transaction()
    {
        using var context = CreateOpenInMemoryContext();
        var service = CreateService(context);

        using var stream = IbkrSampleFixture.OpenStream();
        await service.ImportAsync(stream);

        // TSTG, 2026-05-19: one Dividends row (100 EUR) + one Withholding Tax row (-20 EUR)
        // on the same date -> one Dividend transaction.
        var dividend = context.SecurityTransactions
            .Include(t => t.Position!.Security)
            .Single(t => t.Position!.Security!.Symbol == "TSTG" && t.Date == new DateOnly(2026, 5, 19));

        Assert.Equal(SecurityTransactionType.Dividend, dividend.Type);
        Assert.Equal(100m, dividend.Amount);
        Assert.Equal(-20m, dividend.TaxAmount);
        Assert.Null(dividend.FeeAmount);
        Assert.Null(dividend.FeeCurrency);
    }

    [Fact]
    public async Task ImportAsync_emits_a_standalone_tax_transaction_when_a_group_has_no_dividend_row()
    {
        using var context = CreateOpenInMemoryContext();
        var service = CreateService(context);

        using var stream = IbkrSampleFixture.OpenStream();
        await service.ImportAsync(stream);

        // TSTH, 2024-11-15: two Withholding Tax rows (+5, -0.5) and no Dividends/
        // Payment-in-lieu row on that date -> one standalone Tax transaction.
        var tax = context.SecurityTransactions
            .Include(t => t.Position!.Security)
            .Single(t => t.Position!.Security!.Symbol == "TSTH" && t.Date == new DateOnly(2024, 11, 15));

        Assert.Equal(SecurityTransactionType.Tax, tax.Type);
        Assert.Equal(5m - 0.5m, tax.Amount);
        Assert.Null(tax.FeeAmount);
    }

    [Fact]
    public async Task ImportAsync_maps_an_in_kind_transfer_in_with_zero_amount()
    {
        using var context = CreateOpenInMemoryContext();
        var service = CreateService(context);

        using var stream = IbkrSampleFixture.OpenStream();
        await service.ImportAsync(stream);

        // <Transfer type="FOP" direction="IN" quantity="40" symbol="TSTG" .../>
        var transferIn = context.SecurityTransactions
            .Include(t => t.Position!.Security)
            .Single(t => t.Type == SecurityTransactionType.TransferIn);

        Assert.Equal("TSTG", transferIn.Position!.Security!.Symbol);
        Assert.Equal(40m, transferIn.Quantity);
        Assert.Equal(0m, transferIn.Amount);
        Assert.Equal(new DateOnly(2026, 3, 1), transferIn.Date);
    }

    [Fact]
    public async Task ImportAsync_re_importing_the_same_file_does_not_create_duplicate_transactions()
    {
        using var context = CreateOpenInMemoryContext();
        var service = CreateService(context);

        using (var stream = IbkrSampleFixture.OpenStream())
            await service.ImportAsync(stream);

        var securityTransactionCountAfterFirstImport = context.SecurityTransactions.Count();
        var cashTransactionCountAfterFirstImport = context.CashTransactions.Count();
        var accountCountAfterFirstImport = context.Accounts.Count();

        using (var stream = IbkrSampleFixture.OpenStream())
            await service.ImportAsync(stream);

        Assert.Equal(securityTransactionCountAfterFirstImport, context.SecurityTransactions.Count());
        Assert.Equal(cashTransactionCountAfterFirstImport, context.CashTransactions.Count());
        Assert.Equal(accountCountAfterFirstImport, context.Accounts.Count());
    }

    [Fact]
    public async Task ImportAsync_sets_the_exchange_on_a_newly_created_security_from_the_trade_row()
    {
        using var context = CreateOpenInMemoryContext();
        var service = CreateService(context);

        using var stream = IbkrSampleFixture.OpenStream();
        await service.ImportAsync(stream);

        // <Trade ... symbol="TSTA" ... listingExchange="LSEETF" .../>
        var tsta = context.Securities.Single(s => s.Symbol == "TSTA");
        Assert.Equal("LSEETF", tsta.Exchange);
    }

    [Fact]
    public async Task ImportAsync_backfills_the_exchange_on_a_pre_existing_security_with_no_exchange_recorded()
    {
        using var context = CreateOpenInMemoryContext();
        // Simulates a Security imported before this feature existed: same Symbol +
        // Currency as a real row in the fixture, but no Exchange recorded yet.
        var securityRepository = new SecurityRepository(context);
        var preExisting = await securityRepository.AddAsync(
            new Security { Symbol = "TSTA", Name = "TSTA", Currency = "GBP" });
        Assert.Null(preExisting.Exchange);

        var service = CreateService(context);
        using var stream = IbkrSampleFixture.OpenStream();
        await service.ImportAsync(stream);

        // Re-importing dedupes the transaction itself, but the existing Security row
        // must still get backfilled with the exchange the row reports.
        var tsta = context.Securities.Single(s => s.Symbol == "TSTA");
        Assert.Equal(preExisting.Id, tsta.Id);
        Assert.Equal("LSEETF", tsta.Exchange);
    }

    [Fact]
    public async Task ImportAsync_does_not_overwrite_an_already_populated_exchange()
    {
        using var context = CreateOpenInMemoryContext();
        var securityRepository = new SecurityRepository(context);
        var preExisting = await securityRepository.AddAsync(
            new Security { Symbol = "TSTA", Name = "TSTA", Currency = "GBP", Exchange = "SOMEOTHERCODE" });

        var service = CreateService(context);
        using var stream = IbkrSampleFixture.OpenStream();
        await service.ImportAsync(stream);

        var tsta = context.Securities.Single(s => s.Symbol == "TSTA");
        Assert.Equal(preExisting.Id, tsta.Id);
        Assert.Equal("SOMEOTHERCODE", tsta.Exchange);
    }

    [Fact]
    public async Task ImportAsync_reports_fx_conversion_trades_as_recognized_but_skipped()
    {
        using var context = CreateOpenInMemoryContext();
        var service = CreateService(context);

        using var stream = IbkrSampleFixture.OpenStream();
        var result = await service.ImportAsync(stream);

        // EUR.GBP (x2), EUR.USD (x2), GBP.USD (x2) FX-conversion rows in the fixture.
        Assert.Equal(6, result.RecognizedButSkipped.Count);
        Assert.All(result.RecognizedButSkipped, s => Assert.Equal("Trade", s.ElementName));
        Assert.Empty(result.Unrecognized);
    }
}
