using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>docs/01 AT-11 — the patterns worth a person looking at.</summary>
public class RiskSignalsTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private static RiskSnapshot Snapshot(
        int accountAgeDays = 400, decimal total = 0, int cards = 1, int cancellations = 0, int today = 1) =>
        new()
        {
            AccountCreatedAt = Now.AddDays(-accountAgeDays),
            Now = Now,
            BookingTotal = total,
            DistinctCards = cards,
            RecentCancellations = cancellations,
            BookingsToday = today
        };

    [Fact]
    public void An_ordinary_booking_raises_nothing()
    {
        Assert.Empty(RiskSignals.Check(Snapshot(total: 5_000_000m)));
    }

    [Fact]
    public void A_days_old_account_spending_a_lot_is_flagged()
    {
        var signals = RiskSignals.Check(Snapshot(accountAgeDays: 2, total: 25_000_000m));

        Assert.Single(signals);
        Assert.Equal(RiskKind.NewAccountLargeBooking, signals[0].Kind);
        Assert.Equal(RiskSeverity.Review, signals[0].Severity);
    }

    [Fact]
    public void Twice_the_threshold_is_urgent_rather_than_merely_worth_a_look()
    {
        var signals = RiskSignals.Check(Snapshot(accountAgeDays: 1, total: 50_000_000m));

        Assert.Equal(RiskSeverity.Urgent, signals[0].Severity);
    }

    [Fact]
    public void The_same_amount_on_an_established_account_is_not_a_signal()
    {
        Assert.Empty(RiskSignals.Check(Snapshot(accountAgeDays: 400, total: 50_000_000m)));
    }

    [Fact]
    public void A_week_old_account_is_no_longer_new()
    {
        Assert.Empty(RiskSignals.Check(Snapshot(accountAgeDays: 7, total: 50_000_000m)));
        Assert.NotEmpty(RiskSignals.Check(Snapshot(accountAgeDays: 6, total: 50_000_000m)));
    }

    [Fact]
    public void Three_cards_in_a_month_is_flagged_and_two_is_not()
    {
        Assert.Empty(RiskSignals.Check(Snapshot(cards: 2)));

        var signals = RiskSignals.Check(Snapshot(cards: 3));
        Assert.Single(signals);
        Assert.Equal(RiskKind.ManyCards, signals[0].Kind);
    }

    [Fact]
    public void A_run_of_cancellations_is_only_worth_watching()
    {
        var signals = RiskSignals.Check(Snapshot(cancellations: 4));

        Assert.Single(signals);
        Assert.Equal(RiskKind.ManyCancellations, signals[0].Kind);
        Assert.Equal(RiskSeverity.Watch, signals[0].Severity);
    }

    [Fact]
    public void Four_bookings_in_a_day_is_flagged()
    {
        var signals = RiskSignals.Check(Snapshot(today: 4));

        Assert.Single(signals);
        Assert.Equal(RiskKind.RapidBookings, signals[0].Kind);
    }

    [Fact]
    public void Several_patterns_at_once_each_get_their_own_flag()
    {
        var signals = RiskSignals.Check(
            Snapshot(accountAgeDays: 1, total: 30_000_000m, cards: 5, cancellations: 3, today: 6));

        Assert.Equal(4, signals.Count);
        Assert.Equal(4, signals.Select(s => s.Kind).Distinct().Count());
    }

    [Fact]
    public void Every_signal_carries_something_a_person_can_read()
    {
        var signals = RiskSignals.Check(Snapshot(accountAgeDays: 1, total: 30_000_000m, cards: 4));

        Assert.All(signals, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Summary));
            Assert.False(string.IsNullOrWhiteSpace(s.Detail));
            Assert.False(string.IsNullOrWhiteSpace(RiskSignals.Label(s.Severity)));
        });
    }
}

/// <summary>docs/01 AT-07 — help articles are searchable without accents.</summary>
public class HelpArticleTests
{
    [Fact]
    public void Search_text_strips_accents_so_huy_don_finds_huy_don()
    {
        var article = new HelpArticle
        {
            Title = "Huỷ đặt chỗ",
            Summary = "Được hoàn bao nhiêu",
            Body = "Chính sách huỷ",
            Category = "Huỷ và hoàn tiền"
        };
        article.RefreshSearchText();

        // A guest typing without accents still lands on the article.
        Assert.All(SearchText.Terms("huy dat cho"), t => Assert.Contains(t, article.SearchText));
        Assert.All(SearchText.Terms("hoan tien"), t => Assert.Contains(t, article.SearchText));
        Assert.DoesNotContain("ủ", article.SearchText);
    }

    [Fact]
    public void Every_audience_has_wording_of_its_own()
    {
        Assert.Equal("Dành cho khách", HelpArticle.AudienceLabel(HelpAudience.Guest));
        Assert.Equal("Dành cho chủ nhà", HelpArticle.AudienceLabel(HelpAudience.Host));
        Assert.Equal("Chung", HelpArticle.AudienceLabel(HelpAudience.Everyone));
    }
}
