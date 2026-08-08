using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>docs/07 §12 — when a host's money moves, and when it is held.</summary>
public class PayoutsTests
{
    private static readonly DateTime Now = new(2026, 8, 7, 10, 0, 0, DateTimeKind.Utc);

    private static Payouts.Conditions Clear => new(
        HasOpenDispute: false, HasChargeback: false, ListingSuspended: false,
        AccountVerified: true, AccountChangedAt: null, OwedToPlatform: 0);

    /* --------------------------------------------- §12.2, the account freeze */

    [Fact]
    public void Changing_where_the_money_goes_freezes_payouts_for_three_days()
    {
        Assert.Equal(TimeSpan.FromDays(3), Payouts.AccountChangeFreeze);

        var changed = Now;
        Assert.True(Payouts.AccountFrozen(changed, Now.AddHours(1)));
        Assert.True(Payouts.AccountFrozen(changed, Now.AddDays(2.9)));
        Assert.False(Payouts.AccountFrozen(changed, Now.AddDays(3)));
    }

    [Fact]
    public void An_account_nobody_changed_is_not_frozen()
    {
        Assert.False(Payouts.AccountFrozen(null, Now));
    }

    [Fact]
    public void The_freeze_notice_says_when_it_lifts_and_what_to_do_if_it_was_not_you()
    {
        var notice = Payouts.FreezeNotice(Now);
        Assert.Contains("10/08/2026", notice);
        Assert.Contains("liên hệ hỗ trợ", notice);
    }

    [Fact]
    public void A_freshly_changed_account_stops_the_payout_even_when_all_else_is_well()
    {
        var justChanged = Clear with { AccountChangedAt = Now.AddHours(-1) };
        Assert.False(Payouts.CanPay(justChanged, Now));
        Assert.Equal(PayoutHoldReason.AccountUnverified, Payouts.HoldReason(justChanged, Now));
    }

    /* ------------------------------------------------- §12.3, when it is due */

    [Fact]
    public void Money_is_due_a_day_after_the_guest_arrives()
    {
        var checkIn = new DateOnly(2026, 8, 20);
        Assert.Equal(new DateOnly(2026, 8, 21), Payouts.DueOn(checkIn, completedStays: 10));
    }

    [Fact]
    public void A_hosts_first_stays_are_held_a_few_days_longer()
    {
        var checkIn = new DateOnly(2026, 8, 20);
        Assert.Equal(new DateOnly(2026, 8, 24), Payouts.DueOn(checkIn, completedStays: 0));
        Assert.Equal(new DateOnly(2026, 8, 24), Payouts.DueOn(checkIn, completedStays: 2));

        // The third completed stay ends it.
        Assert.Equal(new DateOnly(2026, 8, 21), Payouts.DueOn(checkIn, completedStays: 3));
    }

    /* ------------------------------------------------------ §12.4, the holds */

    [Fact]
    public void A_clear_booking_pays_out()
    {
        Assert.True(Payouts.CanPay(Clear, Now));
        Assert.Equal(PayoutHoldReason.None, Payouts.HoldReason(Clear, Now));
    }

    [Fact]
    public void Each_of_the_five_reasons_holds_the_money()
    {
        Assert.Equal(PayoutHoldReason.Dispute,
            Payouts.HoldReason(Clear with { HasOpenDispute = true }, Now));
        Assert.Equal(PayoutHoldReason.Chargeback,
            Payouts.HoldReason(Clear with { HasChargeback = true }, Now));
        Assert.Equal(PayoutHoldReason.ListingSuspended,
            Payouts.HoldReason(Clear with { ListingSuspended = true }, Now));
        Assert.Equal(PayoutHoldReason.AccountUnverified,
            Payouts.HoldReason(Clear with { AccountVerified = false }, Now));
        Assert.Equal(PayoutHoldReason.HostOwesPlatform,
            Payouts.HoldReason(Clear with { OwedToPlatform = 500_000m }, Now));
    }

    [Fact]
    public void When_several_reasons_apply_the_gravest_is_the_one_reported()
    {
        var everything = new Payouts.Conditions(true, true, true, false, Now, 900_000m);
        Assert.Equal(PayoutHoldReason.Dispute, Payouts.HoldReason(everything, Now));
    }

    [Fact]
    public void Every_hold_has_something_to_tell_the_host()
    {
        foreach (PayoutHoldReason reason in Enum.GetValues<PayoutHoldReason>())
        {
            var label = Payouts.HoldLabel(reason);
            if (reason == PayoutHoldReason.None) Assert.Empty(label);
            else Assert.NotEmpty(label);
        }
    }

    /* --------------------------------------------------- §12.5, trying again */

    [Fact]
    public void A_failed_transfer_is_retried_after_one_three_and_seven_days()
    {
        var failed = new DateOnly(2026, 8, 7);

        Assert.Equal(new DateOnly(2026, 8, 8), Payouts.NextAttemptOn(failed, 1));
        Assert.Equal(new DateOnly(2026, 8, 10), Payouts.NextAttemptOn(failed, 2));
        Assert.Equal(new DateOnly(2026, 8, 14), Payouts.NextAttemptOn(failed, 3));
    }

    [Fact]
    public void After_the_last_retry_a_person_has_to_fix_it()
    {
        Assert.Null(Payouts.NextAttemptOn(new DateOnly(2026, 8, 7), Payouts.MaxAttempts));
        Assert.True(Payouts.OutOfAttempts(Payouts.MaxAttempts));
        Assert.False(Payouts.OutOfAttempts(Payouts.MaxAttempts - 1));
    }

