namespace StayHost.Domain;

public enum UserRole
{
    Guest = 0,
    Host = 1,
    Admin = 2
}

/// <summary>A real account. Guests become hosts by publishing their first listing.</summary>
public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Initials { get; set; } = "";
    public string? Phone { get; set; }

    public string PasswordHash { get; set; } = "";
    public string PasswordSalt { get; set; } = "";

    public UserRole Role { get; set; } = UserRole.Guest;
    public bool IsIdentityVerified { get; set; }
    public bool EmailConfirmed { get; set; }

    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set when the account was created from an anonymous browsing session.</summary>
    public string? AdoptedSessionId { get; set; }

    public int? HostProfileId { get; set; }
    public HostProfile? HostProfile { get; set; }

    public List<AuthSession> Sessions { get; set; } = [];
}

public enum TokenPurpose
{
    PasswordReset = 0,
    EmailVerification = 1
}

/// <summary>Single-use, short-lived token for password resets and email verification.</summary>
public class UserToken
{
    public int Id { get; set; }
    public string Token { get; set; } = "";
    public int UserId { get; set; }
    public User? User { get; set; }
    public TokenPurpose Purpose { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(2);
    public DateTime? UsedAt { get; set; }
}

/// <summary>Opaque bearer token stored in an HttpOnly cookie.</summary>
public class AuthSession
{
    public int Id { get; set; }
    public string Token { get; set; } = "";
    public int UserId { get; set; }
    public User? User { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(30);
    public string? UserAgent { get; set; }
    public DateTime? RevokedAt { get; set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}

public enum PaymentStatus
{
    Pending = 0,
    Authorized = 1,
    Captured = 2,
    Refunded = 3,
    Failed = 4
}

public enum PayoutStatus
{
    Scheduled = 0,
    Paid = 1,
    OnHold = 2
}

/// <summary>
/// Money movement for one booking. The demo has no real gateway, but the record
/// carries everything a gateway integration would need to reconcile against.
/// </summary>
public class Payment
{
    public int Id { get; set; }
    public string Reference { get; set; } = "";

    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Method { get; set; } = "card";
    public string? CardLast4 { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CapturedAt { get; set; }

    /// <summary>What the platform keeps; the rest is paid out to the host.</summary>
    public decimal PlatformFee { get; set; }
    public decimal HostPayout { get; set; }
    public PayoutStatus PayoutStatus { get; set; } = PayoutStatus.Scheduled;
    public DateOnly? PayoutDueOn { get; set; }
}

/// <summary>One conversation, always anchored to a listing and optionally a booking.</summary>
public class MessageThread
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public Listing? Listing { get; set; }

    public int GuestUserId { get; set; }
    public User? GuestUser { get; set; }

    public int HostUserId { get; set; }
    public User? HostUser { get; set; }

    public int? BookingId { get; set; }
    public Booking? Booking { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    public List<Message> Messages { get; set; } = [];
}

public class Message
{
    public int Id { get; set; }
    public int ThreadId { get; set; }
    public MessageThread? Thread { get; set; }

    public int SenderUserId { get; set; }
    public User? SenderUser { get; set; }

    public string Body { get; set; } = "";
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}

/// <summary>Seasonal override: a date window priced differently from the base rate.</summary>
/// <summary>
/// Which tier of docs/03 §1 step 1 a rule belongs to. A day override beats a
/// season, and both beat the weekend and base rates.
/// </summary>
public enum PriceRuleKind
{
    /// <summary>A season the host named, e.g. "Tết Nguyên đán".</summary>
    Season = 0,
    /// <summary>A price the host set on specific days from the calendar grid.</summary>
    DayOverride = 1
}

public class PriceRule
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public Listing? Listing { get; set; }

    public string Name { get; set; } = "";
    public PriceRuleKind Kind { get; set; } = PriceRuleKind.Season;
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    /// <summary>Flat nightly rate that replaces the base price inside the window.</summary>
    public decimal NightlyRate { get; set; }
    /// <summary>
    /// Minimum nights for stays starting inside this window, overriding the
    /// listing's own minimum (docs/03 §2 step 6). Null leaves it unchanged.
    /// </summary>
    public int? MinNights { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Review a host leaves about a guest after checkout.</summary>
public class GuestReview
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    public int HostUserId { get; set; }
    public User? HostUser { get; set; }

    public int GuestUserId { get; set; }
    public User? GuestUser { get; set; }

    public double Rating { get; set; } = 5;
    public string Text { get; set; } = "";
    public bool WouldHostAgain { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Host-side calendar block that is not backed by a booking (maintenance, own stay).</summary>
public class CalendarBlock
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public Listing? Listing { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
