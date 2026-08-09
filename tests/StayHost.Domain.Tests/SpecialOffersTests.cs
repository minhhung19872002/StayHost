namespace StayHost.Domain.Tests;

/// <summary>docs/01 ĐP-17, QL-14 — a host's private offer and its 24-hour window.</summary>
public class SpecialOffersTests
{
    private static readonly DateTime Sent = new(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc);

    private static SpecialOffer Offer(DateTime? sentAt = null) => new()
    {
        CheckIn = new DateOnly(2026, 9, 1),
        CheckOut = new DateOnly(2026, 9, 4),
        NightlyRate = 800_000m,
        Guests = 2,
        Status = SpecialOfferStatus.Pending,
        CreatedAt = sentAt ?? Sent,
        ExpiresAt = SpecialOffers.ExpiryFrom(sentAt ?? Sent)
    };

    [Fact]
    public void An_offer_lives_for_24_hours()
    {
        Assert.Equal(Sent.AddHours(24), SpecialOffers.ExpiryFrom(Sent));
    }

    [Fact]
    public void It_is_live_inside_the_window_and_dead_after()
    {
        var o = Offer();
        Assert.True(SpecialOffers.IsLive(o, Sent.AddHours(1)));
        Assert.True(SpecialOffers.IsLive(o, Sent.AddHours(23)));
        Assert.False(SpecialOffers.IsLive(o, Sent.AddHours(24)));
        Assert.False(SpecialOffers.IsLive(o, Sent.AddHours(25)));
    }

    [Fact]
    public void An_offer_already_acted_on_is_never_live_again()
    {
        // Status decides once it has moved off Pending, so a withdrawn or accepted
        // offer cannot be booked even a minute after, and a sweep marking expiry
        // cannot resurrect one somebody accepted.
        foreach (var status in new[]
                 { SpecialOfferStatus.Accepted, SpecialOfferStatus.Withdrawn, SpecialOfferStatus.Expired })
        {
            var o = Offer();
            o.Status = status;
            Assert.False(SpecialOffers.IsLive(o, Sent.AddHours(1)));
        }
    }

    [Fact]
    public void A_valid_offer_passes_validation()
    {
        Assert.Null(SpecialOffers.Validate(
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 4), 800_000m, 2));
    }

    [Fact]
    public void Checkout_must_be_after_checkin()
    {
        Assert.NotNull(SpecialOffers.Validate(
            new DateOnly(2026, 9, 4), new DateOnly(2026, 9, 1), 800_000m, 2));
        Assert.NotNull(SpecialOffers.Validate(
            new DateOnly(2026, 9, 4), new DateOnly(2026, 9, 4), 800_000m, 2));
    }

    [Fact]
    public void The_offered_price_must_be_positive()
    {
        Assert.NotNull(SpecialOffers.Validate(
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 4), 0m, 2));
        Assert.NotNull(SpecialOffers.Validate(
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 4), -100m, 2));
    }

    [Fact]
    public void At_least_one_guest_is_required()
    {
        Assert.NotNull(SpecialOffers.Validate(
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 4), 800_000m, 0));
    }
}
