namespace StayHost.Domain;

public enum BalanceStatus
{
    /// <summary>The guest paid the whole thing up front (docs/01 ĐP-05).</summary>
    None = 0,
    /// <summary>A deposit was taken; the rest is waiting for its date.</summary>
    Scheduled = 1,
    /// <summary>The second charge went through.</summary>
    Paid = 2,
    /// <summary>The second charge was refused and is inside the retry window.</summary>
    Retrying = 3,
    /// <summary>The retry window ran out; the booking was cancelled.</summary>
    Failed = 4
}

/// <summary>
/// docs/03 §1 "Trả một phần" and docs/01 ĐP-06 — at least half now, the rest
/// taken automatically 14 days before check-in. A stay booked closer than that
/// is charged in full straight away, because there is no later to wait for.
/// </summary>
public static class PartialPayment
{
    public const decimal MinimumShare = 0.5m;

    /// <summary>How far before check-in the rest is taken.</summary>
    public const int DaysBeforeCheckIn = 14;

    /// <summary>A refused charge is retried inside this window before the booking goes.</summary>
    public static readonly TimeSpan RetryWindow = TimeSpan.FromHours(72);

    /// <summary>Retries are spaced out rather than hammered.</summary>
    public static readonly TimeSpan RetryEvery = TimeSpan.FromHours(12);

    /// <summary>The smallest deposit that may be taken, rounded up to the dong.</summary>
    public static decimal MinimumDeposit(decimal total) => Math.Ceiling(total * MinimumShare);

    /// <summary>
    /// True when there is enough runway for a second charge to make sense. Inside
    /// the 14-day mark the guest pays in full, so the platform never holds a stay
    /// it has not been paid for.
    /// </summary>
    public static bool IsAvailable(DateOnly checkIn, DateOnly today) =>
        checkIn.DayNumber - today.DayNumber > DaysBeforeCheckIn;

    /// <summary>When the rest will be taken.</summary>
    public static DateOnly BalanceDueOn(DateOnly checkIn, DateOnly today)
    {
        var due = checkIn.AddDays(-DaysBeforeCheckIn);
        return due < today ? today : due;
    }

    /// <summary>
    /// The deposit actually taken: what the guest asked for, never below half and
    /// never above the whole amount.
    /// </summary>
    public static decimal Deposit(decimal total, decimal? requested)
    {
        var floor = MinimumDeposit(total);
        if (requested is not { } asked) return floor;
        return Math.Clamp(Math.Round(asked), floor, total);
    }

    public static bool ShouldCharge(DateOnly dueOn, DateOnly today) => dueOn <= today;

    /// <summary>
    /// A refused charge is tried again every twelve hours until the window
    /// closes, then the booking is cancelled under the guest's own policy.
    /// </summary>
    public static bool ShouldRetry(DateTime firstFailedAt, DateTime lastAttemptAt, DateTime now) =>
        now - firstFailedAt < RetryWindow && now - lastAttemptAt >= RetryEvery;

    public static bool GaveUp(DateTime firstFailedAt, DateTime now) =>
        now - firstFailedAt >= RetryWindow;

    public static string Label(BalanceStatus status) => status switch
    {
        BalanceStatus.Scheduled => "Còn phải trả",
        BalanceStatus.Paid => "Đã trả đủ",
        BalanceStatus.Retrying => "Thu lần hai chưa thành công",
        BalanceStatus.Failed => "Không thu được phần còn lại",
        _ => "Đã trả toàn bộ"
    };
}
