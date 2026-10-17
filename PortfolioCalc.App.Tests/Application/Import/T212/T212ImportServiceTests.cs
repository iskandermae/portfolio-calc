using Microsoft.EntityFrameworkCore;
using PortfolioCalc.App.Application.Import.T212;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Data.Import.T212;
using PortfolioCalc.Core.Data.Repositories;
using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.App.Tests.Application.Import.T212;

public class T212ImportServiceTests
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

    private static T212ImportService CreateService(PortfolioDbContext context) =>
        new(new T212CsvImporter(),
            new AccountRepository(context),
            new SecurityRepository(context),
            new PositionRepository(context),
            new CashTransactionRepository(context),
            new SecurityTransactionRepository(context));

    private static async Task<int> CreateAccountAsync(PortfolioDbContext context, string name = "test_account")
    {
        var account = await new AccountRepository(context).AddAsync(new Account { Name = name });
        return account.Id;
    }

    [Fact]
    public async Task ImportAsync_imports_every_recognized_row_into_the_chosen_account()
    {
        using var context = CreateOpenInMemoryContext();
        var accountId = await CreateAccountAsync(context);
        var service = CreateService(context);

        using var stream = T212SampleFixture.OpenStream();
        var result = await service.ImportAsync(stream, accountId);

        // Deposit, Interest, Market buy, Market sell, Limit buy, Limit sell, Dividend = 7
        // recognized rows; the Withdrawal row isn't handled by this slice.
        Assert.Equal(7, result.ImportedCount);
        Assert.Single(result.Unrecognized);
    }

    [Fact]
    public async Task ImportAsync_reports_an_unrecognized_action_without_guessing_it()
    {
        using var context = CreateOpenInMemoryContext();
        var accountId = await CreateAccountAsync(context);
        var service = CreateService(context);

        using var stream = T212SampleFixture.OpenStream();
        var result = await service.ImportAsync(stream, accountId);

        var unrecognized = Assert.Single(result.Unrecognized);
        Assert.Contains("Withdrawal", unrecognized.Reason);
    }

    [Fact]
    public async Task ImportAsync_maps_a_deposit_and_interest_as_cash_transactions_in_the_chosen_account()
    {
        using var context = CreateOpenInMemoryContext();
        var accountId = await CreateAccountAsync(context);
        var service = CreateService(context);

        using var stream = T212SampleFixture.OpenStream();
        await service.ImportAsync(stream, accountId);

        var deposit = context.CashTransactions.Single(t => t.Type == CashTransactionType.Deposit);
        Assert.Equal(accountId, deposit.AccountId);
        Assert.Equal(1000.00m, deposit.Amount);
        Assert.Equal("GBP", deposit.Currency);
        Assert.Equal(new DateOnly(2026, 1, 10), deposit.Date);

        var interest = context.CashTransactions.Single(t => t.Type == CashTransactionType.Interest);
        Assert.Equal(5.00m, interest.Amount);
        Assert.Equal("GBP", interest.Currency);
    }

    [Fact]
    public async Task ImportAsync_computes_a_buy_in_the_trading_currency_with_its_conversion_fee()
    {
        using var context = CreateOpenInMemoryContext();
        var accountId = await CreateAccountAsync(context);
        var service = CreateService(context);

        using var stream = T212SampleFixture.OpenStream();
        await service.ImportAsync(stream, accountId);

        // 10 shares @ 100 USD, 1.50 GBP conversion fee — Amount/Currency stay in the
        // trading currency (USD) rather than the account-currency Total column (GBP).
        var buy = context.SecurityTransactions
            .Include(t => t.Position!.Security)
            .Single(t => t.Type == SecurityTransactionType.Buy && t.Position!.Security!.Symbol == "TSTA");

        Assert.Equal(10m, buy.Quantity);
        Assert.Equal(-(10m * 100m), buy.Amount);
        Assert.Equal("USD", buy.Currency);
        Assert.Equal(-1.50m, buy.FeeAmount);
        Assert.Equal("GBP", buy.FeeCurrency);
        Assert.Equal(new DateOnly(2026, 2, 1), buy.Date);
    }

    [Fact]
    public async Task ImportAsync_computes_a_sell_without_a_conversion_fee()
    {
        using var context = CreateOpenInMemoryContext();
        var accountId = await CreateAccountAsync(context);
        var service = CreateService(context);

        using var stream = T212SampleFixture.OpenStream();
        await service.ImportAsync(stream, accountId);

        var sell = context.SecurityTransactions
            .Include(t => t.Position!.Security)
            .Single(t => t.Type == SecurityTransactionType.Sell && t.Position!.Security!.Symbol == "TSTA");

        Assert.Equal(4m, sell.Quantity);
        Assert.Equal(4m * 110m, sell.Amount);
        Assert.Equal("USD", sell.Currency);
        Assert.Null(sell.FeeAmount);
        Assert.Null(sell.FeeCurrency);
    }

    [Fact]
    public async Task ImportAsync_converts_a_gbx_pence_priced_buy_to_gbp()
    {
        using var context = CreateOpenInMemoryContext();
        var accountId = await CreateAccountAsync(context);
        var service = CreateService(context);

        using var stream = T212SampleFixture.OpenStream();
        await service.ImportAsync(stream, accountId);

        // 50 shares @ 200 GBX (pence) = 2.00 GBP/share -> 100 GBP cost.
        var buy = context.SecurityTransactions
            .Include(t => t.Position!.Security)
            .Single(t => t.Type == SecurityTransactionType.Buy && t.Position!.Security!.Symbol == "TSTB");

        Assert.Equal("GBP", buy.Currency);
        Assert.Equal("GBP", buy.Position!.Security!.Currency);
        Assert.Equal(-100m, buy.Amount);
    }

    [Fact]
    public async Task ImportAsync_assigns_the_guessed_exchange_to_gbp_securities_only()
    {
        using var context = CreateOpenInMemoryContext();
        var accountId = await CreateAccountAsync(context);
        var service = CreateService(context);

        using var stream = T212SampleFixture.OpenStream();
        await service.ImportAsync(stream, accountId);

        // TSTB is GBX-priced (normalized to GBP); TSTA is USD.
        Assert.Equal("MY_T212.L", context.Securities.Single(s => s.Symbol == "TSTB").Exchange);
        Assert.Null(context.Securities.Single(s => s.Symbol == "TSTA").Exchange);
    }

    [Fact]
    public async Task ImportAsync_does_not_overwrite_an_existing_exchange()
    {
        using var context = CreateOpenInMemoryContext();
        var accountId = await CreateAccountAsync(context);
        await new SecurityRepository(context).AddAsync(
            new Security { Symbol = "TSTB", Name = "TSTB", Currency = "GBP", Exchange = "LSEETF" });
        var service = CreateService(context);

        using var stream = T212SampleFixture.OpenStream();
        await service.ImportAsync(stream, accountId);

        Assert.Equal("LSEETF", context.Securities.Single(s => s.Symbol == "TSTB").Exchange);
    }

    [Fact]
    public async Task ImportAsync_maps_a_dividend_with_its_withholding_tax_on_the_same_row()
    {
        using var context = CreateOpenInMemoryContext();
        var accountId = await CreateAccountAsync(context);
        var service = CreateService(context);

        using var stream = T212SampleFixture.OpenStream();
        await service.ImportAsync(stream, accountId);

        // 6 shares @ 0.50 USD = 3.00 USD gross; 0.90 USD withholding tax.
        var dividend = context.SecurityTransactions
            .Include(t => t.Position!.Security)
            .Single(t => t.Type == SecurityTransactionType.Dividend);

        Assert.Equal("TSTA", dividend.Position!.Security!.Symbol);
        Assert.Equal(3.00m, dividend.Amount);
        Assert.Equal("USD", dividend.Currency);
        Assert.Equal(-0.90m, dividend.TaxAmount);
        Assert.Null(dividend.Quantity);
    }

    [Fact]
    public async Task ImportAsync_resolves_the_same_security_across_two_trades_on_the_chosen_account()
    {
        using var context = CreateOpenInMemoryContext();
        var accountId = await CreateAccountAsync(context);
        var service = CreateService(context);

        using var stream = T212SampleFixture.OpenStream();
        await service.ImportAsync(stream, accountId);

        var tsta = context.Securities.Single(s => s.Symbol == "TSTA");
        Assert.Equal("USD", tsta.Currency);
        var position = context.Positions.Single(p => p.SecurityId == tsta.Id);
        Assert.Equal(accountId, position.AccountId);

        var transactionsOnPosition = context.SecurityTransactions.Count(t => t.PositionId == position.Id);
        // Buy, Sell and Dividend all reference TSTA.
        Assert.Equal(3, transactionsOnPosition);
    }

    [Fact]
    public async Task ImportAsync_re_importing_the_same_file_does_not_create_duplicate_transactions()
    {
        using var context = CreateOpenInMemoryContext();
        var accountId = await CreateAccountAsync(context);
        var service = CreateService(context);

        using (var stream = T212SampleFixture.OpenStream())
            await service.ImportAsync(stream, accountId);

        var securityTransactionCountAfterFirstImport = context.SecurityTransactions.Count();
        var cashTransactionCountAfterFirstImport = context.CashTransactions.Count();

        using (var stream = T212SampleFixture.OpenStream())
            await service.ImportAsync(stream, accountId);

        Assert.Equal(securityTransactionCountAfterFirstImport, context.SecurityTransactions.Count());
        Assert.Equal(cashTransactionCountAfterFirstImport, context.CashTransactions.Count());
    }

    [Fact]
    public async Task ImportAsync_throws_for_an_unknown_account_id()
    {
        using var context = CreateOpenInMemoryContext();
        var service = CreateService(context);

        using var stream = T212SampleFixture.OpenStream();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ImportAsync(stream, accountId: 999));
    }
}
