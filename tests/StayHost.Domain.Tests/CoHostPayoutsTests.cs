namespace StayHost.Domain.Tests;

/// <summary>
/// docs/02 G8, docs/07 §19 — dividing what an owner earned between them and the
/// people helping run the place.
///
/// The rules follow Airbnb's, which the customer chose by name on 03/09/2026.
/// Two of them are the whole reason this file exists: the division can never
/// hand out more than the booking earned, and a booking that shrank pays a
/// share of the smaller figure. Get either wrong and the platform is paying
/// people out of somebody else's money — the mistake this repo has already made
/// once with GuestFunds.
/// </summary>
public class CoHostPayoutsTests
{
    private static CoHostPayouts.Terms Percent(int id, decimal percent) =>
        new(id, CoHostPayoutKind.Percent, percent);

    private static CoHostPayouts.Terms WithCleaning(int id, decimal percent) =>
        new(id, CoHostPayoutKind.PercentWithCleaning, percent);

    private static CoHostPayouts.Terms Fixed(int id, decimal amount) =>
        new(id, CoHostPayoutKind.Fixed, 0m, amount);

    private static decimal AmountFor(CoHostPayouts.Split split, int coHostId) =>
        split.Shares.FirstOrDefault(s => s.CoHostId == coHostId).Amount;

    /* ---------------------------------------------------- what each kind takes */

    [Fact]
    public void A_percentage_leaves_the_cleaning_fee_out_of_its_base()
    {
        // 10.000.000 earned, of which 1.000.000 came from the cleaning fee.
        // Twenty percent of the other nine million is 1.800.000 — not two
        // million, which is what taking the percentage off the whole pot would
        // give and what an owner would rightly dispute.
        var split = CoHostPayouts.Allocate(10_000_000m, 1_000_000m, [Percent(1, 20)]);

        Assert.Equal(1_800_000m, AmountFor(split, 1));
        Assert.Equal(8_200_000m, split.ToHost);
    }

    [Fact]
    public void The_other_percentage_kind_does_include_it()
    {
        var split = CoHostPayouts.Allocate(10_000_000m, 1_000_000m, [WithCleaning(1, 20)]);

        Assert.Equal(2_000_000m, AmountFor(split, 1));
    }

    [Fact]
    public void The_cleaning_fee_kind_takes_the_cleaning_fee_and_nothing_else()
    {
        var split = CoHostPayouts.Allocate(10_000_000m, 1_000_000m,
            [new CoHostPayouts.Terms(1, CoHostPayoutKind.CleaningFee)]);

        Assert.Equal(1_000_000m, AmountFor(split, 1));
        Assert.Equal(9_000_000m, split.ToHost);
    }

    [Fact]
    public void The_cleaning_fee_plus_a_percentage_takes_both()
    {
        var split = CoHostPayouts.Allocate(10_000_000m, 1_000_000m,
            [new CoHostPayouts.Terms(1, CoHostPayoutKind.CleaningFeePlusPercent, 10m)]);

        // The whole cleaning share, plus a tenth of the nine million left.
        Assert.Equal(1_900_000m, AmountFor(split, 1));
    }

    [Fact]
    public void A_flat_amount_ignores_how_big_the_booking_was()
    {
        var big = CoHostPayouts.Allocate(10_000_000m, 0m, [Fixed(1, 300_000m)]);
        var small = CoHostPayouts.Allocate(2_000_000m, 0m, [Fixed(1, 300_000m)]);

        Assert.Equal(300_000m, AmountFor(big, 1));
        Assert.Equal(300_000m, AmountFor(small, 1));
    }

    /* ------------------------------------------------------ never invent money */

    [Fact]
    public void The_shares_and_the_owners_remainder_always_add_back_to_the_earnings()
    {
        // Whatever the mix, the total handed out is the total that came in. This
        // is what keeps the ledger balanced: the owner's posting is reduced by
        // exactly what the co-hosts' postings add up to.
        var split = CoHostPayouts.Allocate(7_777_777m, 640_000m,
            [Percent(1, 15), WithCleaning(2, 12.5m), Fixed(3, 250_000m)]);

        Assert.Equal(7_777_777m, split.ToCoHosts + split.ToHost);
    }

