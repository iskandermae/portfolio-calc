using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PortfolioCalc.Core.Data;
using PortfolioCalc.Core.Domain;

namespace PortfolioCalc.Core.Tests.Data;

public class PortfolioDbContextTests
{
    [Fact]
    public void EnsureCreated_creates_schema_on_an_in_memory_sqlite_connection()
    {
        var options = new DbContextOptionsBuilder<PortfolioDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new PortfolioDbContext(options);
        context.Database.OpenConnection();

        Assert.True(context.Database.EnsureCreated());
        Assert.Empty(context.Accounts);
    }

    /// <summary>Reproduces a real "import fails" report: a database file created by an
    /// older version of the app (before <see cref="Security.Symbol"/>/<see
    /// cref="SecurityTransaction.TaxAmount"/> existed) already has its tables, so
    /// EnsureCreated() is a documented no-op against it — the current model and the
    /// database's actual columns silently diverge, and the first insert against a
    /// changed table throws. EnsureCreated has no schema-migration story; see the
    /// follow-up note this test's failure points to.</summary>
    [Fact]
    public void EnsureCreated_does_not_evolve_the_schema_of_an_already_existing_database()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"portfoliocalc-schema-drift-{Guid.NewGuid()}.sqlite");
        try
        {
            using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                connection.Open();
                var command = connection.CreateCommand();
                // The pre-story-02 shape of the Securities table: Ticker (not Symbol),
                // no Isin/TaxAmount at all yet — an even older snapshot than the
                // ISIN-keyed shape that was tried and reverted, to stand in for
                // "whatever a user's already-running app created before today".
                command.CommandText = """
                    CREATE TABLE Securities (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Ticker TEXT NOT NULL,
                        Name TEXT NOT NULL,
                        Currency TEXT NOT NULL
                    );
                    """;
                command.ExecuteNonQuery();
            }

            var options = new DbContextOptionsBuilder<PortfolioDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;
            using var context = new PortfolioDbContext(options);

            // EnsureCreated sees the database already has tables and does nothing —
            // it returns false and leaves the stale schema exactly as it was.
            Assert.False(context.Database.EnsureCreated());

            context.Securities.Add(new Security { Symbol = "AAPL", Name = "AAPL", Currency = "USD" });
            var thrown = Assert.Throws<DbUpdateException>(() => context.SaveChanges());
            Assert.IsType<SqliteException>(thrown.InnerException);
            Assert.Contains("no column named Symbol", thrown.InnerException!.Message);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
        }
    }

    /// <summary>The actual fix for the above: production code (see MauiProgram) now
    /// calls Migrate(), not EnsureCreated(), so a database's schema is brought forward
    /// by the migration history instead of silently left stale.</summary>
    [Fact]
    public void Migrate_creates_a_working_schema_on_a_fresh_database_file()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"portfoliocalc-migrate-{Guid.NewGuid()}.sqlite");
        try
        {
            var options = new DbContextOptionsBuilder<PortfolioDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;
            using var context = new PortfolioDbContext(options);

            context.Database.Migrate();

            context.Securities.Add(new Security { Symbol = "AAPL", Name = "AAPL", Currency = "USD" });
            context.SaveChanges();

            Assert.Equal("AAPL", context.Securities.Single().Symbol);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
        }
    }
}
