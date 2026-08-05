namespace StayHost.Domain;

/// <summary>
/// One place that knows how a stay turns into money. Quotes, bookings and refunds all
/// route through here so the guest never sees two different totals for the same stay.
/// </summary>
public static class Pricing
{
    /// <summary>VAT applied to the room rate and fees.</summary>
    public const decimal TaxRate = 0.08m;

    public readonly record struct Breakdown(
        int Nights,
        decimal NightlyRate,
        decimal Subtotal,
        decimal CleaningFee,
        decimal ServiceFee,
        decimal Tax,
        decimal Total,
        decimal WeekendSurcharge,
        decimal LengthDiscount,
        int LengthDiscountPercent);

    private static decimal Round(decimal v) => Math.Round(v, 0, MidpointRounding.AwayFromZero);

    /// <summary>Friday and Saturday nights carry a surcharge, as most hosts price them.</summary>
    private static bool IsWeekendNight(DateOnly d) =>
        d.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;

    /// <summary>Stays get cheaper per night the longer they run.</summary>
    private static int LengthDiscountPercentFor(int nights) => nights switch
    {
        >= 28 => 20,
        >= 7 => 10,
        _ => 0
    };

    /// <summary>
    /// Prices a stay night by night. A seasonal rule replaces the base rate inside its
    /// window; the weekend uplift then applies on top of whichever rate won.
    /// </summary>
    public static Breakdown Quote(
        Listing listing, DateOnly checkIn, DateOnly checkOut, IReadOnlyCollection<PriceRule>? rules = null)
    {
        var nights = Math.Max(1, checkOut.DayNumber - checkIn.DayNumber);

        var baseTotal = 0m;
        var weekendSurcharge = 0m;
        for (var i = 0; i < nights; i++)
        {
            var night = checkIn.AddDays(i);

            var seasonal = rules?.FirstOrDefault(r => r.From <= night && night <= r.To);
            var rate = seasonal?.NightlyRate ?? listing.PricePerNight;

            if (IsWeekendNight(night))
            {
                var extra = Round(rate * listing.WeekendSurchargeRate);
                weekendSurcharge += extra;
                rate += extra;
            }
            baseTotal += rate;
        }

        var discountPercent = LengthDiscountPercentFor(nights);
        var lengthDiscount = Round(baseTotal * discountPercent / 100m);
        var subtotal = baseTotal - lengthDiscount;

        var serviceFee = Round(subtotal * listing.ServiceFeeRate);
        var tax = Round((subtotal + listing.CleaningFee + serviceFee) * TaxRate);
        var total = subtotal + listing.CleaningFee + serviceFee + tax;

        return new Breakdown(
            nights, listing.PricePerNight, subtotal, listing.CleaningFee,
            serviceFee, tax, total, weekendSurcharge, lengthDiscount, discountPercent);
    }

    public readonly record struct RefundOutcome(decimal Amount, decimal Penalty, string Explanation);

    /// <summary>
    /// What a guest gets back if they cancel <paramref name="now"/>. Fees follow the
    /// listing's tier; the StayHost service fee is always returned in full.
    /// </summary>
    public static RefundOutcome Refund(Booking booking, DateOnly now)
    {
        var daysToCheckIn = booking.CheckIn.DayNumber - now.DayNumber;
        var room = booking.Subtotal;
        var refundableFees = booking.CleaningFee + booking.ServiceFee + booking.Tax;

        // Already checked in: nothing left to refund beyond untouched fees.
        if (daysToCheckIn < 0)
            return new(0m, booking.Total, "Kỳ nghỉ đã bắt đầu nên không hoàn tiền.");

        var (roomShare, note) = booking.CancellationTier switch
        {
            CancellationTier.Flexible => daysToCheckIn >= 1
                ? (1m, "Huỷ trước 24 giờ: hoàn 100% tiền phòng.")
                : (0m, "Huỷ trong vòng 24 giờ trước nhận phòng: không hoàn tiền phòng."),

            CancellationTier.Moderate => daysToCheckIn >= 5
                ? (1m, "Huỷ trước 5 ngày: hoàn 100% tiền phòng.")
                : (0.5m, "Huỷ trong vòng 5 ngày trước nhận phòng: hoàn 50% tiền phòng."),

            _ => daysToCheckIn >= 7
                ? (0.5m, "Chính sách nghiêm ngặt: hoàn 50% tiền phòng khi huỷ trước 7 ngày.")
                : (0m, "Chính sách nghiêm ngặt: không hoàn tiền phòng khi huỷ trong vòng 7 ngày.")
        };

        var amount = Math.Round(room * roomShare, 0, MidpointRounding.AwayFromZero) + refundableFees;
        return new(amount, booking.Total - amount, note + " Phí dịch vụ và thuế luôn được hoàn đủ.");
    }

    public static string TierLabel(CancellationTier tier) => tier switch
    {
        CancellationTier.Flexible => "Linh hoạt",
        CancellationTier.Moderate => "Trung bình",
        _ => "Nghiêm ngặt"
    };

    public static string TierSummary(CancellationTier tier) => tier switch
    {
        CancellationTier.Flexible => "Huỷ miễn phí đến 24 giờ trước khi nhận phòng.",
        CancellationTier.Moderate => "Huỷ miễn phí đến 5 ngày trước khi nhận phòng, sau đó hoàn 50% tiền phòng.",
        _ => "Hoàn 50% tiền phòng nếu huỷ trước 7 ngày; sau đó không hoàn tiền phòng."
    };

    /// <summary>Flexible and moderate listings are the ones marketed as "free cancellation".</summary>
    public static bool HasFreeCancellation(CancellationTier tier) => tier != CancellationTier.Strict;
}
