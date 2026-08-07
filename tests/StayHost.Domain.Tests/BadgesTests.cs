using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>docs/03 §8 — the two titles: who gets them, and when they are re-decided.</summary>
public class BadgesTests
{
    /// <summary>A host who meets all four, so each test can spoil exactly one.</summary>
    private static Badges.HostStats Good => new(
        Rating: 4.9, RatedListings: 3, Stays: 12, Nights: 40, ResponseRate: 95, CancelRate: 0);

    /* -------------------------------------------- Chủ nhà Ưu tú, four gates */

    [Fact]
    public void All_four_criteria_together_and_nothing_less()
    {
        Assert.True(Badges.QualifiesAsSuperhost(Good));

        Assert.False(Badges.QualifiesAsSuperhost(Good with { Rating = 4.79 }));
        Assert.False(Badges.QualifiesAsSuperhost(Good with { Stays = 9, Nights = 20 }));
        Assert.False(Badges.QualifiesAsSuperhost(Good with { ResponseRate = 89.9 }));
        Assert.False(Badges.QualifiesAsSuperhost(Good with { CancelRate = 1 }));
    }

    [Fact]
    public void The_thresholds_are_met_at_the_number_not_past_it()
    {
        Assert.True(Badges.QualifiesAsSuperhost(Good with { Rating = 4.8 }));
        Assert.True(Badges.QualifiesAsSuperhost(Good with { ResponseRate = 90 }));
        Assert.True(Badges.QualifiesAsSuperhost(Good with { CancelRate = 0.99 }));

        // "< 1%", so exactly 1% is not it.
        Assert.False(Badges.QualifiesAsSuperhost(Good with { CancelRate = 1.0 }));
    }

    [Fact]
    public void Three_long_stays_count_the_same_as_ten_short_ones()
    {
        // "Từ 10 chuyến trở lên trong năm (hoặc từ 3 chuyến với tổng ≥ 100 đêm)."
        Assert.True(Badges.QualifiesAsSuperhost(Good with { Stays = 3, Nights = 100 }));
        Assert.False(Badges.QualifiesAsSuperhost(Good with { Stays = 3, Nights = 99 }));
        Assert.False(Badges.QualifiesAsSuperhost(Good with { Stays = 2, Nights = 400 }));
    }

    [Fact]
    public void A_host_nobody_has_reviewed_does_not_pass_on_a_rating_of_zero()
    {
        // No reviews leaves the average at 0, which must read as "not yet", not
        // as a failing score that some later change might invert.
        var fresh = Good with { Rating = 0, RatedListings = 0 };
        Assert.False(Badges.QualifiesAsSuperhost(fresh));
    }

    [Fact]
    public void The_progress_list_and_the_decision_are_the_same_thing()
    {
        foreach (var stats in new[]
                 {
                     Good,
                     Good with { Rating = 4.5 },
                     Good with { CancelRate = 8 },
                     Good with { Stays = 0, Nights = 0, Rating = 0, RatedListings = 0 }
                 })
            Assert.Equal(
                Badges.QualifiesAsSuperhost(stats),
                Badges.SuperhostCriteria(stats).All(c => c.Met));
    }

    [Fact]
    public void Each_criterion_says_where_the_host_stands()
    {
        var criteria = Badges.SuperhostCriteria(Good with { Stays = 4, Nights = 120 });
        var stays = criteria.Single(c => c.Key == "stays");

        Assert.Equal("4 chuyến · 120 đêm", stays.Current);
        Assert.True(stays.Met);
        Assert.Equal(4, criteria.Count);
    }

    /* ------------------------------------------------------- Khách chọn */

    private static Badges.ListingStats Loved => new(
        Rating: 4.95, Reviews: 8, CancelRate: 1, SeriousReports: 0);

    [Fact]
    public void A_place_needs_a_score_a_history_and_a_clean_record()
    {
        Assert.True(Badges.QualifiesAsGuestFavorite(Loved));

        Assert.False(Badges.QualifiesAsGuestFavorite(Loved with { Rating = 4.89 }));
        Assert.False(Badges.QualifiesAsGuestFavorite(Loved with { Reviews = 4 }));
        Assert.False(Badges.QualifiesAsGuestFavorite(Loved with { CancelRate = 5 }));
    }

