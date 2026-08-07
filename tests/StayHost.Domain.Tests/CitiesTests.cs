using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>One place is one place, however somebody typed it.</summary>
public class CitiesTests
{
    private static readonly string[] Known =
        ["TP. Hồ Chí Minh", "Đà Nẵng", "Hội An", "Đà Lạt"];

    [Fact]
    public void The_two_spellings_of_one_city_are_the_same_city()
    {
        // The bug this class exists for: a host wrote the long form and their
        // listing landed in a city of its own.
        Assert.True(Cities.SameCity("Thành phố Hồ Chí Minh", "TP. Hồ Chí Minh"));
        Assert.True(Cities.SameCity("tp ho chi minh", "Hồ Chí Minh"));
        Assert.True(Cities.SameCity("Tỉnh Bình Định", "Bình Định"));
    }

    [Fact]
    public void Two_different_cities_stay_different()
    {
        Assert.False(Cities.SameCity("Đà Nẵng", "Đà Lạt"));
        Assert.False(Cities.SameCity("Hà Nội", "Hà Giang"));
    }

    [Fact]
    public void Nothing_is_the_same_city_as_nothing()
    {
        Assert.False(Cities.SameCity("", ""));
        Assert.False(Cities.SameCity(null, null));
        Assert.False(Cities.SameCity("  ", "Đà Nẵng"));
    }

    [Fact]
    public void What_a_host_types_is_stored_as_the_catalogue_already_spells_it()
    {
        Assert.Equal("TP. Hồ Chí Minh", Cities.Canonical("Thành phố Hồ Chí Minh", Known));
        Assert.Equal("TP. Hồ Chí Minh", Cities.Canonical("  tp.   ho chi minh ", Known));
        Assert.Equal("Đà Nẵng", Cities.Canonical("da nang", Known));
    }

    [Fact]
    public void A_city_the_platform_has_never_covered_is_kept_not_refused()
    {
        // The next city the platform covers has to be able to start somewhere.
        Assert.Equal("Buôn Ma Thuột", Cities.Canonical("Buôn Ma Thuột", Known));
        Assert.Equal("Cà Mau", Cities.Canonical("  Cà   Mau  ", Known));
    }

    [Fact]
    public void An_empty_city_stays_empty_rather_than_matching_everything()
    {
        Assert.Equal("", Cities.Canonical("", Known));
        Assert.Equal("", Cities.Canonical("   ", Known));
        Assert.Equal("", Cities.Canonical(null, Known));
    }

    [Fact]
    public void Where_the_catalogue_holds_both_spellings_the_commoner_one_wins()
    {
        // Normalising must not pick the odd one out and make the split worse.
        string[] split = ["TP. Hồ Chí Minh", "TP. Hồ Chí Minh", "Thành phố Hồ Chí Minh"];
        Assert.Equal("TP. Hồ Chí Minh", Cities.Canonical("Thành phố Hồ Chí Minh", split));
    }

    [Fact]
    public void The_key_ignores_case_accents_and_the_administrative_prefix()
    {
        Assert.Equal(Cities.Key("TP. Hồ Chí Minh"), Cities.Key("thanh pho ho chi minh"));
        Assert.Equal(Cities.Key("Đà Nẵng"), Cities.Key("DA NANG"));
        Assert.NotEqual(Cities.Key("Đà Nẵng"), Cities.Key(""));
    }
}

/// <summary>docs/03 §6 — "gõ 'hcm' phải ra Thành phố Hồ Chí Minh".</summary>
public class CityAliasTests
{
    [Fact]
    public void Every_spelling_of_one_city_reaches_the_same_shorthand()
    {
        var abbreviated = SearchText.AliasesFor("TP. Hồ Chí Minh");
        var written_out = SearchText.AliasesFor("Thành phố Hồ Chí Minh");

        Assert.NotEmpty(abbreviated);
        Assert.Equal(abbreviated, written_out);
    }

    [Fact]
    public void The_full_official_name_is_something_a_guest_may_type()
    {
        // Typing the city out in full used to find nothing, because the stored
        // haystack only ever held the abbreviated form.
        Assert.Contains("thanh pho ho chi minh", SearchText.AliasesFor("TP. Hồ Chí Minh"));
        Assert.Contains("hcm", SearchText.AliasesFor("TP. Hồ Chí Minh"));
    }

    [Fact]
    public void A_city_with_no_shorthand_gets_none_rather_than_somebody_elses()
    {
        Assert.Equal("", SearchText.AliasesFor("Buôn Ma Thuột"));
        Assert.Equal("", SearchText.AliasesFor(null));
    }

    [Fact]
    public void The_cities_that_had_shorthand_still_have_it()
    {
        foreach (var city in new[] { "Hà Nội", "Đà Nẵng", "Đà Lạt", "Nha Trang", "Phú Quốc", "Hội An" })
            Assert.NotEmpty(SearchText.AliasesFor(city));
    }
}
