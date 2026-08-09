namespace StayHost.Domain.Tests;

/// <summary>docs/01 ĐP-10 — the host's hard preconditions to book at all.</summary>
public class BookingPreconditionsTests
{
    [Fact]
    public void With_nothing_required_and_no_rules_a_booking_passes()
    {
        var r = BookingPreconditions.Check(false, false, false, false, hasHouseRules: false, agreedToRules: false);
        Assert.True(r.Ok);
    }

    [Fact]
    public void A_required_photo_stops_a_guest_without_one()
    {
        Assert.False(BookingPreconditions.Check(true, false, guestHasPhoto: false, false, false, true).Ok);
        Assert.True(BookingPreconditions.Check(true, false, guestHasPhoto: true, false, false, true).Ok);
    }

    [Fact]
    public void Required_verification_stops_an_unverified_guest()
    {
        Assert.False(BookingPreconditions.Check(false, true, true, guestVerified: false, false, true).Ok);
        Assert.True(BookingPreconditions.Check(false, true, true, guestVerified: true, false, true).Ok);
    }

    [Fact]
    public void House_rules_must_be_agreed_when_the_listing_has_them()
    {
        Assert.False(BookingPreconditions.Check(false, false, true, true, hasHouseRules: true, agreedToRules: false).Ok);
        Assert.True(BookingPreconditions.Check(false, false, true, true, hasHouseRules: true, agreedToRules: true).Ok);
    }

    [Fact]
    public void No_rules_means_nothing_to_agree_to()
    {
        // A listing without rules must not demand an agreement the guest cannot give.
        Assert.True(BookingPreconditions.Check(false, false, true, true, hasHouseRules: false, agreedToRules: false).Ok);
    }

    [Fact]
    public void The_photo_is_reported_before_verification()
    {
        // Both missing: the message names the photo, the cheaper thing to fix.
        var r = BookingPreconditions.Check(true, true, false, false, false, true);
        Assert.False(r.Ok);
        Assert.Contains("ảnh", r.Error);
    }
}
