using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>docs/01 TK-04 and TK-05 — what somebody may say about themselves, and what strangers read.</summary>
public class ProfilesTests
{
    /* ------------------------------------------------------------- TK-04 */

    [Fact]
    public void One_line_fields_lose_their_padding_and_their_line_breaks()
    {
        Assert.Equal("Đà Nẵng", Profiles.Tidy("  Đà   Nẵng  ", Profiles.LineMax));
        Assert.Equal("Kiến trúc sư", Profiles.Tidy("Kiến\ttrúc\nsư", Profiles.LineMax));
    }

    [Fact]
    public void A_field_somebody_cleared_and_one_they_never_filled_in_are_the_same_thing()
    {
        Assert.Null(Profiles.Tidy("   ", Profiles.LineMax));
        Assert.Null(Profiles.Tidy("", Profiles.LineMax));
        Assert.Null(Profiles.Tidy(null, Profiles.LineMax));
        Assert.Null(Profiles.TidyBio("\n\n  \n"));
    }

    [Fact]
    public void Nothing_gets_past_the_length_the_column_holds()
    {
        var long_line = new string('a', Profiles.LineMax + 50);
        Assert.Equal(Profiles.LineMax, Profiles.Tidy(long_line, Profiles.LineMax)!.Length);

        var long_bio = new string('b', Profiles.BioMax + 500);
        Assert.Equal(Profiles.BioMax, Profiles.TidyBio(long_bio)!.Length);
    }

    [Fact]
    public void A_bio_keeps_the_line_breaks_somebody_meant_and_loses_the_ones_they_did_not()
    {
        var bio = Profiles.TidyBio("Xin chào.\r\n\r\n\r\n\r\nMình sống ở Đà Nẵng.\n\n\n");
        Assert.Equal("Xin chào.\n\nMình sống ở Đà Nẵng.", bio);
    }

    [Fact]
    public void Spoken_languages_round_trip_through_storage()
    {
        var packed = Profiles.PackLanguages(["vi", "en", "ja"]);
        Assert.Equal(["vi", "en", "ja"], Profiles.UnpackLanguages(packed));
    }

    [Fact]
    public void A_language_nobody_offers_is_not_stored()
    {
        // "Klingon" is not on the list, and neither is an empty string.
        Assert.Equal(["vi"], Profiles.PackLanguages(["vi", "tlh", "", "  "]).Split(','));
    }

    [Fact]
    public void The_same_language_twice_is_still_one_language()
    {
        Assert.Equal(["vi", "en"], Profiles.TidyLanguages(["vi", "VI", " vi ", "en"]));
    }

    [Fact]
    public void Nobody_can_list_more_languages_or_interests_than_the_profile_holds()
    {
        var every_code = Profiles.SpokenLanguages.Select(l => l.Code).ToList();
        Assert.True(every_code.Count > Profiles.MaxLanguages, "the test needs more codes than the cap");
        Assert.Equal(Profiles.MaxLanguages, Profiles.TidyLanguages(every_code).Count);

        var many = Enumerable.Range(1, Profiles.MaxInterests + 20).Select(n => $"sở thích {n}");
        Assert.Equal(Profiles.MaxInterests, Profiles.TidyInterests(many).Count);
    }

    [Fact]
    public void An_interest_holding_the_separator_still_survives_storage()
    {
        // Interests are free text and people write commas in them, so the
        // packing has to be the one thing they cannot break.
        var packed = Profiles.PackInterests(["cà phê, sách", "leo núi"]);
        Assert.Equal(["cà phê, sách", "leo núi"], Profiles.UnpackInterests(packed));
    }

    [Fact]
    public void An_interest_longer_than_a_tag_is_cut_rather_than_dropped()
    {
        var kept = Profiles.TidyInterests([new string('x', Profiles.TagMax + 30)]);
        Assert.Single(kept);
        Assert.Equal(Profiles.TagMax, kept[0].Length);
    }

    [Fact]
    public void Unpacking_nothing_gives_an_empty_list_rather_than_a_blank_entry()
    {
        Assert.Empty(Profiles.UnpackLanguages(null));
        Assert.Empty(Profiles.UnpackLanguages(""));
        Assert.Empty(Profiles.UnpackInterests(null));
        Assert.Empty(Profiles.UnpackInterests("  "));
    }

