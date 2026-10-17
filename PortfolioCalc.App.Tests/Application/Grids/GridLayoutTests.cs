using PortfolioCalc.App.Application.Grids;
using PortfolioCalc.Core.Domain;
using PortfolioCalc.Core.Repositories;

namespace PortfolioCalc.App.Tests.Application.Grids;

public class GridLayoutTests
{
    private sealed class FakeLayoutRepository : IUiLayoutSettingRepository
    {
        public Dictionary<string, string> Saved { get; } = [];

        public Task<UiLayoutSetting?> GetAsync(string screenKey) =>
            Task.FromResult(Saved.TryGetValue(screenKey, out var json)
                ? new UiLayoutSetting { ScreenKey = screenKey, LayoutJson = json }
                : null);

        public Task SaveAsync(string screenKey, string layoutJson)
        {
            Saved[screenKey] = layoutJson;
            return Task.CompletedTask;
        }
    }

    private static GridLayout NewLayout() => new([("A", "A"), ("B", "B"), ("C", "C")], "A");

    [Fact]
    public void SortBy_toggles_direction_on_same_key_and_resets_on_new_key()
    {
        var layout = NewLayout();

        layout.SortBy("A");
        Assert.True(layout.SortDescending);
        layout.SortBy("B");
        Assert.Equal("B", layout.SortKey);
        Assert.False(layout.SortDescending);
    }

    [Fact]
    public async Task Saved_order_visibility_and_sort_are_restored_in_a_new_instance()
    {
        var repository = new FakeLayoutRepository();
        var first = NewLayout();
        first.Move(first.Columns[2], -2);
        first.Columns[1].Visible = false;
        first.SortBy("C");
        first.SortBy("C");
        await first.SaveAsync(repository, "Screen", new SavedGridLayout());

        var second = NewLayout();
        var loaded = await second.LoadAsync<SavedGridLayout>(repository, "Screen");

        Assert.NotNull(loaded);
        Assert.Equal(["C", "A", "B"], second.Columns.Select(c => c.Key));
        Assert.Equal([true, false, true], second.Columns.Select(c => c.Visible));
        Assert.Equal("C", second.SortKey);
        Assert.True(second.SortDescending);
    }

    [Fact]
    public async Task Load_appends_columns_missing_from_saved_layout_and_ignores_unknown_ones()
    {
        var repository = new FakeLayoutRepository();
        repository.Saved["Screen"] =
            """{"Columns":[{"Key":"Gone","Visible":true},{"Key":"B","Visible":false}],"SortKey":null,"SortDescending":false}""";

        var layout = NewLayout();
        await layout.LoadAsync<SavedGridLayout>(repository, "Screen");

        Assert.Equal(["B", "A", "C"], layout.Columns.Select(c => c.Key));
        Assert.False(layout.Columns[0].Visible);
        Assert.Equal("A", layout.SortKey);
    }

    [Fact]
    public async Task Load_returns_null_when_nothing_saved()
    {
        Assert.Null(await NewLayout().LoadAsync<SavedGridLayout>(new FakeLayoutRepository(), "Screen"));
    }
}
