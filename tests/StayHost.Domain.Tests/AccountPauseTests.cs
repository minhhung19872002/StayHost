using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>
/// docs/01 TK-12 — the "tạm vô hiệu hoá" half of the code. The erase half had
/// existed for months; this one had no column, no endpoint and no button, and
/// the code was ticked anyway because one clause of an "hoặc" was done.
/// </summary>
public class AccountPauseTests
{
    private static AccountPause.Check Pause(
        bool suspended = false, bool banned = false, int live = 0, decimal owed = 0m) =>
        AccountPause.CanPause(suspended, banned, live, owed);

    [Fact]
    public void An_account_with_nothing_in_flight_may_step_away()
    {
        Assert.True(Pause().Ok);
        Assert.Equal(AccountPause.Refusal.None, Pause().Reason);
    }

    [Fact]
    public void A_stay_that_is_booked_keeps_both_sides_on_the_platform()
    {
        // Vanishing between a booking and a check-in leaves the other side with a
        // reservation and nobody to reach, which is the one thing a reversible,
        // self-service pause must not be able to do.
        var refused = Pause(live: 1);

        Assert.False(refused.Ok);
        Assert.Equal(AccountPause.Refusal.HasLiveBookings, refused.Reason);
        Assert.Contains("1", refused.Message);
    }

    [Fact]
    public void Money_the_platform_still_holds_has_to_land_first()
    {
        var refused = Pause(owed: 2_500_000m);

        Assert.False(refused.Ok);
        Assert.Equal(AccountPause.Refusal.HasMoneyInFlight, refused.Reason);
    }

    [Fact]
    public void A_sanctioned_account_is_not_its_owners_to_pause()
    {
        // docs/08 §5 — a suspension is the platform's decision, answered by an
        // appeal. Letting the owner "pause" over the top of it would let them
        // dress a lock up as a choice, and hide the appeal route behind it.
        Assert.Equal(AccountPause.Refusal.UnderSanction, Pause(suspended: true).Reason);
        Assert.Equal(AccountPause.Refusal.UnderSanction, Pause(banned: true).Reason);

        // And the sanction is named ahead of everything else, so the message
        // points at the appeal rather than at a booking.
        Assert.Equal(AccountPause.Refusal.UnderSanction,
            Pause(suspended: true, live: 3, owed: 1_000m).Reason);
    }

    [Fact]
    public void Every_refusal_says_something_a_person_can_act_on()
    {
        foreach (var check in new[] { Pause(live: 2), Pause(owed: 1m), Pause(banned: true) })
        {
            Assert.False(check.Ok);
            Assert.NotEmpty(check.Message);
        }
    }

    [Fact]
    public void A_pause_ends_by_coming_back()
    {
        // An account nobody can reach to un-pause is a deletion wearing a
        // friendlier word, and docs/01 TK-12 lists the two as different things.
        Assert.True(AccountPause.ResumesOnSignIn);
        Assert.Contains("Đăng nhập lại", AccountPause.Notice);
    }

    [Fact]
    public void The_notice_does_not_promise_anything_is_deleted()
    {
        Assert.DoesNotContain("xoá", AccountPause.Notice.ToLowerInvariant());
        Assert.Contains("nguyên", AccountPause.Notice);
    }
}
