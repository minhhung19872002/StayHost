using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;

namespace StayHost.Web.Services;

/// <summary>
/// Whether the address a crawler asked for names something that exists.
///
/// A single-page app answers every address with the same shell, so until this
/// ran, /rooms/khong-co-that-999 came back 200 carrying the home page's title
/// and an empty body. Google calls that a soft 404: it either indexes the blank
/// page or, once it has seen enough of them, starts distrusting the addresses it
/// has not crawled yet. Neither shows up anywhere on this side — the page looks
/// fine to a person, who simply sees "không tìm thấy" and goes back.
///
/// The check is the same one the API would make, run early enough to put the
/// right number on the response line.
/// </summary>
public static class PageExistence
{
    /// <summary>
    /// True when <paramref name="route"/> should be answered 200.
    ///
    /// Anything not resolvable to a route is false, and so is a listing that
    /// exists but is unpublished or still in review — a draft that answers 200
    /// is a page a crawler can find, and the whole point of review is that it
    /// cannot yet.
    /// </summary>
    public static async Task<bool> ExistsAsync(
        StayHostDbContext db, PageRoute route, CancellationToken ct)
    {
        switch (route.Kind)
        {
            case PageKind.Unknown:
                return false;

            case PageKind.App:
                return true;

            case PageKind.Listing:
                return await db.Listings.AnyAsync(
                    l => l.Slug == route.Slug
                      && l.IsPublished
                      && l.ReviewStatus == ListingReviewStatus.Approved, ct);

            case PageKind.Experience:
                return await db.Experiences.AnyAsync(
                    x => x.Slug == route.Slug && x.IsPublished, ct);

            case PageKind.Service:
                return await db.ServiceOfferings.AnyAsync(
                    o => o.Slug == route.Slug && o.IsPublished, ct);

            case PageKind.HelpArticle:
                return await db.HelpArticles.AnyAsync(a => a.Slug == route.Slug, ct);

            case PageKind.City:
                // The same normalisation CitiesController uses: the slug carries
                // hyphens, the key carries spaces, and Cities.Key also strips the
                // "TP." kind of prefix. Written again here it would drift, so the
                // separators are lined up and the one function does the rest.
                var wanted = Cities.Key(route.Slug.Replace('-', ' '));
                if (wanted.Length == 0) return false;

                var cities = await db.Listings
                    .Where(l => l.IsPublished && l.ReviewStatus == ListingReviewStatus.Approved)
                    .Select(l => l.City)
                    .Distinct()
                    .ToListAsync(ct);

                return cities.Any(c => Cities.Key(c) == wanted);

            default:
                return false;
        }
    }
}
