namespace StayHost.Domain;

/// <summary>
/// docs/01 TĐ-11 and TĐ-21 — the two things the review block owes a reader
/// beyond the reviews themselves: which language each one is in, so they can be
/// filtered down to ones the reader understands, and what the whole set keeps
/// saying, so a hundred of them can be taken in without reading a hundred.
///
/// Both are computed from the text. Nothing here decides anything about money
/// or eligibility, so a wrong guess costs a filter row, not a booking.
/// </summary>
public static class ReviewInsights
{
    /* -------------------------------------------------------- TĐ-11 */

    /// <summary>
    /// The language a review reads as. A review written through the site can say
    /// so itself (<c>Review.Language</c>); this is the answer for the ones that
    /// cannot — every review written before the column existed, and every seeded
    /// one — so the filter works on the whole set from the first request rather
    /// than after a backfill.
    ///
    /// Scripts are decided by the characters, which is exact. Latin text is
    /// called Vietnamese when it carries Vietnamese marks and English otherwise:
    /// this cannot tell English from French, and deliberately does not try. The
    /// interface offers eight languages and the filter is a convenience, so the
    /// honest failure is to group the Latin ones together rather than to sort
    /// them wrongly with confidence.
    /// </summary>
    public static string GuessLanguage(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "en";

        int han = 0, kana = 0, hangul = 0, viet = 0, latin = 0;

        foreach (var c in text)
        {
            if (c is >= '一' and <= '鿿') han++;
            else if (c is >= '぀' and <= 'ヿ') kana++;
            else if (c is >= '가' and <= '힯' or >= 'ᄀ' and <= 'ᇿ') hangul++;
            else if (IsVietnameseMark(c)) viet++;
            else if (char.IsLetter(c)) latin++;
        }

        // Japanese is told from Chinese by kana, which Chinese does not use at
        // all. Checked before han because Japanese prose carries both.
        if (kana > 0) return "ja";
        if (hangul > 0) return "ko";
        if (han > 0) return "zh";
        return viet > 0 ? "vi" : "en";
    }

    /// <summary>
    /// A letter only Vietnamese uses among the languages on offer here. The
    /// plain ASCII vowels are left out on purpose: they say nothing.
    /// </summary>
    private static bool IsVietnameseMark(char c) =>
        "àáảãạăằắẳẵặâầấẩẫậèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵđ"
            .Contains(char.ToLowerInvariant(c));

    /// <summary>
    /// The language of one review: what it said about itself, or the guess.
    /// Stored values are trusted — the writer's own interface language is a
    /// better answer than anything read out of the characters.
    /// </summary>
    public static string LanguageOf(string? stored, string? text) =>
        string.IsNullOrWhiteSpace(stored) ? GuessLanguage(text) : stored.Trim().ToLowerInvariant();

    /* -------------------------------------------------------- TĐ-21 */

    /// <summary>One subject reviewers keep coming back to, and what they said about it.</summary>
    public readonly record struct Theme(string Key, string Label, int Mentions, double Rating);

    /// <summary>
    /// The subjects, and the words that count as talking about them. Written
    /// without diacritics because every review is normalised through
    /// <see cref="SearchText.Normalize"/> before it is searched — the same road
    /// the search box takes, so "sach se" and "sạch sẽ" are one word here.
    /// </summary>
    private static readonly (string Key, string Label, string[] Words)[] Subjects =
    [
        ("location",   "Vị trí",          ["vi tri", "gan", "trung tam", "bien", "pho co", "di bo", "location"]),
        ("clean",      "Sạch sẽ",         ["sach", "ve sinh", "thom", "gon gang", "clean"]),
        ("amenities",  "Tiện nghi",       ["tien nghi", "day du", "bep", "may lanh", "dieu hoa", "wifi", "ho boi", "amenit"]),
        ("host",       "Chủ nhà",         ["chu nha", "nhiet tinh", "than thien", "ho tro", "nhan tin", "host"]),
        ("family",     "Hợp gia đình",    ["gia dinh", "tre em", "em be", "bo me", "con nho", "family", "kid"]),
        ("value",      "Đáng giá tiền",   ["dang gia", "gia tot", "hop ly", "re", "xung dang", "worth", "value"]),
        ("quiet",      "Yên tĩnh",        ["yen tinh", "im lang", "thoang", "quiet"]),
        ("checkin",    "Nhận phòng",      ["nhan phong", "check in", "checkin", "nhan chia khoa", "tra phong"])
    ];

    /// <summary>
    /// docs/01 TĐ-21 — what the reviews keep saying, strongest first.
    ///
    /// A subject earns a row only when enough separate reviews raise it: one
    /// person mentioning the pool is not a theme, and presenting it as one would
    /// read like a summary of the place rather than of the reviews. The rating
    /// shown is the average of the reviews that raised it, not the overall
    /// score, which is the only thing that makes the row worth reading — it says
    /// how people who cared about this subject rated the stay.
    /// </summary>
    public const int MinMentions = 3;

    public static IReadOnlyList<Theme> Themes(
        IEnumerable<(string Text, double Rating)> reviews, int take = 6)
    {
        var rows = reviews.ToList();
        if (rows.Count < MinMentions) return [];

        var normalised = rows
            .Select(r => (Text: SearchText.Normalize(r.Text ?? ""), r.Rating))
            .ToList();

        var found = new List<Theme>();

        foreach (var (key, label, words) in Subjects)
        {
            var hits = normalised.Where(r => words.Any(w => r.Text.Contains(w))).ToList();
            if (hits.Count < MinMentions) continue;

            found.Add(new Theme(key, label, hits.Count,
                Math.Round(hits.Average(h => h.Rating), 1, MidpointRounding.AwayFromZero)));
        }

        return found
            .OrderByDescending(t => t.Mentions)
            .ThenByDescending(t => t.Rating)
            .Take(take)
            .ToList();
    }
}
