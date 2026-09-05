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

    /// <summary>
    /// What a stay actually sold for per night — room only, after the discount.
    ///
    /// Deliberately not <c>Subtotal</c>, which carries the cleaning fee and the
    /// extra-guest and pet surcharges: docs/02 G7 puts this number beside the
    /// area's going rate, and CN-10 samples <c>PricePerNight</c>. Comparing a
    /// total that includes cleaning against a list of nightly asking prices
    /// would make every listing look dearer than its market for a reason that
    /// has nothing to do with the room.
    /// </summary>
    public static decimal AchievedNightlyRate(decimal roomAfterDiscount, int nights) =>
        nights <= 0 ? 0 : Math.Round(roomAfterDiscount / nights, 0);

    /// <summary>
    /// The value at <paramref name="fraction"/> through a list already sorted
    /// ascending.
    ///
    /// Lives here rather than in the controller that first needed it because two
    /// screens now quote the same market: the price suggestion of docs/01 CN-10
    /// and the report of docs/02 G7. Two copies of one rule is how they end up
    /// telling a host two different things about the same city (PLAN.md §9.7).
    /// </summary>
    public static decimal Percentile(IReadOnlyList<decimal> sorted, double fraction)
    {
        if (sorted.Count == 0) return 0;
        var index = Math.Clamp((int)Math.Round(fraction * (sorted.Count - 1)), 0, sorted.Count - 1);
        return sorted[index];
    }

    /// <summary>
    /// The first day of every month from <paramref name="from"/> through
    /// <paramref name="to"/>, oldest first.
    ///
    /// A chart drawn straight from a GROUP BY skips the months that earned
    /// nothing, and a line that joins March to June with no gap says business
    /// was steady when in fact it stopped. The empty months have to be in the
    /// series for the shape to be honest.
    /// </summary>
    public static IReadOnlyList<DateOnly> MonthsBetween(DateOnly from, DateOnly to)
    {
        var months = new List<DateOnly>();
        var cursor = new DateOnly(from.Year, from.Month, 1);
        var last = new DateOnly(to.Year, to.Month, 1);

        // Guard rather than loop forever on a reversed pair.
        while (cursor <= last && months.Count < 600)
        {
            months.Add(cursor);
            cursor = cursor.AddMonths(1);
        }
        return months;
    }
}
