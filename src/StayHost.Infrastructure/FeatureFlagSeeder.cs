using Microsoft.EntityFrameworkCore;
using StayHost.Domain;

namespace StayHost.Infrastructure;

/// <summary>
/// docs/01 QT-08 — a couple of flags to start the console off with, seeded one at
/// a time so adding one later does not disturb the ones an admin has already
/// tuned. Everything ships off, so nothing changes behaviour until an admin turns
/// it on.
/// </summary>
public static class FeatureFlagSeeder
{
    private record Seed(string Key, string Description);

    private static readonly Seed[] Flags =
    [
        new("new-map-search", "Trải nghiệm tìm kiếm trên bản đồ mới"),
        new("ai-trip-ideas", "Gợi ý lịch trình bằng AI trên trang chuyến đi")
    ];

    public static async Task SeedAsync(StayHostDbContext db, CancellationToken ct = default)
    {
        var existing = await db.FeatureFlags.Select(f => f.Key).ToListAsync(ct);
        var have = existing.ToHashSet();

        var added = false;
        foreach (var f in Flags.Where(f => !have.Contains(f.Key)))
        {
            db.FeatureFlags.Add(new FeatureFlag
            {
                Key = f.Key, Description = f.Description, Enabled = false, RolloutPercent = 0
            });
            added = true;
        }

        if (added) await db.SaveChangesAsync(ct);
    }
}
