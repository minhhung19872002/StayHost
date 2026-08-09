namespace StayHost.Domain.Tests;

/// <summary>docs/01 ĐP-09 (a code the guest applies) and TC-09 (a campaign with limits).</summary>
public class CouponsTests
{
    private static readonly DateTime Now = new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);

    private static Coupon Percent(decimal pct, decimal? cap = null) =>
        new() { Code = "P", Kind = CouponKind.Percentage, Value = pct, MaxDiscount = cap, IsActive = true };

    private static Coupon Fixed(decimal amount) =>
        new() { Code = "F", Kind = CouponKind.Fixed, Value = amount, IsActive = true };

    /* ---- the money ---- */

    [Fact]
    public void A_percentage_takes_that_share_off()
    {
        Assert.Equal(200_000m, Coupons.DiscountFor(Percent(10m), 2_000_000m));
    }

    [Fact]
    public void A_percentage_is_capped_where_a_cap_is_set()
    {
        // 10% of 10tr is 1tr, but the campaign caps it at 500k.
        Assert.Equal(500_000m, Coupons.DiscountFor(Percent(10m, cap: 500_000m), 10_000_000m));
    }

    [Fact]
    public void A_flat_code_takes_its_amount_off()
    {
        Assert.Equal(300_000m, Coupons.DiscountFor(Fixed(300_000m), 2_000_000m));
    }

    [Fact]
    public void A_code_never_pays_out_more_than_the_stay_costs()
    {
        // A 300k code on a 200k stay comes off as 200k, not 300k — a coupon is a
        // discount, never a payout.
        Assert.Equal(200_000m, Coupons.DiscountFor(Fixed(300_000m), 200_000m));
    }

    /* ---- when it applies ---- */

    [Fact]
    public void A_good_code_on_a_good_stay_applies()
    {
        var check = Coupons.Evaluate(Percent(10m), 2_000_000m, timesUsedTotal: 0, timesUsedByGuest: 0, Now);
        Assert.True(check.Ok);
        Assert.Equal(200_000m, check.Discount);
    }

    [Fact]
    public void An_inactive_code_is_refused()
    {
        var c = Percent(10m);
        c.IsActive = false;
        Assert.False(Coupons.Evaluate(c, 2_000_000m, 0, 0, Now).Ok);
    }

    [Fact]
    public void A_code_before_its_window_or_after_it_is_refused()
    {
        var early = Percent(10m);
        early.StartsAt = Now.AddDays(1);
        Assert.False(Coupons.Evaluate(early, 2_000_000m, 0, 0, Now).Ok);

        var late = Percent(10m);
        late.EndsAt = Now.AddDays(-1);
        Assert.False(Coupons.Evaluate(late, 2_000_000m, 0, 0, Now).Ok);
    }

    [Fact]
    public void The_end_of_the_window_is_exclusive()
    {
        // A code valid "until the 10th" is spent by the moment the 10th arrives,
        // not still live through it.
        var c = Percent(10m);
        c.EndsAt = Now;
        Assert.False(Coupons.Evaluate(c, 2_000_000m, 0, 0, Now).Ok);
    }

    /* ---- campaign limits (TC-09) ---- */

    [Fact]
    public void A_campaign_that_hit_its_total_cap_is_refused()
    {
        var c = Percent(10m);
        c.MaxRedemptions = 100;
        Assert.True(Coupons.Evaluate(c, 2_000_000m, timesUsedTotal: 99, timesUsedByGuest: 0, Now).Ok);
        Assert.False(Coupons.Evaluate(c, 2_000_000m, timesUsedTotal: 100, timesUsedByGuest: 0, Now).Ok);
    }

    [Fact]
    public void A_guest_who_used_their_allowance_is_refused_even_with_room_in_the_campaign()
    {
        var c = Percent(10m);
        c.MaxPerUser = 1;
        // Plenty of campaign left, but this guest has had their one.
        Assert.False(Coupons.Evaluate(c, 2_000_000m, timesUsedTotal: 3, timesUsedByGuest: 1, Now).Ok);
    }

    [Fact]
    public void A_stay_below_the_minimum_is_refused()
    {
        var c = Fixed(300_000m);
        c.MinBookingTotal = 2_000_000m;
        Assert.False(Coupons.Evaluate(c, 1_500_000m, 0, 0, Now).Ok);
        Assert.True(Coupons.Evaluate(c, 2_000_000m, 0, 0, Now).Ok);
    }

    [Fact]
    public void An_unknown_code_is_refused_rather_than_throwing()
    {
        Assert.False(Coupons.Evaluate(null, 2_000_000m, 0, 0, Now).Ok);
    }

    [Fact]
    public void Codes_are_compared_upper_case_and_trimmed()
    {
        Assert.Equal("CHAOMUNG10", Coupons.Normalize("  chaomung10 "));
    }
}
