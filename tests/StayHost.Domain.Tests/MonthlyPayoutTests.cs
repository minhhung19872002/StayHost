namespace StayHost.Domain.Tests;

/// <summary>docs/01 TC-03, docs/07 §12.3 — the monthly payout schedule for long stays.</summary>
public class MonthlyPayoutTests
{
    private static readonly DateOnly CheckIn = new(2026, 9, 1);

    [Fact]
    public void A_stay_under_28_nights_has_no_monthly_schedule()
    {
        Assert.Empty(Payouts.MonthlySchedule(10_000_000m, CheckIn, 27));
        Assert.Empty(Payouts.MonthlySchedule(10_000_000m, CheckIn, 3));
    }

    [Fact]
    public void A_28_night_stay_is_one_instalment()
    {
        var s = Payouts.MonthlySchedule(9_000_000m, CheckIn, 28);
        Assert.Single(s);
        Assert.Equal(9_000_000m, s[0].Amount);
        Assert.Equal(CheckIn.AddDays(1), s[0].DueOn);   // 24h after check-in
    }

    [Fact]
    public void A_two_month_stay_splits_across_two_dates()
    {
        var s = Payouts.MonthlySchedule(9_000_000m, CheckIn, 45);
        Assert.Equal(2, s.Count);
        Assert.Equal(CheckIn.AddDays(1), s[0].DueOn);
        Assert.Equal(CheckIn.AddDays(31), s[1].DueOn);   // a month on
    }

    [Fact]
    public void The_instalments_sum_to_exactly_the_payout()
    {
        // A total that does not divide evenly still adds back up to the penny.
        foreach (var nights in new[] { 28, 30, 45, 60, 91, 120 })
        {
            var s = Payouts.MonthlySchedule(10_000_001m, CheckIn, nights);
            Assert.Equal(10_000_001m, s.Sum(i => i.Amount));
        }
    }

    [Fact]
    public void Earlier_months_carry_the_larger_share_and_the_last_takes_the_remainder()
    {
        // 60 nights, 6,000,000 → two full months of 3,000,000 each.
        var s = Payouts.MonthlySchedule(6_000_000m, CheckIn, 60);
        Assert.Equal(2, s.Count);
        Assert.Equal(3_000_000m, s[0].Amount);
        Assert.Equal(3_000_000m, s[1].Amount);
    }

    [Fact]
    public void Due_dates_step_by_a_month_each_time()
    {
        var s = Payouts.MonthlySchedule(12_000_000m, CheckIn, 91);
        Assert.Equal(4, s.Count);   // 30+30+30+1
        Assert.Equal(CheckIn.AddDays(1), s[0].DueOn);
        Assert.Equal(CheckIn.AddDays(31), s[1].DueOn);
        Assert.Equal(CheckIn.AddDays(61), s[2].DueOn);
        Assert.Equal(CheckIn.AddDays(91), s[3].DueOn);
    }
}
