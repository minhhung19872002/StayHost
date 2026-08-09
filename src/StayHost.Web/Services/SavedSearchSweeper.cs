using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;

namespace StayHost.Web.Services;

/// <summary>
/// docs/01 TM-23 — tells people when a place they were waiting for shows up.
///
/// Each saved search remembers the newest listing it has already told its owner
/// about. Every sweep it asks for anything newer that still matches the filters,
/// and if there is, sends one notification and moves the high-water mark up — so
/// nobody is told twice about the same place, and a batch of new listings is one
/// message, not twenty.
/// </summary>
public class SavedSearchSweeper(
    StayHostDbContext db, CatalogService catalog, NotificationService notifications,
    ILogger<SavedSearchSweeper> log)
{
    public async Task<int> SweepAsync(CancellationToken ct)
    {
        var searches = await db.SavedSearches
            .Include(s => s.User)
            .Take(500)
            .ToListAsync(ct);

        var sent = 0;
        foreach (var s in searches)
        {
            var query = ToQuery(s);
            var matches = await catalog.MatchNewAsync(query, s.LastNotifiedListingId, 50, ct);
            s.LastCheckedAt = DateTime.UtcNow;
            if (matches.Count == 0) continue;

            s.LastNotifiedListingId = matches.Max(l => l.Id);

            var what = matches.Count == 1
                ? $"1 chỗ mới phù hợp với tìm kiếm \"{s.Label}\""
                : $"{matches.Count} chỗ mới phù hợp với tìm kiếm \"{s.Label}\"";

            await notifications.QueueWithEmailAsync(s.User, NotificationKind.SavedSearchMatch,
                "Có chỗ mới cho bạn", $"{what}. Xem ngay để không bỏ lỡ.", "/", ct);
            sent++;
        }

        if (sent > 0 || searches.Count > 0) await db.SaveChangesAsync(ct);
        if (sent > 0) log.LogInformation("Tìm kiếm đã lưu: {Count} thông báo.", sent);
        return sent;
    }

    private static CatalogService.SearchQuery ToQuery(SavedSearch s)
    {
        var amenities = (s.AmenitiesCsv ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var hostLangs = (s.HostLanguagesCsv ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new CatalogService.SearchQuery(
            s.Q, s.Category, s.MinPrice, s.MaxPrice, s.Guests, amenities, "reco", s.RoomType,
            s.Bedrooms, null, null, s.SuperhostOnly, false, s.InstantBookOnly, false,
            1, 50, HostLanguages: hostLangs);
    }
}
