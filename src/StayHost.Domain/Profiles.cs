namespace StayHost.Domain;

/// <summary>
/// docs/01 TK-04 and TK-05 — what a person may say about themselves, and which
/// of it the rest of the world gets to read.
///
/// The rules live here rather than in the controller because both ends need
/// them: the editor has to refuse a bio of ten thousand characters, and the
/// public page has to render the same list of languages the editor stored.
/// </summary>
public static class Profiles
{
    /// <summary>Long enough for a real introduction, short enough to read.</summary>
    public const int BioMax = 700;

    /// <summary>A place of residence or a job title is one line, not a paragraph.</summary>
    public const int LineMax = 80;

    /// <summary>One spoken language or one interest.</summary>
    public const int TagMax = 40;

    public const int MaxLanguages = 8;
    public const int MaxInterests = 12;

    /// <summary>
    /// Languages are stored as codes so the label can be rewritten without a
    /// migration; interests are free text, so they are stored as typed.
    /// </summary>
    private const char LanguageSeparator = ',';
    private const char InterestSeparator = '\n';

    /// <summary>
    /// The ones the picker offers. Labelled in Vietnamese because that is what
    /// the screen is in — a Vietnamese guest reading a host's profile wants
    /// "Tiếng Nhật", not "日本語".
    /// </summary>
    public static readonly IReadOnlyList<(string Code, string Label)> SpokenLanguages =
    [
        ("vi", "Tiếng Việt"),
        ("en", "Tiếng Anh"),
        ("zh", "Tiếng Trung"),
        ("ko", "Tiếng Hàn"),
        ("ja", "Tiếng Nhật"),
        ("fr", "Tiếng Pháp"),
        ("de", "Tiếng Đức"),
        ("es", "Tiếng Tây Ban Nha"),
        ("ru", "Tiếng Nga"),
        ("th", "Tiếng Thái"),
        ("km", "Tiếng Khmer"),
        ("lo", "Tiếng Lào")
    ];

    /// <summary>An unknown code is shown as itself rather than dropped — data outlives lists.</summary>
    public static string LanguageLabel(string code)
    {
        var key = (code ?? "").Trim().ToLowerInvariant();
        foreach (var (c, label) in SpokenLanguages)
            if (c == key) return label;

        return key;
    }

    public static bool IsKnownLanguage(string? code)
    {
        var key = (code ?? "").Trim().ToLowerInvariant();
        foreach (var (c, _) in SpokenLanguages)
            if (c == key) return true;

        return false;
    }

    /// <summary>
    /// One line of user text: whitespace collapsed, trimmed, cut to length, and
    /// empty turned into null so "cleared the field" and "never filled it in"
    /// are the same thing in the database.
    /// </summary>
    public static string? Tidy(string? raw, int max)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var collapsed = string.Join(' ', raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length <= max ? collapsed : collapsed[..max].TrimEnd();
    }

    /// <summary>docs/01 TK-04 — a bio, at the length a bio is allowed to be.</summary>
    public static string? TidyBio(string? raw) => TidyLines(raw, BioMax);

    /// <summary>
    /// Same as <see cref="Tidy"/>, but the text may keep its line breaks —
    /// people write them on purpose. Runs of blank lines collapse to one so a
    /// page cannot be padded out with empty space.
    /// </summary>
    public static string? TidyLines(string? raw, int max)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var lines = raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var kept = new List<string>();
        foreach (var line in lines)
        {
            var one = string.Join(' ', line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (one.Length == 0 && (kept.Count == 0 || kept[^1].Length == 0)) continue;
            kept.Add(one);
        }
        while (kept.Count > 0 && kept[^1].Length == 0) kept.RemoveAt(kept.Count - 1);

        var text = string.Join('\n', kept);
        if (text.Length == 0) return null;
        return text.Length <= max ? text : text[..max].TrimEnd();
    }

    /// <summary>
    /// A list of tags as the profile should hold it: trimmed, blanks dropped,
    /// duplicates removed case-insensitively keeping the first spelling, and
    /// capped. The separator is stripped out of each value so packing and
    /// unpacking round-trip whatever somebody typed.
    /// </summary>
    private static List<string> TidyList(IEnumerable<string>? raw, int maxItems, char separator)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kept = new List<string>();

        foreach (var value in raw ?? [])
        {
            var one = Tidy(value?.Replace(separator, ' '), TagMax);
            if (one is null || !seen.Add(one)) continue;

            kept.Add(one);
            if (kept.Count == maxItems) break;
        }

