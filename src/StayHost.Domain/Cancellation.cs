namespace StayHost.Domain;

/// <summary>Why a booking ended early — the four pre-rules of docs/03 §4 need to tell these apart.</summary>
public enum CancelledBy
{
    Guest = 0,
    Host = 1,
    /// <summary>Natural disaster, epidemic, or an event an admin has recognised.</summary>
    ForceMajeure = 2,
    /// <summary>The platform cancelled it — a payment that never completed, say.</summary>
    Platform = 3
}

/// <summary>
/// The refund table of docs/03 §4. Two rules hold across every policy: the
/// cleaning fee always comes back in full, and once a guest has checked in the
/// policy percentage applies only to the nights they have not used yet.
/// </summary>
public static class Cancellation
{
    /// <summary>docs/03 §4 pre-rule 1.</summary>
    public const int GraceWindowHours = 48;
    public const int GraceMinDaysToCheckIn = 14;

    /// <summary>docs/03 §4 pre-rule 2 — how often a guest's service fee comes back.</summary>
    public const int ServiceFeeRefundsPerYear = 3;

    /// <summary>docs/03 §4 pre-rule 3 — goodwill credit when the host walks away.</summary>
    public const decimal HostCancelCreditRate = 0.10m;

    public sealed record Outcome
    {
        public required decimal RoomRefund { get; init; }
        public required decimal CleaningRefund { get; init; }
        public required decimal ServiceFeeRefund { get; init; }
        public required decimal TaxRefund { get; init; }
        /// <summary>Extra promotional balance, currently only for host cancellations.</summary>
        public required decimal GoodwillCredit { get; init; }
        public required string Explanation { get; init; }
        public required string RuleKey { get; init; }

        public decimal Amount => RoomRefund + CleaningRefund + ServiceFeeRefund + TaxRefund;
    }

    public sealed record Context
    {
        public required Booking Booking { get; init; }
        public required DateTime Now { get; init; }
        public CancelledBy By { get; init; } = CancelledBy.Guest;
        /// <summary>Service-fee refunds this guest has already had in the last 12 months.</summary>
        public int ServiceFeeRefundsUsed { get; init; }
    }

