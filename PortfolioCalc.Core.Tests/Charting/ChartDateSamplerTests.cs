using PortfolioCalc.Core.Charting;

namespace PortfolioCalc.Core.Tests.Charting;

public class ChartDateSamplerTests
{
    [Fact]
    public void GenerateSampleDates_always_includes_start_and_today()
    {
        var start = new DateOnly(2025, 3, 5);
        var today = new DateOnly(2026, 8, 19);

        var dates = ChartDateSampler.GenerateSampleDates(start, today);

        Assert.Contains(start, dates);
        Assert.Contains(today, dates);
    }

    [Fact]
    public void GenerateSampleDates_returns_sorted_distinct_dates()
    {
        var start = new DateOnly(2025, 1, 1);
        var today = new DateOnly(2026, 1, 1);

        var dates = ChartDateSampler.GenerateSampleDates(start, today);

        Assert.Equal(dates.Distinct(), dates);
        Assert.Equal(dates.OrderBy(d => d), dates);
    }

    [Fact]
    public void GenerateSampleDates_uses_weekly_1_7_14_21_sampling_within_the_most_recent_year()
    {
        var today = new DateOnly(2026, 8, 19);
        var start = today.AddYears(-1); // whole period is within the "recent" window

        var dates = ChartDateSampler.GenerateSampleDates(start, today);

        // June 2026 is fully within the most-recent-year window (cutoff is 2025-08-19).
        Assert.Contains(new DateOnly(2026, 6, 1), dates);
        Assert.Contains(new DateOnly(2026, 6, 7), dates);
        Assert.Contains(new DateOnly(2026, 6, 14), dates);
        Assert.Contains(new DateOnly(2026, 6, 21), dates);
    }

    [Fact]
    public void GenerateSampleDates_uses_monthly_1st_only_sampling_older_than_one_year()
    {
        var today = new DateOnly(2026, 8, 19);
        var start = new DateOnly(2024, 1, 1);

        var dates = ChartDateSampler.GenerateSampleDates(start, today);

        // March 2025 is entirely before the cutoff (2025-08-19) -> monthly only.
        Assert.Contains(new DateOnly(2025, 3, 1), dates);
        Assert.DoesNotContain(new DateOnly(2025, 3, 7), dates);
        Assert.DoesNotContain(new DateOnly(2025, 3, 14), dates);
        Assert.DoesNotContain(new DateOnly(2025, 3, 21), dates);
    }

    [Fact]
    public void GenerateSampleDates_candidate_exactly_on_the_cutoff_date_is_kept()
    {
        // Cutoff = today - 1 year. A candidate date exactly equal to the cutoff is not
        // "older than one year" (strict "<", not "<="), so it's kept even though its
        // neighbors earlier in the same month get coarsened away below — see
        // doc/decisions.md.
        var today = new DateOnly(2026, 8, 21);
        var cutoff = today.AddYears(-1); // 2025-08-21 -> also a 1/7/14/21 candidate day

        var dates = ChartDateSampler.GenerateSampleDates(new DateOnly(2025, 8, 1), today);

        Assert.Contains(cutoff, dates);
    }

    [Fact]
    public void GenerateSampleDates_candidates_strictly_before_the_cutoff_are_coarsened_to_the_1st()
    {
        var today = new DateOnly(2026, 8, 21);
        var start = new DateOnly(2025, 8, 1);
        // Cutoff is 2025-08-21; the 7th/14th of that same month are strictly before it, so
        // only the 1st of August 2025 should survive from that month's candidates.
        var dates = ChartDateSampler.GenerateSampleDates(start, today);

        Assert.Contains(new DateOnly(2025, 8, 1), dates);
        Assert.DoesNotContain(new DateOnly(2025, 8, 14), dates);
        Assert.DoesNotContain(new DateOnly(2025, 8, 7), dates);
    }

    [Fact]
    public void GenerateSampleDates_drops_generated_candidates_outside_the_requested_range()
    {
        var start = new DateOnly(2026, 8, 10);
        var today = new DateOnly(2026, 8, 19);

        var dates = ChartDateSampler.GenerateSampleDates(start, today);

        // The 1st and 7th of August fall before the start date and must not appear.
        Assert.DoesNotContain(new DateOnly(2026, 8, 1), dates);
        Assert.DoesNotContain(new DateOnly(2026, 8, 7), dates);
        Assert.Contains(new DateOnly(2026, 8, 14), dates);
        Assert.Contains(start, dates);
        Assert.Contains(today, dates);
    }

    [Fact]
    public void GenerateSampleDates_throws_when_today_is_before_startDate()
    {
        Assert.Throws<ArgumentException>(() =>
            ChartDateSampler.GenerateSampleDates(new DateOnly(2026, 1, 1), new DateOnly(2025, 1, 1)));
    }
}
