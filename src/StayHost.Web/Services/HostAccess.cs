using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;

namespace StayHost.Web.Services;

/// <summary>
/// docs/01 QL-19 — answers "may this person do this to this listing?" for both
/// the owner and anyone they invited to help. Every host endpoint asks here
/// rather than comparing ids itself, so a scope added later is honoured
/// everywhere at once.
/// </summary>
public class HostAccess(StayHostDbContext db)
{
    /// <summary>The listing, if the current user may act on it within that scope.</summary>
    public async Task<Listing?> ListingAsync(User user, int listingId, CoHostScope scope, CancellationToken ct)
    {
        var listing = await db.Listings.FirstOrDefaultAsync(l => l.Id == listingId, ct);
        if (listing is null) return null;

        return await MayAsync(user, listing, scope, ct) ? listing : null;
    }

    public async Task<bool> MayAsync(User user, Listing listing, CoHostScope scope, CancellationToken ct)
    {
        var profile = await db.Hosts.FirstOrDefaultAsync(h => h.UserId == user.Id, ct);
        if (profile is not null && listing.HostId == profile.Id) return true;

        var owner = await db.Hosts
            .Where(h => h.Id == listing.HostId)
            .Select(h => h.UserId)
            .FirstOrDefaultAsync(ct);

        return await db.CoHosts.AnyAsync(c =>
            c.CoHostUserId == user.Id
            && c.OwnerUserId == owner
            && c.Status == CoHostStatus.Active
            && (c.ListingId == null || c.ListingId == listing.Id)
            && (c.Scope & scope) == scope, ct);
    }

    /// <summary>Every listing the user may act on: their own plus the ones they help with.</summary>
    public async Task<List<int>> ListingIdsAsync(User user, CoHostScope scope, CancellationToken ct)
    {
        var ids = new List<int>();

        var profile = await db.Hosts.FirstOrDefaultAsync(h => h.UserId == user.Id, ct);
        if (profile is not null)
        {
            ids.AddRange(await db.Listings
                .Where(l => l.HostId == profile.Id)
                .Select(l => l.Id)
                .ToListAsync(ct));
        }

        var grants = await db.CoHosts
            .Where(c => c.CoHostUserId == user.Id && c.Status == CoHostStatus.Active && (c.Scope & scope) == scope)
            .Select(c => new { c.OwnerUserId, c.ListingId })
            .ToListAsync(ct);

        foreach (var grant in grants)
        {
            if (grant.ListingId is { } one) { ids.Add(one); continue; }

            // A grant with no listing follows the owner, so listings they add
            // later are covered without re-inviting anyone.
            ids.AddRange(await db.Listings
                .Where(l => l.Host!.UserId == grant.OwnerUserId)
                .Select(l => l.Id)
                .ToListAsync(ct));
        }

        return ids.Distinct().ToList();
    }
}
