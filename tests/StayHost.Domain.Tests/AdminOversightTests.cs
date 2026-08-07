namespace StayHost.Domain.Tests;

/// <summary>docs/08 §10 and §1.3 — watching the watchers.</summary>
public class AdminOversightTests
{
    /* ---- §10, two pairs of eyes ---- */

    [Fact]
    public void A_large_refund_needs_a_second_person()
    {
        // docs/08 §13 scenario 6 — 15 triệu.
        Assert.True(AdminOversight.NeedsSecondApproval(15_000_000m));
        Assert.True(AdminOversight.NeedsSecondApproval(10_000_000m));
        Assert.False(AdminOversight.NeedsSecondApproval(9_999_999m));
    }

    [Fact]
    public void The_threshold_looks_at_size_not_direction()
    {
        // Taking 15 triệu back off somebody deserves the same second look.
        Assert.True(AdminOversight.NeedsSecondApproval(-15_000_000m));
    }

    [Fact]
    public void The_second_signature_cannot_be_the_first_one_again()
    {
        Assert.False(AdminOversight.MayApprove(approverUserId: 3, requesterUserId: 3));
        Assert.True(AdminOversight.MayApprove(approverUserId: 4, requesterUserId: 3));
        Assert.Contains("không tự duyệt được", AdminOversight.SelfApprovalMessage());
    }

    /* ---- §10, the random sample ---- */

    [Fact]
    public void The_same_decision_is_always_either_in_the_sample_or_out_of_it()
    {
        // A sample that reshuffles on every page load cannot be worked through.
        var first = AdminOversight.InRandomSample(4211);
        for (var i = 0; i < 5; i++)
            Assert.Equal(first, AdminOversight.InRandomSample(4211));
    }

    [Fact]
    public void Roughly_one_decision_in_twenty_is_read_back()
    {
        var picked = Enumerable.Range(1, 4000).Count(id => AdminOversight.InRandomSample(id));

        // 5% of 4000 is 200; allow the spread a cheap hash gives.
        Assert.InRange(picked, 120, 320);
    }

    [Fact]
    public void A_zero_percent_sample_reviews_nothing_and_a_hundred_reviews_all()
    {
        Assert.False(AdminOversight.InRandomSample(7, percent: 0));
        Assert.True(AdminOversight.InRandomSample(7, percent: 100));
    }

    /* ---- §10, the scoreboard ---- */

    [Fact]
    public void An_admin_whose_decisions_keep_being_overturned_is_flagged()
    {
        var card = new AdminOversight.Scorecard(3, "Lan", 120, 40, AppealsAgainst: 10, AppealsUpheldAgainst: 6);

        Assert.Equal(0.6, card.OverturnRate, 3);
        Assert.True(AdminOversight.LooksUnreliable(card));
    }

    [Fact]
    public void One_overturned_decision_out_of_two_is_not_yet_a_pattern()
    {
        // An alarm that fires on a single reversal teaches people to ignore alarms.
        var card = new AdminOversight.Scorecard(3, "Lan", 10, 4, AppealsAgainst: 2, AppealsUpheldAgainst: 1);

        Assert.False(AdminOversight.LooksUnreliable(card));
    }

    [Fact]
    public void An_admin_nobody_has_appealed_has_no_rate_to_alarm_about()
    {
        var card = new AdminOversight.Scorecard(3, "Lan", 10, 4, AppealsAgainst: 0, AppealsUpheldAgainst: 0);

        Assert.Equal(0, card.OverturnRate);
        Assert.False(AdminOversight.LooksUnreliable(card));
    }

    /* ---- §3, out of hours ---- */

    [Fact]
    public void Work_at_three_in_the_morning_is_worth_a_look()
    {
        Assert.True(AdminOversight.IsOutOfHours(new DateTime(2026, 8, 7, 3, 0, 0)));
        Assert.False(AdminOversight.IsOutOfHours(new DateTime(2026, 8, 7, 10, 0, 0)));
    }

    [Fact]
    public void So_is_work_on_a_sunday()
    {
        var sunday = new DateTime(2026, 8, 9, 10, 0, 0);

        Assert.Equal(DayOfWeek.Sunday, sunday.DayOfWeek);
        Assert.True(AdminOversight.IsOutOfHours(sunday));
    }

    [Fact]
    public void Every_flag_has_something_a_person_can_read()
    {
        foreach (var flag in Enum.GetValues<OversightFlag>())
            Assert.NotEqual("", AdminOversight.FlagLabel(flag));
    }
}
