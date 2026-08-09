namespace StayHost.Domain.Tests;

/// <summary>docs/01 QL-11 — imported calendar clashing with a confirmed booking.</summary>
public class CalendarConflictsTests
{
    private static CalendarConflicts.Range R(int fromDay, int toDay) =>
        new(new DateOnly(2026, 9, fromDay), new DateOnly(2026, 9, toDay));

    [Fact]
    public void Ranges_that_share_a_night_overlap()
    {
        Assert.True(CalendarConflicts.Overlaps(R(10, 14), R(12, 16)));
    }

    [Fact]
    public void Back_to_back_ranges_do_not_overlap()
    {
        // One checks out the morning the other checks in — the half-open ranges
        // touch at the boundary but share no night.
        Assert.False(CalendarConflicts.Overlaps(R(10, 13), R(13, 16)));
    }

    [Fact]
    public void A_clash_returns_the_confirmed_booking()
    {
        var imported = new[] { R(12, 15) };
        var confirmed = new[] { R(13, 16), R(20, 22) };

        var clashes = CalendarConflicts.Clashes(imported, confirmed);

        Assert.Single(clashes);
        Assert.Equal(R(13, 16), clashes[0]);
    }

    [Fact]
    public void No_clash_when_the_calendars_agree()
    {
        var imported = new[] { R(1, 5) };
        var confirmed = new[] { R(10, 12) };

        Assert.Empty(CalendarConflicts.Clashes(imported, confirmed));
        Assert.Null(CalendarConflicts.Warn(CalendarConflicts.Clashes(imported, confirmed)));
    }

    [Fact]
    public void A_booking_hit_by_several_events_is_reported_once()
    {
        var imported = new[] { R(12, 14), R(13, 15) };
        var confirmed = new[] { R(12, 16) };

        Assert.Single(CalendarConflicts.Clashes(imported, confirmed));
    }

    [Fact]
    public void The_warning_names_the_first_clash_and_counts_the_rest()
    {
        var clashes = new[] { R(20, 22), R(10, 12) };
        var warn = CalendarConflicts.Warn(clashes);

        Assert.NotNull(warn);
        Assert.Contains("10/09", warn);   // earliest first
        Assert.Contains("1 đơn khác", warn);
    }
}
