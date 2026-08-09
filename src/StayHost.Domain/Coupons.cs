namespace StayHost.Domain;

public enum CouponKind
{
    /// <summary>A percentage off the stay, up to an optional cap.</summary>
    Percentage = 0,
    /// <summary>A flat amount off.</summary>
    Fixed = 1
}

/// <summary>
/// docs/01 ĐP-09, TC-09 — a promo code and the campaign it belongs to. The rules
/// that decide whether it may be used, and for how much, live in
/// <see cref="Coupons"/>; this is only what is stored.
/// </summary>
public class Coupon
{
    public int Id { get; set; }

    /// <summary>What the guest types. Stored and compared upper-case.</summary>
    public string Code { get; set; } = "";
    public string Campaign { get; set; } = "";

    public CouponKind Kind { get; set; } = CouponKind.Percentage;
    /// <summary>A whole percent for <see cref="CouponKind.Percentage"/>, otherwise an amount.</summary>
    public decimal Value { get; set; }

    /// <summary>Ceiling on a percentage discount. Null means no ceiling.</summary>
    public decimal? MaxDiscount { get; set; }
    /// <summary>The stay must total at least this before the code applies. Null means no floor.</summary>
    public decimal? MinBookingTotal { get; set; }

    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }

    /// <summary>Total redemptions allowed across everyone. Null means unlimited.</summary>
    public int? MaxRedemptions { get; set; }
    /// <summary>Redemptions allowed per guest. Null means unlimited.</summary>
    public int? MaxPerUser { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// docs/01 ĐP-09, TC-09 — one guest's use of a code on one booking. Append-only,
/// like the money and credit ledgers: the redemption count is the number of rows,
/// never a column ticked up in place, so it cannot drift and a cancelled booking
/// gives its redemption back by writing a reversing row (Voided).
/// </summary>
public class CouponRedemption
{
    public long Id { get; set; }

    public int CouponId { get; set; }
    public Coupon? Coupon { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    public decimal Amount { get; set; }

    /// <summary>
    /// A redemption released because the booking never completed. It stays on the
    /// ledger for the record but no longer counts against a limit.
    /// </summary>
    public bool Voided { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// docs/01 ĐP-09 (the guest applies a code) and TC-09 (a campaign with limits).
/// Everything here is pure: a code, a stay and the counts already spent go in,
/// a discount or a refusal comes out. Whoever calls it has proved the counts
/// against the database and re-checks the winning code at payment.
/// </summary>
public static class Coupons
{
    public sealed record Check(bool Ok, decimal Discount = 0, string? Error = null, string? Label = null);

    /// <summary>What the guest is told when the code is fine but off by a condition.</summary>
    public static string Normalize(string? code) => (code ?? "").Trim().ToUpperInvariant();

    /// <summary>
    /// The discount this code takes off a stay, or why it does not apply.
    /// </summary>
    /// <param name="bookingTotal">The stay's total before any code or balance — what the code discounts.</param>
    /// <param name="timesUsedTotal">Redemptions already counted across everyone.</param>
    /// <param name="timesUsedByGuest">Redemptions already counted for this guest.</param>
    public static Check Evaluate(
        Coupon? coupon, decimal bookingTotal, int timesUsedTotal, int timesUsedByGuest, DateTime now)
    {
        if (coupon is null) return new(false, Error: "Mã giảm giá không tồn tại.");
        if (!coupon.IsActive) return new(false, Error: "Mã giảm giá đã ngừng áp dụng.");

        if (coupon.StartsAt is { } starts && now < starts)
            return new(false, Error: "Mã giảm giá chưa tới ngày áp dụng.");
        if (coupon.EndsAt is { } ends && now >= ends)
            return new(false, Error: "Mã giảm giá đã hết hạn.");

        // The whole-campaign cap and the per-guest cap are checked before the
        // money, so a code that cannot be used says so rather than showing a
        // discount that would be refused a moment later at payment.
        if (coupon.MaxRedemptions is { } max && timesUsedTotal >= max)
            return new(false, Error: "Mã giảm giá đã hết lượt sử dụng.");
        if (coupon.MaxPerUser is { } perUser && timesUsedByGuest >= perUser)
            return new(false, Error: "Bạn đã dùng mã giảm giá này rồi.");

        if (coupon.MinBookingTotal is { } floor && bookingTotal < floor)
            return new(false, Error: $"Đơn tối thiểu {floor:#,##0}₫ mới dùng được mã này.");

        var discount = DiscountFor(coupon, bookingTotal);
        if (discount <= 0) return new(false, Error: "Mã giảm giá không áp dụng cho đơn này.");

        return new(true, discount, Label: LabelFor(coupon));
    }

    /// <summary>The money a code takes off, capped by the stay and by MaxDiscount.</summary>
    public static decimal DiscountFor(Coupon coupon, decimal bookingTotal)
    {
        if (bookingTotal <= 0) return 0;

        var raw = coupon.Kind == CouponKind.Percentage
            ? bookingTotal * (coupon.Value / 100m)
            : coupon.Value;

        if (coupon.Kind == CouponKind.Percentage && coupon.MaxDiscount is { } cap)
            raw = Math.Min(raw, cap);

        // A code never pays out more than the stay costs.
        return Math.Round(Math.Min(raw, bookingTotal), 0, MidpointRounding.AwayFromZero);
    }

    public static string LabelFor(Coupon coupon) =>
        coupon.Kind == CouponKind.Percentage
            ? $"Mã {coupon.Code} (−{coupon.Value:0.#}%)"
            : $"Mã {coupon.Code}";
}
