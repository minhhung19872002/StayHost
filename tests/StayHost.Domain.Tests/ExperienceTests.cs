using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>docs/01 MR-01 → MR-04 — sessions sold by the seat.</summary>
public class ExperienceTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

    private static Experience Make(decimal price = 500_000m, decimal? priv = null, int min = 2, int max = 8) =>
        new()
        {
            Id = 1, Title = "Lớp nấu ăn", City = "Hội An", Country = "Việt Nam",
            PricePerPerson = price, PrivateGroupPrice = priv, MinGuests = min, MaxGroup = max
        };

    private static ExperienceSlot Slot(int capacity = 8, int taken = 0, int hoursAhead = 72, bool priv = false) =>
        new()
        {
            Id = 1, ExperienceId = 1, Capacity = capacity, SeatsTaken = taken,
            StartsAt = Now.AddHours(hoursAhead), IsPrivate = priv
        };

    /* ------------------------------------------------------------- MR-02 */

    [Fact]
    public void A_seat_can_be_taken_while_there_is_room()
    {
        Assert.True(ExperienceRules.CanBook(Make(), Slot(taken: 5), 3, false, Now).Ok);
    }

    [Fact]
    public void One_seat_too_many_is_refused_with_the_number_left()
    {
        var check = ExperienceRules.CanBook(Make(), Slot(taken: 6), 3, false, Now);

        Assert.False(check.Ok);
        Assert.Equal(ExperienceRules.Refusal.NotEnoughSeats, check.Reason);
        Assert.Contains("2", check.Message);
    }

    [Fact]
    public void A_session_that_already_started_takes_nobody()
    {
        Assert.Equal(
            ExperienceRules.Refusal.AlreadyStarted,
            ExperienceRules.CanBook(Make(), Slot(hoursAhead: -1), 1, false, Now).Reason);
    }

    [Fact]
    public void A_cancelled_session_takes_nobody_either()
    {
        var slot = Slot();
        slot.Status = SlotStatus.Cancelled;

        Assert.Equal(
            ExperienceRules.Refusal.SlotCancelled,
            ExperienceRules.CanBook(Make(), slot, 1, false, Now).Reason);
    }

    /* ------------------------------------------------------------- MR-03 */

    [Fact]
    public void A_private_booking_needs_the_host_to_offer_one()
    {
        Assert.Equal(
            ExperienceRules.Refusal.PrivateNotOffered,
            ExperienceRules.CanBook(Make(priv: null), Slot(), 4, true, Now).Reason);

        Assert.True(ExperienceRules.CanBook(Make(priv: 3_000_000m), Slot(), 4, true, Now).Ok);
    }

    [Fact]
    public void A_private_booking_cannot_join_a_session_that_already_has_people()
    {
        Assert.Equal(
            ExperienceRules.Refusal.PrivateNeedsEmptySlot,
            ExperienceRules.CanBook(Make(priv: 3_000_000m), Slot(taken: 1), 4, true, Now).Reason);
    }

    [Fact]
    public void Nobody_else_joins_a_session_that_was_taken_privately()
    {
        Assert.Equal(
            ExperienceRules.Refusal.PrivateNeedsEmptySlot,
            ExperienceRules.CanBook(Make(priv: 3_000_000m), Slot(priv: true), 1, false, Now).Reason);
    }

    [Fact]
    public void A_private_group_is_one_price_not_a_price_each()
    {
        var shared = Pricing.QuoteExperience(new Pricing.ExperienceRequest
        {
            Experience = Make(priv: 3_000_000m), Seats = 4, Private = false, StartsAt = Now.AddDays(3)
        });
        var priv = Pricing.QuoteExperience(new Pricing.ExperienceRequest
        {
            Experience = Make(priv: 3_000_000m), Seats = 4, Private = true, StartsAt = Now.AddDays(3)
        });

        Assert.Equal(2_000_000m, shared.Subtotal);
        Assert.Equal(3_000_000m, priv.Subtotal);
    }

    /* ------------------------------------------------------------ pricing */

    [Fact]
    public void A_ticket_carries_the_same_fee_rates_as_a_stay()
    {
        var price = Pricing.QuoteExperience(new Pricing.ExperienceRequest
        {
            Experience = Make(), Seats = 2, StartsAt = Now.AddDays(3)
        });

        Assert.Equal(1_000_000m, price.Subtotal);
        Assert.Equal(140_000m, price.GuestServiceFee);   // 14% of the subtotal
        Assert.Equal(30_000m, price.HostServiceFee);     //  3% of the subtotal
        Assert.Equal(970_000m, price.HostPayout);
        Assert.Equal(1_140_000m, price.Total);
    }

    [Fact]
    public void The_lines_shown_add_up_to_the_total()
    {
        var price = Pricing.QuoteExperience(new Pricing.ExperienceRequest
        {
            Experience = Make(), Seats = 3, StartsAt = Now.AddDays(3),
            TaxRules = [new TaxRule
            {
                Id = 1, Country = "Việt Nam", City = "Hội An", Name = "VAT",
                Method = TaxMethod.Percentage, Value = 0.08m, Base = TaxBase.Subtotal
            }]
        });

        Assert.Equal(price.Total, price.Lines.Sum(l => l.Amount));
        Assert.Equal(120_000m, price.Tax);
    }

    [Fact]
    public void A_nightly_levy_does_not_apply_to_something_sold_by_the_seat()
    {
        var price = Pricing.QuoteExperience(new Pricing.ExperienceRequest
        {
            Experience = Make(), Seats = 2, StartsAt = Now.AddDays(3),
            TaxRules = [new TaxRule
            {
                Id = 1, Country = "Việt Nam", Name = "Phí lưu trú mỗi đêm",
                Method = TaxMethod.PerNight, Value = 50_000m
            }]
        });

        Assert.Equal(0m, price.Tax);
    }

    /* ------------------------------------------------------------- MR-04 */

    [Fact]
    public void A_session_short_of_its_minimum_inside_the_window_is_called_off()
    {
        Assert.True(ExperienceRules.ShouldCallOff(Make(min: 4), Slot(taken: 2, hoursAhead: 24), Now));
    }

    [Fact]
    public void The_same_session_further_out_is_left_alone()
    {
        Assert.False(ExperienceRules.ShouldCallOff(Make(min: 4), Slot(taken: 2, hoursAhead: 96), Now));
    }

    [Fact]
    public void A_session_that_reached_its_minimum_runs()
    {
        Assert.False(ExperienceRules.ShouldCallOff(Make(min: 4), Slot(taken: 4, hoursAhead: 24), Now));
    }

    [Fact]
    public void A_private_session_never_needs_a_minimum()
    {
        Assert.False(ExperienceRules.ShouldCallOff(Make(min: 6), Slot(taken: 8, hoursAhead: 12, priv: true), Now));
    }

    /* --------------------------------------------------------- refunds */

    [Fact]
    public void A_guest_who_pulls_out_a_day_ahead_gets_everything_back()
    {
        var booking = new ExperienceBooking { Total = 1_140_000m };

        Assert.Equal(1_140_000m, ExperienceRules.GuestRefund(booking, Now.AddHours(25), Now));
        Assert.Equal(0m, ExperienceRules.GuestRefund(booking, Now.AddHours(23), Now));
    }

    [Fact]
    public void Refunding_a_ticket_leaves_the_books_flat()
    {
        var price = Pricing.QuoteExperience(new Pricing.ExperienceRequest
        {
            Experience = Make(), Seats = 3, StartsAt = Now.AddDays(3),
            TaxRules = [new TaxRule
            {
                Id = 1, Country = "Việt Nam", Name = "VAT",
                Method = TaxMethod.Percentage, Value = 0.08m, Base = TaxBase.Subtotal
            }]
        });

        var booking = new ExperienceBooking
        {
            Id = 1, Reference = "XP1", Seats = 3,
            Subtotal = price.Subtotal, ServiceFee = price.GuestServiceFee, Tax = price.Tax,
            Total = price.Total, HostServiceFee = price.HostServiceFee, HostPayout = price.HostPayout
        };

        var captured = Ledger.CaptureExperience(booking, price, Now);
        var refunded = Ledger.RefundExperience(booking, booking.Total, Now);

        Assert.Equal(0m, Ledger.Imbalance(captured));
        Assert.Equal(0m, Ledger.Imbalance(refunded));

        // A full refund leaves nothing behind in any account.
        foreach (var account in Enum.GetValues<LedgerAccount>())
            Assert.Equal(0m, Net(captured.Concat(refunded), account));
    }

    [Fact]
    public void A_partial_refund_still_balances()
    {
        var booking = new ExperienceBooking
        {
            Id = 1, Reference = "XP1",
            Subtotal = 1_000_000m, ServiceFee = 140_000m, Tax = 80_000m,
            Total = 1_220_000m, HostServiceFee = 30_000m, HostPayout = 970_000m
        };

        Assert.Equal(0m, Ledger.Imbalance(Ledger.RefundExperience(booking, 610_000m, Now)));
        Assert.Empty(Ledger.RefundExperience(booking, 0m, Now));
    }

    private static decimal Net(IEnumerable<LedgerEntry> entries, LedgerAccount account) =>
        entries.Where(e => e.Account == account).Sum(e => e.Signed);
}
