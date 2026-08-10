namespace StayHost.Domain.Tests;

/// <summary>docs/01 ĐG-11 — scoring reviews for secondary-account fraud.</summary>
public class ReviewFraudTests
{
    private static ReviewFraud.Signals Sig(
        bool self = false, bool shared = false, int age = 400,
        bool onlyThisHost = false, int stays = 5, double rating = 5) =>
        new(self, shared, age, onlyThisHost, stays, rating);

    [Fact]
    public void A_normal_review_is_not_flagged()
    {
        var a = ReviewFraud.Assess(Sig());
        Assert.Equal(ReviewFraud.Risk.None, a.Level);
        Assert.False(a.Flagged);
    }

    [Fact]
    public void The_host_reviewing_their_own_place_is_high_risk()
    {
        Assert.Equal(ReviewFraud.Risk.High, ReviewFraud.Assess(Sig(self: true)).Level);
    }

    [Fact]
    public void A_shared_creation_session_is_high_risk()
    {
        Assert.Equal(ReviewFraud.Risk.High, ReviewFraud.Assess(Sig(shared: true)).Level);
    }

    [Fact]
    public void A_fresh_account_only_ever_with_this_host_giving_five_stars_is_high_risk()
    {
        var a = ReviewFraud.Assess(Sig(age: 1, onlyThisHost: true, stays: 1, rating: 5));
        Assert.Equal(ReviewFraud.Risk.High, a.Level);
        Assert.NotEmpty(a.Reasons);
    }

    [Fact]
    public void A_single_soft_signal_is_low_not_high()
    {
        // New account, but a normal rating and not exclusive to one host.
        var a = ReviewFraud.Assess(Sig(age: 2, onlyThisHost: false, stays: 3, rating: 3.5));
        Assert.Equal(ReviewFraud.Risk.Low, a.Level);
    }

    [Fact]
    public void An_established_guests_five_star_review_stays_clean()
    {
        var a = ReviewFraud.Assess(Sig(age: 500, onlyThisHost: false, stays: 20, rating: 5));
        Assert.Equal(ReviewFraud.Risk.None, a.Level);
    }
}
