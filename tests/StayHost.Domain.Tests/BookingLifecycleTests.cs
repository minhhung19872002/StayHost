using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>The state machine drawn in docs/03 §3, and the history it leaves behind.</summary>
public class BookingLifecycleTests
{
    private static Booking Make(BookingStatus status) =>
        new() { Id = 1, Reference = "SHTEST", Status = status };

    /* ------------------------------------------------- the arrows that exist */

    [Theory]
    [InlineData(BookingStatus.PendingHostApproval, BookingStatus.PendingPayment)]
    [InlineData(BookingStatus.PendingHostApproval, BookingStatus.Declined)]
    [InlineData(BookingStatus.PendingHostApproval, BookingStatus.Expired)]
    [InlineData(BookingStatus.PendingHostApproval, BookingStatus.CancelledByGuest)]
    [InlineData(BookingStatus.PendingPayment, BookingStatus.Confirmed)]
    [InlineData(BookingStatus.PendingPayment, BookingStatus.PaymentFailed)]
    [InlineData(BookingStatus.Confirmed, BookingStatus.InProgress)]
    [InlineData(BookingStatus.Confirmed, BookingStatus.CancelledByHost)]
    [InlineData(BookingStatus.InProgress, BookingStatus.Completed)]
    [InlineData(BookingStatus.InProgress, BookingStatus.CancelledByGuest)]
    public void Legal_transitions_are_allowed(BookingStatus from, BookingStatus to)
    {
        Assert.True(BookingLifecycle.CanTransition(from, to));
    }

    [Theory]
    // Skipping payment.
    [InlineData(BookingStatus.PendingHostApproval, BookingStatus.Confirmed)]
    // Skipping the stay.
    [InlineData(BookingStatus.Confirmed, BookingStatus.Completed)]
    // Coming back from a terminal state.
    [InlineData(BookingStatus.Completed, BookingStatus.CancelledByGuest)]
    [InlineData(BookingStatus.Declined, BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Expired, BookingStatus.PendingPayment)]
    [InlineData(BookingStatus.CancelledByGuest, BookingStatus.Confirmed)]
    public void Illegal_transitions_are_refused(BookingStatus from, BookingStatus to)
    {
        Assert.False(BookingLifecycle.CanTransition(from, to));

        var booking = Make(from);
        Assert.Throws<BookingLifecycle.IllegalTransitionException>(
            () => BookingLifecycle.Transition(booking, to, "test", "should not happen"));
        Assert.Equal(from, booking.Status);          // and nothing moved
    }

    /* ---------------------------------------------------------- the history */

    [Fact]
    public void Every_move_appends_a_history_row()
    {
        var booking = Make(BookingStatus.PendingHostApproval);

        BookingLifecycle.Transition(booking, BookingStatus.PendingPayment, "host:3", "Chủ nhà chấp nhận.");
        BookingLifecycle.Transition(booking, BookingStatus.Confirmed, "system", "Thanh toán thành công.");

        Assert.Equal(2, booking.Events.Count);
        Assert.Equal(BookingStatus.PendingHostApproval, booking.Events[0].FromStatus);
        Assert.Equal(BookingStatus.PendingPayment, booking.Events[0].ToStatus);
        Assert.Equal("host:3", booking.Events[0].Actor);
        Assert.Equal(BookingStatus.Confirmed, booking.Events[1].ToStatus);
    }

    [Fact]
    public void The_creation_row_has_no_from_status()
    {
        var booking = Make(BookingStatus.Confirmed);
        var evt = BookingLifecycle.Created(booking, "guest:9", "Đặt ngay");

        Assert.Null(evt.FromStatus);
        Assert.Equal(BookingStatus.Confirmed, evt.ToStatus);
    }

    /* ------------------------------------------------------------- the hold */

    [Fact]
    public void Entering_payment_starts_a_fifteen_minute_hold()
    {
        var booking = Make(BookingStatus.PendingHostApproval);
        BookingLifecycle.Transition(booking, BookingStatus.PendingPayment, "host:3", "Chấp nhận.");

        Assert.NotNull(booking.HoldExpiresAt);
        var remaining = booking.HoldExpiresAt!.Value - DateTime.UtcNow;
        Assert.InRange(remaining.TotalMinutes, 14, 15);
    }

    [Fact]
    public void Leaving_payment_clears_the_hold()
    {
        var booking = Make(BookingStatus.PendingPayment) ;
        booking.HoldExpiresAt = DateTime.UtcNow.AddMinutes(10);

        BookingLifecycle.Transition(booking, BookingStatus.Confirmed, "system", "Đã thanh toán.");
        Assert.Null(booking.HoldExpiresAt);
    }

    /* ---------------------------------------------------- who holds the dates */

    [Fact]
    public void A_request_awaiting_the_host_does_not_hold_the_dates()
    {
        // docs/03 §2: "Với chế độ yêu cầu đặt, ngày không bị khoá."
        Assert.False(BookingLifecycle.HoldsDates(BookingStatus.PendingHostApproval));
    }

    [Theory]
    [InlineData(BookingStatus.PendingPayment)]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.InProgress)]
    [InlineData(BookingStatus.Completed)]
    public void Paid_and_live_stays_hold_the_dates(BookingStatus status)
    {
        Assert.True(BookingLifecycle.HoldsDates(status));
    }

    [Theory]
    [InlineData(BookingStatus.Declined)]
    [InlineData(BookingStatus.Expired)]
    [InlineData(BookingStatus.PaymentFailed)]
    [InlineData(BookingStatus.CancelledByGuest)]
    [InlineData(BookingStatus.CancelledByHost)]
    public void Dead_bookings_release_the_dates(BookingStatus status)
    {
        Assert.False(BookingLifecycle.HoldsDates(status));
        Assert.True(BookingLifecycle.IsCancelled(status));
    }

    /* ------------------------------------------------------------- labelling */

    [Fact]
    public void Every_status_has_a_label_and_a_badge()
    {
        foreach (var s in Enum.GetValues<BookingStatus>())
        {
            Assert.False(string.IsNullOrWhiteSpace(BookingLifecycle.Label(s)));
            Assert.Contains(BookingLifecycle.BadgeClass(s), new[] { "pending", "confirmed", "cancelled" });
        }
    }

    [Fact]
    public void All_ten_states_of_the_spec_exist()
    {
        Assert.Equal(10, Enum.GetValues<BookingStatus>().Length);
    }
}
