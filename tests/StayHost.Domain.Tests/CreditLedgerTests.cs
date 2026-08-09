namespace StayHost.Domain.Tests;

/// <summary>
/// docs/01 TC-07 and docs/07 §3 — balance that lapses, and the rule that spending
/// takes from whatever lapses soonest.
///
/// The lifetime itself is not decided here: docs/07 §15.1 leaves the number to the
/// customer, and every setting ships unset. What is tested is that the machinery
/// is right once one is chosen, and that with none chosen the balance behaves
/// exactly as it did before any of this existed.
/// </summary>
public class CreditLedgerTests
{
    private static readonly DateTime Day1 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static CreditEntry Grant(long id, decimal amount, DateTime at, DateTime? expires = null) =>
        new() { Id = id, UserId = 1, Amount = amount, Reason = CreditReason.Goodwill, CreatedAt = at, ExpiresAt = expires };

    private static CreditEntry Spend(long id, decimal amount, DateTime at) =>
        new() { Id = id, UserId = 1, Amount = -amount, Reason = CreditReason.Spent, CreatedAt = at };

    private static CreditEntry Sweep(long id, decimal amount, DateTime at) =>
        new() { Id = id, UserId = 1, Amount = -amount, Reason = CreditReason.Expired, CreatedAt = at };

    /* ---- nothing expires unless somebody chose a lifetime ---- */

    [Fact]
    public void Out_of_the_box_no_kind_of_balance_expires()
    {
        // The shipped default has to match the platform's behaviour before
        // TC-07, or deploying this quietly starts taking money off guests.
        var settings = new CreditSettings();

        Assert.True(settings.NothingExpires);
        Assert.Null(settings.ExpiryFor(CreditReason.GiftCard, Day1));
        Assert.Null(settings.ExpiryFor(CreditReason.Goodwill, Day1));
        Assert.Null(settings.ExpiryFor(CreditReason.Referral, Day1));
        Assert.Null(settings.ExpiryFor(CreditReason.Returned, Day1));
    }

    [Fact]
    public void A_chosen_lifetime_is_counted_in_months_from_the_grant()
    {
        var settings = new CreditSettings { GoodwillMonths = 12 };

        Assert.Equal(Day1.AddMonths(12), settings.ExpiryFor(CreditReason.Goodwill, Day1));
        Assert.False(settings.NothingExpires);
        // Choosing one kind does not silently expire the others.
        Assert.Null(settings.ExpiryFor(CreditReason.GiftCard, Day1));
    }

    [Fact]
    public void A_lifetime_of_zero_months_is_read_as_no_lifetime()
    {
        // Otherwise a stray 0 in configuration would expire every grant the
        // instant it was made.
        Assert.Null(new CreditSettings { GoodwillMonths = 0 }.ExpiryFor(CreditReason.Goodwill, Day1));
    }

    [Fact]
    public void With_no_expiry_the_balance_is_still_just_the_sum_of_the_rows()
    {
        var entries = new[]
        {
            Grant(1, 500_000m, Day1),
            Spend(2, 200_000m, Day1.AddDays(3)),
            Grant(3, 100_000m, Day1.AddDays(5))
        };

        Assert.Equal(400_000m, CreditLedger.Available(entries, Day1.AddYears(5)));
        Assert.Equal(0m, CreditLedger.Lapsed(entries, Day1.AddYears(5)));
        Assert.Null(CreditLedger.NextExpiry(entries, Day1));
    }

    /* ---- docs/07 §3: soonest to lapse goes first ---- */

    [Fact]
    public void Spending_takes_from_the_grant_that_lapses_soonest()
    {
        // The later grant is the one that expires first, so spending must reach
        // past the older one to get at it — otherwise the guest loses the money
        // that was about to lapse and keeps the money that was not.
        var entries = new[]
        {
            Grant(1, 300_000m, Day1, expires: Day1.AddMonths(12)),
            Grant(2, 300_000m, Day1.AddDays(1), expires: Day1.AddMonths(2)),
            Spend(3, 300_000m, Day1.AddDays(2))
        };

        var lots = CreditLedger.Lots(entries);

        Assert.Equal(0m, lots.Single(l => l.EntryId == 2).Remaining);
        Assert.Equal(300_000m, lots.Single(l => l.EntryId == 1).Remaining);
    }

    [Fact]
    public void Balance_that_never_lapses_is_spent_last()
    {
        var entries = new[]
        {
            Grant(1, 200_000m, Day1),
            Grant(2, 200_000m, Day1.AddDays(1), expires: Day1.AddMonths(3)),
            Spend(3, 200_000m, Day1.AddDays(2))
        };

        var lots = CreditLedger.Lots(entries);

        Assert.Equal(0m, lots.Single(l => l.EntryId == 2).Remaining);
        Assert.Equal(200_000m, lots.Single(l => l.EntryId == 1).Remaining);
    }

    [Fact]
    public void A_spend_larger_than_one_grant_runs_across_several_in_order()
    {
        var entries = new[]
        {
            Grant(1, 100_000m, Day1, expires: Day1.AddMonths(1)),
            Grant(2, 100_000m, Day1, expires: Day1.AddMonths(2)),
            Grant(3, 100_000m, Day1),
            Spend(4, 250_000m, Day1.AddDays(1))
        };

        var lots = CreditLedger.Lots(entries);

        Assert.Equal(0m, lots.Single(l => l.EntryId == 1).Remaining);
        Assert.Equal(0m, lots.Single(l => l.EntryId == 2).Remaining);
        Assert.Equal(50_000m, lots.Single(l => l.EntryId == 3).Remaining);
    }

