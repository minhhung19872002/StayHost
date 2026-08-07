namespace StayHost.Domain;

/// <summary>docs/08 §10 and §3 — the kinds of thing worth looking at twice.</summary>
public enum OversightFlag
{
    /// <summary>§3 — a lot of profiles opened in a short stretch.</summary>
    BrowsingProfiles = 0,
    /// <summary>§3 and §10 — a profile opened with no ticket behind it.</summary>
    NoTicket = 1,
    /// <summary>§3 — the same person looked up again and again.</summary>
    RepeatLookups = 2,
    /// <summary>§3 — work outside working hours.</summary>
    OutOfHours = 3,
    /// <summary>§10 — decisions that keep getting overturned.</summary>
    AppealsUpheldAgainst = 4,
    /// <summary>§10 — money moved above the two-person threshold.</summary>
    LargeMoneyMove = 5
}

/// <summary>
/// docs/08 §10 — "Ngoài nhật ký, cần theo dõi chủ động."
///
/// A log nobody reads catches nothing. These are the patterns worth surfacing on
/// their own, and the thresholds are here rather than in a query so they can be
/// argued about in one place.
/// </summary>
public static class AdminOversight
{
    /* ------------------------------------------------- §3, browsing patterns */

    /// <summary>Profiles opened inside <see cref="BrowsingWindow"/> before it looks like browsing.</summary>
    public const int BrowsingThreshold = 25;

    public static readonly TimeSpan BrowsingWindow = TimeSpan.FromHours(1);

    /// <summary>The same person looked up this many times by one admin in a week.</summary>
    public const int RepeatLookupThreshold = 5;

    public static readonly TimeSpan RepeatWindow = TimeSpan.FromDays(7);

    /// <summary>Vietnamese office hours, in the platform's own time zone.</summary>
    public static readonly TimeOnly WorkdayStart = new(7, 0);
    public static readonly TimeOnly WorkdayEnd = new(20, 0);

    public static bool IsOutOfHours(DateTime localTime) =>
        localTime.DayOfWeek is DayOfWeek.Sunday
        || TimeOnly.FromDateTime(localTime) < WorkdayStart
        || TimeOnly.FromDateTime(localTime) > WorkdayEnd;

    /* ------------------------------------------ §10, two pairs of eyes */

    /// <summary>docs/08 §12 QT-E — above this, one signature is not enough.</summary>
    public const decimal TwoPersonThreshold = 10_000_000m;

    public static bool NeedsSecondApproval(decimal amount) => Math.Abs(amount) >= TwoPersonThreshold;

    /// <summary>
    /// docs/08 §10 — the second person cannot be the first. Approving your own
    /// request is one signature written twice.
    /// </summary>
    public static bool MayApprove(int approverUserId, int requesterUserId) =>
        approverUserId != requesterUserId;

    public static string SecondApprovalMessage(decimal amount) =>
        $"{amount:#,##0}₫ vượt ngưỡng {TwoPersonThreshold:#,##0}₫, cần một người thứ hai duyệt trước khi thực hiện.";

    public static string SelfApprovalMessage() =>
        "Bạn là người đề nghị khoản này nên không tự duyệt được. Cần một admin khác.";

    /* ---------------------------------------------- §10, the random sample */

    /// <summary>docs/08 §12 QT-F.</summary>
    public const int RandomReviewPercent = 5;

    /// <summary>
    /// Whether a given decision falls into the month's sample. Derived from the
    /// row's own id rather than a random draw, so the same decision is always
    /// either in or out — a sample that reshuffles on every page load is not a
    /// sample, and cannot be worked through.
    /// </summary>
    public static bool InRandomSample(int decisionId, int percent = RandomReviewPercent)
    {
        if (percent <= 0) return false;
        if (percent >= 100) return true;

        // A cheap spread so consecutive ids do not land together.
        var spread = (decisionId * 2654435761L) % 100;
        return spread < percent;
    }

    /* -------------------------------------------------- §10, the scoreboard */

    /// <summary>One admin's month, as §10 asks for it.</summary>
    public readonly record struct Scorecard(
        int AdminUserId,
        string Name,
        int ProfilesViewed,
        int DecisionsMade,
        int AppealsAgainst,
        int AppealsUpheldAgainst)
    {
        /// <summary>Share of appealed decisions that were reduced or overturned.</summary>
        public double OverturnRate =>
            AppealsAgainst == 0 ? 0 : (double)AppealsUpheldAgainst / AppealsAgainst;
    }

    /// <summary>
    /// docs/08 §10 — "Cảnh báo khi một admin có tỉ lệ khiếu nại thành công cao
    /// bất thường." Below a handful of appeals the rate says nothing, so it is
    /// not raised: an alarm that fires on one overturned decision teaches people
    /// to ignore alarms.
    /// </summary>
    public const int MinAppealsToJudge = 5;

    public const double OverturnRateAlarm = 0.4;

    public static bool LooksUnreliable(Scorecard card) =>
        card.AppealsAgainst >= MinAppealsToJudge && card.OverturnRate >= OverturnRateAlarm;

    public static string FlagLabel(OversightFlag flag) => flag switch
    {
        OversightFlag.BrowsingProfiles => "Xem quá nhiều hồ sơ trong thời gian ngắn",
        OversightFlag.NoTicket => "Xem hồ sơ không gắn với ticket nào",
        OversightFlag.RepeatLookups => "Tra cứu cùng một người dùng nhiều lần",
        OversightFlag.OutOfHours => "Thao tác ngoài giờ làm việc",
        OversightFlag.AppealsUpheldAgainst => "Tỉ lệ bị khiếu nại thành công cao bất thường",
        _ => "Khoản tiền vượt ngưỡng duyệt hai người"
    };
}

/// <summary>
/// docs/08 §10 — one profile an admin opened. Kept because §10 asks for "số hồ
/// sơ đã xem" per admin and for the alarms that read off it; a log of decisions
/// alone would miss the person who only ever looks.
/// </summary>
public class AdminProfileView
{
    public long Id { get; set; }

    public int AdminUserId { get; set; }
    public User? AdminUser { get; set; }

    public int TargetUserId { get; set; }
    public User? TargetUser { get; set; }

    /// <summary>Null is itself the finding: §10 flags a look with no ticket behind it.</summary>
    public int? TicketId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// docs/08 §10 — a money move above the threshold, waiting for a second name.
///
/// It is a row rather than a flag because the request and the approval are two
/// separate acts by two separate people, and both have to be on the record.
/// </summary>
public class MoneyApproval
{
    public int Id { get; set; }

    /// <summary>"finance.refund" or "finance.payout-adjust".</summary>
    public string Action { get; set; } = "";

    /// <summary>What it is about: "booking:SH1234ABCD".</summary>
    public string Target { get; set; } = "";

    public decimal Amount { get; set; }
    public string Reason { get; set; } = "";

    public int RequestedByUserId { get; set; }
    public User? RequestedByUser { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public int? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }
    public DateTime? ApprovedAt { get; set; }

    /// <summary>Set when the second person said no, with their reason.</summary>
    public DateTime? RejectedAt { get; set; }
    public string? RejectedReason { get; set; }

    /// <summary>Set once the approved action actually ran, so it runs once.</summary>
    public DateTime? ExecutedAt { get; set; }

    public bool IsOpen => ApprovedAt is null && RejectedAt is null;
    public bool IsReady => ApprovedAt is not null && ExecutedAt is null;
}
