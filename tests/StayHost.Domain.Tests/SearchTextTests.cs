using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>
/// docs/03 §6: "gõ 'da lat' phải ra 'Đà Lạt', gõ 'hcm' phải ra 'Thành phố Hồ
/// Chí Minh'". The normalised column is what makes both work.
/// </summary>
public class SearchTextTests
{
    [Theory]
    [InlineData("Đà Lạt", "da lat")]
    [InlineData("Đà Nẵng", "da nang")]
    [InlineData("Thành phố Hồ Chí Minh", "thanh pho ho chi minh")]
    [InlineData("Hội An", "hoi an")]
    [InlineData("Vũng Tàu", "vung tau")]
    [InlineData("Phú Quốc", "phu quoc")]
    [InlineData("HUẾ", "hue")]
    public void Diacritics_and_case_are_stripped(string input, string expected)
    {
        Assert.Equal(expected, SearchText.Normalize(input));
    }

    [Fact]
    public void Punctuation_becomes_a_word_break()
    {
        Assert.Equal("tp ho chi minh", SearchText.Normalize("TP. Hồ Chí Minh"));
        Assert.Equal("villa ho boi rieng", SearchText.Normalize("  Villa — hồ bơi   riêng!  "));
    }

    [Fact]
    public void Nothing_in_gives_nothing_out()
    {
        Assert.Equal("", SearchText.Normalize(null));
        Assert.Equal("", SearchText.Normalize("   "));
        Assert.Empty(SearchText.Terms(""));
    }

    [Fact]
    public void A_listing_is_searchable_by_title_city_and_country()
    {
        var text = SearchText.ForListing("Sunset Villa hồ bơi riêng", "Đà Nẵng", "Việt Nam");

        Assert.Contains("sunset villa ho boi rieng", text);
        Assert.Contains("da nang", text);
        Assert.Contains("viet nam", text);
    }

    [Theory]
    [InlineData("Thành phố Hồ Chí Minh", "hcm")]
    [InlineData("Thành phố Hồ Chí Minh", "sg")]
    [InlineData("Thành phố Hồ Chí Minh", "saigon")]
    [InlineData("Đà Lạt", "dalat")]
    [InlineData("Phú Quốc", "pq")]
    [InlineData("Phan Thiết", "mui ne")]
    public void Common_abbreviations_reach_the_right_city(string city, string typed)
    {
        var text = SearchText.ForListing("Bất kỳ", city, "Việt Nam");

        foreach (var term in SearchText.Terms(typed))
            Assert.Contains(term, text);
    }

    [Fact]
    public void Every_typed_word_has_to_appear_somewhere()
    {
        var text = SearchText.ForListing("Lakeview Retreat gỗ ấm", "Đà Lạt", "Việt Nam");

        Assert.All(SearchText.Terms("Lakeview da lat"), t => Assert.Contains(t, text));
        Assert.DoesNotContain("nhatrang", text);
    }
}
