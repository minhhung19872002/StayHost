namespace StayHost.Domain;

/// <summary>
/// What a search engine is allowed to see.
///
/// The interesting half of this is not what to index — it is what must never be
/// indexed. Several routes carry a secret in the address itself: a bill-split
/// invitation, a shared wishlist, an appeal form, a bank-transfer reference. A
/// crawler that reaches one of those has not found a page, it has found somebody
/// else's link, and once it is in an index it is public forever. So the private
/// list is the one that gets a test, and the sitemap is checked against it rather
/// than trusted.
///
/// Second rule, less obvious and more damaging when broken: a single-page app
/// serves the same HTML for every address. Putting one fixed <c>canonical</c> in
/// that HTML tells Google every listing is a duplicate of the home page, and the
/// listings drop out of the index entirely. Canonical has to be per-address or
/// absent — never a constant.
/// </summary>
public static class Seo
{
    /// <summary>
    /// Address prefixes a crawler must stay out of. Grouped by why, because the
    /// reasons are not the same and a later reader will want to know which ones
    /// are merely useless to index and which ones leak something.
    /// </summary>
    public static readonly string[] Disallow =
    [
        // Carries a secret in the address. Indexing one of these publishes it.
        "/split/",
        "/wishlist/",
        "/appeal",
        "/chuyen-khoan/",

        // Somebody's own account. Nothing here belongs in a public index, and
        // /users/ in particular is a real person's profile.
        "/trips",
        "/hosting",
        "/messages",
        "/wishlists",
        "/wallet",
        "/resolutions",
        "/account",
        "/neighbors",
        "/friends",
        "/trip-plans",
        "/users/",
        "/admin",
        "/shield",

        // Mid-purchase. A crawler walking a checkout is at best noise, and these
        // pages are meaningless without the session that opened them.
        "/*/thanh-toan",
        "/thanh-toan/",
        "/experiences/bookings",
        "/services/bookings",

        // Data, not pages.
        "/api/",
    ];

    /// <summary>
    /// Narrower rules that win over the blanket ones above. Google applies the
    /// most specific match, so <c>/shield</c> can be closed while the terms page
    /// underneath it stays open — and that page is worth having indexed, since
    /// docs/06 §11 makes claims about it the platform has to stand behind.
    /// </summary>
    public static readonly string[] Allow =
    [
        "/shield/terms",
    ];

    /// <summary>
    /// True when <paramref name="path"/> falls under a Disallow rule and no
    /// narrower Allow rule rescues it. Used to keep the sitemap honest: anything
    /// this returns true for must never appear in it.
    /// </summary>
    public static bool IsPrivate(string? path)
    {
        var p = (path ?? "").Trim();
        if (p.Length == 0) return false;

        var blocked = Disallow.Where(rule => Matches(rule, p)).ToList();
        if (blocked.Count == 0) return false;

        // Longest rule wins, the way Google resolves a conflict.
        var longestBlock = blocked.Max(r => r.Length);
        var longestAllow = Allow.Where(rule => Matches(rule, p))
                                .Select(r => r.Length)
                                .DefaultIfEmpty(0)
                                .Max();

        return longestAllow < longestBlock;
    }

    /// <summary>
    /// robots.txt prefix matching, with the one wildcard Google supports. A rule
    /// matches when the path starts with it; <c>*</c> stands for any run of
    /// characters, which is how "/*/thanh-toan" covers a checkout under any slug.
    /// </summary>
    private static bool Matches(string rule, string path)
    {
        if (!rule.Contains('*')) return path.StartsWith(rule, StringComparison.OrdinalIgnoreCase);

        var parts = rule.Split('*');
        var at = 0;
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0) continue;

            if (i == 0)
            {
                if (!path.StartsWith(parts[i], StringComparison.OrdinalIgnoreCase)) return false;
                at = parts[i].Length;
                continue;
            }

            var found = path.IndexOf(parts[i], at, StringComparison.OrdinalIgnoreCase);
            if (found < 0) return false;
            at = found + parts[i].Length;
        }
        return true;
    }

    /// <summary>
    /// The body of robots.txt. <paramref name="sitemapUrl"/> is left out entirely
    /// when there is none rather than written as a relative address — the Sitemap
    /// directive is defined as absolute, and a relative one is silently ignored,
    /// which looks exactly like having no sitemap at all.
    /// </summary>
    public static string RobotsTxt(string? sitemapUrl)
    {
        var lines = new List<string> { "User-agent: *" };
        lines.AddRange(Allow.Select(a => $"Allow: {a}"));
        lines.AddRange(Disallow.Select(d => $"Disallow: {d}"));

        if (!string.IsNullOrWhiteSpace(sitemapUrl))
        {
            lines.Add("");
            lines.Add($"Sitemap: {sitemapUrl}");
        }

        return string.Join("\n", lines) + "\n";
    }
}
