namespace StayHost.Domain;

/// <summary>docs/08 §5 — the ladder, lightest first.</summary>
public enum SanctionLevel
{
    /// <summary>§5.1 — said, recorded, nothing blocked.</summary>
    Warning = 1,
    /// <summary>§5.2 — the one behaviour blocked, the rest left alone.</summary>
    Restriction = 2,
    /// <summary>§5.3 — cannot sign in. Live bookings are decided by §6.</summary>
    Suspension = 3,
    /// <summary>§5.4 — Super only, and only for the six things §5.4 lists.</summary>
    Ban = 4
}

/// <summary>docs/08 §5.2 — the individual blocks, each naming what it does not touch.</summary>
public enum RestrictionKind
{
    NoNewBookings = 0,
    NoNewListings = 1,
    ListingsHiddenFromSearch = 2,
    NoReviews = 3,
    NoNewConversations = 4,
    PayoutsHeld = 5
}

/// <summary>One thing that was done to an account, and why.</summary>
public class Sanction
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public SanctionLevel Level { get; set; }

    /// <summary>Set only when <see cref="Level"/> is Restriction.</summary>
    public RestrictionKind? Restriction { get; set; }

    /// <summary>Which policy was broken. docs/08 §5.1 — a warning names the rule.</summary>
    public string Policy { get; set; } = "";

    /// <summary>docs/08 §1.4 — never empty. The row cannot be saved without it.</summary>
    public string Reason { get; set; } = "";

    /// <summary>What the person has to do to get it lifted, in their own language.</summary>
    public string? LiftedWhen { get; set; }

    public int DecidedByUserId { get; set; }
    public User? DecidedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Null means open-ended — until the condition in <see cref="LiftedWhen"/> is met.</summary>
    public DateTime? ExpiresAt { get; set; }

    /* ------------------------------------------------------- being undone */

    public DateTime? LiftedAt { get; set; }
    public int? LiftedByUserId { get; set; }
    public string? LiftedReason { get; set; }

    /// <summary>
    /// docs/08 §8 — an appeal that succeeded outright takes the entry off the
    /// record: "Gỡ bỏ thì xoá luôn khỏi hồ sơ vi phạm." The row survives for the
    /// audit trail; it just stops counting against the person.
    /// </summary>
    public bool OverturnedOnAppeal { get; set; }

    /// <summary>docs/08 §5.6 — jumped straight past the lighter steps.</summary>
    public bool Severe { get; set; }

    public bool IsActive(DateTime now) =>
        LiftedAt is null && !OverturnedOnAppeal && (ExpiresAt is null || ExpiresAt > now);
}

/// <summary>docs/08 §5 — how far you may go, and in what order.</summary>
public static class Sanctions
{
    /* ------------------------------------------------ §5, one step at a time */

    /// <summary>
    /// docs/08 §5 — "Bắt buộc đi tuần tự, trừ vi phạm nghiêm trọng ở §5.6."
    ///
    /// The ladder is not bureaucracy: skipping it is how somebody loses their
    /// livelihood over a first offence nobody told them about.
    /// </summary>
    public static bool MaySkipLadder(bool severe, SanctionLevel to) =>
        severe && to <= SanctionLevel.Suspension;

    /// <summary>
    /// The next level allowed, given what the person has already had. Null when
    /// they are already at the top.
    /// </summary>
    public static SanctionLevel? NextStep(SanctionLevel? highestSoFar) => highestSoFar switch
    {
        null => SanctionLevel.Warning,
        SanctionLevel.Warning => SanctionLevel.Restriction,
        SanctionLevel.Restriction => SanctionLevel.Suspension,
        SanctionLevel.Suspension => SanctionLevel.Ban,
        _ => null
    };

    public static bool IsInOrder(SanctionLevel? highestSoFar, SanctionLevel to, bool severe)
    {
        if (MaySkipLadder(severe, to)) return true;

        // Going no higher than before is always allowed: a second warning after a
        // suspension is a lighter touch, not a skipped step.
        if (highestSoFar is { } had && to <= had) return true;

        return NextStep(highestSoFar) is { } next && to <= next;
    }

    public static string OutOfOrderMessage(SanctionLevel? highestSoFar, SanctionLevel to)
    {
        var next = NextStep(highestSoFar);
        return next is null
            ? $"Tài khoản này đã ở mức cao nhất, không nâng lên {Label(to).ToLowerInvariant()} được nữa."
            : $"Chưa được nhảy thẳng lên {Label(to).ToLowerInvariant()}. " +
              $"Bước tiếp theo là {Label(next.Value).ToLowerInvariant()}, " +
              "trừ khi đây là vi phạm nghiêm trọng theo docs/08 §5.6.";
    }

    /* -------------------------------------------------------- §5.6, severe */

    /// <summary>
    /// docs/08 §5.6 — the six things that may go straight to a suspension.
    /// Written out rather than left to judgement, because "nghiêm trọng" is
    /// exactly the word that stretches when somebody is in a hurry.
    /// </summary>
    public static readonly IReadOnlyList<string> SevereGrounds =
    [
        "Đe doạ hoặc bạo lực",
        "Nội dung liên quan tới trẻ em",
        "Gian lận thanh toán có bằng chứng",
        "Giả mạo giấy tờ",
        "Chiếm đoạt tài khoản người khác",
        "Lừa đảo có tổ chức"
    ];

