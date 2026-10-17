namespace PortfolioCalc.App.Application.Caching;

/// <summary>Remembers results of external provider calls for as long as the user stays on
/// one page, so re-running the same analytics doesn't repeat a lookup that the local DB
/// can't answer (a failure, or a row awaiting review). Cleared on navigation instead of
/// being kept in sync with new data — see doc/decisions.md.</summary>
public class ExternalCallCache
{
    private readonly Dictionary<string, object> _entries = [];

    public async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> fetch) where T : notnull
    {
        if (_entries.TryGetValue(key, out var cached))
            return (T)cached;

        var result = await fetch();
        _entries[key] = result;
        return result;
    }

    public void Clear() => _entries.Clear();
}
