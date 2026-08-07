using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>docs/03 §6 — the weighted score, the penalties, and the diversification rule.</summary>
public class RankingTests
{
    /// <summary>A middling listing, so each test can move exactly one thing.</summary>
    private static Ranking.Candidate Plain => new(
        Id: 1, HostId: 1,
        DistanceKm: 5, RadiusKm: 10,
        Rating: 4.6, ReviewCount: 40,
        Views: 100, Bookings: 10,
        Price: 1_000_000m, MedianPrice: 1_000_000m,
        ResponseRate: 90, InstantBook: true,
        PhotoCount: 8,
        DaysSincePublished: 200,
        HostCancelRate: 0,
        IsComplete: true);

    [Fact]
    public void The_weights_are_shares_of_one_whole()
    {
        Assert.Equal(1.0, Ranking.TotalWeight, 6);
    }

    [Fact]
    public void Every_factor_can_only_help_and_only_by_its_own_weight()
    {
        // Moving one factor from worst to best must move the score by exactly
        // that factor's weight — that is what "trọng số" means.
        // PhotoCount sits at the floor rather than below it: going under five
        // also lifts a penalty, and this test is about the weights alone.
        var worst = Plain with
        {
            DistanceKm = 10, Rating = 3, ReviewCount = 0, Views = 100, Bookings = 0,
            Price = 3_000_000m, ResponseRate = 0, InstantBook = false,
            PhotoCount = Ranking.MinPhotos, DaysSincePublished = 60
        };

        var nearer = worst with { DistanceKm = 0 };
        Assert.Equal(Ranking.Score(worst) + Ranking.NearWeight, Ranking.Score(nearer), 6);

        var faster = worst with { ResponseRate = 100, InstantBook = true };
        Assert.Equal(Ranking.Score(worst) + Ranking.ServiceWeight, Ranking.Score(faster), 6);

        var photographed = worst with { PhotoCount = Ranking.FullPhotoSet };
        var halfWay = Ranking.MinPhotos / (double)Ranking.FullPhotoSet;
        Assert.Equal(Ranking.Score(worst) + Ranking.PhotoWeight * (1 - halfWay), Ranking.Score(photographed), 6);
    }

    /* ------------------------------------------------------- each factor */

    [Fact]
    public void The_centre_of_the_area_is_worth_the_most_and_the_edge_nothing()
    {
        Assert.Equal(1, Ranking.Nearness(0, 10));
        Assert.Equal(0.5, Ranking.Nearness(5, 10), 6);
        Assert.Equal(0, Ranking.Nearness(10, 10));
        Assert.Equal(0, Ranking.Nearness(40, 10));
    }

    [Fact]
    public void A_handful_of_perfect_scores_does_not_beat_a_long_good_record()
    {
        // docs/03 §6 — "điểm đánh giá có tính tới số lượng đánh giá".
        var threeFives = Ranking.Quality(5.0, 3);
        var manyGood = Ranking.Quality(4.8, 200);
        Assert.True(manyGood > threeFives, $"{manyGood} should beat {threeFives}");
    }

    [Fact]
    public void A_place_nobody_has_reviewed_sits_at_the_baseline()
    {
        var unreviewed = Ranking.Quality(0, 0);
        Assert.Equal(Ranking.Quality(Ranking.QualityBaseline, 0), unreviewed);
        Assert.InRange(unreviewed, 0.7, 0.8);
    }

    [Fact]
    public void Being_looked_at_without_being_booked_earns_nothing()
    {
        Assert.Equal(0, Ranking.Conversion(500, 0));
        Assert.Equal(0, Ranking.Conversion(0, 0));

        // One in five is the top of the scale; better than that is still the top.
        Assert.Equal(1, Ranking.Conversion(100, 20));
        Assert.Equal(1, Ranking.Conversion(100, 60));
        Assert.Equal(0.5, Ranking.Conversion(100, 10), 6);
    }

    [Fact]
    public void Price_is_scored_against_what_similar_places_nearby_charge()
    {
        Assert.Equal(0.5, Ranking.PriceFit(1_000_000m, 1_000_000m), 6);
        Assert.Equal(1, Ranking.PriceFit(500_000m, 1_000_000m), 6);
        Assert.Equal(0, Ranking.PriceFit(2_000_000m, 1_000_000m), 6);

        // No comparison to make: neither rewarded nor punished.
        Assert.Equal(0.5, Ranking.PriceFit(1_000_000m, 0));
    }

    [Fact]
    public void A_new_listing_is_helped_for_thirty_days_and_then_stops_being_new()
    {
        Assert.Equal(1, Ranking.Freshness(0));
        Assert.Equal(0.5, Ranking.Freshness(15), 6);
        Assert.Equal(0, Ranking.Freshness(Ranking.FreshDays));
        Assert.Equal(0, Ranking.Freshness(400));
    }

    /* --------------------------------------------------------- penalties */

