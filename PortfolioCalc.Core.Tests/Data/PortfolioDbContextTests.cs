using Microsoft.EntityFrameworkCore;
using PortfolioCalc.Core.Data;

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
}
