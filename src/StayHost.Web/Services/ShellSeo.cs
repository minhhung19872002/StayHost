using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using StayHost.Domain;
using StayHost.Infrastructure;

namespace StayHost.Web.Services;

/// <summary>
/// The head of index.html, written for the address being served.
///
/// The app already sets title, description and og:* on every navigation — but it
/// does it in JavaScript, and the scrapers that matter most here do not run any.
/// Facebook, Zalo and Messenger fetch the HTML and read it as it arrives, which
/// means every room ever pasted into a chat showed the home page's words and, as
/// there was no og:image at all, a blank grey card. In Vietnam that chat paste is
/// most of a listing's traffic.
///
/// So the tags are written twice, and neither copy is redundant: this one is what
/// a scraper reads, and the JavaScript one keeps them right as somebody moves
/// around inside the app without the document ever reloading.
///
/// Google is the one reader that does run the JavaScript. It is also the slowest
/// to get round to it, so having the canonical and the title correct in the very
/// first response is worth having for it too.
/// </summary>
public static class ShellSeo
{
    private const string Open = "<!--seo:start-->";
    private const string Close = "<!--seo:end-->";

    /// <summary>Where the default share card lives, relative to the site root.</summary>
    public const string DefaultImage = "/og-default.png";

    /// <summary>The name Google prints above a result for this site.</summary>
    private const string SiteName = "Staylio";

    /// <summary>
    /// The id lib/seo.js gives its own JSON-LD block. Shared on purpose: when
    /// React runs it replaces this element instead of adding a second, competing
    /// description of the same page.
    /// </summary>
    private const string LdElementId = "seo-jsonld";

    /// <summary>
    /// Unicode left unescaped so Vietnamese stays readable in the page source,
    /// and because the document already declares UTF-8.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static string? _shell;
    private static long _shellStamp;

    /// <summary>
    /// index.html, cached until the file on disk changes. Rebuilt on a new
    /// timestamp rather than held forever, so a deploy that replaces the shell
    /// does not need the process restarted to be seen.
    /// </summary>
    private static async Task<string?> ShellAsync(IFileProvider files, CancellationToken ct)
    {
        var file = files.GetFileInfo("index.html");
        if (!file.Exists) return null;

        var stamp = file.LastModified.ToUnixTimeMilliseconds();
        if (_shell is not null && _shellStamp == stamp) return _shell;

        await using var stream = file.CreateReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var text = await reader.ReadToEndAsync(ct);

        _shell = text;
        _shellStamp = stamp;
        return text;
    }

    /// <summary>
    /// The shell with its meta block replaced for <paramref name="route"/>, or
    /// null when there is no shell to serve.
    /// </summary>
    public static async Task<string?> RenderAsync(
        IFileProvider files, StayHostDbContext db, PageRoute route,
        string origin, string path, int requestedPage, CancellationToken ct)
    {
        var shell = await ShellAsync(files, ct);
        if (shell is null) return null;

        var start = shell.IndexOf(Open, StringComparison.Ordinal);
        var end = shell.IndexOf(Close, StringComparison.Ordinal);

        // No markers means an older shell. Serving it unchanged is the right
        // failure: the page still works, it just carries the default card.
        if (start < 0 || end < start) return shell;

        var page = await DescribeAsync(db, route, path, requestedPage, ct);
        var head = Head(page, origin, path) + SiteJsonLd(origin, path);

        return shell[..start] + head + shell[(end + Close.Length)..];
    }

    /// <summary>
    /// Title, description and share picture for one address.
    ///
    /// <c>Suffix</c> is the query the canonical address keeps - only ever
    /// "?trang=N", and only for a page number the series really has. A city asked
    /// for at ?trang=99 shows page 1 and has to say page 1 is where it lives, or
    /// every number a crawler invents becomes another address claiming to be the
    /// original of the same twelve rooms.
    /// </summary>
    private sealed record PageMeta(string Title, string Description, string? Image, string Suffix = "");

    private static readonly PageMeta Default = new(
        "Đặt phòng khách sạn, thuê nhà & homestay khắp Việt Nam | Staylio",
        "Đặt phòng khách sạn, thuê nhà nguyên căn, căn hộ, villa và homestay khắp Việt Nam. "
            + "Xem giá trọn gói trước khi trả tiền, chính sách huỷ ghi rõ trên từng tin.",
        DefaultImage);

