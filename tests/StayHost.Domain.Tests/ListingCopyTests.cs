using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>docs/01 CN-08 — the titles and description the wizard offers a stuck host.</summary>
public class ListingCopyTests
{
    private static ListingCopy.Facts Villa => new(
        PlaceType.Villa, RoomType.EntirePlace, "Đà Nẵng",
        Bedrooms: 4, MaxGuests: 8,
        AmenityKeys: ["pool", "beach", "wifi", "parking"]);

    [Fact]
    public void A_host_is_offered_choices_not_one_answer()
    {
        var titles = ListingCopy.Titles(Villa);
        Assert.True(titles.Count >= 2, "one suggestion is a decision made for them");
        Assert.Equal(titles.Distinct(), titles);
    }

    [Fact]
    public void No_suggestion_is_longer_than_the_editor_would_accept()
    {
        var wordy = Villa with
        {
            City = "Thành phố Hồ Chí Minh",
            AmenityKeys = ["pool", "beach", "view", "hottub", "bbq", "fire", "kitchen"]
        };

        Assert.All(ListingCopy.Titles(wordy), t => Assert.True(t.Length <= ListingCopy.TitleMax, t));
    }

    [Fact]
    public void A_cut_suggestion_does_not_end_mid_word()
    {
        var wordy = Villa with { City = "Thành phố Hồ Chí Minh", AmenityKeys = ["hottub", "pool", "beach"] };
        Assert.All(ListingCopy.Titles(wordy), t => Assert.DoesNotContain("  ", t));
        Assert.All(ListingCopy.Titles(wordy), t => Assert.False(t.EndsWith(' ') || t.EndsWith(',')));
    }

    [Fact]
    public void Suggestions_use_what_the_host_actually_ticked()
    {
        var titles = ListingCopy.Titles(Villa);
        Assert.Contains(titles, t => t.Contains("hồ bơi riêng"));
        Assert.All(titles, t => Assert.Contains("Đà Nẵng", t));
    }

    [Fact]
    public void Nothing_is_claimed_that_the_listing_does_not_have()
    {
        var bare = Villa with { AmenityKeys = [] };

        foreach (var text in ListingCopy.Titles(bare).Append(ListingCopy.Description(bare)))
        {
            Assert.DoesNotContain("hồ bơi", text);
            Assert.DoesNotContain("sát biển", text);
            Assert.DoesNotContain("lò sưởi", text);
        }
    }

    [Fact]
    public void A_place_with_no_features_still_gets_a_usable_title()
    {
        var bare = new ListingCopy.Facts(PlaceType.House, RoomType.EntirePlace, "Huế", 0, 2, []);
        var titles = ListingCopy.Titles(bare);

        Assert.NotEmpty(titles);
        Assert.All(titles, t => Assert.True(t.Length >= 8, t));
    }

    [Fact]
    public void The_draft_description_is_long_enough_to_be_accepted()
    {
        foreach (var facts in new[] { Villa, Villa with { AmenityKeys = [] }, Villa with { Bedrooms = 0 } })
            Assert.True(ListingCopy.Description(facts).Length >= ListingCopy.DescriptionMin,
                ListingCopy.Description(facts));
    }

    [Fact]
    public void A_shared_room_is_not_described_as_the_whole_place()
    {
        var shared = Villa with { Room = RoomType.PrivateRoom };
        Assert.Contains("khoá riêng", ListingCopy.Description(shared));
        Assert.DoesNotContain("trọn chỗ nghỉ", ListingCopy.Description(shared));

        Assert.Contains("trọn chỗ nghỉ", ListingCopy.Description(Villa));
    }

    [Fact]
    public void The_editor_is_told_what_is_wrong_with_a_title_and_nothing_else()
    {
        Assert.Null(ListingCopy.TitleWarning(null));
        Assert.Null(ListingCopy.TitleWarning(""));
        Assert.Null(ListingCopy.TitleWarning("Villa hồ bơi riêng ở Đà Nẵng"));

        Assert.Contains("ít nhất 8", ListingCopy.TitleWarning("Villa"));
        Assert.Contains("quá", ListingCopy.TitleWarning(new string('x', ListingCopy.TitleMax + 5))!);
    }
}
