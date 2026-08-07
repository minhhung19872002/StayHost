namespace StayHost.Domain;

public enum NotificationKind
{
    BookingCreated = 0,
    BookingConfirmed = 1,
    BookingDeclined = 2,
    BookingCancelled = 3,
    MessageReceived = 4,
    ReviewReceived = 5,
    ListingApproved = 6,
    ListingRejected = 7,
    System = 8,

    // Appended, never reordered: the number is what is in the database.
    /// <summary>docs/03 §11 — money reached the host.</summary>
    PayoutSent = 9,
    /// <summary>docs/03 §11 — 7 days out, 24 hours out, and check-out morning.</summary>
    StayReminder = 10,
    /// <summary>docs/03 §11 — a saved listing got cheaper. The one kind that is marketing.</summary>
    PriceDrop = 11,
    /// <summary>docs/07 §10 — money went back to the guest. Never silenceable.</summary>
    RefundIssued = 12
}

/// <summary>In-app notification. The same row is what an email job would send.</summary>
public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }

    public NotificationKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    /// <summary>Client-side route to open when the notification is clicked.</summary>
    public string? Link { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    public DateTime? EmailedAt { get; set; }
}

public enum ReportStatus
{
    Open = 0,
    Reviewing = 1,
    Resolved = 2,
    Dismissed = 3
}

/// <summary>Guest-submitted report about a listing; worked through in the admin console.</summary>
public class ListingReport
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public Listing? Listing { get; set; }

    public int? ReporterUserId { get; set; }
    public User? ReporterUser { get; set; }
    public string SessionId { get; set; } = "";

    public string Reason { get; set; } = "";
    public string? Detail { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.Open;
    public string? Resolution { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}

/// <summary>Queued outbound email. A worker would drain this; here it is an audit trail.</summary>
public class EmailMessage
{
    public int Id { get; set; }
    public string ToEmail { get; set; } = "";
    public string ToName { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// docs/03 §6 — detail-page views, one row per listing per day.
///
/// A lifetime counter would not answer the question the ranking asks, which is
/// about "gần đây": a place that converted well two years ago and nothing since
/// should not be pushed up today. Daily rows are small — one per listing per day
/// — and let the window move.
/// </summary>
public class ListingView
{
    public long Id { get; set; }

    public int ListingId { get; set; }
    public Listing? Listing { get; set; }

    public DateOnly Day { get; set; }
    public int Views { get; set; }
}
