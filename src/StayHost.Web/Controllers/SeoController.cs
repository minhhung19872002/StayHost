using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

/// <summary>
/// robots.txt and sitemap.xml.
///
/// Both are served rather than shipped as static files, because both have to
/// name the platform's own address and that address is configuration, not a
/// constant — it moved once already, from staylio.bluestar.com.vn to staylio.vn,
/// and a sitemap full of the old host is worse than no sitemap.
///
/// The sitemap is built from the database on request. This is a booking site:
/// the pages worth ranking are the city pages and the individual places, and a
/// crawler has no way to discover a listing that is not linked from somewhere.
/// Without this, search results reach the home page and stop.
/// </summary>
[ApiController]
public class SeoController(StayHostDbContext db, SiteSettings site) : ControllerBase
{
    /// <summary>
    /// Sitemaps cap at 50,000 addresses per file. Far above anything here, but a
    /// silent truncation would look like healthy indexing while most of the
    /// catalogue quietly went missing, so the cap is explicit and logged in the
    /// document itself when it bites.
    /// </summary>
    private const int MaxUrls = 45_000;

    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    /// <summary>
    /// The configured public address, falling back to the address this request
    /// actually arrived on. Configuration is preferred because it is the one
    /// canonical answer; the request is the honest fallback for a deployment
    /// that has not been told its own name yet.
    /// </summary>
    private string BaseUrl =>
        site.PublicUrl.Length > 0
            ? site.PublicUrl.TrimEnd('/')
            : $"{Request.Scheme}://{Request.Host}";

    [HttpGet("/robots.txt")]
    [Produces("text/plain")]
    public ContentResult Robots() =>
        Content(Seo.RobotsTxt($"{BaseUrl}/sitemap.xml"), "text/plain; charset=utf-8");

    [HttpGet("/sitemap.xml")]
    public async Task<IActionResult> Sitemap(CancellationToken ct)
    {
        var root = BaseUrl;
        var urls = new List<XElement>();

        void Add(string path, DateTime? lastMod, string changeFreq, string priority)
        {
            // The private list is the authority, and it is checked here rather
            // than trusted to have been respected upstream. Several of those
            // routes carry a secret in the address; one of them landing in a
            // sitemap publishes it, and no error would ever be raised.
            if (Seo.IsPrivate(path)) return;
            if (urls.Count >= MaxUrls) return;

            var url = new XElement(Ns + "url", new XElement(Ns + "loc", root + path));
            if (lastMod is { } when)
                url.Add(new XElement(Ns + "lastmod", when.ToUniversalTime().ToString("yyyy-MM-dd")));
            url.Add(new XElement(Ns + "changefreq", changeFreq));
            url.Add(new XElement(Ns + "priority", priority));
            urls.Add(url);
        }

        // --- The pages somebody actually searches for -----------------------

        Add("/", null, "daily", "1.0");

        // docs/01 AT-01 — a city page is the landing page for "khách sạn Đà Lạt"
        // and the like, so it ranks above an individual place: one page standing
        // for many, and the query that brings a guest in is usually the city.
        // Only cities that have something to show; an empty one returns 404 from
        // CitiesController and would be a broken promise in the sitemap.
        var published = await db.Listings
            .Where(l => l.IsPublished && l.ReviewStatus == ListingReviewStatus.Approved)
            .Select(l => new { l.Slug, l.City, l.UpdatedAt })
            .ToListAsync(ct);

        var cities = published
            .GroupBy(l => Cities.Key(l.City))
            .Where(g => g.Key.Length > 0)
            .Select(g => new
            {
                Slug = g.Key.Replace(' ', '-'),
                Newest = g.Max(l => l.UpdatedAt),
                Pages = Seo.TotalPages(g.Count()),
            })
            .OrderBy(c => c.Slug);

        foreach (var c in cities)
        {
            // Every page of the series, not only the first. Page 2 onwards is
            // where the places past the first screen live, and leaving them out
            // would put those listings back to being reachable by nothing.
            for (var page = 1; page <= c.Pages; page++)
                Add(Seo.CityPagePath(Uri.EscapeDataString(c.Slug), page),
                    c.Newest, "daily", page == 1 ? "0.9" : "0.6");
        }

        foreach (var l in published.OrderBy(l => l.Slug))
            Add($"/rooms/{Uri.EscapeDataString(l.Slug)}", l.UpdatedAt, "weekly", "0.8");

        // --- docs/09, the other two lines of business -----------------------

        Add("/experiences", null, "daily", "0.7");
        var experiences = await db.Experiences
            .Where(x => x.IsPublished)
            .Select(x => new { x.Slug, x.CreatedAt })
            .ToListAsync(ct);
        foreach (var x in experiences.OrderBy(x => x.Slug))
            Add($"/experiences/{Uri.EscapeDataString(x.Slug)}", x.CreatedAt, "weekly", "0.7");

        Add("/services", null, "daily", "0.7");
        var services = await db.ServiceOfferings
            .Where(o => o.IsPublished)
            .Select(o => new { o.Slug, o.CreatedAt })
            .ToListAsync(ct);
        foreach (var o in services.OrderBy(o => o.Slug))
            Add($"/services/{Uri.EscapeDataString(o.Slug)}", o.CreatedAt, "weekly", "0.7");

        // --- Pages that earn trust rather than bookings ---------------------

        Add("/host", null, "monthly", "0.6");
        Add("/help", null, "monthly", "0.4");

        var articles = await db.HelpArticles
            .Select(a => a.Slug)
            .ToListAsync(ct);
        foreach (var slug in articles.OrderBy(s => s))
            Add($"/help/{Uri.EscapeDataString(slug)}", null, "monthly", "0.3");

        // docs/06 §11 — the platform makes public claims about this programme,
        // so the page carrying them is worth being findable.
        Add("/shield/terms", null, "monthly", "0.3");

        var doc = new XDocument(new XElement(Ns + "urlset", urls));
        return Content(doc.Declaration + doc.ToString(), "application/xml; charset=utf-8");
    }
}
