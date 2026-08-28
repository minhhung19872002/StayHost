using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>docs/01 QL-10 — reading and writing the calendar format everyone else speaks.</summary>
public class IcalTests
{
    private static readonly DateTime Stamp = new(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void An_exported_stay_ends_on_the_check_out_date()
    {
        var text = Ical.Write("Nhà A", [
            new IcalEvent("b1@stayhost", new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 13), "Đã đặt")
        ], Stamp);

        Assert.Contains("DTSTART;VALUE=DATE:20260910", text);
        Assert.Contains("DTEND;VALUE=DATE:20260913", text);
        Assert.Contains("BEGIN:VCALENDAR", text);
        Assert.Contains("END:VCALENDAR", text);
    }

    [Fact]
    public void What_we_write_is_what_we_read_back()
    {
        var events = new[]
        {
            new IcalEvent("a@stayhost", new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 13), "Đã đặt · SH-1"),
            new IcalEvent("b@stayhost", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3), "Chủ nhà khoá")
        };

        var read = Ical.Read(Ical.Write("Nhà A", events, Stamp));

        Assert.Equal(2, read.Count);
        Assert.Equal(events[0], read[0]);
        Assert.Equal(events[1], read[1]);
    }

    [Fact]
    public void A_timestamped_event_from_another_platform_loses_its_clock()
    {
        const string feed = """
            BEGIN:VCALENDAR
            BEGIN:VEVENT
            UID:1234@other
            DTSTART:20260915T140000Z
            DTEND:20260918T110000Z
            SUMMARY:Reserved
            END:VEVENT
            END:VCALENDAR
            """;

        var read = Ical.Read(feed);

        Assert.Single(read);
        Assert.Equal(new DateOnly(2026, 9, 15), read[0].From);
        Assert.Equal(new DateOnly(2026, 9, 18), read[0].To);
    }

    [Fact]
    public void A_folded_line_is_joined_back_together()
    {
        var feed = "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:x@other\r\nDTSTART;VALUE=DATE:20261001\r\n"
                 + "DTEND;VALUE=DATE:20261003\r\nSUMMARY:Một cái tên rất dài bị\r\n  cắt làm hai dòng\r\n"
                 + "END:VEVENT\r\nEND:VCALENDAR\r\n";

        var read = Ical.Read(feed);

        Assert.Single(read);
        Assert.Equal("Một cái tên rất dài bị cắt làm hai dòng", read[0].Summary);
    }

    [Fact]
    public void An_event_with_no_end_is_treated_as_one_night()
    {
        const string feed = "BEGIN:VCALENDAR\nBEGIN:VEVENT\nUID:one@other\nDTSTART;VALUE=DATE:20261005\nEND:VEVENT\nEND:VCALENDAR";

        var read = Ical.Read(feed);

        Assert.Single(read);
        Assert.Equal(new DateOnly(2026, 10, 5), read[0].From);
        Assert.Equal(new DateOnly(2026, 10, 6), read[0].To);
    }

    [Fact]
    public void Rubbish_is_no_events_rather_than_an_exception()
    {
        Assert.Empty(Ical.Read(""));
        Assert.Empty(Ical.Read("<html>404 Not Found</html>"));
        Assert.Empty(Ical.Read("BEGIN:VCALENDAR\nEND:VCALENDAR"));
    }

    [Fact]
    public void An_event_that_ends_before_it_starts_is_dropped()
    {
        const string feed = "BEGIN:VCALENDAR\nBEGIN:VEVENT\nUID:bad@other\nDTSTART;VALUE=DATE:20261010\nDTEND;VALUE=DATE:20261008\nEND:VEVENT\nEND:VCALENDAR";

        Assert.Empty(Ical.Read(feed));
    }

    [Fact]
    public void A_comma_in_a_name_survives_the_round_trip()
    {
        var text = Ical.Write("Nhà A", [
            new IcalEvent("c@stayhost", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2), "Khoá, để sửa nhà")
        ], Stamp);

        Assert.Contains("SUMMARY:Khoá\\, để sửa nhà", text);
        Assert.Equal("Khoá, để sửa nhà", Ical.Read(text)[0].Summary);
    }

    /* ------------------------------------------------------------- ĐP-15 */

    [Fact]
    public void A_guests_own_event_carries_where_and_what_it_is()
    {
        var text = Ical.Write("Staylio", [
            new IcalEvent("booking-7@staylio.vn", new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 13), "Villa A · SH7")
            {
                Location = "12 Trần Phú, Đà Nẵng",
                Description = "Mã đặt chỗ: SH7\n3 đêm · 2 khách"
            }
        ], Stamp);

        // A comma is escaped: unescaped it separates values in this format.
        Assert.Contains(@"LOCATION:12 Trần Phú\, Đà Nẵng", text);

        // A newline inside a property is escaped, not emitted raw: a bare one
        // ends the property, and everything after it is read as a new field.
        Assert.Contains(@"DESCRIPTION:Mã đặt chỗ: SH7\n3 đêm · 2 khách", text);
        Assert.DoesNotContain("DESCRIPTION:Mã đặt chỗ: SH7\r\n3", text);
    }

    [Fact]
    public void An_availability_feed_still_carries_dates_and_nothing_else()
    {
        // docs/01 QL-10 feeds are read by other booking sites; the two fields
        // above are for the guest's own calendar and must stay out of them.
        var text = Ical.Write("Nhà A", [
            new IcalEvent("b1@staylio", new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 13), "Đã đặt")
        ], Stamp);

        Assert.DoesNotContain("LOCATION:", text);
        Assert.DoesNotContain("DESCRIPTION:", text);
    }
}

/// <summary>docs/01 QL-19 — the scopes an owner hands out.</summary>
public class CoHostScopeTests
{
    [Fact]
    public void Unknown_scope_names_are_ignored_rather_than_granted()
    {
        var scope = CoHostScopes.Parse(["calendar", "payouts", "messages"]);

        Assert.True(scope.HasFlag(CoHostScope.Calendar));
        Assert.True(scope.HasFlag(CoHostScope.Messages));
        Assert.False(scope.HasFlag(CoHostScope.Pricing));
        Assert.False(scope.HasFlag(CoHostScope.Listing));
    }

    [Fact]
    public void Nothing_selected_grants_nothing()
    {
        Assert.Equal(CoHostScope.None, CoHostScopes.Parse([]));
        Assert.Equal(CoHostScope.None, CoHostScopes.Parse(null));
    }

    [Fact]
    public void Full_covers_every_named_scope()
    {
        foreach (var (scope, _, _) in CoHostScopes.All)
            Assert.True(CoHostScope.Full.HasFlag(scope));

        Assert.Equal(CoHostScopes.All.Length, CoHostScopes.Keys(CoHostScope.Full).Count);
    }

    [Fact]
    public void Keys_round_trip_through_parse()
    {
        var scope = CoHostScope.Pricing | CoHostScope.Bookings;

        Assert.Equal(scope, CoHostScopes.Parse(CoHostScopes.Keys(scope)));
    }
}