    /* ---- lapsed balance ---- */

    [Fact]
    public void Balance_stops_being_spendable_the_moment_it_lapses()
    {
        // Not when the sweep gets round to it. The sweep runs on a timer; a guest
        // reaching checkout in between must not be offered money that has expired.
        var entries = new[] { Grant(1, 400_000m, Day1, expires: Day1.AddMonths(1)) };

        Assert.Equal(400_000m, CreditLedger.Available(entries, Day1.AddDays(20)));
        Assert.Equal(0m, CreditLedger.Available(entries, Day1.AddMonths(2)));
        Assert.Equal(400_000m, CreditLedger.Lapsed(entries, Day1.AddMonths(2)));
    }

    [Fact]
    public void Only_the_unspent_remainder_of_a_lapsed_grant_is_lost()
    {
        var entries = new[]
        {
            Grant(1, 400_000m, Day1, expires: Day1.AddMonths(1)),
            Spend(2, 150_000m, Day1.AddDays(2))
        };

        var due = CreditLedger.DueToExpire(entries, Day1.AddMonths(2));

        Assert.Equal(250_000m, Assert.Single(due).Remaining);
    }

    [Fact]
    public void A_grant_already_spent_to_nothing_leaves_the_sweep_no_work()
    {
        var entries = new[]
        {
            Grant(1, 400_000m, Day1, expires: Day1.AddMonths(1)),
            Spend(2, 400_000m, Day1.AddDays(2))
        };

        Assert.Empty(CreditLedger.DueToExpire(entries, Day1.AddMonths(2)));
    }

    [Fact]
    public void Once_the_sweep_has_written_its_row_the_balance_is_the_sum_again()
    {
        var swept = Day1.AddMonths(1).AddHours(1);

        var entries = new[]
        {
            Grant(1, 400_000m, Day1, expires: Day1.AddMonths(1)),
            Grant(2, 100_000m, Day1),
            Sweep(3, 400_000m, swept)
        };

        Assert.Equal(100_000m, entries.Sum(e => e.Amount));
        Assert.Equal(100_000m, CreditLedger.Available(entries, swept.AddDays(1)));
        Assert.Equal(0m, CreditLedger.Lapsed(entries, swept.AddDays(1)));
        Assert.Empty(CreditLedger.DueToExpire(entries, swept.AddDays(1)));
    }

    [Fact]
    public void The_sweep_takes_the_lapsed_grant_and_not_the_live_one()
    {
        // Written as an ordinary withdrawal it would have taken the live grant
        // first, leaving the expired one on the books and the guest short.
        var swept = Day1.AddMonths(1).AddHours(1);

        var entries = new[]
        {
            Grant(1, 400_000m, Day1, expires: Day1.AddMonths(1)),
            Grant(2, 100_000m, Day1),
            Sweep(3, 400_000m, swept)
        };

        var lots = CreditLedger.Lots(entries);

        Assert.Equal(0m, lots.Single(l => l.EntryId == 1).Remaining);
        Assert.Equal(100_000m, lots.Single(l => l.EntryId == 2).Remaining);
    }

    [Fact]
    public void A_spend_never_reaches_balance_that_had_already_lapsed()
    {
        var entries = new[]
        {
            Grant(1, 300_000m, Day1, expires: Day1.AddMonths(1)),
            Grant(2, 300_000m, Day1),
            Spend(3, 300_000m, Day1.AddMonths(2))
        };

        var lots = CreditLedger.Lots(entries);

        Assert.Equal(300_000m, lots.Single(l => l.EntryId == 1).Remaining);
        Assert.Equal(0m, lots.Single(l => l.EntryId == 2).Remaining);
    }

    /* ---- the rows always add up ---- */

    [Fact]
    public void What_is_left_of_the_grants_always_equals_the_sum_of_the_rows()
    {
        // The balance is the sum of an append-only run (docs/00 §6.1). Any
        // attribution that loses or invents a dong breaks the one property that
        // makes it explainable, including on rows written before expiry existed
        // and on a withdrawal larger than anything still live.
        var entries = new[]
        {
            Grant(1, 100_000m, Day1, expires: Day1.AddMonths(1)),
            Grant(2, 50_000m, Day1),
            Spend(3, 120_000m, Day1.AddMonths(2))
        };

        Assert.Equal(entries.Sum(e => e.Amount), CreditLedger.Lots(entries).Sum(l => l.Remaining));
    }

    /* ---- telling the guest ---- */

    [Fact]
    public void The_next_date_something_lapses_is_the_soonest_one_still_holding_money()
    {
        var entries = new[]
        {
            Grant(1, 100_000m, Day1, expires: Day1.AddMonths(6)),
            Grant(2, 200_000m, Day1, expires: Day1.AddMonths(2)),
            Grant(3, 300_000m, Day1)
        };

        var next = CreditLedger.NextExpiry(entries, Day1);

        Assert.Equal(Day1.AddMonths(2), next);
        Assert.Equal(200_000m, CreditLedger.ExpiringOn(entries, next!.Value));
    }

    [Fact]
    public void A_date_already_past_is_not_offered_as_the_next_one()
    {
        var entries = new[]
        {
            Grant(1, 100_000m, Day1, expires: Day1.AddMonths(1)),
            Grant(2, 200_000m, Day1, expires: Day1.AddMonths(9))
        };

        Assert.Equal(Day1.AddMonths(9), CreditLedger.NextExpiry(entries, Day1.AddMonths(2)));
    }
}
