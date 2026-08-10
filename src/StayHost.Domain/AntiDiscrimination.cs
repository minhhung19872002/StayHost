namespace StayHost.Domain;

/// <summary>
/// docs/01 AT-12 — watching the reasons hosts give for turning guests away.
///
/// This does not block a decline: a host has any number of legitimate reasons,
/// and second-guessing every one would be worse than the problem. What it does is
/// notice when a stated reason leans on a protected characteristic — origin,
/// religion, disability, family, gender — and flag that one decline for a human to
/// look at. Keyword matching is deliberately a tripwire, not a verdict; the
/// category it returns tells the reviewer where to look, nothing more.
/// </summary>
public static class AntiDiscrimination
{
    public enum Category { None, Origin, Religion, Disability, Family, Gender }

    private static readonly (Category Cat, string[] Terms)[] Signals =
    [
        (Category.Origin, ["dân tộc", "người bắc", "người nam", "người trung", "vùng miền",
            "ngoại quốc", "người nước ngoài", "da đen", "da màu", "chủng tộc", "sắc tộc"]),
        (Category.Religion, ["tôn giáo", "đạo hồi", "thiên chúa", "phật giáo", "công giáo", "theo đạo"]),
        (Category.Disability, ["khuyết tật", "tàn tật", "khiếm thị", "khiếm thính", "xe lăn", "tâm thần"]),
        (Category.Family, ["có con", "trẻ con", "trẻ em", "con nhỏ", "bà bầu", "mang thai", "single mom", "mẹ đơn thân"]),
        (Category.Gender, ["giới tính", "đồng tính", "les", "gay", "chuyển giới", "lgbt", "pê đê"])
    ];

    /// <summary>
    /// The protected characteristic a decline reason leans on, or <see cref="Category.None"/>.
    /// Case- and diacritic-insensitive so "dan toc" trips the same as "dân tộc".
    /// </summary>
    public static Category Screen(string? reason)
    {
        var norm = SearchText.Normalize(reason ?? "");
        if (norm.Length == 0) return Category.None;

        // Word-boundary match, not substring: "ngày" (→"ngay") must not trip "gay".
        // Normalize turns punctuation into spaces, so wrapping both sides in a space
        // matches whole words and whole phrases alike.
        var text = " " + string.Join(' ', norm.Split(' ', StringSplitOptions.RemoveEmptyEntries)) + " ";

        foreach (var (cat, terms) in Signals)
            foreach (var term in terms)
                if (text.Contains(" " + SearchText.Normalize(term) + " "))
                    return cat;

        return Category.None;
    }

    public static bool IsFlagged(string? reason) => Screen(reason) != Category.None;

    public static string CategoryLabel(Category cat) => cat switch
    {
        Category.Origin => "Nguồn gốc / vùng miền / chủng tộc",
        Category.Religion => "Tôn giáo",
        Category.Disability => "Khuyết tật",
        Category.Family => "Gia đình / trẻ em / thai sản",
        Category.Gender => "Giới tính / xu hướng tính dục",
        _ => "Không"
    };
}