    public static bool IsSevereGround(string? ground) =>
        ground is not null && SevereGrounds.Any(g => string.Equals(g, ground.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>docs/08 §5.6 — "chuyển hồ sơ cho Quản trị tối cao xem lại trong 24 giờ".</summary>
    public static readonly TimeSpan SevereReviewWindow = TimeSpan.FromHours(24);

    public static DateTime SevereReviewDueBy(DateTime decidedAt) => decidedAt + SevereReviewWindow;

    /* ---------------------------------------------------------- §5.4, bans */

    /// <summary>
    /// docs/08 §5.4 — a permanent ban is only for these. Anything else that felt
    /// bad enough at the time is a suspension.
    /// </summary>
    public static readonly IReadOnlyList<string> BanGrounds =
    [
        "Gian lận tiền",
        "Giả mạo danh tính",
        "Đe doạ an toàn người khác",
        "Tái phạm nhiều lần sau khi đã cảnh cáo và tạm khoá"
    ];

    /// <summary>
    /// docs/08 §5.4 — a ban blocks the same email, phone, device and payout
    /// account from coming back under a new name.
    /// </summary>
    public static readonly IReadOnlyList<string> BanBlocks =
    [
        "email", "số điện thoại", "thiết bị", "tài khoản nhận tiền"
    ];

    /* ------------------------------------------------------------- labels */

    public static string Label(SanctionLevel level) => level switch
    {
        SanctionLevel.Warning => "Cảnh cáo",
        SanctionLevel.Restriction => "Hạn chế một phần",
        SanctionLevel.Suspension => "Tạm khoá",
        _ => "Khoá vĩnh viễn"
    };

    public static string RestrictionLabel(RestrictionKind kind) => kind switch
    {
        RestrictionKind.NoNewBookings => "Không được đặt đơn mới",
        RestrictionKind.NoNewListings => "Không được đăng tin mới",
        RestrictionKind.ListingsHiddenFromSearch => "Tin đăng bị ẩn khỏi tìm kiếm",
        RestrictionKind.NoReviews => "Không được viết đánh giá",
        RestrictionKind.NoNewConversations => "Không được nhắn tin cho người mới",
        _ => "Khoản chuyển tiền bị giữ lại"
    };

    /// <summary>
    /// docs/08 §5.2 — each restriction says what it leaves alone. A person told
    /// only what they have lost assumes they have lost everything.
    /// </summary>
    public static string RestrictionKeeps(RestrictionKind kind) => kind switch
    {
        RestrictionKind.NoNewBookings => "Đơn đang có vẫn chạy bình thường.",
        RestrictionKind.NoNewListings => "Tin cũ vẫn hiển thị.",
        RestrictionKind.ListingsHiddenFromSearch => "Đơn đã xác nhận vẫn được thực hiện.",
        RestrictionKind.NoNewConversations => "Cuộc trò chuyện đang có vẫn dùng được.",
        RestrictionKind.PayoutsHeld => "Tiền vẫn là của bạn, chỉ tạm giữ cho tới khi làm rõ.",
        _ => ""
    };

    /// <summary>What the person is told. A sanction nobody explains cannot be complied with.</summary>
    public static string Notice(Sanction s)
    {
        var head = s.Level == SanctionLevel.Restriction && s.Restriction is { } kind
            ? $"{Label(s.Level)}: {RestrictionLabel(kind).ToLowerInvariant()}"
            : Label(s.Level);

        var keeps = s.Level == SanctionLevel.Restriction && s.Restriction is { } k
            ? " " + RestrictionKeeps(k)
            : "";

        var until = s.ExpiresAt is { } at
            ? $" Có hiệu lực tới {at:HH:mm dd/MM/yyyy}."
            : s.LiftedWhen is { Length: > 0 }
                ? $" Được gỡ khi: {s.LiftedWhen}"
                : "";

        return $"{head}. Lý do: {s.Reason}{(s.Policy.Length > 0 ? $" (chính sách: {s.Policy})" : "")}.{keeps}{until}";
    }
}

/// <summary>
/// docs/08 §5.2 — reading the restriction bitmask on a <see cref="User"/>.
///
/// Separate from <see cref="Sanctions"/> so the places that only need to ask
/// "may this person do X" do not pull in the whole sanction machinery.
/// </summary>
public static class Restrictions
{
    public static int Bit(RestrictionKind kind) => 1 << (int)kind;

    public static bool Has(int mask, RestrictionKind kind) => (mask & Bit(kind)) != 0;

    public static IReadOnlyList<RestrictionKind> Active(int mask) =>
        Enum.GetValues<RestrictionKind>().Where(k => Has(mask, k)).ToList();

    /// <summary>
    /// What a restricted person is told when they hit the wall. It names what
    /// still works, because §5.2's whole shape is "block the behaviour, keep the
    /// rest" and somebody who only hears "no" assumes everything is gone.
    /// </summary>
    public static string Message(RestrictionKind kind)
    {
        var keeps = Sanctions.RestrictionKeeps(kind);
        return $"Tài khoản của bạn đang bị hạn chế: {Sanctions.RestrictionLabel(kind).ToLowerInvariant()}."
               + (keeps.Length > 0 ? $" {keeps}" : "")
               + " Xem chi tiết và cách khắc phục trong phần Tài khoản.";
    }
}
