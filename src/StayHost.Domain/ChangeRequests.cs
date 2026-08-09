namespace StayHost.Domain;

public enum ChangeRequestStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    Expired = 3,
    /// <summary>The guest called it off before the host answered.</summary>
    Withdrawn = 4
}

/// <summary>
/// docs/01 CĐ-06, docs/04 QT-4 — a guest's request to move a confirmed booking to
/// new dates or a new guest count. The old dates stay held while it waits; only
/// an acceptance moves the booking and settles the difference.
/// </summary>
public class BookingChangeRequest
{
    public int Id { get; set; }

    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    public DateOnly NewCheckIn { get; set; }
    public DateOnly NewCheckOut { get; set; }
    public int NewGuests { get; set; }
    public int NewAdults { get; set; }
    public int NewChildren { get; set; }
    public int NewInfants { get; set; }
    public int NewPets { get; set; }

    /// <summary>The re-quoted total for the new stay, and the difference from the old one.</summary>
    public decimal NewTotal { get; set; }
    public decimal Difference { get; set; }

    public ChangeRequestStatus Status { get; set; } = ChangeRequestStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    /// <summary>Who asked — the guest, or the host proposing a change (docs/04 QT-4).</summary>
    public bool ByHost { get; set; }
}

/// <summary>
/// docs/01 CĐ-06 — the rules around a change request that need no database.
/// </summary>
public static class ChangeRequests
{
    /// <summary>docs/04 QT-4 — a change offer is good for 24 hours.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);

    public static DateTime ExpiryFrom(DateTime at) => at + Window;

    public static bool IsLive(BookingChangeRequest r, DateTime now) =>
        r.Status == ChangeRequestStatus.Pending && now < r.ExpiresAt;

    /// <summary>What a guest may ask for, before the calendar and price are checked.</summary>
    public static string? Validate(DateOnly newCheckIn, DateOnly newCheckOut, int adults)
    {
        if (newCheckOut <= newCheckIn) return "Ngày trả phòng phải sau ngày nhận phòng.";
        if (adults < 1) return "Cần ít nhất một khách.";
        return null;
    }

    public static string DiffLabel(decimal difference) => difference switch
    {
        > 0 => $"Bạn cần trả thêm {difference:#,##0}₫.",
        < 0 => $"Bạn được hoàn lại {-difference:#,##0}₫.",
        _ => "Không thay đổi số tiền."
    };
}
