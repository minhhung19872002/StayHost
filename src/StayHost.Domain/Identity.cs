namespace StayHost.Domain;

/// <summary>Which one of the two things docs/01 TK-01 lets somebody sign up with.</summary>
public enum IdentifierKind
{
    Email = 0,
    Phone = 1,
    /// <summary>docs/01 TK-07 — a company email, proved with the same six-digit code.</summary>
    WorkEmail = 2
}

/// <summary>docs/01 TK-02 — the three the spec names, and nothing else.</summary>
public enum ExternalProvider
{
    Google = 0,
    Apple = 1,
    Facebook = 2
}

/// <summary>
/// docs/01 TK-02 — a link between an account here and one at Google, Apple or
/// Facebook. One row per provider per account, so somebody can attach all three
/// and still be one person.
/// </summary>
public class ExternalLogin
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public ExternalProvider Provider { get; set; }

    /// <summary>The provider's own id for this person. Never their email — people change those.</summary>
    public string ProviderUserId { get; set; } = "";

    /// <summary>What the provider said the email was, kept for support to look at.</summary>
    public string? ProviderEmail { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
}

/// <summary>
/// docs/01 TK-01 — a six-digit code sent to a phone or an email. Separate from
/// <see cref="UserToken"/> because the rules are different: short, guessable by
/// design, so it needs an expiry, an attempt limit and a resend cooldown.
/// </summary>
public class OneTimeCode
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Where it was sent — the phone number or the email address.</summary>
    public string SentTo { get; set; } = "";
    public IdentifierKind Kind { get; set; }

    /// <summary>Hashed, like a password. A leaked database should not hand over live codes.</summary>
    public string CodeHash { get; set; } = "";
    public string CodeSalt { get; set; } = "";

    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(10);
    public DateTime? UsedAt { get; set; }
}

/// <summary>
/// The rules behind signing up and verifying, kept where they can be tested
/// without a database or an SMS provider.
/// </summary>
public static class Identity
{
    /// <summary>docs/01 TK-01 — six digits, no more and no fewer.</summary>
    public const int CodeLength = 6;

    /// <summary>Long enough to read a text and type it, short enough to be worth little if seen.</summary>
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    /// <summary>Guessing one in six attempts out of a million is not a threat; fifty is.</summary>
    public const int MaxAttempts = 5;

    /// <summary>Stops a resend button being used to text somebody repeatedly.</summary>
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    /// <summary>docs/01 TK-03 — nobody under this age may hold an account.</summary>
    public const int MinimumAge = 18;

    public enum Refusal
    {
        None = 0,
        NoIdentifier,
        BadEmail,
        BadPhone,
        Taken,
        WeakPassword,
        NoName,
        TooYoung,
        NoBirthday
    }

    public readonly record struct Check(bool Ok, Refusal Reason, string Message)
    {
        public static Check Pass => new(true, Refusal.None, "");
        public static Check Fail(Refusal reason, string message) => new(false, reason, message);
    }

    /// <summary>
    /// Vietnamese mobile numbers, in any of the shapes people actually type them:
    /// 0912345678, +84912345678, 84912345678, with spaces or dots between.
    /// </summary>
    public static string? NormalisePhone(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var digits = new string(raw.Where(char.IsDigit).ToArray());

        // Strip the country code down to the national 0-leading form, which is
        // the one everybody here recognises on a screen.
        if (digits.StartsWith("84") && digits.Length is 11 or 12) digits = "0" + digits[2..];
        else if (!digits.StartsWith('0') && digits.Length == 9) digits = "0" + digits;

        var valid = digits.Length == 10 && digits[0] == '0' && digits[1] != '0';
        return valid ? digits : null;
    }

    public static bool LooksLikeEmail(string? raw)
    {
        var value = (raw ?? "").Trim();
        var at = value.IndexOf('@');

        return at > 0
               && at < value.Length - 3
               && value.IndexOf('@', at + 1) < 0
               && value.LastIndexOf('.') > at + 1
               && !value.Contains(' ');
    }

    /// <summary>docs/01 TK-03 — age on the day they sign up, not this calendar year.</summary>
    public static int AgeOn(DateOnly birthday, DateOnly today)
    {
        var age = today.Year - birthday.Year;
        if (birthday.AddYears(age) > today) age--;
        return age;
    }

    public static bool IsOldEnough(DateOnly birthday, DateOnly today) =>
        AgeOn(birthday, today) >= MinimumAge;

    /// <summary>
    /// docs/01 TK-01 and TK-03 — everything checked before an account exists.
    /// Either an email or a phone will do, and exactly one of them is enough.
    /// </summary>
    public static Check CanRegister(
        string? email, string? phone, string? password, string? fullName,
        DateOnly? birthday, DateOnly today, bool identifierTaken)
    {
        var hasEmail = !string.IsNullOrWhiteSpace(email);
        var hasPhone = !string.IsNullOrWhiteSpace(phone);

        if (!hasEmail && !hasPhone)
            return Check.Fail(Refusal.NoIdentifier, "Cần email hoặc số điện thoại để đăng ký.");

        if (hasEmail && !LooksLikeEmail(email))
            return Check.Fail(Refusal.BadEmail, "Email không hợp lệ.");

        if (hasPhone && NormalisePhone(phone) is null)
            return Check.Fail(Refusal.BadPhone, "Số điện thoại không hợp lệ. Ví dụ: 0912 345 678.");

        if (string.IsNullOrWhiteSpace(fullName))
            return Check.Fail(Refusal.NoName, "Vui lòng nhập họ tên.");

        if ((password ?? "").Length < 8)
            return Check.Fail(Refusal.WeakPassword, "Mật khẩu cần tối thiểu 8 ký tự.");

        if (birthday is not { } dob)
            return Check.Fail(Refusal.NoBirthday, "Cần ngày sinh để xác nhận bạn đủ 18 tuổi.");

        if (!IsOldEnough(dob, today))
            return Check.Fail(Refusal.TooYoung, $"Bạn cần đủ {MinimumAge} tuổi để tạo tài khoản.");

        return identifierTaken
            ? Check.Fail(Refusal.Taken, hasEmail && !hasPhone
                ? "Email này đã được đăng ký."
                : "Số điện thoại này đã được đăng ký.")
            : Check.Pass;
    }

    /* ------------------------------------------------------------ the code */

    public static bool CodeExpired(DateTime expiresAt, DateTime now) => now >= expiresAt;

    public static bool OutOfAttempts(int attempts) => attempts >= MaxAttempts;

    /// <summary>True while a new code would be too soon after the last one.</summary>
    public static bool ResendTooSoon(DateTime lastSentAt, DateTime now) =>
        now - lastSentAt < ResendCooldown;

    public static string ProviderLabel(ExternalProvider provider) => provider switch
    {
        ExternalProvider.Google => "Google",
        ExternalProvider.Apple => "Apple",
        _ => "Facebook"
    };

    public static string KindLabel(IdentifierKind kind) => kind switch
    {
        IdentifierKind.Phone => "số điện thoại",
        IdentifierKind.WorkEmail => "email công ty",
        _ => "email"
    };
}
