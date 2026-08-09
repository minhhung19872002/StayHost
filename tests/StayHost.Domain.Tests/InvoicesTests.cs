namespace StayHost.Domain.Tests;

/// <summary>docs/01 ĐP-14 — the invoice number and the amounts on a booking's invoice.</summary>
public class InvoicesTests
{
    private static Booking Booking(decimal total, decimal balanceDue = 0) => new()
    {
        Reference = "SHAB12CD34",
        Total = total,
        BalanceDue = balanceDue,
        CreatedAt = new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public void The_number_is_stable_for_a_booking()
    {
        var b = Booking(3_000_000m);
        Assert.Equal("HD-202608-SHAB12CD34", Invoices.Number(b));
        // Same booking, same number every time it is rendered.
        Assert.Equal(Invoices.Number(b), Invoices.Number(b));
    }

    [Fact]
    public void A_fully_paid_booking_shows_the_whole_total_as_paid()
    {
        var b = Booking(3_000_000m);
        Assert.Equal(3_000_000m, Invoices.AmountPaid(b));
        Assert.False(Invoices.HasBalanceDue(b));
    }

    [Fact]
    public void A_deposit_booking_shows_only_what_has_been_paid()
    {
        // docs/01 ĐP-06 — half now, the rest scheduled. The invoice must not claim
        // the whole total was collected when only the deposit was.
        var b = Booking(3_000_000m, balanceDue: 1_500_000m);
        Assert.Equal(1_500_000m, Invoices.AmountPaid(b));
        Assert.True(Invoices.HasBalanceDue(b));
    }

    [Fact]
    public void Amount_paid_never_goes_negative()
    {
        // A balance somehow larger than the total must not print as a negative
        // "paid" figure.
        var b = Booking(1_000_000m, balanceDue: 2_000_000m);
        Assert.Equal(0m, Invoices.AmountPaid(b));
    }
}
