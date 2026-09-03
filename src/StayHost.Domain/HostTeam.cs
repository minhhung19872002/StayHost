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

    // --- docs/02 G8, docs/07 §19: the optional cut of the owner's earnings.

    public CoHostPayoutKind PayoutKind { get; set; } = CoHostPayoutKind.None;

    /// <summary>Whole percent, e.g. 20 for a fifth. Unused by the flat-amount kinds.</summary>
    public decimal PayoutPercent { get; set; }

    /// <summary>Đồng per booking, for <see cref="CoHostPayoutKind.Fixed"/>.</summary>
    public decimal PayoutFixed { get; set; }

    /// <summary>
    /// Terms only divert money once the person receiving it has agreed. An owner
    /// can change what they offer at any time, but the change goes back to
    /// <see cref="CoHostPayoutStatus.Proposed"/> and the old terms stop — nobody
    /// silently ends up on a smaller share than they said yes to.
    /// </summary>
    public CoHostPayoutStatus PayoutStatus { get; set; } = CoHostPayoutStatus.None;

    public DateTime? PayoutProposedAt { get; set; }
    public DateTime? PayoutRespondedAt { get; set; }

    /// <summary>
    /// Where this co-host's money goes: their own payee record, with their own
    /// bank account, their own verification and their own debt to the platform.
    /// Filled the moment they accept, because a share of somebody's income is
    /// income — the person banking it has to be the one who declared the account.
    /// </summary>
    public int? PayeeHostId { get; set; }
    public HostProfile? PayeeHost { get; set; }
}

/// <summary>
/// docs/07 §19 — one co-host's share of one booking, decided when the transfer
/// was decided and settled when the bank executed it.
///
/// It exists as a row rather than a column on the payment because a payment can
/// only carry one payout reference, and this money leaves in a different
/// transfer to a different person's bank. It is also the only record of what a
/// co-host was actually paid for a given stay, which is what both sides ask for
/// the moment they disagree.
/// </summary>
public class CoHostPayout
{
    public long Id { get; set; }

    public int CoHostId { get; set; }
    public CoHost? CoHost { get; set; }

    /// <summary>The payee record the money is routed to — see <see cref="CoHost.PayeeHostId"/>.</summary>
    public int PayeeHostId { get; set; }
    public HostProfile? PayeeHost { get; set; }

    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    public decimal Amount { get; set; }

    /// <summary>
    /// How the amount was arrived at, in words, kept for the payout statement
    /// and the CSV — both of which are read in Vietnamese by a person.
    /// </summary>
    public string Basis { get; set; } = "";

    /// <summary>
    /// The same thing as numbers, frozen as the terms stood that day.
    ///
    /// The screen builds its own sentence from these rather than showing
    /// <see cref="Basis"/>, because that string carries a percentage inside it
    /// and a dictionary cannot key on "20% mỗi đơn". Keeping the parts also
    /// means a co-host reading their history sees the terms that applied to
    /// each stay, not the ones in force today.
    /// </summary>
    public CoHostPayoutKind Kind { get; set; }
    public decimal Percent { get; set; }
    public decimal Fixed { get; set; }

    /// <summary>The owner's earnings this was carved out of, as they stood that day.</summary>
    public decimal Earnings { get; set; }

    public PayoutStatus Status { get; set; } = PayoutStatus.Scheduled;

    /// <summary>The transfer that carried it, matching <see cref="PayoutBatch.Reference"/>.</summary>
    public string? PayoutReference { get; set; }

    /// <summary>
    /// This share's part of what was held back against the co-host's own debt to
    /// the platform (docs/07 §17.4). What reached their bank is
    /// <see cref="Amount"/> minus this.
    /// </summary>
    public decimal Deducted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidOutAt { get; set; }

    /// <summary>
    /// Set when a booking already paid for was refunded afterwards and the share
    /// had to be taken back off future transfers (docs/07 §19.4). Kept rather
    /// than deleted: the co-host was paid, and the books say so.
    /// </summary>
    public decimal ClawedBack { get; set; }
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
