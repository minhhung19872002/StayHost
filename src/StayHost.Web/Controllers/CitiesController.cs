using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

/// <summary>
/// docs/01 TM-26 — a landing page per city, so somebody arriving from a search
/// engine lands on real content rather than an empty search. Open to everyone.
/// </summary>
[ApiController]
[Route("api/cities")]
public class CitiesController(StayHostDbContext db) : ControllerBase
{
    /// <summary>
    /// One page of a city's places. <paramref name="page"/> is 1-based and out of
    /// range is clamped rather than refused: a crawler will ask for ?trang=99 from
    /// some stale link eventually, and answering with an empty page teaches it the
    /// site is full of thin content.
    /// </summary>
    [HttpGet("{key}")]
    public async Task<ActionResult<CityPageDto>> Page(
        string key, [FromQuery] int page, CancellationToken ct)
    {
        // The slug uses hyphens ("da-lat"); the city key uses spaces ("da lat"),
        // so the separators are lined up before matching.
        var wanted = Cities.Key(Uri.UnescapeDataString(key).Replace('-', ' '));
        if (wanted.Length == 0) return NotFound();

        // Published listings only — a landing page must not surface a hidden or
        // draft listing. Matched on the normalised city key in memory, because the
        // stored name carries diacritics the key strips; fine at this scale, and a
        // stored key column is the answer if a city ever holds thousands.
        var listings = await db.Listings
            // docs/01 AT-01 — a city page counts only places the public can see.
            .Where(l => l.IsPublished && l.ReviewStatus == ListingReviewStatus.Approved)
            .Include(l => l.Images)
            .Include(l => l.Host)
            .ToListAsync(ct);

        var here = listings
            .Where(l => Cities.Key(l.City) == wanted)
            .OrderByDescending(l => l.IsGuestFavorite)
            .ThenByDescending(l => l.Rating)
            .ToList();

        if (here.Count == 0) return NotFound();

        var name = here[0].City;

        // Paging is what keeps every place in a city reachable by following a
        // link. Without it the page shows the first twelve and the rest exist
        // only in the sitemap — which works, but is the weaker of the two paths,
        // and the day a city passes twelve nothing would say so.
        var total = here.Count;
        var pageNo = Seo.ClampPage(page <= 0 ? 1 : page, total);
        var cards = here
            .Skip(Seo.Skip(pageNo, total))
            .Take(Seo.CityPageSize)
            .Select(l => CatalogService.ToCard(l, []))
            .ToList();

        return Ok(new CityPageDto(
            name, Cities.Blurb(name), total, cards,
            pageNo, Seo.CityPageSize, Seo.TotalPages(total)));
    }
}
