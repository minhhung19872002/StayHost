namespace StayHost.Domain;

public enum SpecialOfferStatus
{
    /// <summary>Sent, still within its 24 hours, not yet acted on.</summary>
    Pending = 0,
    Accepted = 1,
    /// <summary>The host took it back before the guest acted.</summary>
    Withdrawn = 2,
    /// <summary>The 24 hours ran out.</summary>
    Expired = 3
}

/// <summary>
/// docs/01 ĐP-17, QL-14 — a private price a host offers one guest in a message
/// thread: a special nightly rate for specific dates, good for 24 hours. Accepting
/// it books at that rate. Because the discount is the host's own, it rides through
/// the nightly rate rather than a platform-funded coupon.
/// </summary>
public class SpecialOffer
{
    public int Id { get; set; }

    public int ThreadId { get; set; }
    public MessageThread? Thread { get; set; }

    public int ListingId { get; set; }
    public Listing? Listing { get; set; }

    public int HostUserId { get; set; }
    public int GuestUserId { get; set; }

    public DateOnly CheckIn { get; set; }
    public DateOnly CheckOut { get; set; }
    public int Guests { get; set; } = 1;

    /// <summary>The special price per night the host is offering.</summary>
    public decimal NightlyRate { get; set; }

    public SpecialOfferStatus Status { get; set; } = SpecialOfferStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    /// <summary>Set once a guest books against it, so it cannot be used twice.</summary>
    public int? BookingId { get; set; }
}

/// <summary>
/// docs/01 ĐP-17 — the rules around a private offer, kept pure so the 24-hour
/// window and the guardrails can be tested without a database.
/// </summary>
public static class SpecialOffers
{
    /// <summary>docs/04 §5c — a private offer is good for 24 hours.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);

    public static DateTime ExpiryFrom(DateTime sentAt) => sentAt + Window;

    /// <summary>
    /// What a host may offer, before it touches the database. The caller still has
    /// to prove the host owns the thread's listing and the dates are free.
    /// </summary>
    public static string? Validate(DateOnly checkIn, DateOnly checkOut, decimal nightlyRate, int guests)
    {
        if (checkOut <= checkIn) return "Ngày trả phòng phải sau ngày nhận phòng.";
        if (nightlyRate <= 0) return "Giá đặc biệt phải lớn hơn 0.";
        if (guests < 1) return "Số khách không hợp lệ.";
        return null;
    }

    /// <summary>
    /// True when the guest can still act on it: sent, unspent, inside the window.
    /// A status already moved off Pending is decisive on its own; the clock only
    /// matters while it is still Pending, which is what lets a sweep mark the
    /// lapsed ones without racing a guest who is accepting at the same moment.
    /// </summary>
    public static bool IsLive(SpecialOffer offer, DateTime now) =>
        offer.Status == SpecialOfferStatus.Pending && now < offer.ExpiresAt;

    public static string StatusLabel(SpecialOfferStatus status) => status switch
    {
        SpecialOfferStatus.Accepted => "Đã đặt theo ưu đãi này",
        SpecialOfferStatus.Withdrawn => "Chủ nhà đã thu hồi ưu đãi",
        SpecialOfferStatus.Expired => "Ưu đãi đã hết hạn",
        _ => "Đang chờ bạn"
    };
}
