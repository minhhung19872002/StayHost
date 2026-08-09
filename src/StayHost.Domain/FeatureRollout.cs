namespace StayHost.Domain;

/// <summary>
/// docs/01 QT-08 — a feature switched on for a share of users, not all at once.
///
/// A row per feature. <see cref="Enabled"/> is the master switch; with it off the
/// feature is off for everyone whatever the percentage says. With it on,
/// <see cref="RolloutPercent"/> decides how many people see it — 0 nobody, 100
/// everybody, and anything between picks a stable slice.
/// </summary>
public class FeatureFlag
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Enabled { get; set; }
    /// <summary>0–100. Ignored when <see cref="Enabled"/> is false.</summary>
    public int RolloutPercent { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>docs/01 QT-08 — the pure rule that decides who is in the rollout.</summary>
public static class FeatureRollout
{
    /// <summary>
    /// Which 0–99 bucket a person falls in for one feature. Deterministic and
    /// stable: the same person and feature always land in the same bucket, across
    /// processes and restarts, so nobody flips in and out as the percentage holds.
    /// The feature key is mixed in so a person is not in the same slice for every
    /// feature at once. Uses FNV-1a rather than string.GetHashCode, which is
    /// randomised per run and would break the stability this depends on.
    /// </summary>
    public static int Bucket(string featureKey, string stableKey)
    {
        var seed = $"{featureKey}:{stableKey}";
        const uint offset = 2166136261;
        const uint prime = 16777619;
        var hash = offset;
        foreach (var c in seed)
        {
            hash ^= c;
            hash *= prime;
        }
        return (int)(hash % 100);
    }

    /// <summary>
    /// Whether the feature is on for this person. Off outright when disabled or the
    /// percentage is zero; on for everyone at 100; otherwise on for the stable
    /// slice below the percentage.
    /// </summary>
    public static bool IsOn(bool enabled, int rolloutPercent, string featureKey, string? stableKey)
    {
        if (!enabled || rolloutPercent <= 0) return false;
        if (rolloutPercent >= 100) return true;
        if (string.IsNullOrEmpty(stableKey)) return false;   // nobody to bucket
        return Bucket(featureKey, stableKey) < rolloutPercent;
    }

    public static bool IsOn(FeatureFlag flag, string? stableKey) =>
        IsOn(flag.Enabled, flag.RolloutPercent, flag.Key, stableKey);

    public static int ClampPercent(int percent) => Math.Clamp(percent, 0, 100);
}
