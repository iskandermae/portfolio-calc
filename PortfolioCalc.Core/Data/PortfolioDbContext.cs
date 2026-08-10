using Microsoft.EntityFrameworkCore;

namespace PortfolioCalc.Core.Data;

public class PortfolioDbContext : DbContext
{
    public static string DefaultDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PortfolioCalc");

    public static string DefaultDatabasePath => Path.Combine(DefaultDataDirectory, "portfolio.sqlite");

    public DbSet<AppMetadata> AppMetadata => Set<AppMetadata>();

    public PortfolioDbContext(DbContextOptions<PortfolioDbContext> options) : base(options)
    {
    }

    public static PortfolioDbContext CreateDefault()
    {
        Directory.CreateDirectory(DefaultDataDirectory);
        var options = new DbContextOptionsBuilder<PortfolioDbContext>()
            .UseSqlite($"Data Source={DefaultDatabasePath}")
            .Options;
        return new PortfolioDbContext(options);
    }
}

/// <summary>Placeholder table until the first real domain entity (transactions) lands.</summary>
public class AppMetadata
{
    public int Id { get; set; }
    public string SchemaVersion { get; set; } = "0";
}
