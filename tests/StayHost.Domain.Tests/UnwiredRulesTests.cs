namespace StayHost.Domain.Tests;

/// <summary>
/// The rules found on 15/08/2026 to be written, tested and never called by any
/// app code — the <c>SuspensionImpact</c> failure mode of CLAUDE.md, one layer
/// down. Passing here was never the problem; these tests pin the behaviour the
/// wiring now has to keep.
/// </summary>
public class UnwiredRulesTests
{
    /* ------------------------------------------ docs/06 §8, Q-A force majeure */

    [Fact]
    public void Force_majeure_pays_the_host_the_agreed_share_of_the_booking()
    {
        // Q-A is 25% and the customer locked it on 06/08/2026.
        Assert.Equal(0.25m, ShieldSettings.Current.ForceMajeureHostRate);
        Assert.Equal(500_000m, Shield.ForceMajeureHostAward(2_000_000m));
    }

    [Fact]
    public void The_force_majeure_award_follows_the_setting_not_a_constant()
    {
        var settings = new ShieldSettings { ForceMajeureHostRate = 0.30m };
        Assert.Equal(600_000m, Shield.ForceMajeureHostAward(2_000_000m, settings));
    }

    [Fact]
    public void Nothing_is_owed_on_a_booking_worth_nothing()
    {
        Assert.Equal(0m, Shield.ForceMajeureHostAward(0m));
        Assert.Equal(0m, Shield.ForceMajeureHostAward(-1m));
    }

    /* --------------------------------------------- docs/06 §10 C-D lost income */

    [Fact]
    public void Lost_income_stops_at_the_agreed_number_of_nights()
    {
        Assert.Equal(5, ShieldSettings.Current.LostIncomeNights);

        // Three nights lost is three nights paid...
        Assert.Equal(3_000_000m, Shield.LostIncome(1_000_000m, 3));
        // ...but ten is still only five.
        Assert.Equal(5_000_000m, Shield.LostIncome(1_000_000m, 10));
    }

    [Fact]
    public void A_C3_claim_is_capped_by_nights_not_by_the_high_value_item_ceiling()
    {
        // The item ceiling is about a stolen camera and says nothing about
        // nights: on its own it would have let this claim through at 15m.
        var perNight = 800_000m;
        var ceiling = Shield.LostIncome(perNight, ShieldSettings.Current.LostIncomeNights);

        Assert.Equal(4_000_000m, ceiling);
        Assert.True(ceiling < ShieldSettings.Current.HighValueItemCeiling);
    }

    /* ------------------------------------------------- docs/09 §3.6 DV-D split */

    [Fact]
    public void A_misdeclared_site_still_pays_the_provider_half_the_order()
    {
        Assert.Equal(0.50m, ServiceRules.MisdeclaredShare);
        Assert.Equal(600_000m, ServiceRules.ProviderShareOnMisdeclared(1_200_000m));
    }

    [Fact]
    public void The_two_halves_of_a_misdeclared_job_add_back_to_the_order()
    {
        foreach (var total in new[] { 0m, 1m, 999m, 1_200_000m, 1_234_567m })
        {
            var provider = ServiceRules.ProviderShareOnMisdeclared(total);
            var guest = ServiceRules.GuestRefundOnMisdeclared(total);
            Assert.Equal(total, provider + guest);
        }
    }

    [Fact]
    public void A_misdeclared_job_never_refunds_more_than_was_paid()
    {
        Assert.Equal(0m, ServiceRules.GuestRefundOnMisdeclared(0m));
        Assert.Equal(0m, ServiceRules.GuestRefundOnMisdeclared(-5m));
    }

    /* ------------------------------------- docs/07 §11 step 6, repeat chargebacks */

    [Fact]
    public void One_lost_chargeback_is_an_accident_and_two_is_a_pattern()
    {
        Assert.False(Chargebacks.GuestNeedsWatching(0));
        Assert.False(Chargebacks.GuestNeedsWatching(1));
        Assert.True(Chargebacks.GuestNeedsWatching(2));
        Assert.True(Chargebacks.GuestNeedsWatching(9));
    }

    [Fact]
    public void The_platforms_demand_to_verify_does_not_blame_the_host()
    {
        var r = BookingPreconditions.Check(
            requirePhoto: false, requireVerified: false,
            guestHasPhoto: true, guestVerified: false,
            hasHouseRules: false, agreedToRules: false,
            platformRequiresVerified: true);

        Assert.False(r.Ok);
        // A guest told "chủ nhà yêu cầu" would go and argue with a host who had
        // nothing to do with it.
        Assert.DoesNotContain("Chủ nhà", r.Error!);
        Assert.Contains("Tài khoản của bạn", r.Error!);
    }

    [Fact]
    public void A_verified_guest_passes_even_while_flagged()
    {
        var r = BookingPreconditions.Check(
            requirePhoto: false, requireVerified: false,
            guestHasPhoto: true, guestVerified: true,
            hasHouseRules: false, agreedToRules: false,
            platformRequiresVerified: true);

        Assert.True(r.Ok);
    }

    [Fact]
    public void The_flag_changes_nothing_for_everybody_else()
    {
        // The default has to stay false, or every existing caller would start
        // demanding identity documents.
        var r = BookingPreconditions.Check(
            requirePhoto: false, requireVerified: false,
            guestHasPhoto: false, guestVerified: false,
            hasHouseRules: false, agreedToRules: false);

        Assert.True(r.Ok);
    }

    /* ------------------------------------------------ docs/03 §4, the promise */

    [Fact]
    public void Only_the_two_free_cancellation_tiers_may_say_so()
    {
        Assert.True(Cancellation.HasFreeCancellation(CancellationTier.Flexible));
        Assert.True(Cancellation.HasFreeCancellation(CancellationTier.Moderate));

        foreach (var tier in new[]
                 {
                     CancellationTier.Strict, CancellationTier.SuperStrict,
                     CancellationTier.NonRefundable, CancellationTier.LongTermStrict
                 })
            Assert.False(Cancellation.HasFreeCancellation(tier));
    }

    [Fact]
    public void Every_tier_has_a_headline_and_only_the_free_ones_promise_free()
    {
        foreach (var tier in Enum.GetValues<CancellationTier>())
        {
            var headline = Cancellation.Headline(tier);
            Assert.False(string.IsNullOrWhiteSpace(headline));

            // The listing page used to print "Huỷ miễn phí trước 48 giờ" above a
            // sentence saying the place was non-refundable.
            if (!Cancellation.HasFreeCancellation(tier))
                Assert.DoesNotContain("miễn phí", headline);
        }
    }

    [Fact]
    public void The_headline_does_not_invent_a_deadline_the_refund_would_not_honour()
    {
        // 48 hours was the old hard-coded number and is not any of the six.
        foreach (var tier in Enum.GetValues<CancellationTier>())
            Assert.DoesNotContain("48 giờ", Cancellation.Headline(tier));
    }

    [Fact]
    public void The_free_cancellation_deadline_matches_what_the_summary_says()
    {
        var checkIn = new DateOnly(2026, 9, 20);
        var flexible = new Booking { CheckIn = checkIn, CancellationTier = CancellationTier.Flexible };
        var moderate = new Booking { CheckIn = checkIn, CancellationTier = CancellationTier.Moderate };

        // "đến 24 giờ trước khi nhận phòng" and "đến 5 ngày trước".
        Assert.Equal(checkIn.AddDays(-1), Cancellation.FreeCancellationDeadline(flexible));
        Assert.Equal(checkIn.AddDays(-5), Cancellation.FreeCancellationDeadline(moderate));
    }
}
