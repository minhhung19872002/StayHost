using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>The policy table and the four pre-rules of docs/03 §4.</summary>
public class CancellationTests
{
    private static readonly DateTime BookedAt = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// A 5-night stay costing 5,000,000₫ of room plus a 500,000₫ cleaning fee.
    /// Service fee and tax are round numbers so the arithmetic stays readable.
    /// </summary>
    private static Booking MakeBooking(CancellationTier tier, DateOnly checkIn, int nights = 5) => new()
    {
        Id = 1,
        Reference = "SHTEST",
        CheckIn = checkIn,
        CheckOut = checkIn.AddDays(nights),
        Nights = nights,
        CleaningFee = 500_000m,
        Subtotal = 5_500_000m,          // room 5,000,000 + cleaning 500,000
        ServiceFee = 770_000m,
        Tax = 500_000m,
        Total = 6_770_000m,
        HostServiceFee = 165_000m,
        HostPayout = 5_335_000m,
        CancellationTier = tier,
        CreatedAt = BookedAt
    };

    private static Cancellation.Outcome Cancel(
        CancellationTier tier, int daysBeforeCheckIn, CancelledBy by = CancelledBy.Guest,
        DateTime? now = null, int nights = 5, int serviceFeeRefundsUsed = 0)
    {
        // Cancel well after the 48-hour grace window unless a test says otherwise.
        var at = now ?? BookedAt.AddDays(10);
        var checkIn = DateOnly.FromDateTime(at).AddDays(daysBeforeCheckIn);

        return Cancellation.Refund(new Cancellation.Context
        {
            Booking = MakeBooking(tier, checkIn, nights),
            Now = at,
            By = by,
            ServiceFeeRefundsUsed = serviceFeeRefundsUsed
        });
    }

    /* ------------------------------------------------------ the four pre-rules */

    [Fact]
    public void Grace_window_refunds_everything_when_check_in_is_still_far_off()
    {
        var outcome = Cancel(CancellationTier.NonRefundable, daysBeforeCheckIn: 30,
            now: BookedAt.AddHours(5));

        Assert.Equal("grace-48h", outcome.RuleKey);
        Assert.Equal(6_770_000m, outcome.Amount);
    }

    [Fact]
    public void Grace_window_does_not_apply_when_check_in_is_within_fourteen_days()
    {
        var outcome = Cancel(CancellationTier.NonRefundable, daysBeforeCheckIn: 10,
            now: BookedAt.AddHours(5));

        Assert.NotEqual("grace-48h", outcome.RuleKey);
        Assert.Equal(0m, outcome.RoomRefund);
    }

    [Fact]
    public void Service_fee_stops_coming_back_after_three_refunds_in_a_year()
    {
        var within = Cancel(CancellationTier.Moderate, 10, serviceFeeRefundsUsed: 2);
        Assert.Equal(770_000m, within.ServiceFeeRefund);

        var beyond = Cancel(CancellationTier.Moderate, 10, serviceFeeRefundsUsed: 3);
        Assert.Equal(0m, beyond.ServiceFeeRefund);
    }

    [Fact]
    public void Host_cancellation_refunds_everything_and_adds_ten_percent_of_credit()
    {
        var outcome = Cancel(CancellationTier.NonRefundable, 2, CancelledBy.Host);

        Assert.Equal(6_770_000m, outcome.Amount);
        Assert.Equal(677_000m, outcome.GoodwillCredit);
    }

    [Fact]
    public void Force_majeure_refunds_everything_without_a_credit()
    {
        var outcome = Cancel(CancellationTier.Strict, 1, CancelledBy.ForceMajeure);

        Assert.Equal(6_770_000m, outcome.Amount);
        Assert.Equal(0m, outcome.GoodwillCredit);
    }

    /* ------------------------------------------------------- the policy table */

