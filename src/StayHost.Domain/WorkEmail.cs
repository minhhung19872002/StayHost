namespace StayHost.Domain;

/// <summary>
/// docs/01 TK-07 — verifying a company email, for people travelling for work.
///
/// The point of the badge is that the address belongs to an organisation, so a
/// free consumer mailbox does not count: proving you own a gmail address says
/// nothing about who you work for. The list below is the common free providers;
/// anything else is treated as a company domain. The proof itself is the ordinary
/// six-digit code sent to the address — this class only decides what is eligible.
/// </summary>
public static class WorkEmail
{
    private static readonly HashSet<string> FreeProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com", "googlemail.com", "yahoo.com", "yahoo.com.vn", "ymail.com",
        "outlook.com", "outlook.com.vn", "hotmail.com", "hotmail.com.vn", "live.com",
        "msn.com", "icloud.com", "me.com", "mac.com", "proton.me", "protonmail.com",
        "aol.com", "gmx.com", "gmx.net", "mail.com", "yandex.com", "yandex.ru",
        "zoho.com", "fastmail.com", "tutanota.com", "hey.com"
    };

    public static string? Normalise(string? email)
    {
        var e = (email ?? "").Trim().ToLowerInvariant();
        return e.Length == 0 ? null : e;
    }

    /// <summary>The part after the @, or null if the address is not well formed.</summary>
    public static string? Domain(string? email)
    {
        var e = Normalise(email);
        if (e is null) return null;

        var at = e.LastIndexOf('@');
        if (at <= 0 || at == e.Length - 1) return null;

        var local = e[..at];
        var domain = e[(at + 1)..];

        // A single dot with labels either side, no spaces — enough to reject the
        // obviously broken without pretending to be a full RFC validator.
        if (local.Contains(' ') || domain.Contains(' ')) return null;
        if (!domain.Contains('.') || domain.StartsWith('.') || domain.EndsWith('.')) return null;
        if (domain.Contains("..")) return null;

        return domain;
    }

    public static bool IsFreeProvider(string? email) =>
        Domain(email) is { } d && FreeProviders.Contains(d);

    /// <summary>
    /// True when the address is well formed and its domain is not a known free
    /// consumer provider — the only kind that earns the work-email badge.
    /// </summary>
    public static bool IsCompanyEmail(string? email) =>
        Domain(email) is { } d && !FreeProviders.Contains(d);

    public static string FreeProviderMessage() =>
        "Email công ty phải dùng tên miền của tổ chức, không dùng email cá nhân " +
        "(gmail, yahoo, outlook…).";

    public static string InvalidMessage() => "Địa chỉ email không hợp lệ.";
}
