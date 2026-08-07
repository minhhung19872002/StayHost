namespace StayHost.Domain;

/// <summary>docs/07 §5 — the four ways a guest comes back from their bank.</summary>
public enum AuthOutcome
{
    /// <summary>Still at the bank, or the guest has not come back yet.</summary>
    Pending = 0,
    Succeeded = 1,
    /// <summary>Wrong or expired OTP. The booking is untouched and they may try again.</summary>
    WrongCode = 2,
    /// <summary>The guest closed the tab. The hold decides how long they have.</summary>
    Abandoned = 3,
    BankRefused = 4
}

/// <summary>
/// docs/07 §5 — one trip to the bank's authentication page.
///
/// The row exists so a guest who closes the tab can be picked back up exactly
/// where they were, and so the platform has something to ask the gateway about
/// rather than trusting whatever page the browser landed on.
/// </summary>
public class CardAuthentication
{
    public long Id { get; set; }

    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    /// <summary>Ties this to the idempotent attempt of docs/07 §7.</summary>
    public string AttemptKey { get; set; } = "";

    public decimal Amount { get; set; }
    public string Method { get; set; } = "card";
    public string? CardLast4 { get; set; }

    public AuthOutcome Outcome { get; set; } = AuthOutcome.Pending;

    /// <summary>docs/07 §5 — a wrong code gets three goes.</summary>
    public int CodeAttempts { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SettledAt { get; set; }

    /// <summary>
    /// docs/07 §5 — set when the platform asked the gateway what really happened
    /// rather than believing the browser. Every settled row must have this.
    /// </summary>
    public DateTime? ConfirmedWithGatewayAt { get; set; }

    public DeclineReason Reason { get; set; }
}

/// <summary>docs/07 §5 — the rules of the trip to the bank and back.</summary>
public static class CardAuth
{
    /* ------------------------------------------------ §5.2, holding the room */

    /// <summary>
    /// "Nếu thời gian giữ chỗ còn dưới 5 phút thì tự động gia hạn thêm 10 phút."
    /// The dates must not fall off the market while the guest is on their bank's
    /// page — that is the platform's timer expiring, not the guest giving up.
    /// </summary>
    public static readonly TimeSpan LowWaterMark = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan Extension = TimeSpan.FromMinutes(10);

    public static bool NeedsExtension(DateTime? holdExpiresAt, DateTime now) =>
        holdExpiresAt is { } at && at - now < LowWaterMark;

    /// <summary>
    /// The new expiry, or the old one when there is still time. An expired hold
    /// is extended from now: the guest is standing at the bank's page, and the
    /// alternative is telling them their booking vanished mid-payment.
    /// </summary>
    public static DateTime ExtendedTo(DateTime? holdExpiresAt, DateTime now)
    {
        if (holdExpiresAt is not { } at) return now + Extension;
        if (!NeedsExtension(at, now)) return at;
        return (at > now ? at : now) + Extension;
    }

    /* --------------------------------------------------- §5.4, the outcomes */

    /// <summary>"Cho thử lại tối đa 3 lần, giữ nguyên đơn."</summary>
    public const int MaxCodeAttempts = 3;

    public static bool CanTryCodeAgain(int attemptsSoFar) => attemptsSoFar < MaxCodeAttempts;

    /// <summary>
    /// A guest who has run out of codes has not been refused by their bank: they
    /// still have every other way of paying, and the booking is still theirs
    /// until the hold runs out.
    /// </summary>
    public static string OutcomeMessage(AuthOutcome outcome, int attemptsSoFar) => outcome switch
    {
        AuthOutcome.Succeeded => "Xác thực thành công.",
        AuthOutcome.WrongCode when CanTryCodeAgain(attemptsSoFar) =>
            $"Mã OTP không đúng hoặc đã hết hạn. Bạn còn {MaxCodeAttempts - attemptsSoFar} lần thử.",
        AuthOutcome.WrongCode =>
            "Bạn đã nhập sai mã OTP quá số lần cho phép. Hãy thử lại bằng thẻ khác hoặc cách trả tiền khác — " +
            "chỗ nghỉ vẫn đang được giữ cho bạn.",
        AuthOutcome.Abandoned =>
            "Bạn chưa hoàn tất xác thực. Đơn vẫn đang chờ thanh toán, quay lại để tiếp tục từ đúng chỗ.",
        AuthOutcome.BankRefused => "Ngân hàng từ chối giao dịch. Hãy thử cách trả tiền khác.",
        _ => "Đang chờ bạn xác thực với ngân hàng."
    };

    /// <summary>docs/07 §5 — a refusal points somewhere else to go.</summary>
    public static bool SuggestAnotherMethod(AuthOutcome outcome) =>
        outcome is AuthOutcome.BankRefused
        || outcome is AuthOutcome.WrongCode;

    /* ---------------------------------------- §5, believing the gateway only */

    /// <summary>
    /// docs/07 §5 — "hệ thống phải tự kiểm tra lại kết quả với cổng thanh toán,
    /// không tin vào việc khách quay về trang nào."
    ///
    /// The browser reports what the guest saw. The gateway reports what happened
    /// to the money. Where they disagree, the money wins — which is the case the
    /// spec cares about: charged, then the guest's connection dropped.
    /// </summary>
    public static AuthOutcome Reconcile(AuthOutcome browserSaid, AuthOutcome? gatewaySaid) =>
        gatewaySaid ?? browserSaid;

    /// <summary>
    /// True when the browser's story and the gateway's disagree — worth a log
    /// line, because a pattern of it means either an integration bug or somebody
    /// forging return URLs.
    /// </summary>
    public static bool Disagreed(AuthOutcome browserSaid, AuthOutcome? gatewaySaid) =>
        gatewaySaid is { } real && real != browserSaid;

    /// <summary>
    /// A settled authentication the platform never checked with the gateway is
    /// not settled. Used by the sweep that picks up guests who dropped out.
    /// </summary>
    public static bool NeedsGatewayCheck(CardAuthentication auth) =>
        auth.ConfirmedWithGatewayAt is null;
}
