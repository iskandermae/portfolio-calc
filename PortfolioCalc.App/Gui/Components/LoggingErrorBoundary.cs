using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace PortfolioCalc.App.Gui.Components;

/// <summary>An <see cref="ErrorBoundary"/> that logs the exception via the app's file
/// logger before showing <c>ErrorContent</c> — wraps every routed page in
/// <c>Routes.razor</c>. Without this, an unhandled exception during a page's render (e.g.
/// story 10's chart page) reached .NET MAUI's <c>BlazorWebView</c> default handling, which
/// shows the generic "An unhandled error has occurred / Reload" banner from
/// <c>index.html</c>'s <c>#blazor-error-ui</c> with no way to see what actually went wrong.
/// Catching it here instead means: the real exception is always logged (visible on the Logs
/// page), and the user sees an in-page message instead of the whole WebView appearing to
/// have crashed. See doc/decisions.md.</summary>
public class LoggingErrorBoundary : ErrorBoundary
{
    [Inject]
    private ILogger<LoggingErrorBoundary> Logger { get; set; } = default!;

    protected override Task OnErrorAsync(Exception exception)
    {
        Logger.LogError(exception, "Unhandled exception while rendering a page.");
        return base.OnErrorAsync(exception);
    }
}
