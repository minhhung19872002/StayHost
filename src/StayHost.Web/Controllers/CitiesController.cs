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
    [HttpGet("{key}")]
    public async Task<ActionResult<CityPageDto>> Page(string key, CancellationToken ct)
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
        var cards = here.Take(12).Select(l => CatalogService.ToCard(l, [])).ToList();

        return Ok(new CityPageDto(name, Cities.Blurb(name), here.Count, cards));
    }
}
