using Microsoft.EntityFrameworkCore;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Data.Repositories;
using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Tests.Data.Repositories;

public class AppSettingsRepositoryTests
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

    [Fact]
    public async Task GetAsync_returns_null_when_no_settings_have_been_saved()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new AppSettingsRepository(context);

        Assert.Null(await repository.GetAsync());
    }

    [Fact]
    public async Task SaveAsync_then_GetAsync_round_trips_the_base_currency()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new AppSettingsRepository(context);

        await repository.SaveAsync(new AppSettings { BaseCurrency = "EUR" });

        var fetched = await repository.GetAsync();
        Assert.NotNull(fetched);
        Assert.Equal("EUR", fetched.BaseCurrency);
    }

    [Fact]
    public async Task SaveAsync_upserts_the_single_row_instead_of_adding_a_second_one()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new AppSettingsRepository(context);

        await repository.SaveAsync(new AppSettings { BaseCurrency = "EUR" });
        await repository.SaveAsync(new AppSettings { BaseCurrency = "GBP" });

        Assert.Single(context.AppSettings);
        var fetched = await repository.GetAsync();
        Assert.Equal("GBP", fetched!.BaseCurrency);
    }
}
