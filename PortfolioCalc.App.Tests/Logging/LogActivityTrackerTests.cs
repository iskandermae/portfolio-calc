using PortfolioCalc.App.Logging;

namespace PortfolioCalc.App.Tests.Logging;

public class LogActivityTrackerTests
{
    [Fact]
    public void ReportEntry_sets_HasUnseenEntries_and_raises_Changed()
    {
        var tracker = new LogActivityTracker();
        var changedCount = 0;
        tracker.Changed += () => changedCount++;

        tracker.ReportEntry();

        Assert.True(tracker.HasUnseenEntries);
        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void ReportEntry_does_not_raise_Changed_again_once_already_flagged()
    {
        var tracker = new LogActivityTracker();
        tracker.ReportEntry();
        var changedCount = 0;
        tracker.Changed += () => changedCount++;

        tracker.ReportEntry();

        Assert.Equal(0, changedCount);
    }

    [Fact]
    public void MarkSeen_clears_HasUnseenEntries_and_raises_Changed()
    {
        var tracker = new LogActivityTracker();
        tracker.ReportEntry();
        var changedCount = 0;
        tracker.Changed += () => changedCount++;

        tracker.MarkSeen();

        Assert.False(tracker.HasUnseenEntries);
        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void MarkSeen_does_nothing_when_there_are_no_unseen_entries()
    {
        var tracker = new LogActivityTracker();
        var changedCount = 0;
        tracker.Changed += () => changedCount++;

        tracker.MarkSeen();

        Assert.False(tracker.HasUnseenEntries);
        Assert.Equal(0, changedCount);
    }
}
