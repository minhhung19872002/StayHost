namespace StayHost.Domain;

/// <summary>
/// docs/01 QL-16, docs/02 G7 — the numbers behind a listing's performance report.
/// The counts are gathered from the database by the caller; the rates that turn
/// them into something a host can read live here, where they can be tested and
/// cannot divide by zero.
/// </summary>
public static class Performance
{
    /// <summary>
    /// Views that turned into a booking, 0–1. No views means no rate to show —
    /// zero, not a division by zero, and not a misleading 100%.
    /// </summary>
    public static double ConversionRate(int bookings, int views) =>
        views <= 0 ? 0 : Math.Min(1.0, (double)bookings / views);

    /// <summary>
    /// Share of the window's nights that were booked, 0–1. Capped at 1 because
    /// overlapping or backfilled stays could otherwise total more nights than the
    /// window holds.
    /// </summary>
    public static double OccupancyRate(int bookedNights, int windowNights) =>
        windowNights <= 0 ? 0 : Math.Min(1.0, (double)bookedNights / windowNights);

    /// <summary>Nights of a stay that fall inside a window, for occupancy.</summary>
    public static int NightsInWindow(DateOnly checkIn, DateOnly checkOut, DateOnly from, DateOnly to)
    {
        var start = checkIn > from ? checkIn : from;
        var end = checkOut < to ? checkOut : to;
        var nights = end.DayNumber - start.DayNumber;
        return nights > 0 ? nights : 0;
    }
}
