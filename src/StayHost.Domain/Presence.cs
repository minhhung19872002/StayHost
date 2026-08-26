namespace StayHost.Domain;

/// <summary>
/// Who is on the site right now.
///
/// "Right now" is not a thing the web can tell you. A browser does not say when
/// it leaves — it simply stops asking for anything — so the only honest answer
/// is "how many distinct visitors asked for something in the last few minutes",
/// and the window is the whole definition. Five minutes is the number the
/// analytics products settled on, and it is a fair trade: short enough that
/// somebody who closed the tab drops off quickly, long enough that a person
/// reading one long listing page is not counted as gone.
///
/// The identity being counted is the <c>sh_sid</c> cookie every visitor gets,
/// signed in or not. That matters: most of a booking site's traffic has no
/// account, and a number that counted only signed-in people would read as an
/// empty site on the busiest afternoon.
/// </summary>
public static class Presence
{
    /// <summary>How long after somebody's last request they still count as here.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    /// <summary>
    /// When a quiet visitor is dropped from memory altogether. Longer than
    /// <see cref="Window"/> on purpose: a returning reader keeps their identity
    /// (and their signed-in status) rather than arriving as a stranger, and the
    /// gap gives the tracker something to prune instead of growing forever.
    /// </summary>
    public static readonly TimeSpan Forget = TimeSpan.FromMinutes(30);

    /// <summary>True when a visitor last seen at <paramref name="lastSeen"/> still counts.</summary>
    public static bool StillHere(DateTime lastSeen, DateTime now) =>
        now - lastSeen < Window;

    /// <summary>True when a visitor is quiet enough to forget entirely.</summary>
    public static bool Stale(DateTime lastSeen, DateTime now) =>
        now - lastSeen >= Forget;

    /// <summary>
    /// Addresses that are traffic but not people.
    ///
    /// The payment IPNs are the ones that would really mislead: VNPay, MoMo and
    /// ZaloPay call in server-to-server on every settled order, and counting
    /// those would make the site look busiest at exactly the moment it is
    /// quietest. The health check is the container asking itself.
    /// </summary>
    public static bool IsMachineAddress(string? path)
    {
        var p = (path ?? "").TrimEnd('/');
        if (p.Length == 0) return false;

        return p.Equals("/health", StringComparison.OrdinalIgnoreCase)
            || p.Equals("/robots.txt", StringComparison.OrdinalIgnoreCase)
            || p.Equals("/sitemap.xml", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith("/ipn", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Agents that announce themselves as programs.
    ///
    /// Not a security check and not meant to be exhaustive — anything that wants
    /// to look like a browser can. It catches the honest ones, which is most of
    /// the volume: search crawlers, the chat-app scrapers that fetch a page the
    /// moment somebody pastes a link, and uptime monitors.
    /// </summary>
    public static bool LooksLikeRobot(string? userAgent)
    {
        var ua = (userAgent ?? "").ToLowerInvariant();
        if (ua.Length == 0) return true;   // a browser always sends one

        string[] tells =
        [
            "bot", "crawl", "spider", "slurp", "archiver", "headless",
            "curl/", "wget", "python-requests", "httpclient", "okhttp", "go-http",
            "postman", "insomnia", "uptime", "pingdom", "monitor",
            "facebookexternalhit", "zalo", "skypeuripreview", "whatsapp",
            "telegrambot", "twitterbot", "linkedinbot", "embedly", "preview",
        ];

        return tells.Any(t => ua.Contains(t, StringComparison.Ordinal));
    }

    /// <summary>
    /// Whether one request should move a visitor's "last seen".
    ///
    /// <paramref name="broughtCookie"/> is the strongest filter of the three and
    /// the least obvious. A crawler does not keep cookies, so every one of its
    /// requests arrives without <c>sh_sid</c> and is handed a brand-new one —
    /// counting those would report a fleet of visitors that is really one bot.
    /// Requiring the cookie to have come *back* means only something that stores
    /// and returns it is counted. The cost is that a real person's very first
    /// request is not counted; the page they just loaded calls the API a moment
    /// later, and that one is.
    /// </summary>
    public static bool CountsAsVisit(string? path, string? userAgent, bool broughtCookie) =>
        broughtCookie && !IsMachineAddress(path) && !LooksLikeRobot(userAgent);

    /// <summary>One visitor, as far as this count is concerned.</summary>
    public readonly record struct Visitor(DateTime LastSeen, int? UserId);

    /// <summary>
    /// How many are here, split by whether they have an account open.
    ///
    /// The split is what makes the number usable: fifty guests and two signed-in
    /// people is a different afternoon from fifty signed-in people, and an
    /// operator watching one total cannot tell them apart.
    /// </summary>
    public readonly record struct Tally(int Total, int SignedIn, int Guests);

    public static Tally Count(IEnumerable<Visitor> visitors, DateTime now)
    {
        var total = 0;
        var signedIn = 0;

        foreach (var v in visitors)
        {
            if (!StillHere(v.LastSeen, now)) continue;
            total++;
            if (v.UserId is not null) signedIn++;
        }

        return new(total, signedIn, total - signedIn);
    }
}
