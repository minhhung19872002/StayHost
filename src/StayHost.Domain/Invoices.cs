namespace StayHost.Domain;

/// <summary>
/// docs/01 ĐP-14 — a booking as an invoice the guest can keep. The document is
/// rendered in the web layer; what is here is the part worth testing: a stable
/// number, and the amounts read back from what was actually charged.
/// </summary>
public static class Invoices
{
    /// <summary>
    /// A human invoice number tied to the booking, so the same booking always
    /// produces the same one. The booking reference is already unique; this wraps
    /// it in a dated, invoice-shaped form (HD = hoá đơn).
    /// </summary>
    public static string Number(Booking booking) =>
        $"HD-{booking.CreatedAt:yyyyMM}-{booking.Reference}";

    /// <summary>
    /// What the guest has actually paid so far: the total less anything still
    /// owed on a partial payment (docs/01 ĐP-06). Never negative.
    /// </summary>
    public static decimal AmountPaid(Booking booking) =>
        Math.Max(0m, booking.Total - Math.Max(0m, booking.BalanceDue));

    /// <summary>Whether there is still a scheduled amount to collect.</summary>
    public static bool HasBalanceDue(Booking booking) => booking.BalanceDue > 0;
}
