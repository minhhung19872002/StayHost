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
}