    private static decimal Round(decimal v) => Math.Round(v, 0, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Nights the guest will not use. Before check-in that is every night; after
    /// check-in it is the nights from today to check-out (docs/03 §4, closing rule).
    /// </summary>
    public static int UnusedNights(Booking booking, DateOnly today)
    {
        if (today <= booking.CheckIn) return booking.Nights;
        if (today >= booking.CheckOut) return 0;
        return booking.CheckOut.DayNumber - today.DayNumber;
    }

    public static Outcome Refund(Context ctx)
    {
        var b = ctx.Booking;
        var today = DateOnly.FromDateTime(ctx.Now);
        var hoursSinceBooking = (ctx.Now - b.CreatedAt).TotalHours;
        var daysToCheckIn = b.CheckIn.DayNumber - today.DayNumber;

        // Cleaning is never charged for a stay that does not happen.
        var cleaning = b.CleaningFee;

        // --- pre-rule 3: the host walked away.
        if (ctx.By == CancelledBy.Host)
        {
            return new Outcome
            {
                RoomRefund = b.Subtotal - b.CleaningFee,
                CleaningRefund = cleaning,
                ServiceFeeRefund = b.ServiceFee,
                TaxRefund = b.Tax,
                GoodwillCredit = Round(b.Total * HostCancelCreditRate),
                RuleKey = "host-cancelled",
                Explanation = $"Chủ nhà huỷ đơn: bạn được hoàn 100% và nhận thêm " +
                              $"{Round(b.Total * HostCancelCreditRate):#,##0}₫ số dư khuyến mãi."
            };
        }

        // --- pre-rule 4: force majeure, recognised by an admin.
        if (ctx.By is CancelledBy.ForceMajeure or CancelledBy.Platform)
        {
            return new Outcome
            {
                RoomRefund = b.Subtotal - b.CleaningFee,
                CleaningRefund = cleaning,
                ServiceFeeRefund = b.ServiceFee,
                TaxRefund = b.Tax,
                GoodwillCredit = 0m,
                RuleKey = ctx.By == CancelledBy.ForceMajeure ? "force-majeure" : "platform",
                Explanation = ctx.By == CancelledBy.ForceMajeure
                    ? "Trường hợp bất khả kháng được công nhận: hoàn 100%."
                    : "Staylio huỷ đơn này: hoàn 100%."
            };
        }

        // --- pre-rule 1: 48-hour grace window, only while check-in is far enough away.
        if (hoursSinceBooking <= GraceWindowHours && daysToCheckIn >= GraceMinDaysToCheckIn)
        {
            return new Outcome
            {
                RoomRefund = b.Subtotal - b.CleaningFee,
                CleaningRefund = cleaning,
                ServiceFeeRefund = ServiceFee(b, ctx),
                TaxRefund = b.Tax,
                GoodwillCredit = 0m,
                RuleKey = "grace-48h",
                Explanation = "Huỷ trong 48 giờ đầu sau khi đặt và còn ít nhất 14 ngày tới ngày nhận phòng: hoàn 100%."
            };
        }

        var (roomShare, refundsServiceFee, note) = Policy(b.CancellationTier, daysToCheckIn, b.Nights);

        // The percentage only bites on nights the guest has not used.
        var room = b.Subtotal - b.CleaningFee;
        var unused = UnusedNights(b, today);
        var roomAtRisk = b.Nights > 0 ? Round(room * unused / b.Nights) : room;
        var roomRefund = Round(roomAtRisk * roomShare);

        var stayStarted = today > b.CheckIn;
        if (stayStarted)
        {
            note += $" Bạn đã nhận phòng nên chỉ tính {unused}/{b.Nights} đêm chưa ở.";
            // A stay in progress still gets its cleaning fee back per the table,
            // but the tax on nights already used stays with the tax authority.
        }

        var taxRefund = b.Nights > 0 ? Round(b.Tax * unused / b.Nights) : b.Tax;

        return new Outcome
        {
            RoomRefund = roomRefund,
            CleaningRefund = cleaning,
            ServiceFeeRefund = refundsServiceFee ? ServiceFee(b, ctx) : 0m,
            TaxRefund = taxRefund,
            GoodwillCredit = 0m,
            RuleKey = b.CancellationTier.ToString().ToLowerInvariant(),
            Explanation = note + " Phí vệ sinh luôn được hoàn 100%."
        };
    }

    /// <summary>docs/03 §4 pre-rule 2 — capped at three refunds a year per account.</summary>
    private static decimal ServiceFee(Booking b, Context ctx) =>
        ctx.ServiceFeeRefundsUsed < ServiceFeeRefundsPerYear ? b.ServiceFee : 0m;

    /// <summary>The policy table itself: share of the room charge, and whether the service fee comes back.</summary>
    private static (decimal RoomShare, bool ServiceFee, string Note) Policy(
        CancellationTier tier, int daysToCheckIn, int nights) => tier switch
    {
        CancellationTier.Flexible => daysToCheckIn >= 1
            ? (1m, true, "Chính sách Linh hoạt: huỷ trước 24 giờ nên hoàn 100% tiền phòng.")
            // "mất đêm đầu, hoàn các đêm còn lại"
            : (nights > 1 ? (nights - 1m) / nights : 0m, false,
               "Chính sách Linh hoạt: huỷ trong vòng 24 giờ nên mất đêm đầu tiên, các đêm còn lại được hoàn."),

        CancellationTier.Moderate => daysToCheckIn >= 5
            ? (1m, true, "Chính sách Vừa phải: huỷ trước 5 ngày nên hoàn 100% tiền phòng.")
            : (0.5m, false, "Chính sách Vừa phải: huỷ trong vòng 5 ngày nên hoàn 50% tiền phòng."),

        CancellationTier.Strict => daysToCheckIn switch
        {
            >= 30 => (1m, true, "Chính sách Chặt: huỷ trước 30 ngày nên hoàn 100% tiền phòng."),
            >= 7 => (0.5m, false, "Chính sách Chặt: huỷ trước 7–30 ngày nên hoàn 50% tiền phòng."),
            _ => (0m, false, "Chính sách Chặt: huỷ trong vòng 7 ngày nên không hoàn tiền phòng.")
        },

        CancellationTier.SuperStrict => daysToCheckIn >= 7
            ? (0.5m, false, "Chính sách Rất chặt: huỷ trước 7 ngày nên hoàn 50% tiền phòng.")
            : (0m, false, "Chính sách Rất chặt: huỷ trong vòng 7 ngày nên không hoàn tiền phòng."),

        CancellationTier.NonRefundable =>
            (0m, false, "Chính sách Không hoàn: tiền phòng không được hoàn ở mọi thời điểm."),

        // Long-term stays: the host keeps the first 30 nights, the rest comes back.
        _ => daysToCheckIn >= 30
            ? (1m, true, "Chính sách Dài hạn – chặt: huỷ trước 30 ngày nên hoàn 100% tiền phòng.")
            : (nights > 30 ? (nights - 30m) / nights : 0m, false,
               "Chính sách Dài hạn – chặt: khách trả 30 đêm đầu, phần còn lại được hoàn.")
    };

    public static string Label(CancellationTier tier) => tier switch
    {
        CancellationTier.Flexible => "Linh hoạt",
        CancellationTier.Moderate => "Vừa phải",
        CancellationTier.Strict => "Chặt",
        CancellationTier.SuperStrict => "Rất chặt",
        CancellationTier.NonRefundable => "Không hoàn",
        _ => "Dài hạn – chặt"
    };

    public static string Summary(CancellationTier tier) => tier switch
    {
        CancellationTier.Flexible =>
            "Huỷ miễn phí đến 24 giờ trước khi nhận phòng; sau đó mất đêm đầu tiên.",
        CancellationTier.Moderate =>
            "Huỷ miễn phí đến 5 ngày trước khi nhận phòng; sau đó hoàn 50% tiền phòng.",
        CancellationTier.Strict =>
            "Hoàn 100% nếu huỷ trước 30 ngày, 50% nếu huỷ trước 7 ngày, sau đó không hoàn tiền phòng.",
        CancellationTier.SuperStrict =>
            "Hoàn 50% nếu huỷ trước 7 ngày, sau đó không hoàn tiền phòng.",
        CancellationTier.NonRefundable =>
            "Không hoàn tiền phòng; đổi lại bạn được giảm 10% giá.",
        _ =>
            "Kỳ ở dài: hoàn 100% nếu huỷ trước 30 ngày, sau đó khách trả 30 đêm đầu."
    } + " Phí vệ sinh luôn được hoàn 100%.";

    /// <summary>Which tiers may be marketed as "free cancellation" in search filters.</summary>
    public static bool HasFreeCancellation(CancellationTier tier) =>
        tier is CancellationTier.Flexible or CancellationTier.Moderate;

    /// <summary>
    /// The one-line promise a card or a listing header may make about cancelling.
    ///
    /// It exists because the header used to be the fixed string "Huỷ miễn phí
    /// trước 48 giờ" on every listing — a number that is not any of the six
    /// policies, printed directly above the sentence that contradicted it. A
    /// promise shown to a guest has to come from the same switch the refund
    /// does, so a tier added later cannot quietly keep the old promise.
    /// </summary>
    public static string Headline(CancellationTier tier) => tier switch
    {
        CancellationTier.Flexible => "Huỷ miễn phí đến 24 giờ trước khi nhận phòng",
        CancellationTier.Moderate => "Huỷ miễn phí đến 5 ngày trước khi nhận phòng",
        CancellationTier.Strict => "Hoàn 100% nếu huỷ trước 30 ngày",
        CancellationTier.SuperStrict => "Hoàn 50% nếu huỷ trước 7 ngày",
        CancellationTier.NonRefundable => "Không hoàn tiền phòng, đổi lại giá rẻ hơn 10%",
        _ => "Kỳ ở dài: hoàn 100% nếu huỷ trước 30 ngày"
    };

    /// <summary>
    /// The exact moment free cancellation stops, so the UI can say
    /// "Huỷ miễn phí trước 14:00 ngày 12/09" rather than a vague number of days.
    /// </summary>
    public static DateOnly? FreeCancellationDeadline(Booking b) => b.CancellationTier switch
    {
        CancellationTier.Flexible => b.CheckIn.AddDays(-1),
        CancellationTier.Moderate => b.CheckIn.AddDays(-5),
        CancellationTier.Strict or CancellationTier.LongTermStrict => b.CheckIn.AddDays(-30),
        _ => null
    };
}
