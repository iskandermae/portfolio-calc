namespace PortfolioCalc.App.Logging;

/// <summary>Tracks whether a Warning-or-above line has been written to the log file since
/// the user last opened the Logs page, so <c>NavMenu</c> can highlight the "Logs" link
/// instead of a new problem sitting silently in a file nobody's looking at — see
/// doc/decisions.md. One instance is shared between <see cref="FileLoggerProvider"/> (which
/// reports new entries as they're written, from whatever thread is logging) and the Gui
/// (which reads/clears the flag on the UI thread) — constructed once in
/// <c>MauiProgram.CreateMauiApp</c> and also registered as a DI singleton so Gui components
/// can inject the same instance.</summary>
public sealed class LogActivityTracker
{
    private volatile bool _hasUnseenEntries;

    /// <summary>Raised whenever <see cref="HasUnseenEntries"/> changes value. May fire from
    /// any thread — subscribers touching the UI must marshal back via
    /// <c>InvokeAsync(StateHasChanged)</c>.</summary>
    public event Action? Changed;

    public bool HasUnseenEntries => _hasUnseenEntries;

    /// <summary>Called by <see cref="FileLoggerProvider"/> after actually writing a
    /// Warning-or-above line.</summary>
    public void ReportEntry()
    {
        if (_hasUnseenEntries)
            return; // already flagged — no need to raise the event again
        _hasUnseenEntries = true;
        Changed?.Invoke();
    }

    /// <summary>Called by the Logs page once its content has been (re)loaded.</summary>
    public void MarkSeen()
    {
        if (!_hasUnseenEntries)
            return;
        _hasUnseenEntries = false;
        Changed?.Invoke();
    }
}
