namespace StayHost.Domain;

public enum RiskKind
{
    /// <summary>A days-old account booking a lot of money at once.</summary>
    NewAccountLargeBooking = 0,
    /// <summary>Several different cards on one account in a short span.</summary>
    ManyCards = 1,
    /// <summary>A run of cancellations.</summary>
    ManyCancellations = 2,
    /// <summary>Bookings piling up faster than a person travels.</summary>
    RapidBookings = 3,

    /// <summary>
    /// docs/07 §11 step 6 — an account that has gone to its bank about Staylio
    /// more than once and been found wrong. Unlike the others, this one is not
    /// raised by the periodic sweep: it is raised the moment arbitration lands,
    /// because that is when the fact becomes true.
    /// </summary>
    RepeatChargebacks = 4
}

public enum RiskSeverity
{
    Watch = 0,
    Review = 1,
    Urgent = 2
}

/// <summary>What a check found: nothing, or something worth a human looking at.</summary>
public readonly record struct RiskSignal(RiskKind Kind, RiskSeverity Severity, string Summary, string Detail);

/// <summary>
/// Everything the checks need about one account, gathered once so the rules
/// themselves stay pure and testable.
/// </summary>
public sealed record RiskSnapshot
{
    public required DateTime AccountCreatedAt { get; init; }
    public required DateTime Now { get; init; }

    /// <summary>The booking that triggered the check, if a booking did.</summary>
    public decimal BookingTotal { get; init; }

    /// <summary>Distinct card endings used in the last month.</summary>
    public int DistinctCards { get; init; }

    /// <summary>Bookings the guest cancelled in the last quarter.</summary>
    public int RecentCancellations { get; init; }

    /// <summary>Bookings paid for in the last 24 hours, this one included.</summary>
    public int BookingsToday { get; init; }

    public int AccountAgeDays => Math.Max(0, (int)(Now - AccountCreatedAt).TotalDays);
}

/// <summary>
/// docs/01 AT-11 — "tài khoản mới đặt đơn giá trị lớn, nhiều thẻ, nhiều đơn
/// huỷ". These rules raise a flag for a person to look at; nothing here blocks
/// a booking on its own, because a false positive would turn away a real guest.
/// </summary>
public static class RiskSignals
{
    public const int NewAccountDays = 7;
    public const decimal LargeBooking = 20_000_000m;
    public const int CardsBeforeFlag = 3;
    public const int CancellationsBeforeFlag = 3;
    public const int BookingsPerDayBeforeFlag = 4;

    public static IReadOnlyList<RiskSignal> Check(RiskSnapshot s)
    {
        var signals = new List<RiskSignal>();

        if (s.AccountAgeDays < NewAccountDays && s.BookingTotal >= LargeBooking)
        {
            signals.Add(new RiskSignal(
                RiskKind.NewAccountLargeBooking,
                s.BookingTotal >= LargeBooking * 2 ? RiskSeverity.Urgent : RiskSeverity.Review,
                "Tài khoản mới đặt đơn giá trị lớn",
                $"Tài khoản mở {s.AccountAgeDays} ngày, đơn {s.BookingTotal:#,##0}₫."));
        }

        if (s.DistinctCards >= CardsBeforeFlag)
        {
            signals.Add(new RiskSignal(
                RiskKind.ManyCards,
                s.DistinctCards >= CardsBeforeFlag * 2 ? RiskSeverity.Urgent : RiskSeverity.Review,
                "Dùng nhiều thẻ khác nhau",
                $"{s.DistinctCards} thẻ khác nhau trong 30 ngày."));
        }

        if (s.RecentCancellations >= CancellationsBeforeFlag)
        {
            signals.Add(new RiskSignal(
                RiskKind.ManyCancellations,
                RiskSeverity.Watch,
                "Huỷ nhiều đơn",
                $"{s.RecentCancellations} đơn bị huỷ trong 90 ngày."));
        }

        if (s.BookingsToday >= BookingsPerDayBeforeFlag)
        {
            signals.Add(new RiskSignal(
                RiskKind.RapidBookings,
                RiskSeverity.Review,
                "Đặt liên tiếp trong thời gian ngắn",
                $"{s.BookingsToday} đơn trong 24 giờ."));
        }

        return signals;
    }

    public static string Label(RiskSeverity severity) => severity switch
    {
        RiskSeverity.Urgent => "Khẩn",
        RiskSeverity.Review => "Cần xem",
        _ => "Theo dõi"
    };

    public static string BadgeClass(RiskSeverity severity) => severity switch
    {
        RiskSeverity.Urgent => "cancelled",
        RiskSeverity.Review => "pending",
        _ => "confirmed"
    };
}

public enum RiskFlagStatus
{
    Open = 0,
    Cleared = 1,
    Acted = 2
}

/// <summary>A signal that was raised, kept so the same thing is not flagged twice.</summary>
public class RiskFlag
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public int? BookingId { get; set; }
    public Booking? Booking { get; set; }

    public RiskKind Kind { get; set; }
    public RiskSeverity Severity { get; set; }
    public string Summary { get; set; } = "";
    public string Detail { get; set; } = "";

    public RiskFlagStatus Status { get; set; } = RiskFlagStatus.Open;
    public string? Resolution { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}
