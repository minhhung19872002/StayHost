namespace StayHost.Domain.Tests;

/// <summary>docs/01 ĐP-03 — a host's instant-book conditions.</summary>
public class InstantBookTests
{
    [Fact]
    public void With_no_conditions_anyone_may_instant_book()
    {
        var e = InstantBook.Check(false, false, guestVerified: false, guestRating: 2.0, guestReviewCount: 5);
        Assert.True(e.MayInstantBook);
    }

    [Fact]
    public void Requiring_verification_stops_an_unverified_guest()
    {
        Assert.False(InstantBook.Check(true, false, guestVerified: false, null, 0).MayInstantBook);
        Assert.True(InstantBook.Check(true, false, guestVerified: true, null, 0).MayInstantBook);
    }

    [Fact]
    public void A_new_guest_passes_the_good_reviews_bar()
    {
        // Nobody starts with reviews; blocking every newcomer would make the
        // marketplace unusable, so no history is treated as no red flags.
        var e = InstantBook.Check(false, true, guestVerified: true, guestRating: null, guestReviewCount: 0);
        Assert.True(e.MayInstantBook);
    }

    [Fact]
    public void A_poorly_reviewed_guest_fails_the_good_reviews_bar()
    {
        var e = InstantBook.Check(false, true, guestVerified: true, guestRating: 3.2, guestReviewCount: 4);
        Assert.False(e.MayInstantBook);
    }

    [Fact]
    public void A_well_reviewed_guest_passes()
    {
        var e = InstantBook.Check(false, true, guestVerified: true, guestRating: 4.9, guestReviewCount: 12);
        Assert.True(e.MayInstantBook);
    }

    [Fact]
    public void The_threshold_is_inclusive_at_its_edge()
    {
        Assert.True(InstantBook.Check(false, true, true, InstantBook.GoodReviewThreshold, 3).MayInstantBook);
        Assert.False(InstantBook.Check(false, true, true, InstantBook.GoodReviewThreshold - 0.1, 3).MayInstantBook);
    }

    [Fact]
    public void Verification_is_checked_before_reviews()
    {
        // Both conditions on, guest fails both: the message names verification,
        // the first thing they can act on.
        var e = InstantBook.Check(true, true, guestVerified: false, guestRating: 2.0, guestReviewCount: 5);
        Assert.False(e.MayInstantBook);
        Assert.Contains("xác minh", e.Reason);
    }
}
