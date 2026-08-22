namespace PortfolioCalc.Core.Charting;

/// <summary>Pure forward inflation-adjustment calc for the position value chart — see
/// doc/stories/10-position-value-chart-report.md's worked example. Kept independent of the
/// DB/service layer so the formula is directly unit-testable against that example.</summary>
public static class InflationAdjustmentCalculator
{
    /// <summary>Computes the multiplier that expresses a <paramref name="fromDate"/>-priced
    /// amount in <paramref name="toDate"/>'s prices: for every calendar year Y from
    /// <paramref name="fromDate"/>.Year to <paramref name="toDate"/>.Year inclusive, multiplies
    /// by <c>(1 + rate(Y)) ^ (activeDaysInYear / daysInYear(Y))</c>, where activeDaysInYear is
    /// the portion of [<paramref name="fromDate"/>, <paramref name="toDate"/>] that falls
    /// within year Y (a full year strictly in between contributes exponent 1) and
    /// daysInYear(Y) is 365 or 366 for that specific year.
    /// <para>Returns null if <paramref name="rateForYear"/> returns null for any year in the
    /// span — an unresolvable rate for one year makes the whole adjustment unresolvable
    /// (mirrors excluding a chart point with a missing price/FX rate; see
    /// doc/decisions.md).</para></summary>
    public static decimal? ComputeForwardFactor(
        DateOnly fromDate, DateOnly toDate, Func<int, decimal?> rateForYear)
    {
        if (toDate < fromDate)
            throw new ArgumentException("toDate must not be before fromDate.", nameof(toDate));

        var factor = 1.0;

        for (var year = fromDate.Year; year <= toDate.Year; year++)
        {
            var rate = rateForYear(year);
            if (rate is null)
                return null;

            var yearStart = new DateOnly(year, 1, 1);
            var yearEnd = new DateOnly(year + 1, 1, 1); // exclusive upper bound
            var activeStart = fromDate > yearStart ? fromDate : yearStart;
            var activeEnd = toDate < yearEnd ? toDate : yearEnd;
            var activeDays = activeEnd.DayNumber - activeStart.DayNumber;
            var daysInYear = DateTime.IsLeapYear(year) ? 366 : 365;

            var exponent = (double)activeDays / daysInYear;
            factor *= Math.Pow(1 + (double)rate.Value, exponent);
        }

        return (decimal)factor;
    }
}