    /// <summary>
    /// The fixed pages that are public, listed in sitemap.xml and meant to rank.
    ///
    /// They all resolve to <see cref="PageKind.App"/> with no slug, so without this
    /// they fell to <see cref="Default"/> and were submitted to Google carrying the
    /// home page's title and description word for word — five addresses claiming to
    /// be the same page. /shield/terms was the sharpest case: robots.txt goes out of
    /// its way to Allow it because docs/06 §11 makes public promises there, and it
    /// was indexed under a title about booking hotels.
    ///
    /// Everything else under PageKind.App is noindex, so it keeps the default.
    /// </summary>
    private static readonly Dictionary<string, PageMeta> FixedPages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/experiences"] = new(
            "Trải nghiệm do người địa phương dẫn khắp Việt Nam | Staylio",
            "Lớp nấu ăn, chèo SUP, đi bộ nhiếp ảnh, tour cà phê — trải nghiệm do người địa "
                + "phương dẫn, đặt theo suất và xem giá trọn gói trước khi trả tiền.",
            DefaultImage),

        ["/services"] = new(
            "Đầu bếp, dọn dẹp, đưa đón — dịch vụ tới tận nơi | Staylio",
            "Đặt đầu bếp nấu tại nhà, massage tại phòng, đưa đón sân bay, giữ hành lý và đi "
                + "chợ hộ. Giá trọn gói, người cung cấp có hồ sơ và đánh giá thật.",
            DefaultImage),

        ["/host"] = new(
            "Cho thuê nhà trên Staylio — bắt đầu đón khách | Staylio",
            "Đăng tin miễn phí, tự đặt giá và lịch, phí chủ nhà 3%. Staylio Shield đứng sau "
                + "mỗi lượt đón khách, và tiền về tài khoản sau khi khách nhận phòng.",
            DefaultImage),

        ["/help"] = new(
            "Trung tâm trợ giúp | Staylio",
            "Câu trả lời cho đặt phòng, huỷ và hoàn tiền, thanh toán, đón tiếp khách và "
                + "Staylio Shield — cho cả khách lẫn chủ nhà.",
            DefaultImage),

        ["/shield/terms"] = new(
            "Staylio Shield — chính sách hỗ trợ khách và chủ nhà | Staylio",
            "Phạm vi, hạn mức và loại trừ của Staylio Shield: chỗ ở khác xa mô tả thì được "
                + "đổi chỗ hoặc hoàn tiền, và chủ nhà được hỗ trợ khi có sự cố.",
            DefaultImage),
    };

    private static async Task<PageMeta> DescribeAsync(
        StayHostDbContext db, PageRoute route, string path, int requestedPage, CancellationToken ct)
    {
        switch (route.Kind)
        {
            case PageKind.Listing:
            {
                var l = await db.Listings
                    .Where(x => x.Slug == route.Slug
                             && x.IsPublished
                             && x.ReviewStatus == ListingReviewStatus.Approved)
                    .Select(x => new
                    {
                        x.Title,
                        x.City,
                        x.Description,
                        Image = x.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
                    })
                    .FirstOrDefaultAsync(ct);

                if (l is null) return Default;
                return new(
                    $"{l.Title} — {l.City} | Staylio",
                    Trim(l.Description, $"Chỗ nghỉ tại {l.City} trên Staylio."),
                    l.Image);
            }

            case PageKind.City:
            {
                var wanted = Cities.Key(route.Slug.Replace('-', ' '));
                if (wanted.Length == 0) return Default;

                var here = await db.Listings
                    .Where(x => x.IsPublished && x.ReviewStatus == ListingReviewStatus.Approved)
                    .Select(x => new
                    {
                        x.City,
                        x.IsGuestFavorite,
                        x.Rating,
                        Image = x.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
                    })
                    .ToListAsync(ct);

                var rows = here.Where(x => Cities.Key(x.City) == wanted)
                               .OrderByDescending(x => x.IsGuestFavorite)
                               .ThenByDescending(x => x.Rating)
                               .ToList();
                if (rows.Count == 0) return Default;

                var name = rows[0].City;
                var pageNo = Seo.ClampPage(requestedPage < 1 ? 1 : requestedPage, rows.Count);
                var suffix = pageNo > 1 ? $"?trang={pageNo}" : "";
                var part = pageNo > 1 ? $" - trang {pageNo}/{Seo.TotalPages(rows.Count)}" : "";
                return new(
                    $"Khách sạn, nhà & homestay cho thuê tại {name}{part} | Staylio",
                    $"{rows.Count} chỗ nghỉ tại {name}: khách sạn, nhà nguyên căn, căn hộ, villa "
                        + "và homestay. Giá trọn gói, chính sách huỷ ghi rõ trên từng tin.",
                    rows[0].Image,
                    suffix);
            }

            case PageKind.Experience:
            {
                var x = await db.Experiences
                    .Where(e => e.Slug == route.Slug && e.IsPublished)
                    .Select(e => new
                    {
                        e.Title,
                        e.City,
                        e.Summary,
                        e.Description,
                        Image = e.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
                    })
                    .FirstOrDefaultAsync(ct);

                if (x is null) return Default;
                return new(
                    $"{x.Title} — {x.City} | Staylio",
                    Trim(x.Summary.Length > 0 ? x.Summary : x.Description,
                         $"Trải nghiệm tại {x.City} trên Staylio."),
                    x.Image);
            }

            case PageKind.Service:
            {
                var o = await db.ServiceOfferings
                    .Where(s => s.Slug == route.Slug && s.IsPublished)
                    .Select(s => new
                    {
                        s.Title,
                        s.City,
                        s.Summary,
                        s.Description,
                        Image = s.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
                    })
                    .FirstOrDefaultAsync(ct);

                if (o is null) return Default;
                return new(
                    $"{o.Title} — {o.City} | Staylio",
                    Trim(o.Summary.Length > 0 ? o.Summary : o.Description,
                         $"Dịch vụ tại {o.City} trên Staylio."),
                    o.Image);
            }

            case PageKind.HelpArticle:
            {
                var a = await db.HelpArticles
                    .Where(h => h.Slug == route.Slug)
                    .Select(h => new { h.Title, h.Summary, h.Body })
                    .FirstOrDefaultAsync(ct);

                if (a is null) return Default;
                return new(
                    $"{a.Title} | Trung tâm trợ giúp Staylio",
                    Trim(a.Summary.Length > 0 ? a.Summary : a.Body, Default.Description),
                    DefaultImage);
            }

            default:
            {
                // Trailing slash and case both reach here; the addresses in the
                // sitemap have neither, so normalise before looking one up.
                var key = (path ?? "").Split('?')[0].Split('#')[0].TrimEnd('/');
                if (key.Length == 0) key = "/";
                return FixedPages.TryGetValue(key, out var fixedPage) ? fixedPage : Default;
            }
        }
    }

    /// <summary>
    /// One line of prose out of whatever the host wrote, cut to the length a
    /// search result shows. Falls back rather than returning an empty tag: a
    /// description that is present but blank is worse than the generic one.
    /// </summary>
    private static string Trim(string? text, string fallback)
    {
        var flat = string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (flat.Length == 0) return fallback;
        return flat.Length <= 160 ? flat : flat[..157].TrimEnd() + "…";
    }

    private static string Head(PageMeta page, string origin, string path)
    {
        var url = origin + path + page.Suffix;
        var image = page.Image is { Length: > 0 } src
            ? (src.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? src
                : origin + (src.StartsWith('/') ? src : "/" + src))
            : origin + DefaultImage;

        // og:title carries no site name. The card already shows the domain
        // underneath, so repeating it eats the width the room's own name needs.
        var ogTitle = page.Title.Split('|')[0].Trim();

        var b = new StringBuilder();
        b.Append($"<title>{E(page.Title)}</title>\n");
        b.Append($"<meta name=\"description\" content=\"{E(page.Description)}\">\n");
        b.Append($"<link rel=\"canonical\" href=\"{E(url)}\">\n");
        b.Append($"<meta property=\"og:url\" content=\"{E(url)}\">\n");
        b.Append($"<meta property=\"og:title\" content=\"{E(ogTitle)}\">\n");
        b.Append($"<meta property=\"og:description\" content=\"{E(page.Description)}\">\n");
        b.Append($"<meta property=\"og:image\" content=\"{E(image)}\">\n");
        b.Append($"<meta property=\"og:image:alt\" content=\"{E(ogTitle)}\">\n");
        b.Append($"<meta name=\"twitter:image\" content=\"{E(image)}\">\n");

        // The site defaults, said out loud, because the tags above are no longer
        // constant. lib/seo.js used to read the shipped head once and call that
        // "the defaults" — true while index.html was served byte-identical to
        // every address, false the moment this class started substituting the
        // block. Landing on a room and then navigating anywhere without meta of
        // its own put that room's title, description and photo on the new page:
        // the reset written to stop a title following the visitor was what made
        // it follow them. Serving them keeps one authority — Default, here.
        b.Append("<script type=\"application/json\" id=\"seo-defaults\">");
        b.Append($"{{\"title\":{J(Default.Title)},\"description\":{J(Default.Description)},"
               + $"\"image\":{J(origin + DefaultImage)}}}");
        b.Append("</script>");
        return b.ToString();
    }

    /// <summary>
    /// A JSON string for a block inside &lt;script&gt;. Only "&lt;" needs the extra
    /// care: a literal "&lt;/script" in the data would end the element early.
    /// </summary>
    private static string J(string s) =>
        System.Text.Json.JsonSerializer.Serialize(s).Replace("<", "\\u003c");

    /// <summary>
    /// The four characters that would break out of an attribute, and nothing
    /// else. HtmlEncode also escapes every accented letter, so a Vietnamese
    /// title came out as a wall of &amp;#249; — valid, and understood by a real
    /// browser, but the page already declares UTF-8 and not every chat-app
    /// scraper decodes entities before showing the card.
    /// </summary>
    /// <summary>
    /// Who this site is, on the home page only.
    ///
    /// Google takes the name it prints above a result from the home page, and
    /// the strongest signal it accepts is a WebSite node in structured data —
    /// stronger than og:site_name or the title. lib/seo.js already emits this
    /// node, but only once React has run, and a crawler that indexed the page
    /// before rendering the JavaScript keeps whatever name it learned last time.
    /// That is how "StayHost OS:" stayed on the search result for a day after
    /// every visible trace of the old name was gone.
    ///
    /// Same element id as the client's block, so when React does run it replaces
    /// this one instead of leaving the page with two competing descriptions of
    /// itself — structured data that disagrees with itself is worse than none.
    ///
    /// SearchAction is a promise, not decoration: the address given has to be a
    /// real search page, which /?q= is.
    /// </summary>
    private static string SiteJsonLd(string origin, string path)
    {
        if (path != "/") return "";

        // Built as a serialised object rather than a hand-written literal: the
        // JSON here is full of braces, and a raw interpolated string needs them
        // doubled, which is exactly the kind of quoting that survives review and
        // then ships a document Google silently refuses to parse.
        var graph = new object[]
        {
            new Dictionary<string, object>
            {
                ["@type"] = "Organization",
                ["@id"] = $"{origin}/#org",
                ["name"] = SiteName,
                ["url"] = origin,
            },
            new Dictionary<string, object>
            {
                ["@type"] = "WebSite",
                ["@id"] = $"{origin}/#site",
                ["url"] = origin,
                ["name"] = SiteName,
                ["inLanguage"] = "vi-VN",
                ["publisher"] = new Dictionary<string, object> { ["@id"] = $"{origin}/#org" },
                ["potentialAction"] = new Dictionary<string, object>
                {
                    ["@type"] = "SearchAction",
                    ["target"] = new Dictionary<string, object>
                    {
                        ["@type"] = "EntryPoint",
                        // A promise, not decoration: this has to be a real search
                        // page on this site, which /?q= is.
                        ["urlTemplate"] = origin + "/?q={search_term_string}",
                    },
                    ["query-input"] = "required name=search_term_string",
                },
            },
        };

        var doc = JsonSerializer.Serialize(
            new Dictionary<string, object> { ["@context"] = "https://schema.org", ["@graph"] = graph },
            JsonOptions);

        return "\n<script type=\"application/ld+json\" id=\"" + LdElementId + "\">"
             + doc + "</script>\n";
    }

    private static string E(string s) => s
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;");
}
