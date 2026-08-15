namespace StayHost.Domain.Tests;

/// <summary>docs/01 TĐ-16 "Hiếm có" and the YT-08 "sắp hết phòng" notice.</summary>
public class ScarcityTests
{
    private static Scarcity.Reading Free(int nights) =>
        new(nights, Scarcity.WindowDays);

    [Fact]
    public void A_calendar_with_almost_nothing_left_is_a_rare_find()
    {
        // 25% of 60 is 15, and the rule is strictly below that share.
        Assert.True(Scarcity.IsRareFind(Free(14)));
        Assert.False(Scarcity.IsRareFind(Free(15)));
    }

    [Fact]
    public void A_wide_open_calendar_says_nothing()
    {
        Assert.False(Scarcity.IsRareFind(Free(Scarcity.WindowDays)));
        Assert.False(Scarcity.IsRareFind(Free(40)));
    }

    [Fact]
    public void A_window_too_small_to_read_is_not_evidence_of_demand()
    {
        // A brand new listing that blocked every one of its few open days is
        // empty, not popular.
        var tiny = new Scarcity.Reading(0, Scarcity.MinNightsForSignal - 1);
        Assert.False(Scarcity.IsRareFind(tiny));

        var justEnough = new Scarcity.Reading(0, Scarcity.MinNightsForSignal);
        Assert.True(Scarcity.IsRareFind(justEnough));
    }

    [Fact]
    public void An_empty_window_never_divides_by_zero()
    {
        var nothing = new Scarcity.Reading(0, 0);
        Assert.Equal(1, nothing.FreeShare);
        Assert.False(Scarcity.IsRareFind(nothing));
    }

    [Fact]
    public void The_notice_fires_on_the_crossing_and_only_the_crossing()
    {
        Assert.True(Scarcity.ShouldWarnLowAvailability(Free(30), Free(5)));

        // Already scarce when we last looked: no news in it.
        Assert.False(Scarcity.ShouldWarnLowAvailability(Free(5), Free(3)));

        // Opening up again is not a warning either.
        Assert.False(Scarcity.ShouldWarnLowAvailability(Free(5), Free(40)));
        Assert.False(Scarcity.ShouldWarnLowAvailability(Free(40), Free(30)));
    }

    [Fact]
    public void The_reason_carries_the_numbers_a_guest_can_check()
    {
        var reason = Scarcity.RareFindReason(Free(4));

        Assert.Contains("4", reason);
        Assert.Contains(Scarcity.WindowDays.ToString(), reason);
    }

    [Fact]
    public void The_notice_names_the_place_once_and_reads_as_one_sentence()
    {
        var notice = Scarcity.LowAvailabilityNotice("Tra Que Farmstay", Free(4));

        Assert.Contains("Tra Que Farmstay", notice);
        Assert.Contains("4", notice);
        // The badge's wording has its own subject; splicing a title in front of
        // it gave a sentence with two, so the notice must not reuse it.
        Assert.DoesNotContain("Chỗ này", notice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_badge_and_the_notice_agree_because_they_share_one_threshold()
    {
        // Whatever the badge would show, the sweep must be willing to announce
        // for a listing that has just arrived at it — otherwise a guest clicks
        // through from one and finds the other missing.
        for (var free = 0; free <= Scarcity.WindowDays; free++)
        {
            var after = Free(free);
            Assert.Equal(
                Scarcity.IsRareFind(after),
                Scarcity.ShouldWarnLowAvailability(Free(Scarcity.WindowDays), after));
        }
    }
}
