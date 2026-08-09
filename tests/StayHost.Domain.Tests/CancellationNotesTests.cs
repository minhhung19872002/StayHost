namespace StayHost.Domain.Tests;

/// <summary>docs/01 ĐG-12 — the public note when a host cancels a confirmed stay.</summary>
public class CancellationNotesTests
{
    [Fact]
    public void Days_before_check_in_is_counted_from_the_cancellation()
    {
        var checkIn = new DateOnly(2026, 9, 10);
        Assert.Equal(5, CancellationNotes.DaysBefore(checkIn, new DateTime(2026, 9, 5, 8, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Cancelling_on_or_after_the_day_is_zero_days_not_negative()
    {
        var checkIn = new DateOnly(2026, 9, 10);
        Assert.Equal(0, CancellationNotes.DaysBefore(checkIn, new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(0, CancellationNotes.DaysBefore(checkIn, new DateTime(2026, 9, 12, 8, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void The_sentence_reads_naturally_at_the_edges()
    {
        Assert.Contains("đúng ngày nhận phòng", CancellationNotes.Compose(0));
        Assert.Contains("một ngày trước", CancellationNotes.Compose(1));
        Assert.Contains("7 ngày trước", CancellationNotes.Compose(7));
    }

    [Fact]
    public void A_note_older_than_a_year_stops_showing()
    {
        var now = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        var fresh = new ListingCancellationNote { CreatedAt = now.AddDays(-30) };
        var old = new ListingCancellationNote { CreatedAt = now.AddDays(-400) };

        Assert.True(CancellationNotes.IsVisible(fresh, now));
        Assert.False(CancellationNotes.IsVisible(old, now));
    }
}
