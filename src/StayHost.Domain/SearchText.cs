using System.Globalization;
using System.Text;

namespace StayHost.Domain;

/// <summary>
/// docs/03 §6: "Tìm địa điểm phải bỏ dấu được: gõ 'da lat' phải ra 'Đà Lạt',
/// gõ 'hcm' phải ra 'Thành phố Hồ Chí Minh'." Both halves live here: stripping
/// diacritics, and the handful of abbreviations Vietnamese travellers type.
/// </summary>
public static class SearchText
{
    /// <summary>
    /// Lowercase, diacritic-free, single-spaced. Vietnamese đ/Đ has no combining
    /// form, so it is mapped by hand before the Unicode decomposition.
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";

        var lowered = value.Trim().ToLowerInvariant().Replace('đ', 'd');
        var decomposed = lowered.Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// What a listing is searchable by. Stored on the row so the query is a
    /// plain LIKE the database can index, not a per-row function call.
    /// </summary>
    public static string ForListing(string title, string city, string country) =>
        $"{Normalize(title)} {Normalize(city)} {Normalize(country)} {AliasesFor(city)}".Trim();

    /// <summary>
    /// Shorthand people actually type. Stored alongside the city so "hcm" or
    /// "sg" reaches Thành phố Hồ Chí Minh without a second lookup at query time.
    /// </summary>
    /// <remarks>
    /// Keyed by <see cref="Cities.Key"/>, so every spelling of one city reaches
    /// the same row. It used to be keyed by the literal name, which meant "TP.
    /// Hồ Chí Minh" and "Thành phố Hồ Chí Minh" each needed their own copy —
    /// and the second one was a line somebody had to remember to keep in step.
    /// </remarks>
    private static readonly Dictionary<string, string[]> CityAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ho chi minh"] = ["hcm", "tphcm", "sg", "saigon", "sai gon", "ho chi minh", "thanh pho ho chi minh"],
        ["Hà Nội"] = ["hn", "hanoi"],
        ["Đà Nẵng"] = ["dn", "danang"],
        ["Đà Lạt"] = ["dl", "dalat"],
        ["Nha Trang"] = ["nt", "nhatrang"],
        ["Phú Quốc"] = ["pq", "phuquoc"],
        ["Hội An"] = ["ha", "hoian"],
        ["Vũng Tàu"] = ["vt", "vungtau"],
        ["Sa Pa"] = ["sapa"],
        ["Hạ Long"] = ["halong", "hl"],
        ["Quy Nhơn"] = ["quynhon", "qn"],
        ["Huế"] = ["hue"],
        ["Ninh Bình"] = ["ninhbinh", "nb"],
        ["Phan Thiết"] = ["phanthiet", "mui ne", "muine"]
    };

    public static string AliasesFor(string? city) =>
        CityAliases.TryGetValue(Cities.Key(city), out var aliases) ? string.Join(' ', aliases)
        : city is not null && CityAliases.TryGetValue(city, out var byName) ? string.Join(' ', byName)
        : "";

    /// <summary>Every term a query should match against, already normalised.</summary>
    public static string[] Terms(string? query) =>
        Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