    /* ------------------------------------------ TK-04 — the name others see */

    [Fact]
    public void The_display_name_wins_over_the_name_on_the_account()
    {
        Assert.Equal("Hưng", Profiles.DisplayNameOf("Hưng", "Bùi Minh Hưng"));
        Assert.Equal("Bùi Minh Hưng", Profiles.DisplayNameOf(null, "Bùi Minh Hưng"));
        Assert.Equal("Bùi Minh Hưng", Profiles.DisplayNameOf("   ", "Bùi Minh Hưng"));
    }

    [Fact]
    public void Somebody_with_no_name_at_all_still_has_something_to_show()
    {
        Assert.Equal("Người dùng Staylio", Profiles.DisplayNameOf(null, null));
        Assert.Equal("N", Profiles.InitialsOf("Người dùng Staylio")[..1]);
    }

    [Fact]
    public void Initials_come_from_the_first_and_last_word()
    {
        Assert.Equal("BH", Profiles.InitialsOf("Bùi Minh Hưng"));
        Assert.Equal("H", Profiles.InitialsOf("Hưng"));
        Assert.Equal("?", Profiles.InitialsOf("   "));
    }

    /* ---------------------------------------------- TK-04 — profile photos */

    [Fact]
    public void A_profile_photo_may_only_be_one_this_platform_stored()
    {
        Assert.True(Profiles.IsOwnUpload("/uploads/7-3f2a.jpg"));

        foreach (var hostile in new[]
                 {
                     "https://example.com/tracker.png",
                     "//example.com/tracker.png",
                     "javascript:alert(1)",
                     "/uploads/../appsettings.json",
                     "/uploads/nested/file.jpg",
                     "/uploads/",
                     "/uploads",
                     "uploads/7.jpg",
                     "",
                     null
                 })
            Assert.False(Profiles.IsOwnUpload(hostile), $"\"{hostile}\" should not be accepted");
    }

    /* ------------------------------------------------------------- TK-05 */

    [Fact]
    public void Only_what_was_actually_proved_becomes_a_badge()
    {
        Assert.Empty(Profiles.Badges(false, false, false));

        var all = Profiles.Badges(true, true, true);
        Assert.Equal(3, all.Count);
        Assert.Equal("Đã xác minh danh tính", all[0]);

        Assert.Equal(["Đã xác thực email"], Profiles.Badges(true, false, false));
    }

    [Fact]
    public void A_language_code_is_shown_in_the_language_the_screen_is_in()
    {
        Assert.Equal("Tiếng Nhật", Profiles.LanguageLabel("ja"));
        Assert.Equal("Tiếng Việt", Profiles.LanguageLabel("VI"));
    }

    [Fact]
    public void A_code_that_outlived_the_list_is_shown_rather_than_swallowed()
    {
        Assert.Equal("xx", Profiles.LanguageLabel("xx"));
    }

    [Fact]
    public void A_place_nobody_has_reviewed_yet_does_not_drag_a_superhost_down()
    {
        // Four well-reviewed listings plus one brand-new one. Averaging the five
        // listing averages would read 3.87; the host's actual reviews say 4.82.
        (double, int)[] listings = [(4.92, 64), (4.81, 88), (4.89, 51), (4.75, 120), (0, 0)];
        Assert.Equal(4.82, Profiles.OverallRating(listings));
    }

    [Fact]
    public void Every_review_weighs_the_same_wherever_it_was_left()
    {
        // One listing with a single 5 cannot pull a hundred 4s up to 4.5.
        Assert.Equal(4.01, Profiles.OverallRating([(4.0, 100), (5.0, 1)]));
    }

    [Fact]
    public void Somebody_nobody_has_reviewed_has_no_score_rather_than_a_zero()
    {
        Assert.Null(Profiles.OverallRating([]));
        Assert.Null(Profiles.OverallRating([(0, 0), (0, 0)]));
    }

    [Fact]
    public void Dates_on_a_profile_read_the_way_the_rest_of_the_site_writes_them()
    {
        var august = new DateTime(2026, 8, 7, 3, 45, 0, DateTimeKind.Utc);
        Assert.Equal("Tháng 8, 2026", Profiles.MonthLabel(august));
        Assert.Equal("Tham gia Staylio tháng 8, 2026", Profiles.JoinedLabel(august));
    }
}
