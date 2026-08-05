using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;
using StayHost.Web.Infrastructure;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

[ApiController]
[Route("api/favorites")]
public class FavoritesController(StayHostDbContext db, AuthService auth) : ControllerBase
{
    /// <summary>Signed-in wishlists follow the account; anonymous ones follow the cookie.</summary>
    private async Task<(int? UserId, string SessionId)> ScopeAsync(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        return (user?.Id, HttpContext.SessionId());
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ListingCardDto>>> List(CancellationToken ct)
    {
        var (userId, sid) = await ScopeAsync(ct);
        var listings = await db.Favorites
            .Where(f => userId != null ? f.UserId == userId : f.SessionId == sid && f.UserId == null)
            .OrderByDescending(f => f.CreatedAt)
            .Include(f => f.Listing!).ThenInclude(l => l.Images)
            .Include(f => f.Listing!).ThenInclude(l => l.Amenities).ThenInclude(la => la.Amenity)
            .Select(f => f.Listing!)
            .ToListAsync(ct);

        var ids = listings.Select(l => l.Id).ToHashSet();
        return Ok(listings.Select(l => CatalogService.ToCard(l, ids)).ToList());
    }

    [HttpPost("{listingId:int}")]
    public async Task<ActionResult<object>> Toggle(int listingId, CancellationToken ct)
    {
        if (!await db.Listings.AnyAsync(l => l.Id == listingId, ct)) return NotFound();

        var (userId, sid) = await ScopeAsync(ct);
        var existing = await FindAsync(userId, sid, listingId, ct);

        if (existing is null)
        {
            // A bare heart-tap lands in the default list; the UI can move it later.
            var list = await db.Wishlists
                .Where(w => userId != null ? w.UserId == userId : w.SessionId == sid && w.UserId == null)
                .OrderByDescending(w => w.IsDefault).ThenBy(w => w.Id)
                .FirstOrDefaultAsync(ct);

            if (list is null)
            {
                list = new Wishlist { Name = "Chỗ nghỉ đã lưu", SessionId = sid, UserId = userId, IsDefault = true };
                db.Wishlists.Add(list);
                await db.SaveChangesAsync(ct);
            }

            db.Favorites.Add(new Favorite
            {
                SessionId = sid, UserId = userId, ListingId = listingId, WishlistId = list.Id
            });
            await db.SaveChangesAsync(ct);
            return Ok(new { listingId, isFavorite = true, count = await CountAsync(userId, sid, ct), wishlistId = list.Id });
        }

        db.Favorites.Remove(existing);
        await db.SaveChangesAsync(ct);
        return Ok(new { listingId, isFavorite = false, count = await CountAsync(userId, sid, ct) });
    }

    [HttpDelete("{listingId:int}")]
    public async Task<IActionResult> Remove(int listingId, CancellationToken ct)
    {
        var (userId, sid) = await ScopeAsync(ct);
        var existing = await FindAsync(userId, sid, listingId, ct);
        if (existing is null) return NoContent();

        db.Favorites.Remove(existing);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private Task<Favorite?> FindAsync(int? userId, string sid, int listingId, CancellationToken ct) =>
        db.Favorites.FirstOrDefaultAsync(f => f.ListingId == listingId &&
            (userId != null ? f.UserId == userId : f.SessionId == sid && f.UserId == null), ct);

    private Task<int> CountAsync(int? userId, string sid, CancellationToken ct) =>
        db.Favorites.CountAsync(f => userId != null ? f.UserId == userId : f.SessionId == sid && f.UserId == null, ct);
}
