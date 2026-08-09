using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;
using StayHost.Web.Infrastructure;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

/// <summary>
/// Named wishlists. Every saver gets a default list so a plain heart-tap still
/// works; anything beyond that is opt-in, exactly like Airbnb's "Save to…" sheet.
/// </summary>
[ApiController]
[Route("api/wishlists")]
public class WishlistsController(StayHostDbContext db, AuthService auth) : ControllerBase
{
    private async Task<(int? UserId, string SessionId)> ScopeAsync(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        return (user?.Id, HttpContext.SessionId());
    }

    private IQueryable<Wishlist> Owned(int? userId, string sid) =>
        db.Wishlists.Where(w => userId != null ? w.UserId == userId : w.SessionId == sid && w.UserId == null);

    private async Task<Wishlist> DefaultListAsync(int? userId, string sid, CancellationToken ct)
    {
        var existing = await Owned(userId, sid).OrderBy(w => w.Id).FirstOrDefaultAsync(w => w.IsDefault, ct)
                       ?? await Owned(userId, sid).OrderBy(w => w.Id).FirstOrDefaultAsync(ct);
        if (existing is not null) return existing;

        var created = new Wishlist { Name = "Chỗ nghỉ đã lưu", SessionId = sid, UserId = userId, IsDefault = true };
        db.Wishlists.Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WishlistDto>>> List(CancellationToken ct)
    {
        var (userId, sid) = await ScopeAsync(ct);

        var lists = await Owned(userId, sid)
            .Include(w => w.Items).ThenInclude(f => f.Listing!).ThenInclude(l => l.Images)
            .OrderByDescending(w => w.IsDefault).ThenBy(w => w.Name)
            .AsSplitQuery()
            .ToListAsync(ct);

        // Ensure at least the default list exists so the page is never blank.
        if (lists.Count == 0)
        {
            await DefaultListAsync(userId, sid, ct);
            lists = await Owned(userId, sid).Include(w => w.Items).ToListAsync(ct);
        }

        return Ok(lists.Select(ToSummary).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<WishlistDetailDto>> Detail(int id, CancellationToken ct)
    {
        var (userId, sid) = await ScopeAsync(ct);

        var list = await Owned(userId, sid)
            .Include(w => w.Items).ThenInclude(f => f.Listing!).ThenInclude(l => l.Images)
            .Include(w => w.Items).ThenInclude(f => f.Listing!).ThenInclude(l => l.Amenities).ThenInclude(a => a.Amenity)
            .AsSplitQuery()
            .FirstOrDefaultAsync(w => w.Id == id, ct);

        if (list is null) return NotFound();

        var favIds = list.Items.Select(i => i.ListingId).ToHashSet();
        var entries = list.Items
            .Where(i => i.Listing is not null)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new WishlistEntryDto(CatalogService.ToCard(i.Listing!, favIds), i.Note))
            .ToList();

        return Ok(new WishlistDetailDto(ToSummary(list), entries));
    }

    /// <summary>
    /// docs/01 YT-05 — turn a shareable link on or off. On mints a token if there
    /// is not one; off drops it, so an old link stops resolving.
    /// </summary>
    [HttpPost("{id:int}/share")]
    public async Task<ActionResult<object>> Share(int id, [FromQuery] bool on, CancellationToken ct)
    {
        var (userId, sid) = await ScopeAsync(ct);
        var list = await Owned(userId, sid).FirstOrDefaultAsync(w => w.Id == id, ct);
        if (list is null) return NotFound();

        if (on) list.ShareToken ??= Guid.NewGuid().ToString("N");
        else list.ShareToken = null;

        await db.SaveChangesAsync(ct);
        return Ok(new { shareToken = list.ShareToken });
    }

    /// <summary>
    /// docs/01 YT-05 — a shared list, read-only, for anyone with the link. No
    /// sign-in: the token is the permission. Notes stay private to the owner and
    /// are not returned here.
    /// </summary>
    [HttpGet("/api/shared-wishlists/{token}")]
    public async Task<ActionResult<WishlistDetailDto>> Shared(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return NotFound();

        var list = await db.Wishlists
            .Include(w => w.Items).ThenInclude(f => f.Listing!).ThenInclude(l => l.Images)
            .Include(w => w.Items).ThenInclude(f => f.Listing!).ThenInclude(l => l.Amenities).ThenInclude(a => a.Amenity)
            .AsSplitQuery()
            .FirstOrDefaultAsync(w => w.ShareToken == token, ct);

        if (list is null) return NotFound();

        var favIds = list.Items.Select(i => i.ListingId).ToHashSet();
        var entries = list.Items
            .Where(i => i.Listing is not null)
            .OrderByDescending(i => i.CreatedAt)
            // The viewer is not the owner, so the private note is left out.
            .Select(i => new WishlistEntryDto(CatalogService.ToCard(i.Listing!, favIds), null))
            .ToList();

        return Ok(new WishlistDetailDto(ToSummary(list), entries));
    }

    /// <summary>docs/01 YT-03 — the guest's private note on one saved place.</summary>
    [HttpPut("{id:int}/items/{listingId:int}/note")]
    public async Task<IActionResult> SetNote(
        int id, int listingId, [FromBody] SaveWishlistNoteRequest req, CancellationToken ct)
    {
        var (userId, sid) = await ScopeAsync(ct);

        var item = await Owned(userId, sid)
            .Where(w => w.Id == id)
            .SelectMany(w => w.Items)
            .FirstOrDefaultAsync(f => f.ListingId == listingId, ct);

        if (item is null) return NotFound();

        var note = (req.Note ?? "").Trim();
        item.Note = note.Length == 0 ? null : note.Length > 500 ? note[..500] : note;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult<WishlistDto>> Create([FromBody] SaveWishlistRequest req, CancellationToken ct)
    {
        var name = (req.Name ?? "").Trim();
        if (name.Length is < 1 or > 80) return BadRequest(new { message = "Tên danh sách từ 1 đến 80 ký tự." });

        var (userId, sid) = await ScopeAsync(ct);
        var isFirst = !await Owned(userId, sid).AnyAsync(ct);

        var list = new Wishlist { Name = name, SessionId = sid, UserId = userId, IsDefault = isFirst };
        db.Wishlists.Add(list);
        await db.SaveChangesAsync(ct);

        return Ok(ToSummary(list));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<WishlistDto>> Rename(int id, [FromBody] SaveWishlistRequest req, CancellationToken ct)
    {
        var name = (req.Name ?? "").Trim();
        if (name.Length is < 1 or > 80) return BadRequest(new { message = "Tên danh sách từ 1 đến 80 ký tự." });

        var (userId, sid) = await ScopeAsync(ct);
        var list = await Owned(userId, sid).FirstOrDefaultAsync(w => w.Id == id, ct);
        if (list is null) return NotFound();

        list.Name = name;
        await db.SaveChangesAsync(ct);
        return Ok(ToSummary(list));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var (userId, sid) = await ScopeAsync(ct);
        var list = await Owned(userId, sid).Include(w => w.Items).FirstOrDefaultAsync(w => w.Id == id, ct);
        if (list is null) return NoContent();

        var remaining = await Owned(userId, sid).CountAsync(ct);
        if (remaining <= 1) return BadRequest(new { message = "Cần giữ ít nhất một danh sách." });

        db.Wishlists.Remove(list);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Moves an already-saved listing into another list.</summary>
    [HttpPost("{id:int}/items/{listingId:int}")]
    public async Task<IActionResult> AddItem(int id, int listingId, CancellationToken ct)
    {
        var (userId, sid) = await ScopeAsync(ct);
        var list = await Owned(userId, sid).FirstOrDefaultAsync(w => w.Id == id, ct);
        if (list is null) return NotFound();
        if (!await db.Listings.AnyAsync(l => l.Id == listingId, ct)) return NotFound();

        var existing = await db.Favorites.FirstOrDefaultAsync(f => f.ListingId == listingId &&
            (userId != null ? f.UserId == userId : f.SessionId == sid && f.UserId == null), ct);

        if (existing is null)
        {
            db.Favorites.Add(new Favorite
            {
                ListingId = listingId, SessionId = sid, UserId = userId, WishlistId = list.Id
            });
        }
        else
        {
            existing.WishlistId = list.Id;
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static WishlistDto ToSummary(Wishlist w) => new(
        w.Id,
        w.Name,
        w.IsDefault,
        w.Items.Count,
        w.Items.Where(i => i.Listing is not null)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => i.Listing!.Images.OrderBy(im => im.SortOrder).Select(im => im.Url).FirstOrDefault() ?? "")
            .Where(url => url.Length > 0)
            .Take(4)
            .ToList(),
        w.ShareToken);
}
