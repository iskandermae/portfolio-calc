namespace PortfolioCalc.Core.Charting;

/// <summary>Pure date-sampling calc for the position value chart — see
/// doc/stories/10-position-value-chart-report.md. Kept independent of the DB/service layer
/// (same shape as <see cref="PortfolioCalc.Core.Validation.AnomalyDetector"/>) so the
/// sampling rule is directly unit-testable.</summary>
public static class ChartDateSampler
{
    /// <summary>Days of the month sampled once a candidate date is within the most recent
    /// year (see <see cref="GenerateSampleDates"/>).</summary>
    private static readonly int[] RecentYearSampleDays = [1, 7, 14, 21];

    /// <summary>Builds the chart's sample dates between <paramref name="startDate"/> and
    /// <paramref name="today"/> inclusive.
    /// <para>For every calendar month spanned by the period, candidate dates are the 1st,
    /// 7th, 14th and 21st. A candidate strictly older than <paramref name="today"/> minus one
    /// year (the cutoff) is coarsened down to just the 1st of its month — "older than one
    /// year" per the story, so a candidate exactly on the cutoff date still gets the full
    /// 1/7/14/21 sampling, only a candidate strictly before it doesn't. See
    /// doc/decisions.md.</para>
    /// <para>Candidates outside [<paramref name="startDate"/>, <paramref name="today"/>] are
    /// dropped, then both endpoints are forced in (even if grid alignment wouldn't otherwise
    /// produce them), duplicates removed, and the result sorted ascending.</para></summary>
    public static IReadOnlyList<DateOnly> GenerateSampleDates(DateOnly startDate, DateOnly today)
    {
        if (today < startDate)
            throw new ArgumentException("today must not be before startDate.", nameof(today));

        var cutoff = today.AddYears(-1);
        var dates = new SortedSet<DateOnly>();

        var monthCursor = new DateOnly(startDate.Year, startDate.Month, 1);
        var endMonth = new DateOnly(today.Year, today.Month, 1);

        while (monthCursor <= endMonth)
        {
            var daysInMonth = DateTime.DaysInMonth(monthCursor.Year, monthCursor.Month);
            foreach (var day in RecentYearSampleDays)
            {
                if (day > daysInMonth)
                    continue;

                var candidate = new DateOnly(monthCursor.Year, monthCursor.Month, day);
                var isOlderThanOneYear = candidate < cutoff;
                if (isOlderThanOneYear && day != 1)
                    continue;

                if (candidate < startDate || candidate > today)
                    continue;

                dates.Add(candidate);
            }

            monthCursor = monthCursor.AddMonths(1);
        }

        dates.Add(startDate);
        dates.Add(today);

        return dates.ToList();
    }
}
