namespace StayHost.Domain.Tests;

/// <summary>docs/08 §6 — what a lock does to bookings that are already running.</summary>
public class SuspensionImpactTests
{
    private static SuspensionImpact.Booking B(
        int id, BookingStatus status, decimal paid = 3_000_000m, decimal payout = 2_500_000m,
        bool payoutSent = false, bool disputed = false) =>
        new(id, $"SH{id:0000}", status,
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 4),
            paid, payout, "Khách A", payoutSent, disputed);

    /* ---- docs/08 §13 scenario 3 ---- */

    [Fact]
    public void Locking_a_host_leaves_the_guest_who_is_already_there_alone()
    {
        var preview = SuspensionImpact.ForHost([
            B(1, BookingStatus.InProgress),
            B(2, BookingStatus.Confirmed), B(3, BookingStatus.Confirmed), B(4, BookingStatus.Confirmed),
            B(5, BookingStatus.Confirmed), B(6, BookingStatus.Confirmed)
        ]);

        Assert.Equal(1, preview.GuestsStaying);
        Assert.Equal(5, preview.BookingsCancelled);
        Assert.Equal(BookingFallout.LeaveAlone, preview.Lines.First(l => l.BookingId == 1).Action);
    }

    [Fact]
    public void The_five_upcoming_stays_are_refunded_in_full()
    {
        var preview = SuspensionImpact.ForHost(
            Enumerable.Range(1, 5).Select(i => B(i, BookingStatus.Confirmed)));

        Assert.Equal(5 * 3_000_000m, preview.MoneyRefunded);
        Assert.All(preview.Lines, l => Assert.Equal(BookingFallout.CancelRefundFull, l.Action));
    }

    [Fact]
    public void The_host_is_not_fined_for_a_cancellation_the_platform_made()
    {
        // This is the trap the table exists to avoid: the platform locks the
        // account, the automatic host-cancellation penalty fires, and the host
        // is charged for a decision that was not theirs.
        var preview = SuspensionImpact.ForHost([B(1, BookingStatus.Confirmed)]);

        Assert.Contains("không tính phạt huỷ cho chủ nhà", preview.Lines[0].Note);
    }

    [Fact]
    public void A_request_nobody_will_ever_answer_is_cancelled_and_the_guest_told()
    {
        var preview = SuspensionImpact.ForHost([B(1, BookingStatus.PendingHostApproval)]);

        Assert.Equal(BookingFallout.CancelRequest, preview.Lines[0].Action);
        Assert.Equal(0m, preview.MoneyRefunded);
    }

    [Fact]
    public void Money_already_earned_is_held_not_taken()
    {
        // docs/08 §6 — "Giữ lại cho tới khi xử lý xong vi phạm; không tịch thu tự động."
        var preview = SuspensionImpact.ForHost([B(1, BookingStatus.Completed, payout: 2_500_000m)]);

        Assert.Equal(BookingFallout.HoldPayout, preview.Lines[0].Action);
        Assert.Equal(2_500_000m, preview.PayoutHeld);
        Assert.Contains("Không tịch thu", preview.Lines[0].Note);
    }

    [Fact]
    public void A_payout_already_sent_is_not_listed_at_all()
    {
        var preview = SuspensionImpact.ForHost([B(1, BookingStatus.Completed, payoutSent: true)]);

        Assert.True(preview.Nothing);
    }

    /* ---- locking a guest ---- */

    [Fact]
    public void A_guest_mid_stay_is_left_alone_too()
    {
        var preview = SuspensionImpact.ForGuest([B(1, BookingStatus.InProgress)], refundInFull: false);

        Assert.Equal(1, preview.GuestsStaying);
        Assert.Equal(BookingFallout.LeaveAlone, preview.Lines[0].Action);
    }

    [Fact]
    public void Refunding_a_locked_guest_is_the_one_place_the_admin_chooses()
    {
        // docs/08 §6 — "hoàn tiền theo chính sách huỷ hoặc hoàn 100% tuỳ mức độ
        // vi phạm — admin chọn và ghi lý do".
        var byPolicy = SuspensionImpact.ForGuest([B(1, BookingStatus.Confirmed)], refundInFull: false);
        var inFull = SuspensionImpact.ForGuest([B(1, BookingStatus.Confirmed)], refundInFull: true);

        Assert.Equal(BookingFallout.CancelPerPolicy, byPolicy.Lines[0].Action);
        Assert.Equal(BookingFallout.CancelRefundFull, inFull.Lines[0].Action);
        Assert.Equal(3_000_000m, inFull.MoneyRefunded);
    }

    [Fact]
    public void Somebody_with_an_open_case_keeps_the_right_to_answer_it()
    {
        // docs/08 §6 — "không được cắt quyền tự vệ".
        var preview = SuspensionImpact.ForGuest(
            [B(1, BookingStatus.Confirmed, disputed: true)], refundInFull: false);

        Assert.True(SuspensionImpact.MustKeepAbleToRespond(preview));
        Assert.Contains("không được cắt quyền tự vệ", SuspensionImpact.OpenDisputeNotice());
    }

    /* ---- docs/08 §6 and QT-U-07, the warning before the click ---- */

    [Fact]
    public void The_admin_is_told_the_cost_before_they_confirm()
    {
        var preview = SuspensionImpact.ForHost([
            B(1, BookingStatus.InProgress),
            B(2, BookingStatus.Confirmed),
            B(3, BookingStatus.Completed)
        ]);

        var warning = preview.Warning;

        Assert.Contains("3 đơn", warning);
        Assert.Contains("1 đơn bị huỷ", warning);
        Assert.Contains("KHÔNG bị đụng tới", warning);
    }

    [Fact]
    public void An_account_with_nothing_running_says_so_plainly()
    {
        var preview = SuspensionImpact.ForHost([]);

        Assert.True(preview.Nothing);
        Assert.Contains("không có đơn nào đang chạy", preview.Warning);
    }

    [Fact]
    public void A_safety_case_offers_to_move_the_guests_out()
    {
        var preview = SuspensionImpact.ForHost([B(1, BookingStatus.InProgress)]);

        Assert.Contains("Staylio Shield", SuspensionImpact.SafetyRelocationNotice(preview.GuestsStaying));
        Assert.Equal("", SuspensionImpact.SafetyRelocationNotice(0));
    }
}