        return kept;
    }

    /// <summary>docs/01 TK-04 — the languages somebody says they speak, known codes only.</summary>
    public static IReadOnlyList<string> TidyLanguages(IEnumerable<string>? raw)
    {
        var lowered = (raw ?? []).Select(c => (c ?? "").Trim().ToLowerInvariant()).Where(IsKnownLanguage);
        return TidyList(lowered, MaxLanguages, LanguageSeparator);
    }

    public static IReadOnlyList<string> TidyInterests(IEnumerable<string>? raw) =>
        TidyList(raw, MaxInterests, InterestSeparator);

    public static string PackLanguages(IEnumerable<string>? codes) =>
        string.Join(LanguageSeparator, TidyLanguages(codes));

    public static string PackInterests(IEnumerable<string>? values) =>
        string.Join(InterestSeparator, TidyInterests(values));

    public static IReadOnlyList<string> UnpackLanguages(string? packed) =>
        Unpack(packed, LanguageSeparator);

    public static IReadOnlyList<string> UnpackInterests(string? packed) =>
        Unpack(packed, InterestSeparator);

    private static IReadOnlyList<string> Unpack(string? packed, char separator) =>
        string.IsNullOrWhiteSpace(packed)
            ? []
            : packed.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// docs/01 TK-04 — the name other people see. A display name is what they
    /// chose to be called; the legal name on the account is nobody else's
    /// business, so it is only the fallback.
    /// </summary>
    public static string DisplayNameOf(string? preferred, string? fullName)
    {
        var chosen = Tidy(preferred, LineMax);
        if (chosen is not null) return chosen;

        return Tidy(fullName, LineMax) ?? "Người dùng Staylio";
    }

    /// <summary>Initials for the grey circle shown wherever there is no photo.</summary>
    public static string InitialsOf(string displayName)
    {
        var words = (displayName ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return "?";

        var first = char.ToUpperInvariant(words[0][0]);
        return words.Length == 1 ? first.ToString() : $"{first}{char.ToUpperInvariant(words[^1][0])}";
    }

    /// <summary>
    /// docs/01 TK-04 — a profile photo may only be one this platform stored.
    /// Anything else — an off-site address, a <c>javascript:</c> URL, a path
    /// climbing out of the uploads folder — is not a photo somebody uploaded,
    /// it is somebody telling every other browser what to fetch.
    /// </summary>
    public static bool IsOwnUpload(string? url)
    {
        var value = (url ?? "").Trim();

        return value.StartsWith("/uploads/", StringComparison.Ordinal)
               && value.Length > "/uploads/".Length
               && !value.Contains("..", StringComparison.Ordinal)
               && !value.Contains('\\')
               && value.IndexOf('/', "/uploads/".Length) < 0;
    }

    /// <summary>
    /// A host's overall score, across every place they let. Averaging the
    /// listings' own averages gets this wrong twice: a listing with four
    /// reviews would weigh as much as one with four hundred, and a listing
    /// nobody has reviewed yet — which carries a rating of zero, not of five —
    /// would drag a superhost down to 3.87. Every review counts once instead.
    /// Null means nobody has reviewed anything they let.
    /// </summary>
    public static double? OverallRating(IEnumerable<(double Rating, int ReviewCount)> listings)
    {
        double weighted = 0;
        var reviews = 0;

        foreach (var (rating, count) in listings)
        {
            if (count <= 0) continue;
            weighted += rating * count;
            reviews += count;
        }

        return reviews == 0 ? null : Math.Round(weighted / reviews, 2);
    }

    /// <summary>"Tháng 8, 2026" — how a date reads next to a review or a profile.</summary>
    public static string MonthLabel(DateTime at) => $"Tháng {at.Month}, {at.Year}";

    /// <summary>docs/02 C6 — "Tham gia Staylio tháng 8, 2026".</summary>
    public static string JoinedLabel(DateTime createdAt) =>
        $"Tham gia Staylio tháng {createdAt.Month}, {createdAt.Year}";

    /// <summary>
    /// docs/01 TK-05 — the verification badges a stranger may see. Only what was
    /// actually proved is listed; there is no "not verified" badge, because an
    /// absent badge already says that and a red cross on a profile does not.
    /// </summary>
    public static IReadOnlyList<string> Badges(bool emailConfirmed, bool phoneConfirmed, bool identityVerified)
    {
        var badges = new List<string>();
        if (identityVerified) badges.Add("Đã xác minh danh tính");
        if (emailConfirmed) badges.Add("Đã xác thực email");
        if (phoneConfirmed) badges.Add("Đã xác thực số điện thoại");
        return badges;
    }
}
