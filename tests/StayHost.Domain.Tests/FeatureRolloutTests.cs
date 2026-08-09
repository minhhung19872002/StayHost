namespace StayHost.Domain.Tests;

/// <summary>docs/01 QT-08 — percentage rollout of a feature.</summary>
public class FeatureRolloutTests
{
    [Fact]
    public void Disabled_is_off_for_everyone_whatever_the_percentage()
    {
        Assert.False(FeatureRollout.IsOn(enabled: false, rolloutPercent: 100, "f", "user-1"));
    }

    [Fact]
    public void Zero_percent_is_off_and_hundred_percent_is_on()
    {
        Assert.False(FeatureRollout.IsOn(true, 0, "f", "user-1"));
        Assert.True(FeatureRollout.IsOn(true, 100, "f", "user-1"));
    }

    [Fact]
    public void The_same_person_is_stable_across_calls()
    {
        var a = FeatureRollout.IsOn(true, 50, "beta", "user-42");
        var b = FeatureRollout.IsOn(true, 50, "beta", "user-42");
        Assert.Equal(a, b);
    }

    [Fact]
    public void Bucket_is_deterministic_and_in_range()
    {
        for (var i = 0; i < 200; i++)
        {
            var bucket = FeatureRollout.Bucket("beta", $"user-{i}");
            Assert.InRange(bucket, 0, 99);
            Assert.Equal(bucket, FeatureRollout.Bucket("beta", $"user-{i}"));
        }
    }

    [Fact]
    public void A_person_is_not_in_the_same_slice_for_every_feature()
    {
        // Different feature keys shuffle the buckets, so being in feature A's 10%
        // does not put you in feature B's 10%. Over many users the two rollouts
        // should not be identical.
        var same = 0;
        for (var i = 0; i < 300; i++)
        {
            var inA = FeatureRollout.IsOn(true, 10, "feature-a", $"user-{i}");
            var inB = FeatureRollout.IsOn(true, 10, "feature-b", $"user-{i}");
            if (inA == inB) same++;
        }
        Assert.True(same < 300, "two features must not bucket identically");
    }

    [Fact]
    public void A_fifty_percent_rollout_covers_roughly_half()
    {
        var on = 0;
        const int n = 2000;
        for (var i = 0; i < n; i++)
            if (FeatureRollout.IsOn(true, 50, "beta", $"user-{i}")) on++;

        // Deterministic hash, so this is a fixed number, not a flaky one — just
        // assert it is in a sane band around half.
        Assert.InRange(on, (int)(n * 0.40), (int)(n * 0.60));
    }

    [Fact]
    public void An_anonymous_visitor_with_no_key_is_off_below_full_rollout()
    {
        Assert.False(FeatureRollout.IsOn(true, 50, "beta", null));
        Assert.True(FeatureRollout.IsOn(true, 100, "beta", null));   // 100% needs no bucket
    }
}
