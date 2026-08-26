namespace StayHost.Domain;

/// <summary>docs/07 §12.4 — why a payout is being held back.</summary>
public enum PayoutHoldReason
{
    None = 0,
    /// <summary>An open dispute or Staylio Shield case on the stay.</summary>
    Dispute = 1,
    /// <summary>The guest has gone to their bank about the charge (docs/07 §11).</summary>
    Chargeback = 2,
    /// <summary>The listing is suspended pending review.</summary>
    ListingSuspended = 3,
    /// <summary>Payout account unverified, or changed within the cooling-off window.</summary>
    AccountUnverified = 4,
    /// <summary>The host owes the platform — a cancellation penalty, a settled claim.</summary>
    HostOwesPlatform = 5,
    /// <summary>
    /// docs/08 §5.2 and §6 — an admin held the account's money (PayoutsHeld
    /// restriction, suspension, ban). Appended, never reordered: the number is in
    /// the database.
    /// </summary>
    AccountUnderReview = 6
}

/// <summary>
/// docs/07 §12 — when a host's money moves, and when it does not.
///
/// Every rule here is about not sending money that may have to come back. The
/// platform is holding it on somebody's behalf either way; the question is only
/// whose behalf, and for how long.
/// </summary>
/// <summary>
/// docs/01 TC-03 — one month's share of a long stay's host payout, with its own
/// due date and lifecycle. Kept apart from the single-shot Payment path so the
/// ordinary payout engine is untouched.
/// </summary>
public class PayoutInstallment
{
    public int Id { get; set; }

    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    public decimal Amount { get; set; }
    public DateOnly DueOn { get; set; }

    public bool Paid { get; set; }
    public DateTime? PaidAt { get; set; }
    public int Attempts { get; set; }
    public DateOnly? LastAttemptOn { get; set; }
}

public static class Payouts
{
    /// <summary>
    /// docs/09 §4 (MR-C-03, scenario 12) — an experience or service pays its
    /// provider a day after the session ENDS, not a day after it starts the way
    /// a stay pays from check-in. The session's end is its start plus its length.
    /// </summary>
    public static readonly TimeSpan SessionPayoutDelay = TimeSpan.FromDays(1);

    public static DateTime SessionEnd(DateTime startsAt, int durationMinutes) =>
        startsAt.AddMinutes(durationMinutes);

    public static DateTime SessionPayoutDue(DateTime startsAt, int durationMinutes) =>
        SessionEnd(startsAt, durationMinutes) + SessionPayoutDelay;

    /// <summary>Whether a session's provider payout is due by the given moment.</summary>
    public static bool SessionPayoutReady(DateTime startsAt, int durationMinutes, DateTime now) =>
        now >= SessionPayoutDue(startsAt, durationMinutes);

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

    /* -------------------------------------- §12.2, proving the account */

    /// <summary>
    /// docs/07 §12.2 — the amount sent to prove the account exists and belongs to
    /// who it says. Small enough that getting it wrong costs nothing, large enough
    /// that a bank will actually move it.
    /// </summary>
    public const decimal TestTransferAmount = 2_000m;

    /// <summary>
    /// docs/07 §12.2 — "Tên chủ tài khoản phải khớp tên trên hồ sơ đã xác minh
    /// danh tính." Vietnamese banks hold the name unaccented and in capitals, and
    /// people type it either way, so the comparison ignores both.
    /// </summary>
    public static bool NameMatchesIdentity(string? accountName, string? legalName)
    {
        var a = SearchText.Normalize(accountName ?? "");
        var b = SearchText.Normalize(legalName ?? "");
        return a.Length > 0 && a == b;
    }

    public static string NameMismatchNotice() =>
        "Tên chủ tài khoản không khớp tên trên hồ sơ đã xác minh danh tính. " +
        "Chúng tôi sẽ xem xét thủ công trước khi chuyển khoản đầu tiên.";

    public static string VerifiedNotice(string? last4) =>
        $"Đã chuyển thử {TestTransferAmount:#,##0}₫ tới tài khoản ••••{last4} và xác nhận thành công.";

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

    /// <summary>
    /// docs/07 §12.3 — "Gom nhiều đơn thành một lần chuyển trong ngày để giảm phí,
    /// nhưng báo cáo vẫn tách theo từng đơn." One host, one day, one transfer: the
    /// reference is what lets a host holding a bank statement line for 12 triệu
    /// find the four bookings that made it up.
    /// </summary>
    /// <param name="sequence">
    /// Normally 1: everything due today goes in one sweep. A payout that only
    /// becomes payable later the same day — a hold lifted, a retry coming good —
    /// is a second transfer, and must not borrow the first one's reference.
    /// </param>
    public static string BatchReference(int hostId, DateOnly day, int sequence = 1) =>
        sequence <= 1 ? $"PO-{day:yyyyMMdd}-{hostId}" : $"PO-{day:yyyyMMdd}-{hostId}-{sequence}";

    /* -------------------------------------------------- §12.4, the holds */

    /// <summary>Everything that decides whether this payout may go out today.</summary>
    public readonly record struct Conditions(
        bool HasOpenDispute,
        bool HasChargeback,
        bool ListingSuspended,
        bool AccountVerified,
        DateTime? AccountChangedAt,
        decimal OwedToPlatform,
        decimal Payable = 0m,
        bool AccountUnderReview = false);