    [Fact]
    public void The_host_is_told_their_money_is_still_theirs()
    {
        // docs/07 §12.5 — the money is held, not lost, and saying so is the point.
        Assert.Contains("giữ nguyên cho bạn", Payouts.ExhaustedNotice());
    }

    /* ---- docs/07 §12.3, one transfer a day per host ---- */

    [Fact]
    public void A_days_bookings_for_one_host_share_a_single_transfer_reference()
    {
        var day = new DateOnly(2026, 8, 7);

        Assert.Equal(Payouts.BatchReference(12, day), Payouts.BatchReference(12, day));
        Assert.NotEqual(Payouts.BatchReference(12, day), Payouts.BatchReference(13, day));
        Assert.NotEqual(Payouts.BatchReference(12, day), Payouts.BatchReference(12, day.AddDays(1)));
    }

    [Fact]
    public void A_second_transfer_the_same_day_does_not_borrow_the_first_ones_reference()
    {
        var day = new DateOnly(2026, 8, 7);

        Assert.NotEqual(Payouts.BatchReference(12, day, 1), Payouts.BatchReference(12, day, 2));
        Assert.Equal(Payouts.BatchReference(12, day), Payouts.BatchReference(12, day, 1));
    }

    /* ---- docs/07 §12.2, proving the account ---- */

    [Fact]
    public void The_account_name_matches_however_it_was_typed()
    {
        // Banks hold it unaccented and shouted; people type it either way.
        Assert.True(Payouts.NameMatchesIdentity("NGUYEN VAN AN", "Nguyễn Văn An"));
        Assert.True(Payouts.NameMatchesIdentity("nguyen van an", "NGUYEN VAN AN"));
    }

    [Fact]
    public void A_different_person_on_the_account_is_not_a_match()
    {
        Assert.False(Payouts.NameMatchesIdentity("NGUYEN VAN BINH", "Nguyễn Văn An"));
        Assert.False(Payouts.NameMatchesIdentity("", "Nguyễn Văn An"));
        Assert.False(Payouts.NameMatchesIdentity(null, "Nguyễn Văn An"));
    }

    /* ---- docs/07 §17.4, what the host owes comes off the transfer ---- */

    [Fact]
    public void A_debt_smaller_than_the_payout_is_deducted_and_the_rest_still_goes_out()
    {
        var d = Payouts.Deduct(5_000_000m, 1_200_000m);

        Assert.Equal(3_800_000m, d.Transfer);
        Assert.Equal(1_200_000m, d.Applied);
        Assert.Equal(0m, d.StillOwed);
    }

    [Fact]
    public void A_debt_bigger_than_the_payout_takes_all_of_it_and_carries_the_rest_forward()
    {
        var d = Payouts.Deduct(1_000_000m, 2_500_000m);

        Assert.Equal(0m, d.Transfer);
        Assert.Equal(1_000_000m, d.Applied);
        Assert.Equal(1_500_000m, d.StillOwed);
    }

    [Fact]
    public void Only_a_debt_that_swallows_the_whole_payout_holds_it()
    {
        var clear = new Payouts.Conditions(false, false, false, true, null, 0m, 5_000_000m);
        var partial = clear with { OwedToPlatform = 1_200_000m };
        var total = clear with { OwedToPlatform = 5_000_000m };

        Assert.Equal(PayoutHoldReason.None, Payouts.HoldReason(clear, Now));
        // Deducting is not holding: 3,8 triệu still leaves today.
        Assert.Equal(PayoutHoldReason.None, Payouts.HoldReason(partial, Now));
        Assert.Equal(PayoutHoldReason.HostOwesPlatform, Payouts.HoldReason(total, Now));
    }

    [Fact]
    public void The_deduction_notice_says_whether_anything_is_still_owed()
    {
        Assert.Contains("còn lại", Payouts.DeductionNote(1_000_000m, 500_000m));
        Assert.Contains("không còn nợ", Payouts.DeductionNote(1_000_000m, 0m));
    }

    /* ---- docs/08 §5.2 and §6: an admin's hold on the whole account ---- */

    [Fact]
    public void An_account_under_review_outranks_every_other_reason()
    {
        // The sweep recomputes holds from live data every run. Before this the
        // recomputation quietly undid the admin's hold on the very next tick.
        var clean = new Payouts.Conditions(false, false, false, true, null, 0m, 5_000_000m);
        var held = clean with { AccountUnderReview = true };

        Assert.Equal(PayoutHoldReason.None, Payouts.HoldReason(clean, Now));
        Assert.Equal(PayoutHoldReason.AccountUnderReview, Payouts.HoldReason(held, Now));
        Assert.False(Payouts.CanPay(held, Now));
    }

    [Fact]
    public void The_account_hold_is_named_first_even_when_a_dispute_is_open_too()
    {
        var both = new Payouts.Conditions(true, false, false, true, null, 0m, 5_000_000m, AccountUnderReview: true);

        Assert.Equal(PayoutHoldReason.AccountUnderReview, Payouts.HoldReason(both, Now));
    }

    [Fact]
    public void The_host_is_told_the_money_is_still_theirs()
    {
        // docs/08 §6 — "Giữ lại cho tới khi xử lý xong vi phạm; không tịch thu."
        var label = Payouts.HoldLabel(PayoutHoldReason.AccountUnderReview);

        Assert.Contains("xem xét", label);
        Assert.Contains("vẫn là của bạn", label);
    }
}
