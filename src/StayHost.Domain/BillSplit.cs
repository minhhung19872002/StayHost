namespace StayHost.Domain;

public enum BillSplitStatus
{
    /// <summary>Waiting for people to pay their share.</summary>
    Collecting = 0,
    /// <summary>Everyone paid; the booking went through.</summary>
    Complete = 1,
    /// <summary>The 24 hours ran out; whatever was collected went back.</summary>
    Expired = 2,
    /// <summary>The organiser called it off.</summary>
    Cancelled = 3
}

public enum BillShareStatus
{
    Waiting = 0,
    Paid = 1,
    /// <summary>Refunded because the split never completed.</summary>
    Returned = 2
}

/// <summary>
/// docs/01 ĐP-07 — one booking paid by up to sixteen people, each following
/// their own link. The booking is not confirmed until the last share lands.
/// </summary>
public class BillSplit
{
    public int Id { get; set; }

    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    /// <summary>The person who booked and invited the others.</summary>
    public int OrganiserUserId { get; set; }
    public User? OrganiserUser { get; set; }

    public decimal Total { get; set; }
    public BillSplitStatus Status { get; set; } = BillSplitStatus.Collecting;

    /// <summary>When unpaid shares are given up on and paid ones sent back.</summary>
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public List<BillShare> Shares { get; set; } = [];
}

public class BillShare
{
    public int Id { get; set; }

    public int SplitId { get; set; }
    public BillSplit? Split { get; set; }

    public string Email { get; set; } = "";
    public string? Name { get; set; }
    public decimal Amount { get; set; }

    /// <summary>What goes in the link. Whoever holds it can pay this share and nothing else.</summary>
    public string Token { get; set; } = Guid.NewGuid().ToString("N");

    public BillShareStatus Status { get; set; } = BillShareStatus.Waiting;
    public string? CardLast4 { get; set; }
    public DateTime? PaidAt { get; set; }
}

/// <summary>The rules a split obeys, kept apart from the plumbing that stores it.</summary>
public static class BillSplitRules
{
    /// <summary>The spec's ceiling: sixteen people on one booking.</summary>
    public const int MaxPeople = 16;

    /// <summary>How long the others have before the booking is let go.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);

    /// <summary>
    /// Splits a total into <paramref name="people"/> shares that add back up to
    /// it exactly. The remainder lands on the organiser, who is share one — the
    /// person doing the asking absorbs the odd dong rather than their friends.
    /// </summary>
    public static IReadOnlyList<decimal> Divide(decimal total, int people)
    {
        if (people < 1) throw new ArgumentOutOfRangeException(nameof(people));

        var each = Math.Floor(total / people);
        var shares = Enumerable.Repeat(each, people).ToList();
        shares[0] += total - each * people;
        return shares;
    }

    public static bool IsOpen(BillSplitStatus status) => status == BillSplitStatus.Collecting;

    public static bool Expired(DateTime expiresAt, DateTime now) => now >= expiresAt;

    public static string Label(BillSplitStatus status) => status switch
    {
        BillSplitStatus.Collecting => "Đang chờ mọi người trả",
        BillSplitStatus.Complete => "Đã trả đủ",
        BillSplitStatus.Expired => "Hết hạn, đã hoàn lại",
        _ => "Đã huỷ"
    };

    public static string ShareLabel(BillShareStatus status) => status switch
    {
        BillShareStatus.Paid => "Đã trả",
        BillShareStatus.Returned => "Đã hoàn lại",
        _ => "Chưa trả"
    };
}
