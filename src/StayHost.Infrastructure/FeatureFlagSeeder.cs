using Microsoft.EntityFrameworkCore;
using StayHost.Domain;

namespace StayHost.Infrastructure;

/// <summary>
/// docs/01 QT-08 — the flags the console can dial, seeded one at a time so
/// adding one later does not disturb the ones an admin has already tuned.
///
/// Every key here gates something that actually exists and that the client
/// actually reads (`/api/features`). The first two seeds did not: they named a
/// new map search and AI trip ideas, neither of which was ever built, so the
/// console offered two switches wired to nothing and the rollout maths in
/// <see cref="FeatureRollout"/> had no caller at all. They are removed below
/// rather than left in place, because a toggle that does nothing is worse than
/// no toggle: somebody eventually turns it and believes something happened.
///
/// These ship <b>on</b>, unlike the originals. They gate parts of the product
/// that are already live, so shipping them off would take a working feature away
/// from everybody the first time this runs.
/// </summary>
public static class FeatureFlagSeeder
{
    private record Seed(string Key, string Description);

    private static readonly Seed[] Flags =
    [
        new("price-match", "Cam kết giá tốt: khách gửi yêu cầu bù chênh lệch (docs/01 MR-10)"),
        new("trip-plans", "Lịch trình chuyến đi gộp nhiều đơn (docs/01 CĐ-10, CĐ-11)")
    ];

    /// <summary>Keys that once existed and gated nothing. Cleared on startup.</summary>
    private static readonly string[] Retired = ["new-map-search", "ai-trip-ideas"];

    public static async Task SeedAsync(StayHostDbContext db, CancellationToken ct = default)
    {
        var existing = await db.FeatureFlags.Select(f => f.Key).ToListAsync(ct);
        var have = existing.ToHashSet();

        var changed = false;
        foreach (var f in Flags.Where(f => !have.Contains(f.Key)))
        {
            db.FeatureFlags.Add(new FeatureFlag
            {
                Key = f.Key, Description = f.Description, Enabled = true, RolloutPercent = 100
            });
            changed = true;
        }

        var dead = await db.FeatureFlags.Where(f => Retired.Contains(f.Key)).ToListAsync(ct);
        if (dead.Count > 0)
        {
            db.FeatureFlags.RemoveRange(dead);
            changed = true;
        }

        if (changed) await db.SaveChangesAsync(ct);
    }
}
