namespace StayHost.Domain;

/// <summary>
/// docs/01 QL-19 — what a co-host is allowed to touch. Flags, because an owner
/// hands out any combination and revokes them one at a time.
/// </summary>
[Flags]
public enum CoHostScope
{
    None = 0,
    /// <summary>Block days, open days, change the minimum stay.</summary>
    Calendar = 1,
    /// <summary>Base rate, seasons, day overrides, discounts.</summary>
    Pricing = 2,
    /// <summary>Read and answer the guest.</summary>
    Messages = 4,
    /// <summary>Accept or decline requests, and see who is arriving.</summary>
    Bookings = 8,
    /// <summary>Edit the listing itself: photos, description, rules.</summary>
    Listing = 16,

    /// <summary>Everything except money leaving the account.</summary>
    Full = Calendar | Pricing | Messages | Bookings | Listing
}

public enum CoHostStatus
{
    Invited = 0,
    Active = 1,
    Revoked = 2,
    Declined = 3
}

/// <summary>
/// A person the host asked to help run one listing or all of them. The invite
/// is keyed by email so it can be sent before that person has an account.
/// </summary>
public class CoHost
{
    public int Id { get; set; }

    /// <summary>The owner handing out the access.</summary>
    public int OwnerUserId { get; set; }
    public User? OwnerUser { get; set; }

    public string Email { get; set; } = "";

    /// <summary>Filled in the moment the invited person accepts.</summary>
    public int? CoHostUserId { get; set; }
    public User? CoHostUser { get; set; }

    /// <summary>Null means every listing the owner has, now and later.</summary>
    public int? ListingId { get; set; }
    public Listing? Listing { get; set; }

    public CoHostScope Scope { get; set; } = CoHostScope.Calendar | CoHostScope.Messages;
    public CoHostStatus Status { get; set; } = CoHostStatus.Invited;

    public string InviteToken { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime InvitedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public static class CoHostScopes
{
    public static readonly (CoHostScope Scope, string Key, string Label)[] All =
    [
        (CoHostScope.Calendar, "calendar", "Lịch trống"),
        (CoHostScope.Pricing, "pricing", "Giá"),
        (CoHostScope.Messages, "messages", "Tin nhắn"),
        (CoHostScope.Bookings, "bookings", "Đơn đặt"),
        (CoHostScope.Listing, "listing", "Nội dung tin đăng")
    ];

    public static CoHostScope Parse(IEnumerable<string>? keys)
    {
        var scope = CoHostScope.None;
        foreach (var key in keys ?? [])
        {
            var match = All.FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (match.Key is not null) scope |= match.Scope;
        }
        return scope;
    }

    public static IReadOnlyList<string> Keys(CoHostScope scope) =>
        All.Where(s => scope.HasFlag(s.Scope)).Select(s => s.Key).ToList();

    public static string Describe(CoHostScope scope) =>
        scope == CoHostScope.None
            ? "Chưa có quyền nào"
            : string.Join(", ", All.Where(s => scope.HasFlag(s.Scope)).Select(s => s.Label));
}

/// <summary>
/// docs/01 QL-10 — a calendar the host keeps somewhere else and wants honoured
/// here. Everything it brings in becomes a block, never a booking: another
/// platform's dates cost this host nothing but availability.
/// </summary>
public class CalendarFeed
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public Listing? Listing { get; set; }

    public string Label { get; set; } = "";
    public string Url { get; set; } = "";

    public DateTime? LastSyncedAt { get; set; }
    public string? LastError { get; set; }
    public int EventCount { get; set; }

    /// <summary>
    /// docs/01 QL-11 — a warning when the last import clashed with a confirmed
    /// Staylio booking: the same nights are sold on both platforms. Null when the
    /// feeds are consistent.
    /// </summary>
    public string? OverlapWarning { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
