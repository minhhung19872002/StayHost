namespace StayHost.Domain.Tests;

/// <summary>docs/01 QL-16 — the rates on a listing's performance report.</summary>
public class PerformanceTests
{
    [Fact]
    public void Conversion_is_bookings_over_views()
    {
        Assert.Equal(0.1, Performance.ConversionRate(bookings: 10, views: 100), 3);
    }

    [Fact]
    public void No_views_is_a_zero_rate_not_a_division_by_zero()
    {
        Assert.Equal(0, Performance.ConversionRate(bookings: 0, views: 0));
        // A booking with no recorded view (a stale counter) reads as zero, not ∞.
        Assert.Equal(0, Performance.ConversionRate(bookings: 3, views: 0));
    }

    [Fact]
    public void Conversion_never_exceeds_one()
    {
        // More bookings than views can happen if views were under-counted; the
        // rate is still capped so it never prints above 100%.
        Assert.Equal(1.0, Performance.ConversionRate(bookings: 5, views: 3));
    }

    [Fact]
    public void Occupancy_is_booked_nights_over_the_window()
    {
        Assert.Equal(0.5, Performance.OccupancyRate(bookedNights: 15, windowNights: 30), 3);
        Assert.Equal(0, Performance.OccupancyRate(bookedNights: 0, windowNights: 0));
        Assert.Equal(1.0, Performance.OccupancyRate(bookedNights: 40, windowNights: 30));
    }

    [Fact]
    public void Only_the_nights_inside_the_window_count()
    {
        var from = new DateOnly(2026, 8, 1);
        var to = new DateOnly(2026, 8, 31);

        // A stay wholly inside.
        Assert.Equal(3, Performance.NightsInWindow(
            new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 13), from, to));

        // A stay straddling the start counts only the part inside.
        Assert.Equal(4, Performance.NightsInWindow(
            new DateOnly(2026, 7, 28), new DateOnly(2026, 8, 5), from, to));

        // A stay entirely outside counts nothing.
        Assert.Equal(0, Performance.NightsInWindow(
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 5), from, to));
    }

    /* ------------------------------------------------ docs/02 G7 additions */

    [Fact]
    public void The_nightly_rate_achieved_is_the_room_only()
    {
        // Three nights, 3,000,000 of room after discount.
        Assert.Equal(1_000_000m, Performance.AchievedNightlyRate(3_000_000m, 3));

        // Nothing sold is not a division by zero.
        Assert.Equal(0m, Performance.AchievedNightlyRate(0m, 0));
        Assert.Equal(0m, Performance.AchievedNightlyRate(500_000m, 0));
    }

    [Fact]
    public void The_median_of_a_market_is_the_middle_of_it()
    {
        var sorted = new[] { 400_000m, 800_000m, 1_200_000m, 1_600_000m, 2_000_000m };

        Assert.Equal(1_200_000m, Performance.Percentile(sorted, 0.5));
        Assert.Equal(800_000m, Performance.Percentile(sorted, 0.25));
        Assert.Equal(1_600_000m, Performance.Percentile(sorted, 0.75));

        // An empty market has no going rate, and must not throw at a host who is
        // simply the first person listing in their town.
        Assert.Equal(0m, Performance.Percentile([], 0.5));
        Assert.Equal(900_000m, Performance.Percentile([900_000m], 0.5));
    }

    /// <summary>
    /// The rule that keeps a chart honest: a month that earned nothing has to be
    /// in the series. Drawn from a GROUP BY alone the line joins March straight
    /// to June and reads as steady business when in fact it stopped.
    /// </summary>
    [Fact]
    public void Every_month_in_the_run_is_listed_including_the_empty_ones()
    {
        var months = Performance.MonthsBetween(new DateOnly(2026, 3, 14), new DateOnly(2026, 6, 2));

        Assert.Equal(4, months.Count);
        Assert.Equal(new DateOnly(2026, 3, 1), months[0]);
        Assert.Equal(new DateOnly(2026, 6, 1), months[3]);
        Assert.All(months, m => Assert.Equal(1, m.Day));
    }

    [Fact]
    public void A_run_inside_one_month_is_that_month()
    {
        var months = Performance.MonthsBetween(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));
        Assert.Single(months);
        Assert.Equal(new DateOnly(2026, 9, 1), months[0]);
    }

    [Fact]
    public void A_backwards_run_is_empty_rather_than_endless()
    {
        Assert.Empty(Performance.MonthsBetween(new DateOnly(2026, 9, 1), new DateOnly(2026, 3, 1)));
    }

    [Fact]
    public void A_year_of_months_crosses_the_new_year()
    {
        var months = Performance.MonthsBetween(new DateOnly(2025, 12, 1), new DateOnly(2026, 2, 1));
        Assert.Equal(3, months.Count);
        Assert.Equal(new DateOnly(2025, 12, 1), months[0]);
        Assert.Equal(new DateOnly(2026, 1, 1), months[1]);
        Assert.Equal(new DateOnly(2026, 2, 1), months[2]);
    }
}
