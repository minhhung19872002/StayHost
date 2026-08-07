using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>docs/01 CĐ-03 and CĐ-04, docs/03 §10 — the arrival guide and who may read it.</summary>
public class CheckInGuideTests
{
    private static readonly DateOnly CheckIn = new(2026, 8, 20);
    private static readonly TimeOnly From = new(14, 0);

    /// <summary>Check-in opens 2026-08-20 14:00, so the code appears 2026-08-18 14:00.</summary>
    private static DateTime At(int day, int hour, int minute = 0) => new(2026, 8, day, hour, minute, 0);

    /* ------------------------------------------------------------- CĐ-03 */

    [Fact]
    public void The_guide_belongs_to_a_stay_that_is_actually_happening()
    {
        Assert.True(CheckInGuide.CanSeeGuide(BookingStatus.Confirmed));
        Assert.True(CheckInGuide.CanSeeGuide(BookingStatus.InProgress));
        Assert.True(CheckInGuide.CanSeeGuide(BookingStatus.Completed));
    }

    [Fact]
    public void An_unanswered_request_carries_no_address()
    {
        // docs/03 §10 — the exact address is released on confirmation, and a
        // request to book has not been confirmed by anybody.
        Assert.False(CheckInGuide.CanSeeGuide(BookingStatus.PendingHostApproval));
        Assert.False(CheckInGuide.CanSeeGuide(BookingStatus.PendingPayment));
    }

    [Fact]
    public void A_cancelled_stay_stops_being_a_way_into_somebody_s_home()
    {
        foreach (var status in new[]
                 {
                     BookingStatus.CancelledByGuest, BookingStatus.CancelledByHost,
                     BookingStatus.Declined, BookingStatus.Expired, BookingStatus.PaymentFailed
                 })
        {
            Assert.False(CheckInGuide.CanSeeGuide(status), $"{status} should not read the guide");
            Assert.False(CheckInGuide.CanSeeDoorCode(status, CheckIn, From, At(19, 12)),
                $"{status} should not read the door code");
        }
    }

    [Fact]
    public void The_arrival_window_reads_the_same_wherever_it_is_shown()
    {
        Assert.Equal(
            "Nhận phòng 14:00 – 22:00 · Trả phòng trước 12:00",
            CheckInGuide.WindowLabel(new TimeOnly(14, 0), new TimeOnly(22, 0), new TimeOnly(12, 0)));
    }

    [Fact]
    public void Appliance_notes_come_back_one_instruction_per_line()
    {
        var lines = CheckInGuide.Lines("Điều hoà: nút xanh.\n\n  Bình nóng lạnh: chờ 10 phút.  \n");
        Assert.Equal(["Điều hoà: nút xanh.", "Bình nóng lạnh: chờ 10 phút."], lines);
        Assert.Empty(CheckInGuide.Lines(null));
        Assert.Empty(CheckInGuide.Lines("   "));
    }

    [Fact]
    public void A_garbled_time_leaves_the_listing_on_the_time_it_already_had()
    {
        var current = new TimeOnly(15, 30);
        Assert.Equal(new TimeOnly(14, 0), CheckInGuide.ParseTime("14:00", current));
        Assert.Equal(new TimeOnly(14, 0), CheckInGuide.ParseTime("14:00:00", current));

        // Not midnight — that would quietly move check-in to the small hours.
        Assert.Equal(current, CheckInGuide.ParseTime("chiều", current));
        Assert.Equal(current, CheckInGuide.ParseTime("", current));
        Assert.Equal(current, CheckInGuide.ParseTime(null, current));
    }

    /* ------------------------------------------------------------- CĐ-04 */

    [Fact]
    public void The_door_code_appears_exactly_forty_eight_hours_before_check_in()
    {
        Assert.Equal(new DateTime(2026, 8, 18, 14, 0, 0), CheckInGuide.DoorCodeVisibleFrom(CheckIn, From));

        bool Visible(DateTime at) => CheckInGuide.CanSeeDoorCode(BookingStatus.Confirmed, CheckIn, From, at);

        Assert.False(Visible(At(18, 13, 59)));
        Assert.True(Visible(At(18, 14, 0)));
        Assert.True(Visible(At(19, 9)));
    }

    [Fact]
    public void A_stay_booked_months_ahead_does_not_hand_out_the_code_at_booking_time()
    {
        Assert.False(CheckInGuide.CanSeeDoorCode(BookingStatus.Confirmed, CheckIn, From, At(1, 10)));
    }

    [Fact]
    public void Somebody_already_inside_can_still_read_the_code()
    {
        // Locked out on night three: telling them to come back later would be absurd.
        Assert.True(CheckInGuide.CanSeeDoorCode(BookingStatus.InProgress, CheckIn, From, At(22, 23)));
    }

    [Fact]
    public void The_wait_says_when_rather_than_just_saying_no()
    {
        var note = CheckInGuide.DoorCodeWaitNote(CheckIn, From);
        Assert.Contains("14:00 18/08", note);
        Assert.Contains("48 giờ", note);
    }

    [Fact]
    public void An_earlier_check_in_hour_moves_the_window_with_it()
    {
        var noon = new TimeOnly(12, 0);
        Assert.Equal(new DateTime(2026, 8, 18, 12, 0, 0), CheckInGuide.DoorCodeVisibleFrom(CheckIn, noon));
        Assert.False(CheckInGuide.CanSeeDoorCode(BookingStatus.Confirmed, CheckIn, noon, At(18, 11, 30)));
        Assert.True(CheckInGuide.CanSeeDoorCode(BookingStatus.Confirmed, CheckIn, noon, At(18, 12, 0)));
    }

    [Fact]
    public void Only_the_ways_in_that_need_a_code_ask_for_one()
    {
        Assert.True(CheckInGuide.NeedsDoorCode(CheckInMethod.Keypad));
        Assert.True(CheckInGuide.NeedsDoorCode(CheckInMethod.Lockbox));
        Assert.True(CheckInGuide.NeedsDoorCode(CheckInMethod.SmartLock));

        Assert.False(CheckInGuide.NeedsDoorCode(CheckInMethod.Host));
        Assert.False(CheckInGuide.NeedsDoorCode(CheckInMethod.Reception));
    }

    [Fact]
    public void Every_way_in_has_wording_of_its_own()
    {
        var seen = new HashSet<string>();
        foreach (CheckInMethod method in Enum.GetValues<CheckInMethod>())
            Assert.True(seen.Add(CheckInGuide.MethodLabel(method)), $"{method} reuses another label");
    }
}
