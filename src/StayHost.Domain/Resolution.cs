namespace StayHost.Domain;

/// <summary>What the person who opened the case is asking for (docs/01 AT-04).</summary>
public enum ResolutionKind
{
    /// <summary>Host claims for damage the guest caused.</summary>
    Damage = 0,
    /// <summary>Guest claims the place was not as described or not usable.</summary>
    NotAsDescribed = 1,
    /// <summary>Guest asks for money back outside the cancellation policy.</summary>
    Refund = 2,
    Other = 3
}

/// <summary>
/// docs/01 AT-04 and QT-05. The other party has 24 hours to answer; if they
/// object, an admin decides and the money is split accordingly.
/// </summary>
public enum ResolutionStatus
{
    /// <summary>Opened; waiting on the other party.</summary>
    AwaitingResponse = 0,
    /// <summary>The other party agreed; the amount is paid as claimed.</summary>
    Accepted = 1,
    /// <summary>The other party objected; an admin has to decide.</summary>
    Disputed = 2,
    /// <summary>An admin has ruled and the money has moved.</summary>
    Resolved = 3,
    /// <summary>The opener changed their mind.</summary>
    Withdrawn = 4
}

/// <summary>
/// One claim about one booking. Immutable history lives in
/// <see cref="Events"/>; the row itself only carries the current position.
/// </summary>
public class ResolutionCase
{
    public int Id { get; set; }
    public string Reference { get; set; } = "";

    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    /// <summary>Whoever opened it — guest or host.</summary>
    public int OpenedByUserId { get; set; }
    public User? OpenedByUser { get; set; }
    public bool OpenedByHost { get; set; }

    public ResolutionKind Kind { get; set; }
    public ResolutionStatus Status { get; set; } = ResolutionStatus.AwaitingResponse;

    public decimal AmountClaimed { get; set; }
    public string Description { get; set; } = "";
    /// <summary>Uploaded photos, newline-separated.</summary>
    public string EvidenceUrls { get; set; } = "";

    /// <summary>docs/01 AT-04 — the other party has 24 hours to answer.</summary>
    public DateTime ResponseDueAt { get; set; } = DateTime.UtcNow.AddHours(24);
    public string? Response { get; set; }
    public DateTime? RespondedAt { get; set; }

    // --- the admin's ruling (docs/01 QT-05).
    public int? DecidedByUserId { get; set; }
    public User? DecidedByUser { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? Decision { get; set; }
    /// <summary>How much actually moved, which need not equal the claim.</summary>
    public decimal AmountAwarded { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ResolutionEvent> Events { get; set; } = [];
}

/// <summary>Append-only history of a case, same rule as booking events.</summary>
public class ResolutionEvent
{
    public long Id { get; set; }
    public int CaseId { get; set; }
    public ResolutionCase? Case { get; set; }

    public ResolutionStatus? FromStatus { get; set; }
    public ResolutionStatus ToStatus { get; set; }
    public string Actor { get; set; } = "system";
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>The state machine for a claim, and the words each state goes by.</summary>
public static class Resolutions
{
    public static readonly TimeSpan ResponseWindow = TimeSpan.FromHours(24);

    private static readonly Dictionary<ResolutionStatus, ResolutionStatus[]> Allowed = new()
    {
        [ResolutionStatus.AwaitingResponse] =
        [
            ResolutionStatus.Accepted, ResolutionStatus.Disputed, ResolutionStatus.Withdrawn
        ],
        // An agreed claim still needs an admin to release the money.
        [ResolutionStatus.Accepted] = [ResolutionStatus.Resolved, ResolutionStatus.Withdrawn],
        [ResolutionStatus.Disputed] = [ResolutionStatus.Resolved, ResolutionStatus.Withdrawn],
        [ResolutionStatus.Resolved] = [],
        [ResolutionStatus.Withdrawn] = []
    };

    public static bool CanTransition(ResolutionStatus from, ResolutionStatus to) =>
        Allowed.TryGetValue(from, out var next) && next.Contains(to);

    public sealed class IllegalTransitionException(ResolutionStatus from, ResolutionStatus to)
        : InvalidOperationException($"Không thể chuyển hồ sơ từ \"{Label(from)}\" sang \"{Label(to)}\".");

    public static ResolutionEvent Transition(ResolutionCase c, ResolutionStatus to, string actor, string note)
    {
        if (!CanTransition(c.Status, to)) throw new IllegalTransitionException(c.Status, to);

        var evt = new ResolutionEvent
        {
            CaseId = c.Id, FromStatus = c.Status, ToStatus = to, Actor = actor, Note = note
        };
        c.Status = to;
        c.Events.Add(evt);
        return evt;
    }

    public static ResolutionEvent Opened(ResolutionCase c, string actor, string note) =>
        new() { CaseId = c.Id, FromStatus = null, ToStatus = c.Status, Actor = actor, Note = note };

    public static string Label(ResolutionStatus s) => s switch
    {
        ResolutionStatus.AwaitingResponse => "Chờ bên kia trả lời",
        ResolutionStatus.Accepted => "Bên kia đã đồng ý",
        ResolutionStatus.Disputed => "Đang tranh chấp",
        ResolutionStatus.Resolved => "Đã phân xử",
        _ => "Đã rút lại"
    };

    public static string BadgeClass(ResolutionStatus s) => s switch
    {
        ResolutionStatus.Resolved or ResolutionStatus.Accepted => "confirmed",
        ResolutionStatus.Withdrawn => "cancelled",
        _ => "pending"
    };

    public static string KindLabel(ResolutionKind k) => k switch
    {
        ResolutionKind.Damage => "Bồi thường thiệt hại",
        ResolutionKind.NotAsDescribed => "Không đúng mô tả",
        ResolutionKind.Refund => "Yêu cầu hoàn tiền",
        _ => "Vấn đề khác"
    };

    /// <summary>
    /// A claim can never take back more than the booking was worth, and never
    /// less than nothing.
    /// </summary>
    public static decimal Clamp(decimal amount, Booking booking) =>
        Math.Clamp(Math.Round(amount, 0, MidpointRounding.AwayFromZero), 0m, booking.Total);
}
