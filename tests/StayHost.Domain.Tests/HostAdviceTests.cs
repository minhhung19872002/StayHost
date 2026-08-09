namespace StayHost.Domain.Tests;

/// <summary>docs/01 QL-09, QL-18, CN-14 — the pre-decision advice for hosts.</summary>
public class HostAdviceTests
{
    /* -------- CN-14 income estimate -------- */

    [Fact]
    public void Income_estimate_gives_three_scenarios_rising_with_occupancy()
    {
        var s = HostAdvice.EstimateIncome(1_000_000m, 200_000m, avgStayNights: 3, hostFeeRate: 0.03m);
        Assert.Equal(3, s.Count);
        Assert.True(s[0].MonthlyNet < s[1].MonthlyNet);
        Assert.True(s[1].MonthlyNet < s[2].MonthlyNet);
        Assert.Equal(new[] { 40, 60, 80 }, s.Select(x => x.OccupancyPercent).ToArray());
    }

    [Fact]
    public void Income_estimate_is_net_of_the_host_fee_and_annual_is_twelve_months()
    {
        // 80% of 30 nights = 24 nights; 24/3 = 8 stays.
        // subtotal = 24*1,000,000 + 8*200,000 = 25,600,000; net = *0.97 = 24,832,000.
        var s = HostAdvice.EstimateIncome(1_000_000m, 200_000m, avgStayNights: 3, hostFeeRate: 0.03m);
        var high = s[2];
        Assert.Equal(24_832_000m, high.MonthlyNet);
        Assert.Equal(high.MonthlyNet * 12, high.AnnualNet);
    }

    /* -------- QL-09 suggested price -------- */

    [Fact]
    public void Too_few_comparables_is_not_a_firm_suggestion()
    {
        var p = HostAdvice.SuggestPrice(900_000m, comparables: 3, low: 800_000m, median: 1_000_000m, high: 1_200_000m);
        Assert.False(p.IsFirm);
        Assert.Equal(900_000m, p.SuggestedPrice);   // leaves the current price alone
    }

    [Fact]
    public void A_price_below_market_is_nudged_up_to_the_median()
    {
        var p = HostAdvice.SuggestPrice(600_000m, comparables: 12, low: 800_000m, median: 1_000_000m, high: 1_200_000m);
        Assert.True(p.IsFirm);
        Assert.Equal(1_000_000m, p.SuggestedPrice);
        Assert.Contains("thấp hơn", p.Rationale);
    }

    [Fact]
    public void A_price_within_range_still_reports_the_median()
    {
        var p = HostAdvice.SuggestPrice(1_050_000m, comparables: 12, low: 800_000m, median: 1_000_000m, high: 1_200_000m);
        Assert.True(p.IsFirm);
        Assert.Equal(1_000_000m, p.SuggestedPrice);
        Assert.Contains("trong khoảng", p.Rationale);
    }

    /* -------- QL-18 improvements -------- */

    [Fact]
    public void A_well_built_listing_has_nothing_to_fix()
    {
        var f = new HostAdvice.ListingFacts(
            PhotoCount: 12, InstantBook: true, DescriptionLength: 400, AmenityCount: 12,
            HasHighlight: true, FlexibleCancellation: true, Price: HostAdvice.PriceStanding.Within,
            Rating: 4.9, ReviewCount: 20);
        Assert.Empty(HostAdvice.Improvements(f));
    }

    [Fact]
    public void A_thin_listing_gets_the_cheap_high_leverage_fixes_first()
    {
        var f = new HostAdvice.ListingFacts(
            PhotoCount: 2, InstantBook: false, DescriptionLength: 50, AmenityCount: 3,
            HasHighlight: false, FlexibleCancellation: false, Price: HostAdvice.PriceStanding.Above,
            Rating: 0, ReviewCount: 0);
        var imp = HostAdvice.Improvements(f);
        Assert.NotEmpty(imp);
        Assert.Equal("Ảnh", imp[0].Area);   // photos lead the list
        Assert.Contains(imp, i => i.Area == "Đặt ngay");
        Assert.Contains(imp, i => i.Area == "Giá");
        Assert.All(imp, i => Assert.False(string.IsNullOrWhiteSpace(i.EstimatedImpact)));
    }
}
