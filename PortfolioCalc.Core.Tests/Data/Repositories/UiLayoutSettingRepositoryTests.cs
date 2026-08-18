using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Data.Repositories;

namespace PortfolioCalc.Core.Tests.Data.Repositories;

public class UiLayoutSettingRepositoryTests
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
    public async Task GetAsync_returns_null_when_no_layout_has_been_saved()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new UiLayoutSettingRepository(context);

        Assert.Null(await repository.GetAsync("TransactionsReport"));
    }

    [Fact]
    public async Task SaveAsync_then_GetAsync_round_trips_the_layout()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new UiLayoutSettingRepository(context);

        await repository.SaveAsync("TransactionsReport", """{"SortKey":"Date"}""");

        var fetched = await repository.GetAsync("TransactionsReport");
        Assert.NotNull(fetched);
        Assert.Equal("""{"SortKey":"Date"}""", fetched.LayoutJson);
    }

    [Fact]
    public async Task SaveAsync_upserts_the_row_for_a_screen_instead_of_adding_a_second_one()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new UiLayoutSettingRepository(context);

        await repository.SaveAsync("TransactionsReport", """{"SortKey":"Date"}""");
        await repository.SaveAsync("TransactionsReport", """{"SortKey":"Amount"}""");

        Assert.Single(context.UiLayoutSettings);
        var fetched = await repository.GetAsync("TransactionsReport");
        Assert.Equal("""{"SortKey":"Amount"}""", fetched!.LayoutJson);
    }

    [Fact]
    public async Task SaveAsync_keeps_layouts_for_different_screens_independent()
    {
        using var context = CreateOpenInMemoryContext();
        var repository = new UiLayoutSettingRepository(context);

        await repository.SaveAsync("TransactionsReport", """{"SortKey":"Date"}""");
        await repository.SaveAsync("OtherScreen", """{"SortKey":"Name"}""");

        Assert.Equal("""{"SortKey":"Date"}""", (await repository.GetAsync("TransactionsReport"))!.LayoutJson);
        Assert.Equal("""{"SortKey":"Name"}""", (await repository.GetAsync("OtherScreen"))!.LayoutJson);
    }

    [Fact]
    public async Task Saved_layout_survives_the_app_restarting()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"portfolio-calc-tests-{Guid.NewGuid():N}.sqlite");
        try
        {
            var options = new DbContextOptionsBuilder<PortfolioDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            await using (var firstRun = new PortfolioDbContext(options))
            {
                await firstRun.Database.EnsureCreatedAsync();
                await new UiLayoutSettingRepository(firstRun).SaveAsync(
                    "TransactionsReport", """{"SortKey":"Amount","SortDescending":true}""");
            }

            await using (var afterRestart = new PortfolioDbContext(options))
            {
                var fetched = await new UiLayoutSettingRepository(afterRestart).GetAsync("TransactionsReport");
                Assert.NotNull(fetched);
                Assert.Equal("""{"SortKey":"Amount","SortDescending":true}""", fetched.LayoutJson);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }
}
