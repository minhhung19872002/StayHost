using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>
/// docs/01 AT-04 and QT-05, and acceptance scenario 10 of docs/04: a claim, an
/// objection, an admin's ruling, and books that still balance afterwards.
/// </summary>
public class ResolutionTests
{
    private static Booking MakeBooking() => new()
    {
        Id = 1,
        Reference = "SHTEST",
        Nights = 3,
        CleaningFee = 300_000m,
        Subtotal = 3_300_000m,
        ServiceFee = 462_000m,
        Tax = 300_000m,
        Total = 4_062_000m,
        HostServiceFee = 99_000m,
        HostPayout = 3_201_000m
    };

    private static ResolutionCase MakeCase(ResolutionStatus status = ResolutionStatus.AwaitingResponse) =>
        new() { Id = 1, Reference = "HSTEST", BookingId = 1, Status = status };

    /* ------------------------------------------------------ the state machine */

    [Theory]
    [InlineData(ResolutionStatus.AwaitingResponse, ResolutionStatus.Accepted)]
    [InlineData(ResolutionStatus.AwaitingResponse, ResolutionStatus.Disputed)]
    [InlineData(ResolutionStatus.AwaitingResponse, ResolutionStatus.Withdrawn)]
    [InlineData(ResolutionStatus.Disputed, ResolutionStatus.Resolved)]
    [InlineData(ResolutionStatus.Accepted, ResolutionStatus.Resolved)]
    public void Legal_transitions_are_allowed(ResolutionStatus from, ResolutionStatus to)
    {
        Assert.True(Resolutions.CanTransition(from, to));
    }

    [Theory]
    // Straight to resolved without anyone answering.
    [InlineData(ResolutionStatus.AwaitingResponse, ResolutionStatus.Resolved)]
    // Back out of a terminal state.
    [InlineData(ResolutionStatus.Resolved, ResolutionStatus.Disputed)]
    [InlineData(ResolutionStatus.Withdrawn, ResolutionStatus.Accepted)]
    public void Illegal_transitions_are_refused(ResolutionStatus from, ResolutionStatus to)
    {
        Assert.False(Resolutions.CanTransition(from, to));

        var kase = MakeCase(from);
        Assert.Throws<Resolutions.IllegalTransitionException>(
            () => Resolutions.Transition(kase, to, "admin:1", "should not happen"));
        Assert.Equal(from, kase.Status);
    }

    [Fact]
    public void Every_move_appends_a_history_row()
    {
        var kase = MakeCase();

        Resolutions.Transition(kase, ResolutionStatus.Disputed, "guest:9", "Không đồng ý.");
        Resolutions.Transition(kase, ResolutionStatus.Resolved, "admin:1", "Chia đôi trách nhiệm.");

        Assert.Equal(2, kase.Events.Count);
        Assert.Equal(ResolutionStatus.AwaitingResponse, kase.Events[0].FromStatus);
        Assert.Equal("admin:1", kase.Events[1].Actor);
    }

    /* ------------------------------------------------------------- the amount */

    [Fact]
    public void A_claim_can_never_exceed_what_the_booking_was_worth()
    {
        var booking = MakeBooking();

        Assert.Equal(booking.Total, Resolutions.Clamp(99_000_000m, booking));
        Assert.Equal(0m, Resolutions.Clamp(-500_000m, booking));
        Assert.Equal(600_000m, Resolutions.Clamp(600_000m, booking));
    }

    [Fact]
    public void Amounts_are_rounded_to_whole_dong()
    {
        Assert.Equal(600_001m, Resolutions.Clamp(600_000.5m, MakeBooking()));
    }

    /* ------------------------------------------------------------ the ledger */

    [Fact]
    public void An_award_to_the_host_balances()
    {
        var entries = Ledger.SettleClaim(MakeBooking(), toGuest: 0m, toHost: 600_000m, DateTime.UtcNow);

        Assert.Equal(0m, Ledger.Imbalance(entries));
        Assert.Contains(entries, e => e.Account == LedgerAccount.HostPayable && e.Direction == LedgerDirection.Credit);
    }

    [Fact]
    public void An_award_to_the_guest_balances_and_comes_out_of_the_host()
    {
        var entries = Ledger.SettleClaim(MakeBooking(), toGuest: 800_000m, toHost: 0m, DateTime.UtcNow);

        Assert.Equal(0m, Ledger.Imbalance(entries));
        Assert.Contains(entries, e => e.Account == LedgerAccount.HostPayable && e.Direction == LedgerDirection.Debit);
        Assert.Contains(entries, e => e.Account == LedgerAccount.GuestRefundPayable);
    }

    [Fact]
    public void Awarding_nothing_writes_nothing()
    {
        Assert.Empty(Ledger.SettleClaim(MakeBooking(), 0m, 0m, DateTime.UtcNow));
    }

    /* ----------------------------------------------------------------- labels */

    [Fact]
    public void Every_status_and_kind_has_a_label()
    {
        foreach (var s in Enum.GetValues<ResolutionStatus>())
        {
            Assert.False(string.IsNullOrWhiteSpace(Resolutions.Label(s)));
            Assert.Contains(Resolutions.BadgeClass(s), new[] { "pending", "confirmed", "cancelled" });
        }

        foreach (var k in Enum.GetValues<ResolutionKind>())
            Assert.False(string.IsNullOrWhiteSpace(Resolutions.KindLabel(k)));
    }

    [Fact]
    public void The_other_party_gets_twenty_four_hours()
    {
        Assert.Equal(TimeSpan.FromHours(24), Resolutions.ResponseWindow);
    }
}
