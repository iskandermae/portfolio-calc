using Microsoft.Extensions.Logging;
using PortfolioCalc.App.Logging;

namespace PortfolioCalc.App.Tests.Logging;

public class FileLoggerProviderTests
{
    private static string CreateTempLogPath() => Path.Combine(Path.GetTempPath(), $"portfoliocalc-test-{Guid.NewGuid():N}.log");

    [Fact]
    public void Log_at_Warning_or_above_writes_the_line_and_reports_to_the_tracker()
    {
        var path = CreateTempLogPath();
        try
        {
            var tracker = new LogActivityTracker();
            var provider = new FileLoggerProvider(path, tracker);
            var logger = provider.CreateLogger("Test.Category");

            logger.LogWarning("Something needs attention.");

            Assert.True(tracker.HasUnseenEntries);
            var content = File.ReadAllText(path);
            Assert.Contains("Something needs attention.", content);
            Assert.Contains("Test.Category", content);
            Assert.Contains("[Warning]", content);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void Log_below_Warning_writes_nothing_and_does_not_report_to_the_tracker()
    {
        var path = CreateTempLogPath();
        try
        {
            var tracker = new LogActivityTracker();
            var provider = new FileLoggerProvider(path, tracker);
            var logger = provider.CreateLogger("Test.Category");

            logger.LogInformation("Routine activity.");

            Assert.False(tracker.HasUnseenEntries);
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void Log_with_an_exception_includes_it_in_the_written_line()
    {
        var path = CreateTempLogPath();
        try
        {
            var tracker = new LogActivityTracker();
            var provider = new FileLoggerProvider(path, tracker);
            var logger = provider.CreateLogger("Test.Category");

            logger.LogError(new InvalidOperationException("boom"), "Unhandled exception while rendering a page.");

            var content = File.ReadAllText(path);
            Assert.Contains("Unhandled exception while rendering a page.", content);
            Assert.Contains("InvalidOperationException", content);
            Assert.Contains("boom", content);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