    [Fact]
    public void Nobody_can_be_promised_more_than_the_booking_earned()
    {
        // Three people on 50% each. The first two are paid in full, the third
        // gets what is left, and the owner gets nothing — which is harsh and is
        // exactly what Airbnb does, because the alternative is paying somebody
        // with money the guest never handed over.
        var split = CoHostPayouts.Allocate(1_000_000m, 0m,
            [Percent(1, 50), Percent(2, 50), Percent(3, 50)]);

        Assert.Equal(500_000m, AmountFor(split, 1));
        Assert.Equal(500_000m, AmountFor(split, 2));
        Assert.Equal(0m, AmountFor(split, 3));
        Assert.Equal(0m, split.ToHost);
        Assert.Equal(1_000_000m, split.ToCoHosts);
    }

    [Fact]
    public void A_booking_that_earned_nothing_pays_nobody()
    {
        // A stay refunded down to zero. Not an error, and not a negative share:
        // there is simply nothing to divide.
        var split = CoHostPayouts.Allocate(0m, 0m, [Percent(1, 20), Fixed(2, 500_000m)]);

        Assert.Empty(split.Shares);
        Assert.Equal(0m, split.ToHost);
    }

    [Fact]
    public void A_cancelled_stay_pays_a_share_of_what_survived()
    {
        // docs/07 §19.4, and Airbnb's own worked example: a booking cut from
        // 150 to 50 pays a 10% co-host 5, not 15. Falling out of this rule is
        // how a co-host profits from a cancellation.
        var whole = CoHostPayouts.Allocate(150m, 0m, [Percent(1, 10)]);
        var cut = CoHostPayouts.Allocate(50m, 0m, [Percent(1, 10)]);

        Assert.Equal(15m, AmountFor(whole, 1));
        Assert.Equal(5m, AmountFor(cut, 1));
    }

    /* --------------------------------------------------------- who goes short */

    [Fact]
    public void The_order_is_cleaning_then_percentages_then_flat_amounts()
    {
        var ordered = CoHostPayouts.Ordered([
            Fixed(1, 900_000m),
            WithCleaning(2, 5m),
            Percent(3, 5m),
            new CoHostPayouts.Terms(4, CoHostPayoutKind.CleaningFee)
        ]).Select(t => t.CoHostId).ToList();

        Assert.Equal([4, 3, 2, 1], ordered);
    }

    [Fact]
    public void Inside_a_kind_the_larger_claim_is_paid_first()
    {
        var ordered = CoHostPayouts.Ordered([Percent(1, 5m), Percent(2, 30m), Percent(3, 15m)])
            .Select(t => t.CoHostId).ToList();

        Assert.Equal([2, 3, 1], ordered);
    }

    [Fact]
    public void Two_identical_claims_always_resolve_the_same_way_round()
    {
        // Otherwise which of two equal co-hosts goes short depends on the order
        // the database happened to return them, and the same booking divides
        // differently on a retry.
        var one = CoHostPayouts.Ordered([Percent(7, 20m), Percent(3, 20m)]).Select(t => t.CoHostId);
        var other = CoHostPayouts.Ordered([Percent(3, 20m), Percent(7, 20m)]).Select(t => t.CoHostId);

        Assert.Equal(one, other);
        Assert.Equal([3, 7], one.ToList());
    }

    [Fact]
    public void Terms_that_share_nothing_are_left_out_entirely()
    {
        var split = CoHostPayouts.Allocate(1_000_000m, 0m,
            [new CoHostPayouts.Terms(1, CoHostPayoutKind.None), Percent(2, 10m)]);

        Assert.Single(split.Shares);
        Assert.Equal(100_000m, AmountFor(split, 2));
    }

    /* ------------------------------------------------------ the cleaning share */

    [Fact]
    public void The_cleaning_share_is_net_of_the_service_fee_the_owner_paid_on_it()
    {
        // Subtotal 10.000.000 of which cleaning is 1.000.000; the host fee of 3%
        // leaves 9.700.000. The cleaning fee did not reach the owner whole, so a
        // co-host paid "the cleaning fee" cannot be paid more of it than the
        // owner actually received.
        var share = CoHostPayouts.CleaningShare(1_000_000m, 10_000_000m, 9_700_000m);

        Assert.Equal(970_000m, share);
        Assert.True(share < 1_000_000m);
    }