    /// <summary>
    /// The first reason that applies, in the order docs/07 §12.4 lists them —
    /// so a host chasing a payout is told the thing they can act on soonest.
    /// </summary>
    public static PayoutHoldReason HoldReason(Conditions c, DateTime now)
    {
        // docs/08 §5.2/§6 — an admin's hold on the whole account outranks every
        // per-booking condition. It is derived from the account's live state, so
        // the sweep cannot recompute it away, and payments that arrive after the
        // sanction are held just the same.
        if (c.AccountUnderReview) return PayoutHoldReason.AccountUnderReview;

        if (c.HasOpenDispute) return PayoutHoldReason.Dispute;
        if (c.HasChargeback) return PayoutHoldReason.Chargeback;
        if (c.ListingSuspended) return PayoutHoldReason.ListingSuspended;
        if (!c.AccountVerified || AccountFrozen(c.AccountChangedAt, now)) return PayoutHoldReason.AccountUnverified;
        if (c.OwedToPlatform >= c.Payable && c.OwedToPlatform > 0) return PayoutHoldReason.HostOwesPlatform;
        return PayoutHoldReason.None;
    }

    public static bool CanPay(Conditions c, DateTime now) => HoldReason(c, now) == PayoutHoldReason.None;

    /* -------------------------------------------- §17.4, the deduction */

    /// <summary>What a debt does to a transfer.</summary>
    public readonly record struct Deduction(decimal Transfer, decimal Applied, decimal StillOwed);

    /// <summary>
    /// docs/07 §17.4 — "khoản khấu trừ do nợ sàn". A host who owes the platform
    /// has it taken out of the next transfer rather than being chased for it, and
    /// only when the debt swallows the whole transfer does nothing go out at all
    /// (which is the case <see cref="PayoutHoldReason.HostOwesPlatform"/> covers).
    /// </summary>
    public static Deduction Deduct(decimal payable, decimal owed)
    {
        if (owed <= 0 || payable <= 0) return new Deduction(Math.Max(0m, payable), 0m, Math.Max(0m, owed));

        var applied = Math.Min(payable, owed);
        return new Deduction(payable - applied, applied, owed - applied);
    }

    public static string DeductionNote(decimal applied, decimal stillOwed) =>
        stillOwed > 0
            ? $"Đã khấu trừ {applied:#,##0}₫ vào khoản bạn còn nợ Staylio; còn lại {stillOwed:#,##0}₫."
            : $"Đã khấu trừ {applied:#,##0}₫ — bạn không còn nợ Staylio khoản nào.";

    /// <summary>docs/07 §12.4 — "báo chủ nhà lý do". A hold nobody explains is a hold nobody trusts.</summary>
    public static string HoldLabel(PayoutHoldReason reason) => reason switch
    {
        PayoutHoldReason.Dispute => "Đơn đang có tranh chấp hoặc hồ sơ Staylio Shield mở",
        PayoutHoldReason.Chargeback => "Khách đang khiếu nại giao dịch với ngân hàng",
        PayoutHoldReason.ListingSuspended => "Tin đăng đang bị tạm dừng để xem xét",
        PayoutHoldReason.AccountUnverified => "Tài khoản nhận tiền chưa xác minh hoặc vừa được đổi",
        PayoutHoldReason.HostOwesPlatform => "Đang khấu trừ khoản bạn còn nợ Staylio",
        PayoutHoldReason.AccountUnderReview => "Tài khoản của bạn đang được Staylio xem xét — tiền vẫn là của bạn, chỉ tạm giữ",
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

    /* -------------------------------------------- §12.3, long-stay monthly payout */

    /// <summary>docs/07 §12.3, docs/01 TC-03 — a stay this long is paid out monthly.</summary>
    public const int MonthlyPayoutThreshold = 28;

    /// <summary>A payout month is 30 nights for scheduling.</summary>
    private const int NightsPerMonth = 30;

    public readonly record struct PayoutInstalment(decimal Amount, DateOnly DueOn);

    /// <summary>
    /// docs/07 §12.3, docs/01 TC-03 — for a stay of 28 nights or more the host is
    /// paid month by month rather than all at once: a guest committing to a long
    /// stay is protected, and the host earns as the stay is delivered. The first
    /// instalment falls due 24 hours after check-in, each next one a month on.
    ///
    /// The amounts follow the nights in each month and sum to exactly the payout —
    /// the last instalment carries the rounding remainder, so nothing is lost or
    /// invented. A stay under the threshold returns nothing: it uses the ordinary
    /// single payout.
    /// </summary>
    public static IReadOnlyList<PayoutInstalment> MonthlySchedule(
        decimal hostPayout, DateOnly checkIn, int nights)
    {
        if (nights < MonthlyPayoutThreshold || hostPayout <= 0) return [];

        var schedule = new List<PayoutInstalment>();
        var allocated = 0m;
        var monthIndex = 0;

        for (var start = 0; start < nights; start += NightsPerMonth)
        {
            var blockNights = Math.Min(NightsPerMonth, nights - start);
            var isLast = start + blockNights >= nights;

            var amount = isLast
                ? hostPayout - allocated   // the remainder, so the total is exact
                : Math.Round(hostPayout * blockNights / nights, 0, MidpointRounding.AwayFromZero);
            allocated += amount;

            schedule.Add(new PayoutInstalment(amount, checkIn.AddDays(1 + monthIndex * NightsPerMonth)));
            monthIndex++;
        }

        return schedule;
    }
}
