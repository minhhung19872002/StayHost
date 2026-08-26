using System.Text;
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

        var page = await DescribeAsync(db, route, requestedPage, ct);
        var head = Head(page, origin, path);

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

    private static async Task<PageMeta> DescribeAsync(
        StayHostDbContext db, PageRoute route, int requestedPage, CancellationToken ct)
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
                return Default;
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
        b.Append($"<meta name=\"twitter:image\" content=\"{E(image)}\">");
        return b.ToString();
    }

    /// <summary>
    /// The four characters that would break out of an attribute, and nothing
    /// else. HtmlEncode also escapes every accented letter, so a Vietnamese
    /// title came out as a wall of &amp;#249; — valid, and understood by a real
    /// browser, but the page already declares UTF-8 and not every chat-app
    /// scraper decodes entities before showing the card.
    /// </summary>
    private static string E(string s) => s
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;");
}
