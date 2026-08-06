using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>docs/01 ĐP-06 and docs/03 §1 — deposit now, the rest before the stay.</summary>
public class PartialPaymentTests
{
    private static readonly DateOnly Today = new(2026, 9, 1);

    [Fact]
    public void The_deposit_is_never_less_than_half()
    {
        Assert.Equal(5_000_000m, PartialPayment.Deposit(10_000_000m, 3_000_000m));
        Assert.Equal(5_000_000m, PartialPayment.Deposit(10_000_000m, null));
        Assert.Equal(7_000_000m, PartialPayment.Deposit(10_000_000m, 7_000_000m));
    }

    [Fact]
    public void A_deposit_is_never_more_than_the_whole_amount()
    {
        Assert.Equal(10_000_000m, PartialPayment.Deposit(10_000_000m, 12_000_000m));
    }

    [Fact]
    public void Half_of_an_odd_amount_rounds_up_so_the_platform_is_never_short()
    {
        Assert.Equal(500_001m, PartialPayment.MinimumDeposit(1_000_001m));
    }

    [Fact]
    public void A_stay_inside_two_weeks_must_be_paid_in_full()
    {
        Assert.False(PartialPayment.IsAvailable(Today.AddDays(14), Today));
        Assert.False(PartialPayment.IsAvailable(Today.AddDays(3), Today));
        Assert.True(PartialPayment.IsAvailable(Today.AddDays(15), Today));
    }

    [Fact]
    public void The_rest_falls_due_fourteen_days_before_check_in()
    {
        Assert.Equal(new DateOnly(2026, 9, 16), PartialPayment.BalanceDueOn(new DateOnly(2026, 9, 30), Today));
    }

    [Fact]
    public void A_due_date_already_past_means_now()
    {
        Assert.Equal(Today, PartialPayment.BalanceDueOn(Today.AddDays(5), Today));
    }

    [Fact]
    public void A_refused_charge_is_tried_again_every_twelve_hours()
    {
        var failed = new DateTime(2026, 9, 16, 8, 0, 0, DateTimeKind.Utc);

        Assert.False(PartialPayment.ShouldRetry(failed, failed, failed.AddHours(6)));
        Assert.True(PartialPayment.ShouldRetry(failed, failed, failed.AddHours(12)));
        Assert.True(PartialPayment.ShouldRetry(failed, failed.AddHours(12), failed.AddHours(24)));
    }

    [Fact]
    public void After_seventy_two_hours_the_booking_is_given_up_on()
    {
        var failed = new DateTime(2026, 9, 16, 8, 0, 0, DateTimeKind.Utc);

        Assert.False(PartialPayment.GaveUp(failed, failed.AddHours(71)));
        Assert.True(PartialPayment.GaveUp(failed, failed.AddHours(72)));

        // Past the window there is nothing left to retry.
        Assert.False(PartialPayment.ShouldRetry(failed, failed.AddHours(60), failed.AddHours(80)));
    }

    [Fact]
    public void A_deposit_leaves_the_rest_as_a_receivable_and_the_books_still_balance()
    {
        var (booking, price) = Sell();
        var deposit = PartialPayment.MinimumDeposit(price.Total);

        var entries = Ledger.CaptureBooking(booking, price, DateTime.UtcNow, deposit);

        Assert.Equal(0m, Ledger.Imbalance(entries));
        Assert.Equal(deposit, Sum(entries, LedgerAccount.GuestFunds, LedgerDirection.Debit));
        Assert.Equal(price.Total - deposit, Sum(entries, LedgerAccount.GuestReceivable, LedgerDirection.Debit));

        // The host's share and the fees are recognised whole at booking time.
        Assert.Equal(price.HostPayout, Sum(entries, LedgerAccount.HostPayable, LedgerDirection.Credit));
    }

    [Fact]
    public void Collecting_the_rest_clears_the_receivable_and_nothing_else()
    {
        var booking = new Booking { Id = 1, Reference = "SH-1" };

        var entries = Ledger.CollectBalance(booking, 5_000_000m, DateTime.UtcNow);

        Assert.Equal(0m, Ledger.Imbalance(entries));
        Assert.Equal(5_000_000m, Sum(entries, LedgerAccount.GuestFunds, LedgerDirection.Debit));
        Assert.Equal(5_000_000m, Sum(entries, LedgerAccount.GuestReceivable, LedgerDirection.Credit));
    }

    [Fact]
    public void Setting_a_refund_against_what_is_still_owed_balances()
    {
        var booking = new Booking { Id = 1, Reference = "SH-1" };

        var netted = Ledger.NetRefundAgainstReceivable(booking, 2_000_000m, DateTime.UtcNow);
        var written = Ledger.WriteOffReceivable(booking, 3_000_000m, DateTime.UtcNow);

        Assert.Equal(0m, Ledger.Imbalance([.. netted, .. written]));
        Assert.Empty(Ledger.WriteOffReceivable(booking, 0m, DateTime.UtcNow));
    }

    [Fact]
    public void Paying_in_full_posts_no_receivable_at_all()
    {
        var (booking, price) = Sell();

        var entries = Ledger.CaptureBooking(booking, price, DateTime.UtcNow);

        Assert.Equal(0m, Ledger.Imbalance(entries));
        Assert.Equal(0m, Sum(entries, LedgerAccount.GuestReceivable, LedgerDirection.Debit));
    }

    /// <summary>A real quote, so the numbers in these tests are numbers the engine produces.</summary>
    private static (Booking Booking, Pricing.Breakdown Price) Sell()
    {
        var checkIn = new DateOnly(2026, 10, 7);
        var listing = new Listing
        {
            Id = 1, City = "Đà Lạt", Country = "Việt Nam",
            PricePerNight = 1_000_000m, CleaningFee = 500_000m, WeekendSurchargeRate = 0m
        };

        var price = Pricing.Quote(new Pricing.Request
        {
            Listing = listing,
            CheckIn = checkIn,
            CheckOut = checkIn.AddDays(5),
            Party = new PartySize(2),
            TaxRules = [new TaxRule { Id = 1, City = "Đà Lạt", Name = "VAT", Method = TaxMethod.Percentage, Value = 0.08m }]
        });

        var booking = new Booking
        {
            Id = 1, Reference = "SH-1",
            CheckIn = checkIn, CheckOut = checkIn.AddDays(5),
            Nights = price.Nights, Total = price.Total,
            HostPayout = price.HostPayout, HostServiceFee = price.HostServiceFee
        };

        return (booking, price);
    }

    private static decimal Sum(
        IEnumerable<LedgerEntry> entries, LedgerAccount account, LedgerDirection direction) =>
        entries.Where(e => e.Account == account && e.Direction == direction).Sum(e => e.Amount);
}
