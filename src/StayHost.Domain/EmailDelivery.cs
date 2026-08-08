namespace StayHost.Domain;

/// <summary>
/// When a queued email is tried again, and when it is given up on.
///
/// The two kinds of failure are not alike. A mailbox that does not exist will
/// never exist, and retrying it wastes a sending reputation that the codes in
/// <see cref="OneTimeCode"/> depend on. A connection that dropped is worth
/// another go in a minute. So the rule is about which kind of "no" came back.
/// </summary>
public static class EmailDelivery
{
    /// <summary>
    /// Minutes to wait before each further attempt. Short at first, because the
    /// most common failure is a moment's network trouble and somebody is sitting
    /// at a sign-in screen waiting for a six-digit code.
    /// </summary>
    public static readonly int[] RetryAfterMinutes = [1, 5, 15, 60, 240];

    public static int MaxAttempts => RetryAfterMinutes.Length + 1;

    /// <summary>The moment the next attempt is due, or null when there are none left.</summary>
    public static DateTime? NextAttemptAt(DateTime lastAttempt, int attemptsSoFar)
    {
        var index = attemptsSoFar - 1;
        if (index < 0 || index >= RetryAfterMinutes.Length) return null;
        return lastAttempt.AddMinutes(RetryAfterMinutes[index]);
    }

    public static bool OutOfAttempts(int attemptsSoFar) => attemptsSoFar >= MaxAttempts;

    /// <summary>
    /// A refusal the receiving server will give every time — a mailbox that does
    /// not exist, a domain that does not resolve, a message the recipient's
    /// server has blocked outright. Retrying these is how a sender ends up on a
    /// blocklist, which would take down the sign-in codes with it.
    /// </summary>
    public static bool IsPermanent(int? smtpStatusCode) =>
        smtpStatusCode is >= 500 and < 600;

    /// <summary>
    /// Whether to queue another attempt at all: a permanent refusal stops now,
    /// everything else waits its turn until the attempts run out.
    /// </summary>
    public static bool ShouldRetry(int? smtpStatusCode, int attemptsSoFar) =>
        !IsPermanent(smtpStatusCode) && !OutOfAttempts(attemptsSoFar);

    public static string GiveUpNotice(int attempts) =>
        $"Đã thử gửi {attempts} lần không thành công. Thư này sẽ không được gửi lại.";

    /* ------------------------------------------------- keeping codes private */

    /// <summary>
    /// A subject line shows up in a phone's lock-screen preview, in a mail
    /// client's list, and in every log between here and the recipient. A one-time
    /// code has no business in any of them.
    /// </summary>
    public static string CodeSubject() => "Mã xác thực StayHost";

    /// <summary>True when a subject is leaking something that belongs in the body.</summary>
    public static bool SubjectLeaksCode(string? subject) =>
        (subject ?? "").Any(char.IsDigit)
        && (subject ?? "").Count(char.IsDigit) >= Identity.CodeLength;
}
