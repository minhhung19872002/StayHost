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
    RefundIssued = 12,
    /// <summary>docs/01 TM-23 — a saved search has new matching places.</summary>
    SavedSearchMatch = 13
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

/// <summary>docs/01 AT-02 — the four things somebody can report.</summary>
public enum ReportTarget
{
    Listing = 0,
    User = 1,
    Message = 2,
    Review = 3
}

/// <summary>
/// docs/01 AT-02 — a report about a listing, a person, a message or a review,
/// worked through in the admin console. One row carries exactly one subject: the
/// id matching <see cref="Target"/> is set and the other three stay empty, which
/// is what keeps "what was reported" answerable without guessing.
/// </summary>
public class AbuseReport
{
    public int Id { get; set; }

    public ReportTarget Target { get; set; }

    public int? ListingId { get; set; }
    public Listing? Listing { get; set; }

    public int? ReportedUserId { get; set; }
    public User? ReportedUser { get; set; }

    public int? MessageId { get; set; }
    public Message? Message { get; set; }

    public int? ReviewId { get; set; }
    public Review? Review { get; set; }

    public int? ReporterUserId { get; set; }
    public User? ReporterUser { get; set; }
    public string SessionId { get; set; } = "";

    public string Reason { get; set; } = "";
    public string? Detail { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.Open;
    public string? Resolution { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }

    /// <summary>The id of whatever this report is about, whichever kind it is.</summary>
    public int? SubjectId => Target switch
    {
        ReportTarget.Listing => ListingId,
        ReportTarget.User => ReportedUserId,
        ReportTarget.Message => MessageId,
        ReportTarget.Review => ReviewId,
        _ => null
    };
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

    /// <summary>How many times a mail server has been asked to take this.</summary>
    public int Attempts { get; set; }

    /// <summary>
    /// When to try again. Null with <see cref="SentAt"/> also null means the
    /// message has been given up on — see <see cref="EmailDelivery"/>.
    /// </summary>
    public DateTime? NextAttemptAt { get; set; }

    /// <summary>Set when the receiving server refused outright and always will.</summary>
    public bool Undeliverable { get; set; }

    /// <summary>
    /// docs/01 TK-09 — the reader's language. Null means unknown, which means
    /// Vietnamese: exactly what every mail was before the column existed.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// The untranslated title and body, kept apart from the assembled
    /// <see cref="Body"/> so the dispatcher can machine-translate the CONTENT
    /// while the frame stays hand-translated (Emails.Compose). Null on
    /// secret-bearing mail — codes and tokens are never handed to a machine
    /// that might "improve" a digit — and on everything queued before the
    /// columns existed, which the dispatcher therefore leaves untouched.
    /// </summary>
    public string? RawTitle { get; set; }
    public string? RawBody { get; set; }
    /// <summary>The absolute link, already resolved at queue time.</summary>
    public string? CtaUrl { get; set; }

    /// <summary>
    /// When the translation pass finished with this row — set even when the
    /// translator failed, because the Vietnamese original is the designed
    /// fallback and a mail must never sit in the queue waiting on a translator.
    /// </summary>
    public DateTime? TranslatedAt { get; set; }
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
