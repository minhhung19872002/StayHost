namespace StayHost.Domain;

/// <summary>
/// One line of a booking's history. docs/00 §6.2: a state change is recorded,
/// never overwritten, so these rows are only ever inserted.
/// </summary>
public class BookingEvent
{
    public long Id { get; set; }

    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    /// <summary>Null on the row that created the booking.</summary>
    public BookingStatus? FromStatus { get; set; }
    public BookingStatus ToStatus { get; set; }

    /// <summary>Who did it: "guest:12", "host:3", "admin:1" or "system".</summary>
    public string Actor { get; set; } = "system";
    public string Reason { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// The state machine of docs/03 §3, plus the two timers that drive it: the
/// 15-minute payment hold and the 24-hour limit on a request to book.
/// </summary>
public static class BookingLifecycle
{
    /// <summary>docs/03 §2 — dates are held this long while the guest pays.</summary>
    public static readonly TimeSpan PaymentHold = TimeSpan.FromMinutes(15);

    /// <summary>docs/03 §3 — a request the host ignores expires after this.</summary>
    public static readonly TimeSpan RequestWindow = TimeSpan.FromHours(24);

    /// <summary>Exactly the arrows drawn in docs/03 §3, nothing else.</summary>
    private static readonly Dictionary<BookingStatus, BookingStatus[]> Allowed = new()
    {
        [BookingStatus.PendingHostApproval] =
        [
            BookingStatus.PendingPayment, BookingStatus.Declined,
            BookingStatus.Expired, BookingStatus.CancelledByGuest
        ],
        [BookingStatus.PendingPayment] =
        [
            BookingStatus.Confirmed, BookingStatus.PaymentFailed, BookingStatus.CancelledByGuest
        ],
        [BookingStatus.Confirmed] =
        [
            BookingStatus.InProgress, BookingStatus.CancelledByGuest, BookingStatus.CancelledByHost
        ],
        [BookingStatus.InProgress] =
        [
            BookingStatus.Completed, BookingStatus.CancelledByGuest, BookingStatus.CancelledByHost
        ],
        // Terminal states.
        [BookingStatus.Completed] = [],
        [BookingStatus.Declined] = [],
        [BookingStatus.Expired] = [],
        [BookingStatus.PaymentFailed] = [],
        [BookingStatus.CancelledByGuest] = [],
        [BookingStatus.CancelledByHost] = []
    };

    public static bool CanTransition(BookingStatus from, BookingStatus to) =>
        Allowed.TryGetValue(from, out var next) && next.Contains(to);

    public sealed class IllegalTransitionException(BookingStatus from, BookingStatus to)
        : InvalidOperationException($"Không thể chuyển đơn từ \"{Label(from)}\" sang \"{Label(to)}\".");

    /// <summary>
    /// Moves the booking and returns the history row to append. The caller adds
    /// it to the unit of work, so a rejected transition never half-applies.
    /// </summary>
    public static BookingEvent Transition(Booking booking, BookingStatus to, string actor, string reason)
    {
        if (!CanTransition(booking.Status, to)) throw new IllegalTransitionException(booking.Status, to);

        var evt = new BookingEvent
        {
            BookingId = booking.Id,
            FromStatus = booking.Status,
            ToStatus = to,
            Actor = actor,
            Reason = reason
        };

        booking.Status = to;
        if (to is BookingStatus.PendingPayment)
            booking.HoldExpiresAt = DateTime.UtcNow + PaymentHold;
        else
            booking.HoldExpiresAt = null;

        booking.Events.Add(evt);
        return evt;
    }

    /// <summary>
    /// Something worth recording that did not change the status — a second
    /// charge landing, say. The history is append-only either way.
    /// </summary>
    public static BookingEvent Note(Booking booking, string actor, string reason)
    {
        var evt = new BookingEvent
        {
            BookingId = booking.Id,
            FromStatus = booking.Status,
            ToStatus = booking.Status,
            Actor = actor,
            Reason = reason
        };
        booking.Events.Add(evt);
        return evt;
    }

    /// <summary>The row that records a booking being created in the first place.</summary>
    public static BookingEvent Created(Booking booking, string actor, string reason) =>
        new() { BookingId = booking.Id, FromStatus = null, ToStatus = booking.Status, Actor = actor, Reason = reason };

    /// <summary>
    /// Statuses that take the dates off the market. A request awaiting host
    /// approval is deliberately absent: docs/03 §2 says it must not hold dates.
    /// </summary>
    public static readonly BookingStatus[] BlocksDates =
    [
        BookingStatus.PendingPayment, BookingStatus.Confirmed,
        BookingStatus.InProgress, BookingStatus.Completed
    ];

    public static bool HoldsDates(BookingStatus s) => BlocksDates.Contains(s);

    public static bool IsCancelled(BookingStatus s) =>
        s is BookingStatus.CancelledByGuest or BookingStatus.CancelledByHost
          or BookingStatus.Declined or BookingStatus.Expired or BookingStatus.PaymentFailed;

    /// <summary>The stay is real money that has not finished yet.</summary>
    public static bool IsLive(BookingStatus s) =>
        s is BookingStatus.Confirmed or BookingStatus.InProgress;

    public static string Label(BookingStatus s) => s switch
    {
        BookingStatus.PendingHostApproval => "Chờ chủ nhà duyệt",
        BookingStatus.PendingPayment => "Chờ thanh toán",
        BookingStatus.Confirmed => "Đã xác nhận",
        BookingStatus.InProgress => "Đang lưu trú",
        BookingStatus.Completed => "Đã hoàn tất",
        BookingStatus.Declined => "Bị từ chối",
        BookingStatus.Expired => "Hết hạn",
        BookingStatus.PaymentFailed => "Không thành công",
        BookingStatus.CancelledByGuest => "Khách đã huỷ",
        _ => "Chủ nhà đã huỷ"
    };

    /// <summary>CSS badge class the UI already ships: pending / confirmed / cancelled.</summary>
    public static string BadgeClass(BookingStatus s) =>
        IsCancelled(s) ? "cancelled"
        : s is BookingStatus.Confirmed or BookingStatus.InProgress or BookingStatus.Completed ? "confirmed"
        : "pending";
}
