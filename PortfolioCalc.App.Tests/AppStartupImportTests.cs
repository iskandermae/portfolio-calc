using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PortfolioCalc.App.Application.Import.Ibkr;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Data.Import.Ibkr;
using PortfolioCalc.Core.Data.Repositories;
using PortfolioCalc.Core.Import;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.App.Tests;

/// <summary>End-to-end test through the exact composition root the running app uses
/// (see MauiProgram.CreateMauiApp) — real DI container, a real file-based SQLite
/// database brought up via Database.Migrate() (not the in-memory/EnsureCreated shortcuts
/// the other test classes use), and the real sample export. Exists because a bug can
/// live in the wiring itself (DI registrations, migrations, the exact DbContext options)
/// rather than in any one class's logic — those are individually well-tested elsewhere,
/// but never previously exercised together the way the app actually runs.</summary>
public class AppStartupImportTests
{
    private static string SamplePath => Path.Combine(AppContext.BaseDirectory, "samples", "all-total-export-2026.xml");

    private static ServiceProvider BuildAppServices(string dbPath)
    {
        var services = new ServiceCollection();

        services.AddDbContext<PortfolioDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ISecurityRepository, SecurityRepository>();
        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<ICashTransactionRepository, CashTransactionRepository>();
        services.AddScoped<ISecurityTransactionRepository, SecurityTransactionRepository>();
        services.AddScoped<ITransactionImporter, IbkrFlexQueryImporter>();
        services.AddScoped<IbkrImportService>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Importing_the_sample_export_through_the_real_app_wiring_succeeds()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"portfoliocalc-startup-{Guid.NewGuid()}.sqlite");
        try
        {
            using var provider = BuildAppServices(dbPath);

            // Mirrors the migration step MauiProgram runs once at startup.
            using (var scope = provider.CreateScope())
                scope.ServiceProvider.GetRequiredService<PortfolioDbContext>().Database.Migrate();

            using (var scope = provider.CreateScope())
            {
                var importService = scope.ServiceProvider.GetRequiredService<IbkrImportService>();
                using var stream = File.OpenRead(SamplePath);

                var result = await importService.ImportAsync(stream);

                Assert.True(result.ImportedCount > 0);
                Assert.Empty(result.Unrecognized);
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
        }
    }

    /// <summary>The GUI (Import.razor) reads the picked file via
    /// InputFileChangeEventArgs.File.OpenReadStream() — a forward-only stream over the
    /// browser/WebView bridge, not a seekable FileStream like the other tests use. If
    /// anything downstream (XDocument.Load, etc.) implicitly relies on seeking, this is
    /// where it would surface.</summary>
    [Fact]
    public async Task Importing_the_sample_export_from_a_non_seekable_stream_succeeds()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"portfoliocalc-nonseekable-{Guid.NewGuid()}.sqlite");
        try
        {
            using var provider = BuildAppServices(dbPath);

            using (var scope = provider.CreateScope())
                scope.ServiceProvider.GetRequiredService<PortfolioDbContext>().Database.Migrate();

            using (var scope = provider.CreateScope())
            {
                var importService = scope.ServiceProvider.GetRequiredService<IbkrImportService>();
                using var fileStream = File.OpenRead(SamplePath);
                using var stream = new NonSeekableStream(fileStream);

                var result = await importService.ImportAsync(stream);

                Assert.True(result.ImportedCount > 0);
                Assert.Empty(result.Unrecognized);
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
        }
    }
}