    [Fact]
    public void One_upheld_report_is_enough_to_lose_it()
    {
        Assert.False(Badges.QualifiesAsGuestFavorite(Loved with { SeriousReports = 1 }));
    }

    [Fact]
    public void Five_reviews_at_four_point_nine_is_exactly_enough()
    {
        Assert.True(Badges.QualifiesAsGuestFavorite(Loved with { Rating = 4.9, Reviews = 5 }));
    }

    /* --------------------------------------------------- when it is decided */

    [Fact]
    public void The_quarters_are_the_four_dates_the_spec_names()
    {
        Assert.Equal(new DateOnly(2026, 4, 1), Badges.NextSuperhostReview(new DateOnly(2026, 1, 1)));
        Assert.Equal(new DateOnly(2026, 4, 1), Badges.NextSuperhostReview(new DateOnly(2026, 3, 31)));
        Assert.Equal(new DateOnly(2026, 7, 1), Badges.NextSuperhostReview(new DateOnly(2026, 4, 1)));
        Assert.Equal(new DateOnly(2026, 10, 1), Badges.NextSuperhostReview(new DateOnly(2026, 8, 7)));
        Assert.Equal(new DateOnly(2027, 1, 1), Badges.NextSuperhostReview(new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public void The_quarter_somebody_is_in_is_the_one_that_already_started()
    {
        Assert.Equal(new DateOnly(2026, 1, 1), Badges.CurrentQuarterStart(new DateOnly(2026, 3, 31)));
        Assert.Equal(new DateOnly(2026, 4, 1), Badges.CurrentQuarterStart(new DateOnly(2026, 4, 1)));
        Assert.Equal(new DateOnly(2026, 7, 1), Badges.CurrentQuarterStart(new DateOnly(2026, 8, 7)));
        Assert.Equal(new DateOnly(2026, 10, 1), Badges.CurrentQuarterStart(new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void A_server_that_missed_the_first_of_the_quarter_still_catches_up()
    {
        // This is the whole reason the stamp is compared against the quarter
        // start rather than against "is today 1 April".
        var lastQuarter = new DateOnly(2026, 1, 1);

        Assert.True(Badges.SuperhostDue(lastQuarter, new DateOnly(2026, 4, 1)));
        Assert.True(Badges.SuperhostDue(lastQuarter, new DateOnly(2026, 4, 9)));
        Assert.False(Badges.SuperhostDue(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 9)));
    }

    [Fact]
    public void Never_decided_is_always_due()
    {
        Assert.True(Badges.SuperhostDue(null, new DateOnly(2026, 8, 7)));
        Assert.True(Badges.FavoriteDue(null, new DateOnly(2026, 8, 7)));
    }

    [Fact]
    public void Deciding_twice_in_one_period_does_not_happen()
    {
        var friday = new DateOnly(2026, 8, 7);
        var monday = Badges.CurrentWeekStart(friday);

        Assert.Equal(DayOfWeek.Monday, monday.DayOfWeek);
        Assert.False(Badges.FavoriteDue(monday, friday));
        Assert.False(Badges.SuperhostDue(Badges.CurrentQuarterStart(friday), friday));
    }

    [Fact]
    public void The_week_turns_on_monday_including_when_today_is_sunday()
    {
        // Sunday is the end of the week here, not the start of a new one — a
        // Sunday sweep must not re-decide what Monday already decided.
        var sunday = new DateOnly(2026, 8, 9);
        Assert.Equal(DayOfWeek.Sunday, sunday.DayOfWeek);
        Assert.Equal(new DateOnly(2026, 8, 3), Badges.CurrentWeekStart(sunday));

        var nextMonday = new DateOnly(2026, 8, 10);
        Assert.Equal(nextMonday, Badges.CurrentWeekStart(nextMonday));
        Assert.True(Badges.FavoriteDue(new DateOnly(2026, 8, 3), nextMonday));
    }
}
