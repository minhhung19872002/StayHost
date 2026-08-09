namespace StayHost.Domain.Tests;

/// <summary>docs/01 TM-26 — the intro line on a city landing page.</summary>
public class CityBlurbTests
{
    [Fact]
    public void A_known_city_gets_its_own_line()
    {
        Assert.Contains("Đà Lạt", Cities.Blurb("Đà Lạt"));
        Assert.Contains("Hội An", Cities.Blurb("Hội An"));
    }

    [Fact]
    public void Spelling_and_prefix_variants_reach_the_same_line()
    {
        // "TP. Hồ Chí Minh" and "Thành phố Hồ Chí Minh" share a key, so the same blurb.
        Assert.Equal(Cities.Blurb("TP. Hồ Chí Minh"), Cities.Blurb("Thành phố Hồ Chí Minh"));
    }

    [Fact]
    public void An_unknown_city_still_gets_a_natural_sentence_with_its_name()
    {
        var blurb = Cities.Blurb("Quy Nhơn");
        Assert.Contains("Quy Nhơn", blurb);
        Assert.NotEqual("", blurb);
    }

    [Fact]
    public void An_empty_city_falls_back_to_a_generic_line()
    {
        Assert.NotEqual("", Cities.Blurb(""));
        Assert.NotEqual("", Cities.Blurb(null));
    }
}
