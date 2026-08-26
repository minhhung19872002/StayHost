using StayHost.Domain;

namespace StayHost.Domain.Tests;

public class SiteLinksTests
{
    [Fact]
    public void Prefixes_the_path_with_the_configured_address()
    {
        Assert.Equal("https://staylio.vn/trips/12",
            SiteLinks.Absolute("https://staylio.vn", "/trips/12"));
    }

    [Fact]
    public void A_trailing_slash_on_the_address_does_not_double_up()
    {
        Assert.Equal("https://staylio.vn/trips/12",
            SiteLinks.Absolute("https://staylio.vn/", "/trips/12"));
    }

    [Fact]
    public void A_path_without_its_leading_slash_still_gets_one()
    {
        // Without this the two halves glue into "https://staylio.vntrips/12",
        // which is a different host and fails silently in a mail client.
        Assert.Equal("https://staylio.vn/trips/12",
            SiteLinks.Absolute("https://staylio.vn", "trips/12"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void No_configured_address_means_no_link_rather_than_a_guessed_host(string? baseUrl)
    {
        // The whole point: a deployment that has not been told its own address
        // must leave the line out. Inventing one is how every notification email
        // ended up pointing at a domain the platform does not own.
        Assert.Null(SiteLinks.Absolute(baseUrl, "/trips/12"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void A_notification_with_no_path_gets_no_link(string? path)
    {
        Assert.Null(SiteLinks.Absolute("https://staylio.vn", path));
    }
}