    [Fact]
    public void The_cleaning_share_never_exceeds_the_whole_payout()
    {
        // A listing that is nothing but a cleaning fee. Nonsense as a listing,
        // but it must not produce a share bigger than the money.
        var share = CoHostPayouts.CleaningShare(5_000_000m, 1_000_000m, 970_000m);

        Assert.Equal(970_000m, share);
    }

    [Fact]
    public void No_cleaning_fee_means_no_cleaning_share()
    {
        Assert.Equal(0m, CoHostPayouts.CleaningShare(0m, 10_000_000m, 9_700_000m));
        Assert.Equal(0m, CoHostPayouts.CleaningShare(1_000_000m, 0m, 0m));
    }

    /* ------------------------------------------------------------- the offer */

    [Fact]
    public void A_proposal_lapses_after_fourteen_days()
    {
        var sent = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        Assert.False(CoHostPayouts.ProposalExpired(sent, sent.AddDays(13)));
        Assert.True(CoHostPayouts.ProposalExpired(sent, sent.AddDays(14)));
        Assert.Equal(sent.AddDays(14), CoHostPayouts.ConfirmBy(sent));
    }

    [Fact]
    public void Terms_that_make_no_sense_are_named_rather_than_silently_clamped()
    {
        Assert.NotNull(CoHostPayouts.Invalid(CoHostPayoutKind.Percent, 0m, 0m));
        Assert.NotNull(CoHostPayouts.Invalid(CoHostPayoutKind.Percent, 150m, 0m));
        Assert.NotNull(CoHostPayouts.Invalid(CoHostPayoutKind.Fixed, 0m, 0m));

        Assert.Null(CoHostPayouts.Invalid(CoHostPayoutKind.Percent, 20m, 0m));
        Assert.Null(CoHostPayouts.Invalid(CoHostPayoutKind.Fixed, 0m, 300_000m));
        Assert.Null(CoHostPayouts.Invalid(CoHostPayoutKind.CleaningFee, 0m, 0m));
        Assert.Null(CoHostPayouts.Invalid(CoHostPayoutKind.None, 0m, 0m));
    }

    [Fact]
    public void Promising_away_more_than_a_hundred_percent_is_a_warning_not_a_refusal()
    {
        // A percentage that only overshoots on a one-night stay is an ordinary
        // arrangement, and the shares are capped at payout time anyway. What the
        // owner is owed here is a heads-up, not a locked door.
        Assert.Equal(120m, CoHostPayouts.Overcommitted([Percent(1, 60m), Percent(2, 60m)]));
        Assert.Equal(0m, CoHostPayouts.Overcommitted([Percent(1, 60m), Percent(2, 40m)]));

        // Flat amounts carry no percentage, so they never trip it.
        Assert.Equal(0m, CoHostPayouts.Overcommitted([Fixed(1, 900_000m), Fixed(2, 900_000m)]));
    }

    /* ---------------------------------------------------------- what is said */

    [Fact]
    public void Every_kind_reads_as_a_sentence_a_person_can_check()
    {
        foreach (var kind in CoHostPayouts.All)
        {
            var text = CoHostPayouts.Describe(kind.Kind, 20m, 300_000m);
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.NotEqual("Không chia thu nhập", text);
        }

        Assert.Equal("Không chia thu nhập", CoHostPayouts.Describe(CoHostPayoutKind.None, 0m, 0m));
    }

    [Fact]
    public void Every_kind_survives_the_round_trip_through_its_key()
    {
        foreach (var kind in CoHostPayouts.All)
            Assert.Equal(kind.Kind, CoHostPayouts.Parse(CoHostPayouts.Key(kind.Kind)));

        // Anything unrecognised is "no share", never a guess at what was meant.
        Assert.Equal(CoHostPayoutKind.None, CoHostPayouts.Parse("khong-co-that"));
        Assert.Equal(CoHostPayoutKind.None, CoHostPayouts.Parse(null));
    }

    [Fact]
    public void A_clawback_says_which_booking_it_came_from()
    {
        // A deduction nobody explains is how somebody concludes their money was
        // taken. The reference is the one thing that lets them check.
        var text = CoHostPayouts.ClawbackNotice(450_000m, "SH-2026-0142");

        Assert.Contains("SH-2026-0142", text);
        Assert.Contains("450.000", text);
    }
}
