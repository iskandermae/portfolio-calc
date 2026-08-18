using Microsoft.Extensions.Logging;

namespace PortfolioCalc.App.Logging;

/// <summary>Minimal app-wide file logger: appends timestamped lines to a log file (see
/// <c>MauiProgram</c> for wiring — the path is <c>PortfolioDbContext.DefaultDataDirectory
/// \log.txt</c>). Exists so a non-developer user has somewhere to look (the Logs page)
/// when a page fails instead of the WebView just going blank — see doc/decisions.md.
/// No log levels UI, rotation, or structured viewer — proportionate to what was asked.</summary>
public sealed class FileLoggerProvider(string filePath) : ILoggerProvider
{
    private readonly object _writeLock = new();

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, filePath, _writeLock);

    public void Dispose()
    {
    }

    private sealed class FileLogger(string categoryName, string filePath, object writeLock) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{logLevel}] {categoryName}: {formatter(state, exception)}";
            if (exception is not null)
                line += Environment.NewLine + exception;

            lock (writeLock)
            {
                File.AppendAllText(filePath, line + Environment.NewLine);
            }
        }
    }
}