    [Theory]
    // Flexible: 100% at 24h+, then the first night is lost (4 of 5 nights = 80%).
    [InlineData(CancellationTier.Flexible, 2, 5_000_000)]
    [InlineData(CancellationTier.Flexible, 0, 4_000_000)]
    // Moderate: 100% at 5 days+, else half.
    [InlineData(CancellationTier.Moderate, 6, 5_000_000)]
    [InlineData(CancellationTier.Moderate, 3, 2_500_000)]
    // Strict: 100% / 50% / nothing.
    [InlineData(CancellationTier.Strict, 31, 5_000_000)]
    [InlineData(CancellationTier.Strict, 10, 2_500_000)]
    [InlineData(CancellationTier.Strict, 3, 0)]
    // Super strict: 50% / nothing.
    [InlineData(CancellationTier.SuperStrict, 8, 2_500_000)]
    [InlineData(CancellationTier.SuperStrict, 3, 0)]
    // Non-refundable: never.
    [InlineData(CancellationTier.NonRefundable, 40, 0)]
    public void Room_refund_follows_the_policy_table(CancellationTier tier, int daysBefore, decimal expectedRoom)
    {
        var outcome = Cancel(tier, daysBefore);
        Assert.Equal(expectedRoom, outcome.RoomRefund);
    }

    [Fact]
    public void The_cleaning_fee_always_comes_back_in_full()
    {
        foreach (var tier in Enum.GetValues<CancellationTier>())
        {
            var outcome = Cancel(tier, daysBeforeCheckIn: 1);
            Assert.Equal(500_000m, outcome.CleaningRefund);
        }
    }

    [Fact]
    public void The_service_fee_only_comes_back_on_an_early_cancellation()
    {
        Assert.Equal(770_000m, Cancel(CancellationTier.Moderate, 6).ServiceFeeRefund);
        Assert.Equal(0m, Cancel(CancellationTier.Moderate, 3).ServiceFeeRefund);
    }

    [Fact]
    public void Long_term_strict_charges_the_first_thirty_nights_inside_the_window()
    {
        // A 40-night stay cancelled 10 days out: 10 of 40 nights are refundable.
        var outcome = Cancel(CancellationTier.LongTermStrict, 10, nights: 40);

        Assert.Equal(5_000_000m * 10 / 40, outcome.RoomRefund);
    }

    /* ---------------------------------------------------- mid-stay cancellation */

    [Fact]
    public void After_check_in_only_the_unused_nights_are_considered()
    {
        var at = BookedAt.AddDays(40);
        var checkIn = DateOnly.FromDateTime(at).AddDays(-2);     // two nights already used
        var booking = MakeBooking(CancellationTier.Flexible, checkIn);

        var outcome = Cancellation.Refund(new Cancellation.Context
        {
            Booking = booking,
            Now = at,
            By = CancelledBy.Guest
        });

        // Three unused nights, Flexible inside 24h keeps 4/5 of them.
        Assert.Equal(3, Cancellation.UnusedNights(booking, DateOnly.FromDateTime(at)));
        Assert.Equal(Math.Round(5_000_000m * 3 / 5 * 0.8m), outcome.RoomRefund);
    }

    [Fact]
    public void Nothing_is_left_to_refund_once_the_stay_has_ended()
    {
        var at = BookedAt.AddDays(60);
        var checkIn = DateOnly.FromDateTime(at).AddDays(-10);
        var booking = MakeBooking(CancellationTier.Moderate, checkIn);

        Assert.Equal(0, Cancellation.UnusedNights(booking, DateOnly.FromDateTime(at)));
    }

    /* ----------------------------------------------------------------- labels */

    [Fact]
    public void Every_tier_has_a_label_and_a_summary()
    {
        foreach (var tier in Enum.GetValues<CancellationTier>())
        {
            Assert.False(string.IsNullOrWhiteSpace(Cancellation.Label(tier)));
            Assert.Contains("Phí vệ sinh", Cancellation.Summary(tier));
        }
    }
}
