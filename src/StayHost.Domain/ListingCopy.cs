namespace StayHost.Domain;

/// <summary>
/// docs/01 CN-08 — "viết tiêu đề (giới hạn ký tự) và mô tả, có gợi ý tự động".
///
/// The suggestions are built from what the host has already told the wizard —
/// the type of place, the city, the size, the amenities they ticked. That is
/// deliberate: a host stuck on the title usually has the facts entered already
/// and is only stuck on the sentence. Nothing here invents a claim the listing
/// does not support.
/// </summary>
public static class ListingCopy
{
    /// <summary>docs/01 CN-08 — the limit the editor counts down from.</summary>
    public const int TitleMax = 60;

    public const int DescriptionMin = 40;

    /// <summary>The amenities worth putting in a title, in the order they sell.</summary>
    private static readonly (string Key, string Phrase)[] Headline =
    [
        ("pool", "hồ bơi riêng"),
        ("beach", "sát biển"),
        ("view", "view đẹp"),
        ("hottub", "bồn tắm nước nóng"),
        ("bbq", "khu BBQ"),
        ("fire", "lò sưởi"),
        ("kitchen", "bếp đầy đủ"),
        ("workspace", "góc làm việc riêng"),
        ("parking", "chỗ đậu xe"),
        ("pet", "đón thú cưng")
    ];

    private static string TypeWord(PlaceType type) => type switch
    {
        PlaceType.Villa => "Villa",
        PlaceType.Apartment => "Căn hộ",
        PlaceType.Homestay => "Homestay",
        PlaceType.Cabin => "Cabin gỗ",
        PlaceType.Boutique => "Boutique",
        PlaceType.Hotel => "Khách sạn",
        _ => "Nhà nguyên căn"
    };

    private static string RoomWord(RoomType room) => room switch
    {
        RoomType.PrivateRoom => "phòng riêng",
        RoomType.SharedRoom => "phòng chung",
        _ => "nguyên căn"
    };

    /// <summary>What the wizard knows by the time it can offer a suggestion.</summary>
    public readonly record struct Facts(
        PlaceType Type,
        RoomType Room,
        string City,
        int Bedrooms,
        int MaxGuests,
        IReadOnlyList<string> AmenityKeys);

    private static List<string> Phrases(Facts f) =>
        Headline.Where(h => f.AmenityKeys.Contains(h.Key)).Select(h => h.Phrase).ToList();

    /// <summary>
    /// docs/01 CN-08 — three titles to pick from or edit, never one to accept.
    /// Each is cut to <see cref="TitleMax"/>, so a suggestion is never a title
    /// the editor would then refuse.
    /// </summary>
    public static IReadOnlyList<string> Titles(Facts f)
    {
        var type = TypeWord(f.Type);
        var city = (f.City ?? "").Trim();
        var phrases = Phrases(f);
        var best = phrases.FirstOrDefault();

        var candidates = new List<string>
        {
            best is null ? $"{type} {RoomWord(f.Room)} tại {city}" : $"{type} {best} tại {city}",

            f.Bedrooms > 0
                ? $"{type} {f.Bedrooms} phòng ngủ ở {city}{(best is null ? "" : $", {best}")}"
                : $"{type} ấm cúng ở {city}",

            phrases.Count >= 2
                ? $"{type} {city} — {phrases[0]} và {phrases[1]}"
                : $"{type} {city} cho {Math.Max(1, f.MaxGuests)} khách"
        };

        return candidates
            .Select(Tidy)
            .Where(t => t.Length > 0)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// docs/01 CN-08 — a first draft of the description: what the place is, who
    /// it fits, and what they get. The host is expected to rewrite it; having
    /// something on the page is what gets them past the blank box.
    /// </summary>
    public static string Description(Facts f)
    {
        var type = TypeWord(f.Type).ToLowerInvariant();
        var city = (f.City ?? "").Trim();
        var phrases = Phrases(f);

        var opening = f.Bedrooms > 0
            ? $"{char.ToUpperInvariant(type[0])}{type[1..]} {f.Bedrooms} phòng ngủ tại {city}, ở được {Math.Max(1, f.MaxGuests)} khách."
            : $"{char.ToUpperInvariant(type[0])}{type[1..]} tại {city}, ở được {Math.Max(1, f.MaxGuests)} khách.";

        var middle = phrases.Count switch
        {
            0 => "Không gian gọn gàng, đầy đủ tiện nghi cơ bản cho một kỳ nghỉ thoải mái.",
            1 => $"Điểm bạn sẽ thích nhất là {phrases[0]}.",
            _ => $"Chỗ nghỉ có {string.Join(", ", phrases.Take(3))} — đủ để bạn không phải ra ngoài nếu không muốn."
        };

        var closing = f.Room == RoomType.EntirePlace
            ? "Bạn dùng trọn chỗ nghỉ, không chia sẻ với ai."
            : "Khu vực chung được chia sẻ, phòng của bạn có khoá riêng.";

        return $"{opening} {middle} {closing}";
    }

    /// <summary>
    /// What the editor should say about a title as it is typed: how much room
    /// is left, and whether it is short enough to be worth nothing.
    /// </summary>
    public static string? TitleWarning(string? title)
    {
        var text = (title ?? "").Trim();
        if (text.Length == 0) return null;
        if (text.Length < 8) return "Tiêu đề cần ít nhất 8 ký tự.";
        if (text.Length > TitleMax) return $"Tiêu đề quá {text.Length - TitleMax} ký tự.";
        return null;
    }

    private static string Tidy(string raw)
    {
        var text = string.Join(' ', (raw ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (text.Length <= TitleMax) return text;

        // Cut on a word so a suggestion never ends mid-syllable.
        var cut = text[..TitleMax];
        var space = cut.LastIndexOf(' ');
        return (space > 20 ? cut[..space] : cut).TrimEnd(' ', ',', '—', '-');
    }
}
