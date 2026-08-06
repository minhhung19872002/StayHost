using System.Text.RegularExpressions;

namespace StayHost.Domain;

/// <summary>
/// docs/03 §10 and §7: contact details are masked in messages until a booking
/// is confirmed, and reviews carrying them are refused outright. One place
/// knows what "contact details" means so both rules stay in step.
/// </summary>
public static partial class ContentGuard
{
    /// <summary>
    /// Vietnamese mobile numbers, with or without spacing, plus the generic
    /// long-digit-run case. Deliberately broad: a false positive costs a guest
    /// one rephrasing, a false negative moves the deal off the platform.
    /// </summary>
    [GeneratedRegex(@"(?:\+?84|0)\s*(?:\d[\s.\-]?){8,10}", RegexOptions.CultureInvariant)]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"[\w.+\-]+\s*(?:@|\(at\)|\[at\])\s*[\w\-]+\s*(?:\.|\(dot\))\s*[\w.\-]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?:https?://|www\.)\S+|\b[\w\-]+\.(?:com|net|org|vn|io|me|info|biz)\b(?:/\S*)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LinkRegex();

    /// <summary>Messaging apps people reach for to leave the platform.</summary>
    [GeneratedRegex(@"\b(?:zalo|whatsapp|telegram|viber|wechat|messenger|facebook|fb)\b\s*[:\-]?\s*[\w.@+]{4,}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HandleRegex();

    public const string Mask = "[đã ẩn]";

    public readonly record struct Finding(bool HasPhone, bool HasEmail, bool HasLink, bool HasHandle)
    {
        public bool Any => HasPhone || HasEmail || HasLink || HasHandle;

        /// <summary>What to tell the writer, naming only what was actually found.</summary>
        public string Explain()
        {
            var parts = new List<string>();
            if (HasPhone) parts.Add("số điện thoại");
            if (HasEmail) parts.Add("địa chỉ email");
            if (HasLink) parts.Add("đường liên kết");
            if (HasHandle) parts.Add("tài khoản mạng xã hội");
            return parts.Count == 0 ? "" : string.Join(", ", parts);
        }
    }

    public static Finding Inspect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return default;

        return new Finding(
            PhoneRegex().IsMatch(text),
            EmailRegex().IsMatch(text),
            LinkRegex().IsMatch(text),
            HandleRegex().IsMatch(text));
    }

    /// <summary>
    /// docs/03 §10 — what the other party sees before a booking is confirmed.
    /// The original is never altered; only the copy on its way out is masked.
    /// </summary>
    public static string MaskContacts(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text ?? "";

        var masked = EmailRegex().Replace(text, Mask);
        masked = LinkRegex().Replace(masked, Mask);
        masked = HandleRegex().Replace(masked, Mask);
        masked = PhoneRegex().Replace(masked, Mask);
        return masked;
    }

    /// <summary>
    /// docs/01 ĐG-09 — words that get a review refused rather than masked.
    /// Kept short and specific; borderline wording is a job for a human.
    /// </summary>
    private static readonly string[] SlurStems =
    [
        "đồ mọi", "bọn mọi", "mọi rợ", "da đen bẩn", "đồ chó", "súc vật",
        "đĩ", "điếm", "thằng ngu", "con ngu", "đồ ngu"
    ];

    public readonly record struct ReviewCheck(bool Ok, string Message)
    {
        public static ReviewCheck Pass => new(true, "");
    }

    /// <summary>A review is refused, not masked: it is permanent and public.</summary>
    public static ReviewCheck CheckReview(string? text)
    {
        var finding = Inspect(text);
        if (finding.Any)
        {
            return new ReviewCheck(false,
                $"Đánh giá không được chứa {finding.Explain()}. Vui lòng bỏ phần đó rồi gửi lại.");
        }

        var lowered = (text ?? "").ToLowerInvariant();
        if (SlurStems.Any(lowered.Contains))
        {
            return new ReviewCheck(false,
                "Đánh giá có ngôn từ xúc phạm hoặc phân biệt đối xử nên không được đăng.");
        }

        return ReviewCheck.Pass;
    }
}
