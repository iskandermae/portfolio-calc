using Microsoft.EntityFrameworkCore;
using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Data;

public class PortfolioDbContext : DbContext
{
    public static string DefaultDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PortfolioCalc");

    public static string DefaultDatabasePath => Path.Combine(DefaultDataDirectory, "portfolio.sqlite");

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Security> Securities => Set<Security>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<CashTransaction> CashTransactions => Set<CashTransaction>();
    public DbSet<SecurityTransaction> SecurityTransactions => Set<SecurityTransaction>();
    public DbSet<FxRate> FxRates => Set<FxRate>();
    public DbSet<SecurityPrice> SecurityPrices => Set<SecurityPrice>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();
    public DbSet<InflationRate> InflationRates => Set<InflationRate>();
    public DbSet<UiLayoutSetting> UiLayoutSettings => Set<UiLayoutSetting>();

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Security>()
            .HasIndex(s => new { s.Symbol, s.Currency })
            .IsUnique();

        modelBuilder.Entity<Position>()
            .HasIndex(p => new { p.AccountId, p.SecurityId })
            .IsUnique();

        modelBuilder.Entity<Position>()
            .HasOne(p => p.Account)
            .WithMany()
            .HasForeignKey(p => p.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Position>()
            .HasOne(p => p.Security)
            .WithMany()
            .HasForeignKey(p => p.SecurityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CashTransaction>()
            .HasOne(t => t.Account)
            .WithMany()
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SecurityTransaction>()
            .HasOne(t => t.Position)
            .WithMany()
            .HasForeignKey(t => t.PositionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<FxRate>()
            .HasIndex(r => new { r.FromCurrency, r.ToCurrency, r.Date })
            .IsUnique();

        modelBuilder.Entity<SecurityPrice>()
            .HasIndex(p => new { p.SecurityId, p.Date })
            .IsUnique();

        modelBuilder.Entity<SecurityPrice>()
            .HasOne(p => p.Security)
            .WithMany()
            .HasForeignKey(p => p.SecurityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InflationRate>()
            .HasIndex(r => new { r.BaseCurrency, r.Period })
            .IsUnique();

        modelBuilder.Entity<UiLayoutSetting>()
            .HasKey(s => s.ScreenKey);
    }
}
