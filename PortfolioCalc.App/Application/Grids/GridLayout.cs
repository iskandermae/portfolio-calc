using System.Text.Json;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.App.Application.Grids;

public sealed class GridColumnState
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public bool Visible { get; set; } = true;
}

public class SavedGridColumn
{
    public required string Key { get; set; }
    public bool Visible { get; set; }
}

/// <summary>JSON stored in <c>UiLayoutSetting.LayoutJson</c>. A screen with extra persisted
/// state derives from this and adds its own properties.</summary>
public class SavedGridLayout
{
    public List<SavedGridColumn> Columns { get; set; } = [];
    public string? SortKey { get; set; }
    public bool SortDescending { get; set; }
}

/// <summary>Column order/visibility and sort state of a grid, saved per screen key (stories
/// 08 and 15). Columns missing from a saved layout are appended; saved columns that no
/// longer exist are dropped.</summary>
public sealed class GridLayout(IEnumerable<(string Key, string Label)> columns, string defaultSortKey)
{
    public List<GridColumnState> Columns { get; private set; } =
        columns.Select(c => new GridColumnState { Key = c.Key, Label = c.Label }).ToList();

    public string SortKey { get; private set; } = defaultSortKey;
    public bool SortDescending { get; private set; }

    public void SortBy(string key)
    {
        if (SortKey == key)
            SortDescending = !SortDescending;
        else
        {
            SortKey = key;
            SortDescending = false;
        }
    }

    public void Move(GridColumnState column, int direction)
    {
        var index = Columns.IndexOf(column);
        var newIndex = index + direction;
        if (newIndex < 0 || newIndex >= Columns.Count)
            return;

        Columns.RemoveAt(index);
        Columns.Insert(newIndex, column);
    }

    /// <summary>Applies the saved layout for <paramref name="screenKey"/> and returns it (so
    /// the caller can read its extra properties), or returns null when none is saved.</summary>
    public async Task<T?> LoadAsync<T>(IUiLayoutSettingRepository repository, string screenKey)
        where T : SavedGridLayout
    {
        var setting = await repository.GetAsync(screenKey);
        if (setting is null)
            return null;

        var saved = JsonSerializer.Deserialize<T>(setting.LayoutJson);
        if (saved is null)
            return null;

        var byKey = Columns.ToDictionary(c => c.Key);
        var ordered = new List<GridColumnState>();
        foreach (var savedColumn in saved.Columns)
        {
            if (!byKey.TryGetValue(savedColumn.Key, out var column))
                continue;
            column.Visible = savedColumn.Visible;
            ordered.Add(column);
        }
        foreach (var column in Columns)
        {
            if (!ordered.Contains(column))
                ordered.Add(column);
        }
        Columns = ordered;

        SortKey = saved.SortKey ?? SortKey;
        SortDescending = saved.SortDescending;
        return saved;
    }

    /// <summary>Fills <paramref name="saved"/> with this layout and saves it; the caller sets
    /// any extra properties on it first.</summary>
    public Task SaveAsync<T>(IUiLayoutSettingRepository repository, string screenKey, T saved)
        where T : SavedGridLayout
    {
        saved.Columns = Columns.Select(c => new SavedGridColumn { Key = c.Key, Visible = c.Visible }).ToList();
        saved.SortKey = SortKey;
        saved.SortDescending = SortDescending;
        return repository.SaveAsync(screenKey, JsonSerializer.Serialize(saved));
    }
}
