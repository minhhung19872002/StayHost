namespace StayHost.Domain.Tests;

/// <summary>docs/08 §1.3 — nobody handles their own business.</summary>
public class AdminConflictTests
{
    [Fact]
    public void An_admin_cannot_act_on_their_own_account()
    {
        // docs/08 §13 scenario 2.
        var kind = AdminConflict.Check(new AdminConflict.Links(
            SameAccount: true, ShareABooking: false, AdminDecidedAgainstThem: false, ShareContactOrDevice: false));

        Assert.Equal(ConflictKind.Self, kind);
        Assert.True(AdminConflict.Blocks(kind));
        Assert.Contains("chính tài khoản của mình", AdminConflict.Message(kind));
    }

    [Fact]
    public void Being_the_other_party_on_a_booking_is_a_conflict_too()
    {
        var kind = AdminConflict.Check(new AdminConflict.Links(false, ShareABooking: true, false, false));

        Assert.Equal(ConflictKind.SharedBooking, kind);
        Assert.True(AdminConflict.Blocks(kind));
    }

    [Fact]
    public void A_shared_phone_or_device_is_treated_as_a_relationship()
    {
        // The platform already computes this for fraud; the same signal answers
        // "is this your cousin's account".
        var kind = AdminConflict.Check(new AdminConflict.Links(false, false, false, ShareContactOrDevice: true));

        Assert.Equal(ConflictKind.LinkedAccount, kind);
    }

    [Fact]
    public void Having_already_ruled_on_somebody_bars_ruling_again()
    {
        var kind = AdminConflict.Check(new AdminConflict.Links(false, false, AdminDecidedAgainstThem: true, false));

        Assert.Equal(ConflictKind.AlreadyDecided, kind);
    }

    [Fact]
    public void An_unrelated_account_is_ordinary_work()
    {
        var kind = AdminConflict.Check(new AdminConflict.Links(false, false, false, false));

        Assert.Equal(ConflictKind.None, kind);
        Assert.False(AdminConflict.Blocks(kind));
        Assert.Equal("", AdminConflict.Message(kind));
    }

    [Fact]
    public void The_worst_conflict_is_the_one_reported()
    {
        // All four at once still reads as "this is your own account".
        var kind = AdminConflict.Check(new AdminConflict.Links(true, true, true, true));

        Assert.Equal(ConflictKind.Self, kind);
    }

    [Fact]
    public void Reading_is_watched_but_deciding_is_blocked()
    {
        // Blocking a support agent from opening a profile they happen to share a
        // device fingerprint with would stop them working.
        Assert.False(AdminConflict.AppliesTo(AdminAction.FindUser));
        Assert.False(AdminConflict.AppliesTo(AdminAction.ViewBookingHistory));
        Assert.True(AdminConflict.AppliesTo(AdminAction.Suspend));
        Assert.True(AdminConflict.AppliesTo(AdminAction.ManualRefund));
    }
}
