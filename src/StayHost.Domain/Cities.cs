namespace StayHost.Domain;

/// <summary>
/// One place is one place, however somebody typed it.
///
/// The city on a listing is free text a host writes, and the catalogue groups by
/// that string exactly: the home page's city rails, the "chỗ nghỉ ở X" links and
/// the market-price comparison (docs/01 CN-10) all key off it. So a host who
/// wrote "Thành phố Hồ Chí Minh" where the catalogue says "TP. Hồ Chí Minh"
/// created a second city containing one listing — theirs — and their new place
/// appeared in none of the places they went looking for it.
/// </summary>
public static class Cities
{
    /// <summary>
    /// Prefixes people put in front of a city name that carry no meaning for
    /// matching. Longest first, so "thanh pho" is stripped before "tp".
    /// </summary>
    private static readonly string[] Prefixes =
    [
        "thanh pho ", "tinh ", "tp. ", "tp ", "t.p ", "t.p. "
    ];

    /// <summary>
    /// The comparison key: no diacritics, no administrative prefix, no
    /// punctuation, collapsed spaces. "TP. Hồ Chí Minh", "Thành phố Hồ Chí Minh"
    /// and "tphcm" do not all collapse to the same thing — the first two do, and
    /// that is the confusion worth fixing here. Abbreviations are a search
    /// problem, and <see cref="SearchText"/> already owns that one.
    /// </summary>
    public static string Key(string? name)
    {
        var text = SearchText.Normalize(name);
        if (text.Length == 0) return "";

        foreach (var prefix in Prefixes)
        {
            if (!text.StartsWith(prefix, StringComparison.Ordinal)) continue;
            text = text[prefix.Length..];
            break;
        }

        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    public static bool SameCity(string? a, string? b)
    {
        var left = Key(a);
        return left.Length > 0 && left == Key(b);
    }

    /// <summary>
    /// What a typed city should be stored as: the catalogue's own spelling when
    /// it already knows this place, otherwise what the host wrote, tidied.
    ///
    /// A city nobody has listed before is not an error — it is the next city the
    /// platform covers — so an unknown name is kept rather than refused.
    /// </summary>
    public static string Canonical(string? typed, IEnumerable<string> known)
    {
        var tidy = string.Join(' ', (typed ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (tidy.Length == 0) return "";

        var key = Key(tidy);
        if (key.Length == 0) return tidy;

        // Where the catalogue holds two spellings already, the one it holds most
        // of wins, so normalising does not pick the odd one out.
        var match = known
            .Where(c => Key(c) == key)
            .GroupBy(c => c)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key.Length)
            .Select(g => g.Key)
            .FirstOrDefault();

        return match ?? tidy;
    }

    /// <summary>
    /// docs/01 TM-26 — a short intro line for a city landing page. Known cities get
    /// their own; anywhere else gets a sentence that still reads naturally, so a
    /// new city the platform covers has a real page rather than a blank one.
    /// </summary>
    public static string Blurb(string? city)
    {
        var name = string.Join(' ', (city ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return Key(name) switch
        {
            "da lat" => "Đà Lạt se lạnh quanh năm, thông reo và cà phê sáng sớm — chọn một chỗ nghỉ ấm cúng giữa lòng thành phố sương.",
            "da nang" => "Đà Nẵng có biển Mỹ Khê, cầu Rồng và đồ ăn ngon — từ căn hộ view biển tới villa gần Ngũ Hành Sơn.",
            "hoi an" => "Phố cổ Hội An lung linh đèn lồng, đạp xe ra biển An Bàng — ở homestay trong vườn hoặc nhà cổ ven sông Hoài.",
            "ha noi" => "Hà Nội nghìn năm với phố cổ, hồ Gươm và cà phê trứng — chọn căn hộ trung tâm để đi bộ khám phá.",
            "ho chi minh" => "TP. Hồ Chí Minh không ngủ, sôi động cả ngày lẫn đêm — căn hộ cao cấp Quận 1 hay chỗ yên tĩnh ven kênh.",
            "nha trang" => "Nha Trang biển xanh cát trắng, hải sản tươi và đảo gần bờ — nhiều căn hộ nhìn thẳng ra vịnh.",
            "phu quoc" => "Phú Quốc đảo ngọc với hoàng hôn Bãi Trường và chợ đêm — villa có bể bơi hoặc bungalow sát biển.",
            "" => "Khám phá những chỗ nghỉ được yêu thích trên khắp Việt Nam.",
            _ => $"Khám phá những chỗ nghỉ được yêu thích ở {name} trên Staylio."
        };
    }
}
