namespace StayHost.Domain;

/// <summary>docs/07 §12.4 — why a payout is being held back.</summary>
public enum PayoutHoldReason
{
    None = 0,
    /// <summary>An open dispute or StayShield case on the stay.</summary>
    Dispute = 1,
    /// <summary>The guest has gone to their bank about the charge (docs/07 §11).</summary>
    Chargeback = 2,
    /// <summary>The listing is suspended pending review.</summary>
    ListingSuspended = 3,
    /// <summary>Payout account unverified, or changed within the cooling-off window.</summary>
    AccountUnverified = 4,
    /// <summary>The host owes the platform — a cancellation penalty, a settled claim.</summary>
    HostOwesPlatform = 5
}

/// <summary>
/// docs/07 §12 — when a host's money moves, and when it does not.
///
/// Every rule here is about not sending money that may have to come back. The
/// platform is holding it on somebody's behalf either way; the question is only
/// whose behalf, and for how long.
/// </summary>
public static class Payouts
{
    /* ------------------------------------------------------ §12.2, the wait */

    /// <summary>
    /// docs/07 §12.2 — changing where the money goes freezes payouts for three
    /// days and warns the old address. This is the anti-takeover rule: somebody
    /// who steals an account should not be able to redirect a payout before the
    /// real host has had a chance to read the warning.
    /// </summary>
    public static readonly TimeSpan AccountChangeFreeze = TimeSpan.FromDays(3);

    public static DateTime FrozenUntil(DateTime changedAt) => changedAt + AccountChangeFreeze;

    public static bool AccountFrozen(DateTime? changedAt, DateTime now) =>
        changedAt is { } at && now < FrozenUntil(at);

    public static string FreezeNotice(DateTime changedAt) =>
        $"Bạn vừa đổi tài khoản nhận tiền, nên các khoản chuyển tạm hoãn tới " +
        $"{FrozenUntil(changedAt):HH:mm dd/MM/yyyy}. Nếu không phải bạn đổi, hãy liên hệ hỗ trợ ngay.";

    /* ------------------------------------------------- §12.3, the new host */

    /// <summary>docs/07 §16 TT-C — a host's first stays are held a little longer.</summary>
    public const int NewHostExtraDays = 3;

    public const int NewHostCompletedStays = 3;

    public static bool IsNewHost(int completedStays) => completedStays < NewHostCompletedStays;

    /// <summary>
    /// docs/07 §12.3 — 24 hours after check-in, plus the new-host wait where it
    /// applies. Long stays are paid monthly and decided elsewhere (docs/01 TC-03).
    /// </summary>
    public static DateOnly DueOn(DateOnly checkIn, int completedStays) =>
        checkIn.AddDays(1 + (IsNewHost(completedStays) ? NewHostExtraDays : 0));

    /* -------------------------------------------------- §12.4, the holds */

    /// <summary>Everything that decides whether this payout may go out today.</summary>
    public readonly record struct Conditions(
        bool HasOpenDispute,
        bool HasChargeback,
        bool ListingSuspended,
        bool AccountVerified,
        DateTime? AccountChangedAt,
        decimal OwedToPlatform);

    /// <summary>
    /// The first reason that applies, in the order docs/07 §12.4 lists them —
    /// so a host chasing a payout is told the thing they can act on soonest.
    /// </summary>
    public static PayoutHoldReason HoldReason(Conditions c, DateTime now)
    {
        if (c.HasOpenDispute) return PayoutHoldReason.Dispute;
        if (c.HasChargeback) return PayoutHoldReason.Chargeback;
        if (c.ListingSuspended) return PayoutHoldReason.ListingSuspended;
        if (!c.AccountVerified || AccountFrozen(c.AccountChangedAt, now)) return PayoutHoldReason.AccountUnverified;
        if (c.OwedToPlatform > 0) return PayoutHoldReason.HostOwesPlatform;
        return PayoutHoldReason.None;
    }

    public static bool CanPay(Conditions c, DateTime now) => HoldReason(c, now) == PayoutHoldReason.None;

    /// <summary>docs/07 §12.4 — "báo chủ nhà lý do". A hold nobody explains is a hold nobody trusts.</summary>
    public static string HoldLabel(PayoutHoldReason reason) => reason switch
    {
        PayoutHoldReason.Dispute => "Đơn đang có tranh chấp hoặc hồ sơ StayShield mở",
        PayoutHoldReason.Chargeback => "Khách đang khiếu nại giao dịch với ngân hàng",
        PayoutHoldReason.ListingSuspended => "Tin đăng đang bị tạm dừng để xem xét",
        PayoutHoldReason.AccountUnverified => "Tài khoản nhận tiền chưa xác minh hoặc vừa được đổi",
        PayoutHoldReason.HostOwesPlatform => "Đang khấu trừ khoản bạn còn nợ StayHost",
        _ => ""
    };

    /* ------------------------------------------------- §12.5, trying again */

    /// <summary>docs/07 §12.5 — "Thử lại sau 1 ngày, 3 ngày, 7 ngày."</summary>
    public static readonly int[] RetryAfterDays = [1, 3, 7];

    public static int MaxAttempts => RetryAfterDays.Length + 1;

    /// <summary>
    /// When the next attempt is due, or null when there are none left and a
    /// person has to fix the account.
    /// </summary>
    public static DateOnly? NextAttemptOn(DateOnly lastAttempt, int attemptsSoFar)
    {
        var index = attemptsSoFar - 1;
        if (index < 0 || index >= RetryAfterDays.Length) return null;
        return lastAttempt.AddDays(RetryAfterDays[index]);
    }

    public static bool OutOfAttempts(int attemptsSoFar) => attemptsSoFar >= MaxAttempts;

    public static string ExhaustedNotice() =>
        "Chúng tôi đã thử chuyển tiền nhiều lần nhưng không thành công. " +
        "Vui lòng kiểm tra lại tài khoản nhận tiền — tiền vẫn được giữ nguyên cho bạn.";
}
