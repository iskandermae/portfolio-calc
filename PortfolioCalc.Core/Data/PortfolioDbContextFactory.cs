using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PortfolioCalc.Core.Data;

/// <summary>Lets `dotnet ef migrations add`/`dotnet ef database update` run against this
/// class library directly, without needing the MAUI app (a different TFM/executable) as
/// the startup project. Not used at runtime — the app builds its own options via
/// <see cref="PortfolioDbContext.CreateDefault"/>.</summary>
public class PortfolioDbContextFactory : IDesignTimeDbContextFactory<PortfolioDbContext>
{
    public PortfolioDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PortfolioDbContext>()
            .UseSqlite($"Data Source={PortfolioDbContext.DefaultDatabasePath}")
            .Options;
        return new PortfolioDbContext(options);
    }
}
