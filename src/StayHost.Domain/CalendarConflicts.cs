namespace StayHost.Domain;

/// <summary>
/// docs/01 QL-11 — spotting when an imported calendar sells nights this platform
/// has already confirmed. Both sides are half-open [check-in, check-out) ranges,
/// so they clash when each starts before the other ends. Pure, so the overlap
/// maths is tested without a database or a live feed.
/// </summary>
public static class CalendarConflicts
{
    public readonly record struct Range(DateOnly From, DateOnly ToExclusive);

    public static bool Overlaps(Range a, Range b) =>
        a.From < b.ToExclusive && b.From < a.ToExclusive;

    /// <summary>
    /// The confirmed bookings an import would collide with. A booking is returned
    /// once even if several imported events touch it.
    /// </summary>
    public static IReadOnlyList<Range> Clashes(
        IEnumerable<Range> importedEvents, IEnumerable<Range> confirmedBookings)
    {
        var events = importedEvents.ToList();
        return confirmedBookings
            .Where(booking => events.Any(e => Overlaps(e, booking)))
            .ToList();
    }

    /// <summary>The host-facing warning, or null when nothing clashes.</summary>
    public static string? Warn(IReadOnlyList<Range> clashes)
    {
        if (clashes.Count == 0) return null;

        var first = clashes.OrderBy(c => c.From).First();
        var extra = clashes.Count - 1;
        var tail = extra > 0 ? $" và {extra} đơn khác" : "";
        return $"Lịch nhập về trùng đơn đã xác nhận trên StayHost: "
             + $"{first.From:dd/MM}–{first.ToExclusive:dd/MM}{tail}. "
             + "Kiểm tra để tránh nhận trùng khách.";
    }
}
