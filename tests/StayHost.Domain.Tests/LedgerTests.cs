using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>
/// docs/00 §6.1: "tổng tiền vào phải bằng tổng tiền ra… lệch một đồng là báo
/// động". These tests are that alarm, run before the money ever moves.
/// </summary>
public class LedgerTests
{
    private static readonly DateTime At = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Listing MakeListing() => new()
    {
        Id = 1, City = "Đà Lạt", Country = "Việt Nam",
        PricePerNight = 1_000_000m, CleaningFee = 500_000m, WeekendSurchargeRate = 0m
    };

    private static (Booking Booking, Pricing.Breakdown Price) Sell(decimal promotion = 0m)
    {
        var checkIn = new DateOnly(2026, 9, 7);
        var price = Pricing.Quote(new Pricing.Request
        {
            Listing = MakeListing(),
            CheckIn = checkIn,
            CheckOut = checkIn.AddDays(5),
            Party = new PartySize(2),
            TaxRules = [new TaxRule { Id = 1, City = "Đà Lạt", Name = "VAT", Method = TaxMethod.Percentage, Value = 0.08m }],
            PromotionAmount = promotion
        });

        var booking = new Booking
        {
            Id = 1,
            Reference = "SHTEST",
            CheckIn = checkIn,
            CheckOut = checkIn.AddDays(5),
            Nights = price.Nights,
            CleaningFee = price.CleaningFee,
            Subtotal = price.Subtotal,
            ServiceFee = price.GuestServiceFee,
            Tax = price.Tax,
            Promotion = price.Promotion,
            Total = price.Total,
            HostServiceFee = price.HostServiceFee,
            HostPayout = price.HostPayout,
            CancellationTier = CancellationTier.Moderate,
            CreatedAt = At
        };

        return (booking, price);
    }

    [Fact]
    public void Capturing_a_booking_balances()
    {
        var (booking, price) = Sell();
        var entries = Ledger.CaptureBooking(booking, price, At);

        Assert.Equal(0m, Ledger.Imbalance(entries));
        Assert.Single(entries.Select(e => e.TransactionId).Distinct());
    }

    [Fact]
    public void Capturing_a_discounted_booking_still_balances()
    {
        var (booking, price) = Sell(promotion: 400_000m);
        var entries = Ledger.CaptureBooking(booking, price, At);

        Assert.Equal(0m, Ledger.Imbalance(entries));
        // The promotion is the platform's cost, not a smaller payout to the host.
        Assert.Contains(entries, e => e.Account == LedgerAccount.PlatformExpense && e.Amount == 400_000m);
        Assert.Equal(price.HostPayout,
            entries.Single(e => e.Account == LedgerAccount.HostPayable).Amount);
    }

    [Fact]
    public void What_the_guest_pays_equals_host_plus_fees_plus_tax()
    {
        var (booking, price) = Sell();
        var entries = Ledger.CaptureBooking(booking, price, At);

        var received = entries.Single(e => e.Account == LedgerAccount.GuestFunds).Amount;
        var distributed = entries
            .Where(e => e.Direction == LedgerDirection.Credit)
            .Sum(e => e.Amount);

        Assert.Equal(received, distributed);
        Assert.Equal(price.Total, received);
    }

    [Fact]
    public void A_full_sell_then_refund_cycle_leaves_the_books_at_zero()
    {
        var (booking, price) = Sell();
        var outcome = Cancellation.Refund(new Cancellation.Context
        {
            Booking = booking,
            Now = At.AddDays(1),           // still inside the 48h grace window
            By = CancelledBy.Guest
        });

        var hostFeeReturned = Math.Round(
            booking.HostServiceFee * (outcome.RoomRefund + outcome.CleaningRefund) / booking.Subtotal,
            0, MidpointRounding.AwayFromZero);

        var all = Ledger.CaptureBooking(booking, price, At)
            .Concat(Ledger.RefundBooking(booking, outcome, hostFeeReturned, At.AddDays(1)))
            .Concat(Ledger.SettleRefund(booking, outcome.Amount, At.AddDays(1)))
            .ToList();

        Assert.Equal(0m, Ledger.Imbalance(all));

        // A full refund puts every đồng back: nothing is left owed to the guest.
        Assert.Equal(0m, all.Where(e => e.Account == LedgerAccount.GuestRefundPayable).Sum(e => e.Signed));
        Assert.Equal(price.Total, outcome.Amount);
    }

    [Fact]
    public void A_partial_refund_leaves_the_host_and_platform_with_the_rest()
    {
        var (booking, price) = Sell();

        // Well past the grace window and inside five days: Moderate returns half the room.
        var now = At.AddDays(30);
        booking.CheckIn = DateOnly.FromDateTime(now).AddDays(3);
        booking.CheckOut = booking.CheckIn.AddDays(5);

        var outcome = Cancellation.Refund(new Cancellation.Context
        {
            Booking = booking, Now = now, By = CancelledBy.Guest
        });

        var hostFeeReturned = Math.Round(
            booking.HostServiceFee * (outcome.RoomRefund + outcome.CleaningRefund) / booking.Subtotal,
            0, MidpointRounding.AwayFromZero);

        var all = Ledger.CaptureBooking(booking, price, At)
            .Concat(Ledger.RefundBooking(booking, outcome, hostFeeReturned, now))
            .Concat(Ledger.SettleRefund(booking, outcome.Amount, now))
            .ToList();

        Assert.Equal(0m, Ledger.Imbalance(all));
        Assert.True(outcome.Amount < price.Total);
        Assert.True(all.Where(e => e.Account == LedgerAccount.HostPayable).Sum(e => e.Signed) < 0);
    }

    [Fact]
    public void Paying_the_host_out_clears_what_they_were_owed()
    {
        var (booking, price) = Sell();

        var all = Ledger.CaptureBooking(booking, price, At)
            .Concat(Ledger.PayoutHost(booking, price.HostPayout, At.AddDays(7)))
            .ToList();

        Assert.Equal(0m, Ledger.Imbalance(all));
        Assert.Equal(0m, all.Where(e => e.Account == LedgerAccount.HostPayable).Sum(e => e.Signed));
    }

    [Fact]
    public void An_unbalanced_transaction_is_refused()
    {
        var (booking, price) = Sell();
        var broken = price with { Tax = price.Tax + 1 };

        Assert.Throws<Ledger.UnbalancedTransactionException>(
            () => Ledger.CaptureBooking(booking, broken, At));
    }
}
