namespace StayHost.Domain;

/// <summary>Why a guest's balance moved. The rows are append-only, like the ledger.</summary>
public enum CreditReason
{
    /// <summary>A gift card someone bought and this guest redeemed.</summary>
    GiftCard = 0,
    /// <summary>Compensation: a host cancelled, a price match, an arbitration.</summary>
    Goodwill = 1,
    /// <summary>Both sides of a referral, once the newcomer actually travelled.</summary>
    Referral = 2,
    /// <summary>Spent on a booking. Negative.</summary>
    Spent = 3,
    /// <summary>Given back when the booking it was spent on was cancelled.</summary>
    Returned = 4,
    /// <summary>
    /// docs/01 TC-07 — a grant reached its expiry with something left on it.
    /// Negative, written by the sweep rather than by anything the guest did, so
    /// the balance stays the sum of its rows instead of quietly shrinking.
    /// </summary>
    Expired = 5
}

/// <summary>
/// One movement of a guest's balance. Never edited and never deleted: the
/// balance is the sum of these, exactly as the money ledger works (docs/00 §6.1).
/// </summary>
public class CreditEntry
{
    public long Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Positive when granted, negative when spent.</summary>
    public decimal Amount { get; set; }

    public CreditReason Reason { get; set; }
    public string Memo { get; set; } = "";

    public int? BookingId { get; set; }
    public Booking? Booking { get; set; }

    /// <summary>
    /// docs/01 TC-07 — when this grant lapses. Only meaningful on a positive
    /// entry, and null means it never lapses, which is what every row written
    /// before this column existed says. Spending draws on whichever grant lapses
    /// soonest, per docs/07 §3.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum GiftCardStatus
{
    Active = 0,
    /// <summary>Every dong has been moved onto somebody's balance.</summary>
    Redeemed = 1,
    Cancelled = 2
}

public class GiftCard
{
    public int Id { get; set; }

    /// <summary>What the recipient types in. Unique, and the only thing needed to redeem.</summary>
    public string Code { get; set; } = "";

    public decimal Amount { get; set; }
    public decimal Remaining { get; set; }

    public int? PurchasedByUserId { get; set; }
    public User? PurchasedByUser { get; set; }

    public string RecipientEmail { get; set; } = "";
    public string? RecipientName { get; set; }
    public string? Message { get; set; }

    public int? RedeemedByUserId { get; set; }
    public User? RedeemedByUser { get; set; }
    public DateTime? RedeemedAt { get; set; }

    public GiftCardStatus Status { get; set; } = GiftCardStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum ReferralStatus
{
    /// <summary>Invited, not signed up yet.</summary>
    Sent = 0,
    /// <summary>The newcomer has an account but has not travelled.</summary>
    Joined = 1,
    /// <summary>They completed a stay; both sides were paid.</summary>
    Rewarded = 2,
    Expired = 3
}

public class Referral
{
    public int Id { get; set; }

    public int ReferrerUserId { get; set; }
    public User? ReferrerUser { get; set; }

    public string Code { get; set; } = "";
    public string InviteeEmail { get; set; } = "";

    public int? InviteeUserId { get; set; }
    public User? InviteeUser { get; set; }

    public ReferralStatus Status { get; set; } = ReferralStatus.Sent;

    public decimal ReferrerReward { get; set; }
    public decimal InviteeReward { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? JoinedAt { get; set; }
    public DateTime? RewardedAt { get; set; }
}

/// <summary>
/// The rules behind gift cards, balance and referrals. Amounts are the
/// platform's own money, so they live here rather than being sprinkled around.
/// </summary>
public static class CreditRules
{
    public const decimal MinGiftCard = 200_000m;
    public const decimal MaxGiftCard = 20_000_000m;

    /// <summary>What each side gets when a newcomer finishes their first stay.</summary>
    public const decimal ReferrerReward = 300_000m;
    public const decimal InviteeReward = 200_000m;

    /// <summary>
    /// Balance never covers the fees or the tax — it comes off the room charge,
    /// which is the part the guest is actually choosing to spend on.
    /// </summary>
    public static decimal Spendable(decimal balance, decimal roomAfterDiscount) =>
        Math.Max(0m, Math.Min(balance, roomAfterDiscount));

    /// <summary>Codes people read aloud and type in, so no O/0 or I/1.</summary>
    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string NewCode(string prefix, Func<int, int> pick, int length = 10)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++) chars[i] = Alphabet[pick(Alphabet.Length)];
        return $"{prefix}-{new string(chars)}";
    }

    public static bool CanRedeem(GiftCard card) =>
        card.Status == GiftCardStatus.Active && card.Remaining > 0;

    public static string StatusLabel(GiftCardStatus status) => status switch
    {
        GiftCardStatus.Redeemed => "Đã dùng hết",
        GiftCardStatus.Cancelled => "Đã huỷ",
        _ => "Còn hiệu lực"
    };

    public static string StatusLabel(ReferralStatus status) => status switch
    {
        ReferralStatus.Joined => "Đã tạo tài khoản",
        ReferralStatus.Rewarded => "Đã thưởng cả hai bên",
        ReferralStatus.Expired => "Đã hết hạn",
        _ => "Đã gửi lời mời"
    };

    public static string ReasonLabel(CreditReason reason) => reason switch
    {
        CreditReason.GiftCard => "Thẻ quà tặng",
        CreditReason.Goodwill => "Bù đắp từ Staylio",
        CreditReason.Referral => "Thưởng giới thiệu bạn",
        CreditReason.Spent => "Dùng cho đơn đặt",
        CreditReason.Expired => "Hết hạn sử dụng",
        _ => "Hoàn lại số dư"
    };
}
