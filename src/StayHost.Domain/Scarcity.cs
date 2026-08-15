namespace StayHost.Domain;

/// <summary>
/// docs/01 TĐ-23, YT-08 — how full a listing's near calendar is, and what that
/// is allowed to say to a guest.
///
/// Two callers, one rule. The listing page shows "Hiếm có" when a place is
/// nearly always taken, and a saved place that crosses the same line sends the
/// "sắp hết phòng" half of YT-08 — the half that was never built when the price
/// drop half was. Keeping both on one threshold means the badge a guest saw and
/// the notice they were sent can never disagree.
/// </summary>
public static class Scarcity
{
    /// <summary>How far ahead "usually booked" is measured.</summary>
    public const int WindowDays = 60;

    /// <summary>
    /// Below this many free nights left in the window, the place is scarce.
    /// Expressed as a share so a listing whose calendar is only open two months
    /// is judged on what it actually offers.
    /// </summary>
    public const double RareBelowFreeShare = 0.25;

    /// <summary>
    /// A window with almost nothing in it is not evidence of demand — a brand
    /// new listing that blocked every day is not a rare find. Fewer than this
    /// many nights of signal and we say nothing at all.
    /// </summary>
    public const int MinNightsForSignal = 14;

    public readonly record struct Reading(int FreeNights, int TotalNights)
    {
        public double FreeShare => TotalNights <= 0 ? 1 : (double)FreeNights / TotalNights;
    }

    /// <summary>
    /// True when the calendar is full enough to call this a rare find. False for
    /// anything we cannot support, including a window too small to read.
    /// </summary>
    public static bool IsRareFind(Reading r) =>
        r.TotalNights >= MinNightsForSignal && r.FreeShare < RareBelowFreeShare;

    /// <summary>
    /// docs/01 YT-08 — the notice is worth sending only on the crossing. A place
    /// that was already scarce when the guest saved it has no news in it, and
    /// resending on every sweep would train people to ignore the channel.
    /// </summary>
    public static bool ShouldWarnLowAvailability(Reading before, Reading after) =>
        !IsRareFind(before) && IsRareFind(after);

    /// <summary>What the badge says. Vietnamese, because it is shown as written.</summary>
    public const string RareFindLabel = "Hiếm có";

    /// <summary>
    /// Why it says it, in words a guest can check against the calendar below.
    /// </summary>
    public static string RareFindReason(Reading r) =>
        $"Chỗ này thường kín — chỉ còn {r.FreeNights} đêm trống trong {r.TotalNights} đêm tới.";

    /// <summary>
    /// The same fact as a sentence that already has a subject, for the YT-08
    /// notice. Lower-casing <see cref="RareFindReason"/> and gluing a quoted
    /// title in front of it produced "«Tra Que Farmstay» chỗ này thường kín",
    /// which is two subjects and reads as broken Vietnamese.
    /// </summary>
    public static string LowAvailabilityNotice(string listingTitle, Reading r) =>
        $"\"{listingTitle}\" chỉ còn {r.FreeNights} đêm trống trong {r.TotalNights} đêm tới. " +
        "Nếu định đi, đặt sớm cho chắc.";
}
