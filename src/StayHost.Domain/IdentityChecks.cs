namespace StayHost.Domain;

/// <summary>docs/01 TK-06 — the papers a person may prove themselves with.</summary>
public enum IdentityDocument
{
    NationalId = 0,
    Passport = 1,
    DriverLicence = 2
}

public enum IdentityCheckStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

/// <summary>
/// docs/01 TK-06 — one attempt at proving who somebody is: a photo of a
/// document, a selfie, and a human decision about the pair.
///
/// The images are kept because a decision nobody can re-examine is not a
/// decision; the document number is not, beyond its last four digits. A full
/// identity-document number is the kind of thing that has to be worth holding,
/// and for deciding "is this the same face" it is not.
/// </summary>
public class IdentityCheck
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public IdentityDocument Document { get; set; }

    /// <summary>Enough to tell two submissions apart in a support call, and no more.</summary>
    public string? DocumentLast4 { get; set; }

    public string FrontImageUrl { get; set; } = "";
    /// <summary>Passports have one side; identity cards have two.</summary>
    public string? BackImageUrl { get; set; }
    public string SelfieImageUrl { get; set; } = "";

    public IdentityCheckStatus Status { get; set; } = IdentityCheckStatus.Pending;

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAt { get; set; }

    public int? DecidedByUserId { get; set; }
    public User? DecidedByUser { get; set; }

    /// <summary>Why it was refused. Shown to the person, so it has to be a reason they can act on.</summary>
    public string? Note { get; set; }
}

/// <summary>docs/01 TK-06 — when somebody may submit, and what happens after.</summary>
public static class IdentityChecks
{
    public static string DocumentLabel(IdentityDocument document) => document switch
    {
        IdentityDocument.Passport => "Hộ chiếu",
        IdentityDocument.DriverLicence => "Giấy phép lái xe",
        _ => "Căn cước công dân"
    };

    public static string StatusLabel(IdentityCheckStatus status) => status switch
    {
        IdentityCheckStatus.Approved => "Đã xác minh",
        IdentityCheckStatus.Rejected => "Bị từ chối",
        _ => "Đang chờ duyệt"
    };

    public static string BadgeClass(IdentityCheckStatus status) => status switch
    {
        IdentityCheckStatus.Approved => "confirmed",
        IdentityCheckStatus.Rejected => "cancelled",
        _ => "pending"
    };

    /// <summary>Only the back of an identity card is asked for; a passport has one page.</summary>
    public static bool NeedsBackImage(IdentityDocument document) =>
        document is not IdentityDocument.Passport;

    public readonly record struct Check(bool Ok, string Message)
    {
        public static Check Pass => new(true, "");
        public static Check Fail(string message) => new(false, message);
    }

    /// <summary>
    /// docs/01 TK-06 — what has to be true before a submission is accepted.
    /// <paramref name="latest"/> is the person's most recent attempt, if any.
    /// </summary>
    public static Check CanSubmit(
        IdentityCheck? latest,
        IdentityDocument document,
        string? frontUrl,
        string? backUrl,
        string? selfieUrl)
    {
        if (latest?.Status == IdentityCheckStatus.Approved)
            return Check.Fail("Danh tính của bạn đã được xác minh.");

        if (latest?.Status == IdentityCheckStatus.Pending)
            return Check.Fail("Hồ sơ trước của bạn đang chờ duyệt.");

        if (!IsIdentityUpload(frontUrl))
            return Check.Fail("Cần ảnh mặt trước giấy tờ.");

        if (NeedsBackImage(document) && !IsIdentityUpload(backUrl))
            return Check.Fail("Cần ảnh mặt sau giấy tờ.");

        if (!IsIdentityUpload(selfieUrl))
            return Check.Fail("Cần ảnh chân dung tự chụp.");

        // A document photo submitted twice as both sides, or as the selfie, is
        // not two pieces of evidence — and it is the commonest way of faking one.
        var urls = new[] { frontUrl, backUrl, selfieUrl }.Where(u => !string.IsNullOrWhiteSpace(u)).ToList();
        if (urls.Distinct(StringComparer.OrdinalIgnoreCase).Count() != urls.Count)
            return Check.Fail("Mỗi ảnh phải là một ảnh khác nhau.");

        return Check.Pass;
    }

    /// <summary>
    /// docs/08 §4 — an image this platform stored for an identity check.
    ///
    /// These three checks used to call <see cref="Profiles.IsOwnUpload"/>, which
    /// answers for the public <c>/uploads/</c> folder — the one an avatar goes
    /// in. Identity papers stopped going there when they were moved outside the
    /// web root, and the uploader has answered <c>/api/identity-files/…</c> ever
    /// since, so every real submission was refused with "Cần ảnh mặt trước giấy
    /// tờ" and nobody could prove who they were at all. Nothing failed loudly:
    /// the guest saw a plausible complaint about their own photo.
    ///
    /// The shape is the same one <c>IdentityFilesController</c> serves, so a URL
    /// that passes here is one that route can find: user id, a GUID, an image
    /// extension, and no path of its own.
    /// </summary>
    public static bool IsIdentityUpload(string? url)
    {
        var value = (url ?? "").Trim();
        const string prefix = "/api/identity-files/";

        if (!value.StartsWith(prefix, StringComparison.Ordinal)) return false;

        var name = value[prefix.Length..];
        if (name.Length == 0 || name.Contains('/') || name.Contains('\\') || name.Contains("..")) return false;

        var dot = name.LastIndexOf('.');
        if (dot <= 0) return false;

        var extension = name[dot..].ToLowerInvariant();
        if (extension is not (".jpg" or ".png" or ".webp" or ".avif")) return false;

        var parts = name[..dot].Split('-');
        return parts.Length == 2
               && parts[0].Length > 0 && parts[0].All(char.IsAsciiDigit)
               && parts[1].Length == 32 && parts[1].All(char.IsAsciiHexDigitLower);
    }

    /// <summary>The last four characters of whatever was typed, digits and letters only.</summary>
    public static string? Last4(string? documentNumber)
    {
        var kept = new string((documentNumber ?? "").Where(char.IsLetterOrDigit).ToArray());
        return kept.Length == 0 ? null : kept[^Math.Min(4, kept.Length)..].ToUpperInvariant();
    }
}
