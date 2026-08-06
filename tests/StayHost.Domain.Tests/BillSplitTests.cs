using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>docs/01 ĐP-07 — one booking split between up to sixteen people.</summary>
public class BillSplitTests
{
    [Fact]
    public void An_even_total_divides_evenly()
    {
        var shares = BillSplitRules.Divide(4_000_000m, 4);

        Assert.Equal(4, shares.Count);
        Assert.All(shares, s => Assert.Equal(1_000_000m, s));
    }

    [Fact]
    public void The_odd_dong_lands_on_the_organiser_not_on_a_friend()
    {
        var shares = BillSplitRules.Divide(1_000_000m, 3);

        Assert.Equal(1_000_000m, shares.Sum());
        Assert.Equal(333_334m, shares[0]);
        Assert.Equal(333_333m, shares[1]);
        Assert.Equal(333_333m, shares[2]);
    }

    [Fact]
    public void Shares_always_add_back_up_to_the_total()
    {
        foreach (var people in Enumerable.Range(1, BillSplitRules.MaxPeople))
        {
            var shares = BillSplitRules.Divide(9_876_543m, people);

            Assert.Equal(people, shares.Count);
            Assert.Equal(9_876_543m, shares.Sum());
        }
    }

    [Fact]
    public void Splitting_between_nobody_is_a_mistake_rather_than_a_zero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BillSplitRules.Divide(1_000_000m, 0));
    }

    [Fact]
    public void The_window_runs_out_exactly_a_day_later()
    {
        var opened = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var expires = opened + BillSplitRules.Window;

        Assert.False(BillSplitRules.Expired(expires, opened.AddHours(23)));
        Assert.True(BillSplitRules.Expired(expires, opened.AddHours(24)));
    }

    [Fact]
    public void Only_a_collecting_split_takes_more_money()
    {
        Assert.True(BillSplitRules.IsOpen(BillSplitStatus.Collecting));
        Assert.False(BillSplitRules.IsOpen(BillSplitStatus.Complete));
        Assert.False(BillSplitRules.IsOpen(BillSplitStatus.Expired));
        Assert.False(BillSplitRules.IsOpen(BillSplitStatus.Cancelled));
    }

    [Fact]
    public void A_share_held_and_then_released_leaves_the_books_flat()
    {
        var booking = new Booking { Id = 7, Reference = "SH-7" };

        var held = BillSplitRules.Divide(3_000_000m, 3)
            .SelectMany(a => Ledger.HoldShare(booking.Id, booking.Reference, a, DateTime.UtcNow))
            .ToList();
        var released = Ledger.ReleaseEscrow(booking, 3_000_000m, DateTime.UtcNow);

        Assert.Equal(0m, Ledger.Imbalance(held));
        Assert.Equal(0m, Ledger.Imbalance([.. held, .. released]));

        // Nothing is left sitting in escrow once the booking goes through.
        Assert.Equal(0m, Net(held.Concat(released), LedgerAccount.SplitEscrow));
    }

    [Fact]
    public void Giving_the_shares_back_leaves_nothing_behind_either()
    {
        var booking = new Booking { Id = 7, Reference = "SH-7" };

        var held = Ledger.HoldShare(booking.Id, booking.Reference, 1_500_000m, DateTime.UtcNow);
        var returned = Ledger.ReturnShare(booking.Id, booking.Reference, 1_500_000m, DateTime.UtcNow);

        Assert.Equal(0m, Ledger.Imbalance([.. held, .. returned]));
        Assert.Equal(0m, Net(held.Concat(returned), LedgerAccount.SplitEscrow));
        Assert.Equal(0m, Net(held.Concat(returned), LedgerAccount.GuestFunds));
    }

    [Fact]
    public void Every_status_has_wording_of_its_own()
    {
        foreach (var status in Enum.GetValues<BillSplitStatus>())
            Assert.False(string.IsNullOrWhiteSpace(BillSplitRules.Label(status)));

        foreach (var status in Enum.GetValues<BillShareStatus>())
            Assert.False(string.IsNullOrWhiteSpace(BillSplitRules.ShareLabel(status)));
    }

    private static decimal Net(IEnumerable<LedgerEntry> entries, LedgerAccount account) =>
        entries.Where(e => e.Account == account)
               .Sum(e => e.Direction == LedgerDirection.Debit ? e.Amount : -e.Amount);
}