    [Fact]
    public void The_four_penalties_of_the_spec_all_bite()
    {
        var baseline = Ranking.Score(Plain);

        Assert.Equal(Ranking.PoorRatingPenalty,
            baseline - Ranking.Score(Plain with { Rating = 3.9 }) - QualityDrop(3.9), 6);

        Assert.True(Ranking.Score(Plain with { HostCancelRate = 9 }) < baseline);
        Assert.True(Ranking.Score(Plain with { PhotoCount = 4 }) < baseline);
        Assert.True(Ranking.Score(Plain with { IsComplete = false }) < baseline);

        double QualityDrop(double rating) =>
            Ranking.QualityWeight * (Ranking.Quality(Plain.Rating, Plain.ReviewCount)
                                     - Ranking.Quality(rating, Plain.ReviewCount));
    }

    [Fact]
    public void A_place_nobody_has_reviewed_is_not_punished_for_a_low_score()
    {
        // Rating 0 with no reviews means "unknown", not "bad".
        var unreviewed = Plain with { Rating = 0, ReviewCount = 0 };
        Assert.Equal(0, Ranking.Penalty(unreviewed));
    }

    [Fact]
    public void Penalties_stack_but_never_take_a_listing_below_zero()
    {
        var awful = new Ranking.Candidate(
            1, 1, 100, 10, 1.0, 30, 100, 0, 9_000_000m, 1_000_000m,
            0, false, 0, 900, 90, false);

        Assert.True(Ranking.Penalty(awful) > 0.5);
        Assert.Equal(0, Ranking.Score(awful));
    }

    /* ---------------------------------------------------- diversification */

    private static List<(int Id, int HostId)> Rows(params int[] hosts) =>
        hosts.Select((h, i) => (i + 1, h)).ToList();

    [Fact]
    public void One_host_cannot_hold_more_than_two_of_the_first_twelve()
    {
        // Six listings from host 1 at the top, then enough other hosts to fill
        // the window without them.
        var ordered = Rows(1, 1, 1, 1, 1, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11);
        var result = Ranking.Diversify(ordered, r => r.HostId);

        var firstTwelve = result.Take(Ranking.DiverseWindow).ToList();
        Assert.Equal(2, firstTwelve.Count(r => r.HostId == 1));
    }

    [Fact]
    public void With_too_few_hosts_to_go_round_the_window_is_still_filled()
    {
        // Only two hosts exist, so "at most two each" cannot fill twelve slots.
        // Returning four results instead of twelve would be worse for the guest
        // than relaxing the rule, so the rest of the window is topped up in
        // score order.
        var ordered = Rows([.. Enumerable.Repeat(9, 12), .. Enumerable.Repeat(1, 6)]);
        var result = Ranking.Diversify(ordered, r => r.HostId);

        Assert.Equal(18, result.Count);
        Assert.Equal(Ranking.DiverseWindow, result.Take(Ranking.DiverseWindow).Count());

        // The two best from each host still lead.
        Assert.Equal([1, 2, 13, 14], result.Take(4).Select(r => r.Id));
    }

    [Fact]
    public void Nothing_is_dropped_only_moved_back()
    {
        var ordered = Rows(1, 1, 1, 1, 1, 1, 2, 3, 4, 5, 6, 7, 8, 9);
        var result = Ranking.Diversify(ordered, r => r.HostId);

        Assert.Equal(ordered.Count, result.Count);
        Assert.Equal(ordered.OrderBy(r => r.Id), result.OrderBy(r => r.Id));
    }

    [Fact]
    public void What_is_held_back_keeps_its_own_order()
    {
        var ordered = Rows(1, 1, 1, 1, 2, 3);
        var result = Ranking.Diversify(ordered, r => r.HostId);

        // Listings 3 and 4 were the third and fourth from host 1, in that order.
        var tail = result.SkipWhile(r => r.Id is not (3 or 4)).ToList();
        Assert.Equal(3, tail[0].Id);
        Assert.Equal(4, tail[1].Id);
    }

    [Fact]
    public void A_page_that_is_all_one_host_is_left_alone_when_there_is_nobody_else()
    {
        var ordered = Rows(1, 1, 1, 1, 1);
        var result = Ranking.Diversify(ordered, r => r.HostId);

        Assert.Equal(ordered.Count, result.Count);
        Assert.Equal(ordered, result);
    }

    [Fact]
    public void Beyond_the_window_a_host_may_appear_as_often_as_it_likes()
    {
        // The rule is about the first twelve, not about the whole result set:
        // past the window host 1 takes every remaining slot.
        var ordered = Rows(2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 1, 1, 1, 1, 1);
        var result = Ranking.Diversify(ordered, r => r.HostId);

        Assert.Equal(17, result.Count);
        Assert.Equal(5, result.Skip(Ranking.DiverseWindow).Count(r => r.HostId == 1));
    }

    /* ------------------------------------------------------------ distance */

    [Fact]
    public void Distance_is_measured_on_the_globe_not_on_the_numbers()
    {
        // Đà Nẵng to Hội An is about 25 km.
        var km = Ranking.DistanceKm(16.0544, 108.2022, 15.8801, 108.3380);
        Assert.InRange(km, 20, 30);

        Assert.Equal(0, Ranking.DistanceKm(16.0544, 108.2022, 16.0544, 108.2022), 6);
    }
}
